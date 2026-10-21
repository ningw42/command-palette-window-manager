using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

internal sealed partial class DecreaseSizeCommand : InvokableCommand
{
    private readonly WindowController _windows;

    internal DecreaseSizeCommand(WindowController windows)
    {
        _windows = windows;
        Id = "ningw.WindowManager.DecreaseSize";
        Name = "Decrease by 5%";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override CommandResult Invoke()
    {
        var result = _windows.DecreaseSize();
        return result.Succeeded
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(result.Error ?? "The window could not be resized.");
    }
}
