namespace WindowManager.Windowing;

internal readonly record struct WindowIdentity(nint Handle, uint ProcessId, uint ThreadId);

// All rectangles use physical virtual-screen pixels, never WINDOWPLACEMENT workspace coordinates.
internal readonly record struct WindowRectangle(int X, int Y, int Width, int Height)
{
    internal bool IsValid => Width > 0 && Height > 0;
}

internal enum WindowKind
{
    Application,
    IgnoredTransient,
    Unsupported,
}

internal enum WindowState
{
    Normal,
    Maximized,
    Minimized,
}

internal sealed record WindowSnapshot(
    WindowIdentity Identity,
    WindowKind Kind,
    WindowRectangle? WorkArea,
    WindowRectangle? VisibleBounds = null,
    WindowState State = WindowState.Normal);

internal readonly record struct TargetCapture(WindowIdentity? Target, string? Error);

public readonly record struct WindowActionResult(bool Succeeded, string? Error)
{
    internal static WindowActionResult Applied => new(true, null);

    internal static WindowActionResult Failure(string message) => new(false, message);
}

// The native adapter owns HWND inspection, eligibility, and bounded placement requests.
internal interface IWindowSystem
{
    WindowSnapshot? ReadForegroundWindow();

    WindowSnapshot? ReadWindow(nint handle);

    WindowActionResult ApplyBounds(WindowIdentity target, WindowRectangle bounds, CancellationToken cancellationToken);

    WindowActionResult ApplyState(WindowIdentity target, WindowState state, CancellationToken cancellationToken);
}
