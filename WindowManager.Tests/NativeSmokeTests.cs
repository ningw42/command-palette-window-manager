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
    [DataRow(true)]
    [DataRow(false)]
    public void PlacementRejectsAnInvalidHandleWithoutChangingAnyWindows(bool moveOnly)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Assert.Inconclusive("Requires Windows.");
            return;
        }

        var windows = new Win32WindowSystem();
        var bounds = new WindowRectangle(0, 0, 100, 100);
        var result = moveOnly ? windows.ApplyPosition(default, bounds, CancellationToken.None)
            : windows.ApplyBounds(default, bounds, CancellationToken.None);
        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void AlreadyCanceledPlacementDoesNotEvenInspectOrRestoreAWindow(bool moveOnly)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Assert.Inconclusive("Requires Windows.");
            return;
        }

        var windows = new Win32WindowSystem();
        var bounds = new WindowRectangle(0, 0, 100, 100);
        var token = new CancellationToken(true);
        var result = moveOnly ? windows.ApplyPosition(default, bounds, token) : windows.ApplyBounds(default, bounds, token);
        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "canceled");
    }
}
