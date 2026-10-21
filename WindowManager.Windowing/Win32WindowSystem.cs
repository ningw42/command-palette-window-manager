using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WindowManager.Windowing;

[SupportedOSPlatform("windows10.0.19041")]
internal sealed unsafe class Win32WindowSystem : IWindowSystem
{
    private readonly ConcurrentDictionary<string, PaletteCaptions> _captions = new(StringComparer.OrdinalIgnoreCase);

    public WindowSnapshot? ReadForegroundWindow() => ReadWindow((nint)PInvoke.GetForegroundWindow().Value);

    public WindowSnapshot? ReadWindow(nint handle)
    {
        var hwnd = new HWND(handle);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
        {
            return null;
        }

        // GA_ROOT does not discard ownership information like GA_ROOTOWNER would.
        hwnd = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
        uint processId;
        var threadId = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        if (hwnd.IsNull || threadId == 0 || processId == 0)
        {
            return null;
        }

        var facts = Inspect(hwnd, processId);
        var features = facts.Features;
        if (HasExcludedOwner(hwnd))
        {
            features |= WindowFeatures.ExcludedOwner;
        }

        var kind = WindowPolicy.Classify(features, facts.ClassName, facts.ExecutableName, facts.Title, facts.Captions);
        WindowRectangle? workArea = null;
        if (kind == WindowKind.Application)
        {
            var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            if (!monitor.IsNull && PInvoke.GetMonitorInfo(monitor, &info))
            {
                workArea = ToRectangle(info.rcWork);
            }
        }

        return new WindowSnapshot(new WindowIdentity((nint)hwnd.Value, processId, threadId), kind, workArea);
    }

    public WindowActionResult ApplyBounds(WindowIdentity target, WindowRectangle bounds, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return Canceled();
        var snapshot = ReadWindow(target.Handle);
        if (snapshot?.Identity != target || snapshot.Kind != WindowKind.Application)
        {
            return WindowActionResult.Failure("The target window changed or is no longer eligible.");
        }

        var hwnd = new HWND(target.Handle);
        var style = (WINDOW_STYLE)(uint)(nuint)PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        if ((style & WINDOW_STYLE.WS_THICKFRAME) == 0)
        {
            return WindowActionResult.Failure("This application window does not support resizing.");
        }

        var elapsed = Stopwatch.StartNew();
        if (PInvoke.IsIconic(hwnd) || PInvoke.IsZoomed(hwnd))
        {
            // Unlike SW_RESTORE, SW_SHOWNOACTIVATE does not request foreground activation.
            if (cancellationToken.IsCancellationRequested) return Canceled();
            if (!MatchesIdentity(target) || !PInvoke.ShowWindowAsync(hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE))
            {
                return WindowActionResult.Failure("Windows could not restore the target window. It may be elevated or unavailable.");
            }

            if (!WaitUntil(() => !PInvoke.IsIconic(hwnd) && !PInvoke.IsZoomed(hwnd), elapsed, 500, cancellationToken))
            {
                return WindowActionResult.Failure("The application did not finish restoring. Try again when it is responsive.");
            }
        }

        // One correction accounts for an application's DPI/frame change when it returns from maximized state.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (cancellationToken.IsCancellationRequested || !MatchesIdentity(target)
                || !TryGetBounds(hwnd, out var outer, out var visible))
            {
                return WindowActionResult.Failure("The target window is no longer available.");
            }

            var desiredOuter = WindowLayout.IncludeInvisibleFrame(bounds, outer, visible);
            var current = ReadWindow(target.Handle);
            if (current?.Identity != target || current.Kind != WindowKind.Application)
            {
                return WindowActionResult.Failure("The target window changed before it could be resized.");
            }

            if (cancellationToken.IsCancellationRequested) return Canceled();
            if (!PInvoke.SetWindowPos(hwnd, HWND.Null,
                desiredOuter.X, desiredOuter.Y, desiredOuter.Width, desiredOuter.Height,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER | SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS))
            {
                return WindowActionResult.Failure("Windows denied the resize request. The application may be elevated or unavailable.");
            }

