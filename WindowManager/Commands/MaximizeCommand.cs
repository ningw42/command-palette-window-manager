using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

internal sealed partial class MaximizeCommand : InvokableCommand
{
    private readonly WindowController _windows;

    internal MaximizeCommand(WindowController windows)
    {
        _windows = windows;
        Id = "ningw.WindowManager.Maximize";
        Name = "Maximize";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override CommandResult Invoke()
    {
        var result = _windows.Maximize();
        return result.Succeeded
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(result.Error ?? "The window could not be maximized.");
    }
}
