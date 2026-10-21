namespace WindowManager.Windowing;

[Flags]
internal enum WindowFeatures
{
    None = 0,
    Visible = 1,
    Cloaked = 2,
    ToolWindow = 4,
    NoActivate = 8,
    Child = 16,
    Owned = 32,
    AppWindow = 64,
    ExcludedOwner = 128,
    OwnProcess = 256,
    Shell = 512,
}

internal readonly record struct PaletteCaptions(string? Launcher, string? Settings);

// Host behavior verified against PowerToys v0.102.2933.0:
// https://github.com/microsoft/PowerToys/blob/v0.102.2933.0/src/modules/cmdpal/Microsoft.CmdPal.UI/HiddenOwnerWindowBehavior.cs
// SettingsWindow is a separate ordinary window; matching a process or WinUI class alone is unsafe.
// Resource/language or future host differences deliberately fail closed.
internal static class WindowPolicy
{
    internal const string PaletteExecutable = "Microsoft.CmdPal.UI.exe";
    internal const string WinUiClass = "WinUIDesktopWin32WindowClass";

    internal static WindowKind Classify(
        WindowFeatures features, string className, string? executableName, string title, PaletteCaptions captions)
    {
        const WindowFeatures excluded = WindowFeatures.Cloaked | WindowFeatures.ToolWindow
            | WindowFeatures.NoActivate | WindowFeatures.Child | WindowFeatures.ExcludedOwner
            | WindowFeatures.OwnProcess | WindowFeatures.Shell;
        if (!features.HasFlag(WindowFeatures.Visible) || (features & excluded) != 0)
        {
            return WindowKind.IgnoredTransient;
        }

        if (string.Equals(executableName, PaletteExecutable, StringComparison.OrdinalIgnoreCase))
        {
            // Both are windows of the SAME executable. Unknown host windows fail closed.
            // Resolve localized captions from the installed host's resources.pri; English is the tagged source fallback.
            if (Matches(title, captions.Launcher, "Command Palette"))
            {
                return WindowKind.IgnoredTransient;
            }

            if (!features.HasFlag(WindowFeatures.Owned)
                && className == WinUiClass
                && Matches(title, captions.Settings, "Command Palette Settings"))
            {
                return WindowKind.Application;
            }

            return WindowKind.Unsupported;
        }

        if (className == WinUiClass && string.IsNullOrEmpty(executableName))
        {
            // Without process identity, a normal-looking debug launcher cannot be ruled out.
            return WindowKind.Unsupported;
        }

        return features.HasFlag(WindowFeatures.Owned) && !features.HasFlag(WindowFeatures.AppWindow)
            ? WindowKind.IgnoredTransient
            : WindowKind.Application;
    }

    private static bool Matches(string title, string? localized, string fallback) =>
        title == fallback || (!string.IsNullOrEmpty(localized) && title == localized);
}
