using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

internal sealed partial class IncreaseSizeCommand : InvokableCommand
{
    private readonly WindowController _windows;

    internal IncreaseSizeCommand(WindowController windows)
    {
        _windows = windows;
        Id = "ningw.WindowManager.IncreaseSize";
        Name = "Increase by 5%";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override CommandResult Invoke()
    {
        var result = _windows.IncreaseSize();
        return result.Succeeded
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(result.Error ?? "The window could not be resized.");
    }
}
