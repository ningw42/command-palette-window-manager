using System;
using Microsoft.CommandPalette.Extensions.Toolkit;
using WindowManager.Windowing;

namespace WindowManager;

internal sealed partial class SetWindowSizeCommand : InvokableCommand
{
    private readonly WindowController _windows;
    private readonly int _percentage;
    private readonly Func<string> _getInput;

    internal SetWindowSizeCommand(WindowController windows, int percentage, Func<string> getInput)
    {
        _windows = windows;
        _percentage = percentage;
        _getInput = getInput;
        Id = $"ningw.WindowManager.SetSize.{percentage}";
        Name = percentage switch { 0 => "Minimize", 100 => "Maximize", _ => "Resize" };
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override CommandResult Invoke()
    {
        if (!WindowSizePercentage.TryParse(_getInput(), out var current))
        {
            return CommandResult.ShowToast(WindowSizePercentage.InvalidInputMessage);
        }

        // The host may still hold an old result while the input page is refreshing.
        if (current != _percentage)
        {
            return CommandResult.ShowToast("The size changed. Select the updated result and try again.");
        }

        var result = _windows.SetSize(_percentage);
        return result.Succeeded
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(result.Error ?? "The window size could not be set.");
    }
}
