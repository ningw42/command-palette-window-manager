using System.Runtime.Versioning;

namespace WindowManager.Windowing;

/// <summary>Tracks one last-active application and applies window commands without activating it.</summary>
public sealed class WindowController : IDisposable
{
    private readonly IWindowSystem _windows;
    private readonly object _targetGate = new();
    private readonly object _operationGate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private WindowIdentity? _lastApplication;
    private Action? _stopMonitoring;
    private Func<TargetCapture> _captureTarget;
    private int _disposed;

    internal WindowController(IWindowSystem windows, Func<TargetCapture>? captureTarget = null)
    {
        _windows = windows;
        _captureTarget = captureTarget ?? CaptureCurrentTarget;
    }

    /// <remarks>The packaged host declares PerMonitorV2 DPI awareness in app.manifest.</remarks>
    [SupportedOSPlatform("windows10.0.19041")]
    public static WindowController CreateForCurrentDesktop()
    {
        var controller = new WindowController(new Win32WindowSystem());
        var monitor = new ForegroundWindowMonitor(controller._windows, controller.ObserveForeground, controller.GetRememberedTarget);
        controller._stopMonitoring = monitor.Dispose;
        controller._captureTarget = monitor.CaptureTarget;
        return controller;
    }

    internal void ObserveForeground(WindowSnapshot? window)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        lock (_targetGate)
        {
            if (window?.Kind == WindowKind.Application)
            {
                _lastApplication = window.Identity;
            }
            else if (window is null || window.Kind == WindowKind.Unsupported)
            {
                // An uninspectable event or ambiguous application must not select an older target.
                _lastApplication = null;
            }
        }
    }

    public WindowActionResult AlmostMaximize()
    {
        lock (_operationGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return WindowActionResult.Failure("Window Manager is shutting down. Reload the extension and retry.");
            }

            var capture = _captureTarget();
            if (capture.Error is not null)
            {
                return WindowActionResult.Failure(capture.Error);
            }

            var target = capture.Target;

            if (target is null)
            {
                return WindowActionResult.Failure("Close Command Palette, focus the application you want to resize, then try again.");
            }

            // Freeze identity for this invocation; never fall back to an unrelated window.
            var current = _windows.ReadWindow(target.Value.Handle);
            if (current is null || current.Identity != target.Value || current.Kind != WindowKind.Application)
            {
                return WindowActionResult.Failure("The previous application window is no longer available. Focus the desired window and retry.");
            }

            if (current.WorkArea is not { IsValid: true } workArea)
            {
                return WindowActionResult.Failure("The target monitor's work area could not be determined.");
            }

            var bounds = WindowLayout.AlmostMaximized(workArea);
            return _windows.ApplyBounds(target.Value, bounds, _lifetime.Token);
        }
    }

    private TargetCapture CaptureCurrentTarget()
    {
        var current = _windows.ReadForegroundWindow();
        if (current is null) return new(null, "The foreground window could not be identified. Please retry.");
        ObserveForeground(current);
        // Freeze the inspected application directly, not a concurrently updated remembered identity.
        return new(current.Kind == WindowKind.Application ? current.Identity : GetRememberedTarget(), null);
    }

    private WindowIdentity? GetRememberedTarget()
    {
        lock (_targetGate) return _lastApplication;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        _stopMonitoring?.Invoke();
        lock (_operationGate)
        {
            _lifetime.Dispose();
        }
    }
}