            var deadline = attempt == 0 ? 700 : 1200;
            if (WaitUntil(() => MatchesIdentity(target)
                && TryGetBounds(hwnd, out _, out var actual)
                && NearlyEqual(actual, bounds), elapsed, deadline, cancellationToken))
            {
                return WindowActionResult.Applied;
            }
        }

        // An asynchronous request cannot be withdrawn; a busy app may still apply it later.
        return WindowActionResult.Failure("Windows did not confirm the requested size. The app may be busy or enforce size limits.");
    }

    public WindowActionResult ApplyState(WindowIdentity target, WindowState state, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return Canceled();
        var snapshot = ReadWindow(target.Handle);
        if (snapshot?.Identity != target || snapshot.Kind != WindowKind.Application)
        {
            return WindowActionResult.Failure("The target window changed or is no longer eligible.");
        }

        var hwnd = new HWND(target.Handle);
        var (command, requiredStyle) = state switch
        {
            // Native maximization explicitly activates the target. There is no SW_MAXIMIZE_NOACTIVATE.
            // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-showwindow
            WindowState.Maximized => (SHOW_WINDOW_CMD.SW_MAXIMIZE, WINDOW_STYLE.WS_MAXIMIZEBOX),
            WindowState.Minimized => (SHOW_WINDOW_CMD.SW_SHOWMINNOACTIVE, WINDOW_STYLE.WS_MINIMIZEBOX),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
        if (HasState(hwnd, state)) return WindowActionResult.Applied;

        var style = (WINDOW_STYLE)(uint)(nuint)PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        if ((style & requiredStyle) == 0)
        {
            return WindowActionResult.Failure(state == WindowState.Maximized
                ? "This application window does not support maximizing."
                : "This application window does not support minimizing.");
        }

        var current = ReadWindow(target.Handle);
        if (current?.Identity != target || current.Kind != WindowKind.Application)
        {
            return WindowActionResult.Failure("The target window changed before its state could be updated.");
        }

        if (cancellationToken.IsCancellationRequested) return Canceled();
        if (!MatchesIdentity(target) || !PInvoke.ShowWindowAsync(hwnd, command))
        {
            return WindowActionResult.Failure("Windows denied the state change. The application may be elevated or unavailable.");
        }

        if (WaitUntil(() => MatchesIdentity(target) && HasState(hwnd, state), Stopwatch.StartNew(), 1200, cancellationToken))
        {
            return WindowActionResult.Applied;
        }

        // Like asynchronous placement, an accepted request may still complete after this deadline.
        return cancellationToken.IsCancellationRequested ? Canceled()
            : WindowActionResult.Failure("Windows did not confirm the requested state. The application may be busy.");
    }

    private static bool HasState(HWND hwnd, WindowState state) => state switch
    {
        WindowState.Maximized => PInvoke.IsZoomed(hwnd) && !PInvoke.IsIconic(hwnd),
        WindowState.Minimized => PInvoke.IsIconic(hwnd),
        _ => false,
    };

    private static WindowActionResult Canceled() => WindowActionResult.Failure("The window operation was canceled.");

    private static bool WaitUntil(Func<bool> condition, Stopwatch elapsed, int deadlineMs, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (condition())
            {
                return true;
            }

            if (elapsed.ElapsedMilliseconds >= deadlineMs)
            {
                return false;
            }

            token.WaitHandle.WaitOne(10);
        }

        return false;
    }

    private static bool NearlyEqual(WindowRectangle actual, WindowRectangle expected) =>
        Math.Abs((long)actual.X - expected.X) <= 1 && Math.Abs((long)actual.Y - expected.Y) <= 1
        && Math.Abs((long)actual.Width - expected.Width) <= 1 && Math.Abs((long)actual.Height - expected.Height) <= 1;

    private static bool MatchesIdentity(WindowIdentity target)
    {
        uint processId;
        var hwnd = new HWND(target.Handle);
        return PInvoke.IsWindow(hwnd)
            && PInvoke.GetWindowThreadProcessId(hwnd, &processId) == target.ThreadId
            && processId == target.ProcessId;
    }

    private static bool TryGetBounds(HWND hwnd, out WindowRectangle outer, out WindowRectangle visible)
    {
        RECT rect;
        if (!PInvoke.GetWindowRect(hwnd, &rect))
        {
            outer = visible = default;
            return false;
        }

        outer = ToRectangle(rect);
        RECT frame;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &frame, (uint)sizeof(RECT)).Value < 0)
        {
            // Outer HWND bounds are not an equivalent substitute for the promised visible rectangle.
            visible = default;
            return false;
        }

        visible = ToRectangle(frame);
        return outer.IsValid && visible.IsValid;
    }

    private bool HasExcludedOwner(HWND hwnd)
    {
        var owner = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER);
        var visited = new HashSet<nint>();
        while (!owner.IsNull)
        {
            if (visited.Count >= 16 || !visited.Add((nint)owner.Value))
            {
                return true;
            }

            uint processId;
            if (PInvoke.GetWindowThreadProcessId(owner, &processId) == 0) return true;
            var facts = Inspect(owner, processId);
            if (processId == (uint)Environment.ProcessId
                || (string.Equals(facts.ExecutableName, WindowPolicy.PaletteExecutable, StringComparison.OrdinalIgnoreCase)
                    && WindowPolicy.Classify(facts.Features, facts.ClassName, facts.ExecutableName, facts.Title, facts.Captions) != WindowKind.Application))
            {
                return true;
            }

            owner = PInvoke.GetWindow(owner, GET_WINDOW_CMD.GW_OWNER);
        }

        return false;
    }

    private WindowFacts Inspect(HWND hwnd, uint processId)
    {
        var style = (WINDOW_STYLE)(uint)(nuint)PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var extended = (WINDOW_EX_STYLE)(uint)(nuint)PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var features = WindowFeatures.None;
        if (PInvoke.IsWindowVisible(hwnd)) features |= WindowFeatures.Visible;
        if ((style & WINDOW_STYLE.WS_CHILD) != 0) features |= WindowFeatures.Child;
        if ((extended & WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0) features |= WindowFeatures.ToolWindow;
        if ((extended & WINDOW_EX_STYLE.WS_EX_NOACTIVATE) != 0) features |= WindowFeatures.NoActivate;
        if ((extended & WINDOW_EX_STYLE.WS_EX_APPWINDOW) != 0) features |= WindowFeatures.AppWindow;
        if (!PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull) features |= WindowFeatures.Owned;
        if (processId == (uint)Environment.ProcessId) features |= WindowFeatures.OwnProcess;

        uint cloaked = 0;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).Value >= 0 && cloaked != 0)
        {
            features |= WindowFeatures.Cloaked;
        }

        var buffer = stackalloc char[1024];
        var length = PInvoke.GetClassName(hwnd, buffer, 1024);
        var className = length > 0 ? new string(buffer, 0, length) : string.Empty;
        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "#32768" or "tooltips_class32" or "Xaml_WindowedPopupClass")
        {
            features |= WindowFeatures.Shell;
        }

        var imagePath = GetImagePath(processId);
        var imageName = imagePath is null ? null : Path.GetFileName(imagePath);
        var title = string.Empty;
        var captions = default(PaletteCaptions);
        if (string.Equals(imageName, WindowPolicy.PaletteExecutable, StringComparison.OrdinalIgnoreCase))
        {
            length = PInvoke.GetWindowText(hwnd, buffer, 1024);
            title = length > 0 ? new string(buffer, 0, length) : string.Empty;
            captions = _captions.GetOrAdd(imagePath!, LoadPaletteCaptions);
        }

        return new WindowFacts(features, className, imageName, title, captions);
    }

    private static string? GetImagePath(uint processId)
    {
        var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process.IsNull) return null;
        try
        {
            var buffer = stackalloc char[32768];
            uint length = 32768;
            return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, &length)
                ? new string(buffer, 0, checked((int)length)) : null;
        }
        finally
        {
            PInvoke.CloseHandle(process);
        }
    }

    private static PaletteCaptions LoadPaletteCaptions(string imagePath)
    {
        var priPath = Path.Combine(Path.GetDirectoryName(imagePath)!, "resources.pri");
        return new PaletteCaptions(LoadCaption(priPath, "AppName"), LoadCaption(priPath, "SettingsWindowTitle"));
    }

    private static string? LoadCaption(string priPath, string key)
    {
        // SHLoadIndirectString resolves the current Shell resource context, not an assumed English UI.
        var reference = $"@{{{priPath}?ms-resource://Microsoft.CommandPalette/Resources/{key}}}";
        fixed (char* source = reference)
        {
            var buffer = stackalloc char[512];
            return PInvoke.SHLoadIndirectString(source, buffer, 512, null).Value >= 0
                ? new string(buffer) : null;
        }
    }

    private static WindowRectangle ToRectangle(RECT rect) => new(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);

    private readonly record struct WindowFacts(
        WindowFeatures Features, string ClassName, string? ExecutableName, string Title, PaletteCaptions Captions);
}
