using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

internal sealed partial class AlmostMaximizedCommand : InvokableCommand
{
    private readonly WindowController _windows;

    internal AlmostMaximizedCommand(WindowController windows)
    {
        _windows = windows;
        Id = "ningw.WindowManager.AlmostMaximized";
        Name = "Almost Maximized";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override CommandResult Invoke()
    {
        var result = _windows.AlmostMaximize();
        return result.Succeeded
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(result.Error ?? "The window could not be resized.");
    }
}
