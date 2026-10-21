using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowManager.Windowing;

namespace WindowManager.Tests;

[TestClass]
public sealed class NativeSmokeTests
{
    [TestMethod]
    public void ForegroundInspectionAndHookLifetimeDoNotRequireChangingAnyWindows()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Assert.Inconclusive("Requires Windows 10 version 2004 or later.");
            return;
        }

        var windows = new Win32WindowSystem();
        _ = windows.ReadForegroundWindow();
        using var monitor = new ForegroundWindowMonitor(windows, _ => { }, () => null);
        Assert.IsNull(monitor.Error);
        Assert.IsTrue(monitor.IsRunning);
        Assert.IsNull(monitor.CaptureTarget().Error);
        Assert.IsNull(monitor.CaptureTarget().Error);

        monitor.Dispose();
        Assert.IsFalse(monitor.IsRunning);
        Assert.IsNull(monitor.Error);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void AlreadyCanceledStateChangeDoesNotInspectOrChangeAWindow(bool maximize)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Assert.Inconclusive("Requires Windows.");
            return;
        }

        var state = maximize ? WindowState.Maximized : WindowState.Minimized;
        var result = new Win32WindowSystem().ApplyState(default, state, new CancellationToken(true));
        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "canceled");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void StateChangesRejectAnInvalidHandleWithoutChangingAnyWindows(bool maximize)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Assert.Inconclusive("Requires Windows.");
            return;
        }

        var state = maximize ? WindowState.Maximized : WindowState.Minimized;
        Assert.IsFalse(new Win32WindowSystem().ApplyState(default, state, CancellationToken.None).Succeeded);
    }

    [TestMethod]
    public void AlreadyCanceledPlacementDoesNotEvenInspectOrRestoreAWindow()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Assert.Inconclusive("Requires Windows.");
            return;
        }

        var result = new Win32WindowSystem().ApplyBounds(default, new WindowRectangle(0, 0, 100, 100), new CancellationToken(true));
        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "canceled");
    }
}
