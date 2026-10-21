using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

internal sealed partial class MinimizeCommand : InvokableCommand
{
    private readonly WindowController _windows;

    internal MinimizeCommand(WindowController windows)
    {
        _windows = windows;
        Id = "ningw.WindowManager.Minimize";
        Name = "Minimize";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override CommandResult Invoke()
    {
        var result = _windows.Minimize();
        return result.Succeeded
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(result.Error ?? "The window could not be minimized.");
    }
}
