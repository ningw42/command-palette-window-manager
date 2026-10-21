using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowManager.Windowing;

namespace WindowManager.Tests;

[TestClass]
public sealed class WindowControllerTests
{
    private static readonly WindowSnapshot App = new(new WindowIdentity(101, 10, 11), WindowKind.Application, new WindowRectangle(0, 40, 1920, 1040));
    private static readonly WindowSnapshot Palette = new(new WindowIdentity(201, 20, 21), WindowKind.IgnoredTransient, null);

    [TestMethod]
    public void OperatesOnTheRememberedApplicationNotTheForegroundPalette()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);
        controller.ObserveForeground(Palette);

        var result = controller.AlmostMaximize();

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
        Assert.AreEqual(new WindowRectangle(192, 144, 1536, 832), windows.AppliedBounds);
    }

    [TestMethod]
    public void CanCaptureAnApplicationThatIsStillForegroundWhenInvoked()
    {
        var windows = new FakeWindows { Foreground = App };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);

        Assert.IsTrue(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
    }

    [TestMethod]
    public void DoesNotGuessATargetOnColdStartInsideThePalette()
    {
        var windows = new FakeWindows { Foreground = Palette };
        using var controller = new WindowController(windows);

        var result = controller.AlmostMaximize();

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "focus the application");
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void SettingsInThePaletteProcessCanBecomeTheTarget()
    {
        var settings = new WindowSnapshot(new WindowIdentity(202, 20, 21), WindowKind.Application, App.WorkArea);
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[settings.Identity.Handle] = settings;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);
        controller.ObserveForeground(settings);
        controller.ObserveForeground(Palette);

        Assert.IsTrue(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(settings.Identity, windows.AppliedTarget);
    }

    [TestMethod]
    public void AnAmbiguousHostWindowInvalidatesThePreviousTarget()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);
        controller.ObserveForeground(Palette with { Kind = WindowKind.Unsupported });

        Assert.IsFalse(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ClosedOrReusedHandlesAreNotResized(bool handleReused)
    {
        var windows = new FakeWindows { Foreground = Palette };
        if (handleReused)
        {
            windows.Existing[App.Identity.Handle] = App with { Identity = App.Identity with { ProcessId = 999 } };
        }

        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsFalse(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void RevalidatesWindowEligibilityAtInvocation()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with { Kind = WindowKind.IgnoredTransient };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsFalse(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void UsesTheLatestWorkAreaRatherThanTheObservedOne()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with { WorkArea = new WindowRectangle(-2560, 0, 2560, 1400) };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsTrue(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(new WindowRectangle(-2304, 140, 2048, 1120), windows.AppliedBounds);
    }

    [TestMethod]
    public void MissingWorkAreaDoesNotCauseAResize()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with { WorkArea = null };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsFalse(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void ReportsNativeFailureInsteadOfClaimingSuccess()
    {
        var expected = WindowActionResult.Failure("Access denied");
        var windows = new FakeWindows { Foreground = App, NextResult = expected };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);

        Assert.AreEqual(expected, controller.AlmostMaximize());
    }

    [TestMethod]
    public void DisposalIsIdempotentAndPreventsFurtherOperations()
    {
        var windows = new FakeWindows { Foreground = App };
        using var controller = new WindowController(windows);
        controller.Dispose();
        controller.Dispose();

        Assert.IsFalse(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void AnUninspectableQueuedForegroundEventClearsOlderHistory()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);
        // Another application's nonzero HWND was queued, then destroyed before inspection.
        controller.ObserveForeground(null);
        controller.ObserveForeground(Palette);

        Assert.IsFalse(controller.AlmostMaximize().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void SynchronizationFailureDoesNotFallBackToOlderHistory()
    {
        var windows = new FakeWindows { Foreground = Palette };
        using var controller = new WindowController(windows, () => new TargetCapture(null, "Tracking is catching up"));
        controller.ObserveForeground(App);

        var result = controller.AlmostMaximize();

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("Tracking is catching up", result.Error);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void TheCapturedTargetIsNotReplacedByALaterHistoryUpdate()
    {
        var selected = App with { Identity = new WindowIdentity(303, 30, 31) };
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[selected.Identity.Handle] = selected;
        WindowController? controller = null;
        controller = new WindowController(windows, () =>
        {
            controller!.ObserveForeground(App);
            return new TargetCapture(selected.Identity, null);
        });
        using (controller)
        {
            Assert.IsTrue(controller.AlmostMaximize().Succeeded);
            Assert.AreEqual(selected.Identity, windows.AppliedTarget);
        }
    }

    private sealed class FakeWindows : IWindowSystem
    {
        internal WindowSnapshot? Foreground { get; init; }
        internal Dictionary<nint, WindowSnapshot> Existing { get; } = [];
        internal WindowIdentity? AppliedTarget { get; private set; }
        internal WindowRectangle? AppliedBounds { get; private set; }
        internal int ApplyCount { get; private set; }
        internal WindowActionResult NextResult { get; init; } = WindowActionResult.Applied;

        public WindowSnapshot? ReadForegroundWindow() => Foreground;
        public WindowSnapshot? ReadWindow(nint handle) => Existing.GetValueOrDefault(handle);
        public WindowActionResult ApplyBounds(WindowIdentity target, WindowRectangle bounds, CancellationToken cancellationToken)
        {
            Assert.IsFalse(cancellationToken.IsCancellationRequested);
            AppliedTarget = target;
            AppliedBounds = bounds;
            ApplyCount++;
            return NextResult;
        }
    }
}
