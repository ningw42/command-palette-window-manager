using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

internal sealed partial class CenterCommand : InvokableCommand
{
    private readonly WindowController _windows;

    internal CenterCommand(WindowController windows)
    {
        _windows = windows;
        Id = "ningw.WindowManager.Center";
        Name = "Center";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override CommandResult Invoke()
    {
        var result = _windows.Center();
        return result.Succeeded
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(result.Error ?? "The window could not be centered.");
    }
}
