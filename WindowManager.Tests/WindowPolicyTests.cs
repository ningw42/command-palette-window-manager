using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowManager.Windowing;

namespace WindowManager.Tests;

[TestClass]
public sealed class WindowPolicyTests
{
    [TestMethod]
    public void RejectsThePaletteEvenWithOrdinaryDebuggerWindowStyles()
    {
        Assert.AreEqual(WindowKind.IgnoredTransient, Classify("Command Palette"));
    }

    [TestMethod]
    public void AllowsCommandPaletteSettingsInTheSameProcess()
    {
        Assert.AreEqual(WindowKind.Application, Classify("Command Palette Settings"));
    }

    [TestMethod]
    public void UsesResolvedLocalizedCaptionsRatherThanEnglishOnlyMatching()
    {
        var captions = new PaletteCaptions("Lokalisierte Palette", "Lokalisierte Einstellungen");
        Assert.AreEqual(WindowKind.Application, Classify(captions.Settings!, captions: captions));
        Assert.AreEqual(WindowKind.IgnoredTransient, Classify(captions.Launcher!, captions: captions));
    }

    [TestMethod]
    public void UnknownHostWindowsFailClosed()
    {
        Assert.AreEqual(WindowKind.Unsupported, Classify("Unrecognized host surface"));
    }

    [TestMethod]
    public void NormalPowerToysSettingsIsNotExcludedByProductName()
    {
        Assert.AreEqual(WindowKind.Application, WindowPolicy.Classify(
            WindowFeatures.Visible, WindowPolicy.WinUiClass, "PowerToys.Settings.exe", "PowerToys Settings", default));
    }

    [TestMethod]
    [DataRow((int)WindowFeatures.ToolWindow)]
    [DataRow((int)WindowFeatures.NoActivate)]
    [DataRow((int)WindowFeatures.ExcludedOwner)]
    [DataRow((int)WindowFeatures.Cloaked)]
    [DataRow((int)WindowFeatures.Shell)]
    [DataRow((int)WindowFeatures.Child)]
    [DataRow((int)WindowFeatures.OwnProcess)]
    public void IgnoresTransientAndNonApplicationWindows(int feature)
    {
        Assert.AreEqual(WindowKind.IgnoredTransient, Classify("Command Palette Settings", WindowFeatures.Visible | (WindowFeatures)feature));
    }

    [TestMethod]
    public void DoesNotAcceptAnOwnedPopupWithASettingsCaption()
    {
        Assert.AreEqual(WindowKind.Unsupported, Classify("Command Palette Settings", WindowFeatures.Visible | WindowFeatures.Owned));
    }

    [TestMethod]
    public void KeepsOrdinaryTaskbarVisibleOwnedApplicationWindows()
    {
        Assert.AreEqual(WindowKind.Application, WindowPolicy.Classify(
            WindowFeatures.Visible | WindowFeatures.Owned | WindowFeatures.AppWindow,
            "ApplicationWindow", "application.exe", string.Empty, default));
    }

    [TestMethod]
    public void CannotTreatAnUnidentifiedWinUiWindowAsAnApplication()
    {
        Assert.AreEqual(WindowKind.Unsupported, WindowPolicy.Classify(
            WindowFeatures.Visible, WindowPolicy.WinUiClass, null, string.Empty, default));
    }

    private static WindowKind Classify(string title, WindowFeatures features = WindowFeatures.Visible, PaletteCaptions captions = default) =>
        WindowPolicy.Classify(features, WindowPolicy.WinUiClass, WindowPolicy.PaletteExecutable, title, captions);
}
