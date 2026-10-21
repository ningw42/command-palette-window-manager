using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WindowManager.Windowing;

[SupportedOSPlatform("windows10.0.19041")]
internal sealed unsafe class ForegroundWindowMonitor : IDisposable
{
    [ThreadStatic]
    private static ForegroundWindowMonitor? _current;

    private readonly IWindowSystem _windows;
    private readonly Action<WindowSnapshot?> _observe;
    private readonly Func<WindowIdentity?> _getTarget;
    private readonly ConcurrentQueue<TaskCompletionSource<TargetCapture>> _captures = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private const uint ForegroundMessage = PInvoke.WM_APP + 1;
    private const uint CaptureMessage = PInvoke.WM_APP + 2;
    private const uint CaptureReadyMessage = PInvoke.WM_APP + 3;
    private long _queuedEvents;
    private long _processedEvents;
    private uint _threadId;
    private int _stopping;
    private string? _error;

    internal bool IsRunning => _thread.IsAlive;

    internal string? Error
    {
        get => Volatile.Read(ref _error);
        private set => Volatile.Write(ref _error, value);
    }

    internal ForegroundWindowMonitor(IWindowSystem windows, Action<WindowSnapshot?> observe, Func<WindowIdentity?> getTarget)
    {
        _windows = windows;
        _observe = observe;
        _getTarget = getTarget;
        _thread = new Thread(Run) { IsBackground = true, Name = "Window Manager foreground tracker" };
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(2)))
        {
            Error = "Window tracking did not start. Reload the Window Manager extension and retry.";
        }
    }

    internal TargetCapture CaptureTarget()
    {
        if (Error is { } error) return new(null, error);
        if (Volatile.Read(ref _stopping) != 0 || !_thread.IsAlive) return new(null, "Window tracking is not running.");

        var request = new TaskCompletionSource<TargetCapture>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captures.Enqueue(request);
        if (!PInvoke.PostThreadMessage(_threadId, CaptureMessage, default, default))
        {
            Error = "Window tracking could not synchronize. Reload the extension.";
            request.TrySetResult(new(null, Error));
        }

        if (!request.Task.Wait(TimeSpan.FromMilliseconds(500)))
        {
            request.TrySetResult(new(null, "Window tracking is catching up. Please retry the command."));
        }

        return request.Task.Result;
    }

    private void Run()
    {
        HWINEVENTHOOK hook = default;
        try
        {
            _current = this;
            _threadId = PInvoke.GetCurrentThreadId();
            MSG message;
            // Create the thread's message queue before publishing readiness or posting WM_QUIT.
            PInvoke.PeekMessage(&message, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
            hook = PInvoke.SetWinEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND,
                default, &OnForegroundChanged, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
            if (hook.IsNull)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            // No reconstruction from z-order: a popup at startup leaves the target unknown.
            _observe(_windows.ReadForegroundWindow());
            _ready.Set();
            while (Volatile.Read(ref _stopping) == 0)
            {
                var result = PInvoke.GetMessage(&message, HWND.Null, 0, 0);
                if (result.Value < 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
                if (result.Value == 0)
                {
                    if (Volatile.Read(ref _stopping) == 0) Error = "Window tracking stopped. Reload the extension.";
                    break;
                }

                if (message.message == ForegroundMessage)
                {
                    _observe(_windows.ReadWindow((nint)message.wParam.Value));
                    _processedEvents++;
                    continue;
                }

                if (message.message == CaptureMessage)
                {
                    // A second pump turn lets pending native callbacks enqueue their observations first.
                    PostCaptureReady();
                    continue;
                }

                if (message.message == CaptureReadyMessage)
                {
                    CompleteCapture();
                    continue;
                }

                PInvoke.TranslateMessage(&message);
                PInvoke.DispatchMessage(&message);
            }
        }
        catch (Exception error)
        {
            Error = "Window tracking stopped. Reload the Window Manager extension and retry.";
            Debug.WriteLine(error);
        }
        finally
        {
            if (!hook.IsNull) PInvoke.UnhookWinEvent(hook);
            _current = null;
            while (_captures.TryDequeue(out var request))
            {
                request.TrySetResult(new(null, Error ?? "Window tracking has stopped."));
            }
            _ready.Set();
        }
    }

    private void PostCaptureReady()
    {
        if (!PInvoke.PostThreadMessage(_threadId, CaptureReadyMessage, default, default))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private void CompleteCapture()
    {
        while (_captures.TryPeek(out var expired) && expired.Task.IsCompleted) _captures.TryDequeue(out _);
        if (!_captures.TryPeek(out var request)) return;
        if (Error is { } trackingError)
        {
            _captures.TryDequeue(out _);
            request.TrySetResult(new(null, trackingError));
            return;
        }

        if (_queuedEvents != _processedEvents)
        {
            PostCaptureReady();
            return;
        }

        var current = _windows.ReadForegroundWindow();
        // Inspection may pump native callbacks too; do not freeze history until those events are applied.
        if (_queuedEvents != _processedEvents)
        {
            PostCaptureReady();
            return;
        }

        TargetCapture capture;
        if (Error is { } error) capture = new(null, error);
        else if (current is null) capture = new(null, "The foreground window could not be identified. Please retry.");
        else
        {
            _observe(current);
            capture = new(current.Kind == WindowKind.Application ? current.Identity : _getTarget(), null);
        }

        _captures.TryDequeue(out _);
        request.TrySetResult(capture);
    }

    // Out-of-context callbacks run on the thread that installed the hook. A static unmanaged
    // function pointer avoids a collected delegate or runtime-marshaling dependency during trimming.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnForegroundChanged(HWINEVENTHOOK hook, uint eventType, HWND hwnd,
        int objectId, int childId, uint eventThread, uint eventTime)
    {
        var monitor = _current;
        if (monitor is null || Volatile.Read(ref monitor._stopping) != 0) return;
        try
        {
            // Preserve event order without doing COM/resource lookups inside a reentrant callback.
            // Inspect the event HWND later, not GetForegroundWindow (which may already be the popup).
            monitor._queuedEvents++;
            if (!PInvoke.PostThreadMessage(monitor._threadId, ForegroundMessage, new WPARAM((nuint)hwnd.Value), default))
            {
                monitor.Error = "A foreground event could not be queued. Reload the Window Manager extension.";
            }
        }
        catch (Exception error)
        {
            // Fail closed if tracking fails; exceptions must never cross the unmanaged callback.
            monitor.Error = "Window tracking failed. Reload the Window Manager extension and retry.";
            Debug.WriteLine(error);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0) return;
        if (_threadId != 0) PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        // Native inspection never sends blocking messages to the target application.
        if (_thread.Join(TimeSpan.FromSeconds(2))) _ready.Dispose();
    }
}
