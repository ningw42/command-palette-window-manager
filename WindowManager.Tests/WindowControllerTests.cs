using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowManager.Windowing;

namespace WindowManager.Tests;

[TestClass]
public sealed class WindowControllerTests
{
    private static readonly WindowSnapshot App = new(new WindowIdentity(101, 10, 11), WindowKind.Application,
        new WindowRectangle(0, 40, 1920, 1040), new WindowRectangle(400, 300, 800, 600));
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
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void CanCaptureAnApplicationThatIsStillForegroundWhenInvoked(string command)
    {
        var windows = new FakeWindows { Foreground = App };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);

        Assert.IsTrue(Invoke(controller, command).Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void DoesNotGuessATargetOnColdStartInsideThePalette(string command)
    {
        var windows = new FakeWindows { Foreground = Palette };
        using var controller = new WindowController(windows);

        var result = Invoke(controller, command);

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "focus the application");
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void SettingsInThePaletteProcessCanBecomeTheTarget(string command)
    {
        var settings = new WindowSnapshot(new WindowIdentity(202, 20, 21), WindowKind.Application, App.WorkArea, App.VisibleBounds);
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[settings.Identity.Handle] = settings;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);
        controller.ObserveForeground(settings);
        controller.ObserveForeground(Palette);

        Assert.IsTrue(Invoke(controller, command).Succeeded);
        Assert.AreEqual(settings.Identity, windows.AppliedTarget);
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void AnAmbiguousHostWindowInvalidatesThePreviousTarget(string command)
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);
        controller.ObserveForeground(Palette with { Kind = WindowKind.Unsupported });

        Assert.IsFalse(Invoke(controller, command).Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("AlmostMaximize", true)]
    [DataRow("AlmostMaximize", false)]
    [DataRow("Maximize", true)]
    [DataRow("Maximize", false)]
    [DataRow("Minimize", true)]
    [DataRow("Minimize", false)]
    [DataRow("IncreaseSize", true)]
    [DataRow("IncreaseSize", false)]
    [DataRow("DecreaseSize", true)]
    [DataRow("DecreaseSize", false)]
    [DataRow("Center", true)]
    [DataRow("Center", false)]
    [DataRow("SetSize0", true)]
    [DataRow("SetSize0", false)]
    [DataRow("SetSize75", true)]
    [DataRow("SetSize75", false)]
    [DataRow("SetSize100", true)]
    [DataRow("SetSize100", false)]
    public void ClosedOrReusedHandlesAreNotChanged(string command, bool handleReused)
    {
        var windows = new FakeWindows { Foreground = Palette };
        if (handleReused)
        {
            windows.Existing[App.Identity.Handle] = App with { Identity = App.Identity with { ProcessId = 999 } };
        }

        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsFalse(Invoke(controller, command).Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void RevalidatesWindowEligibilityAtInvocation(string command)
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with { Kind = WindowKind.IgnoredTransient };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsFalse(Invoke(controller, command).Succeeded);
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
    [DataRow("AlmostMaximize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize75")]
    public void MissingWorkAreaDoesNotCausePlacement(string command)
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with { WorkArea = null };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsFalse(Invoke(controller, command).Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void ReportsNativeFailureInsteadOfClaimingSuccess(string command)
    {
        var expected = WindowActionResult.Failure("Access denied");
        var windows = new FakeWindows { Foreground = App, NextResult = expected };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);

        Assert.AreEqual(expected, Invoke(controller, command));
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void DisposalIsIdempotentAndPreventsFurtherOperations(string command)
    {
        var windows = new FakeWindows { Foreground = App };
        using var controller = new WindowController(windows);
        controller.Dispose();
        controller.Dispose();

        Assert.IsFalse(Invoke(controller, command).Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void AnUninspectableQueuedForegroundEventClearsOlderHistory(string command)
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);
        // Another application's nonzero HWND was queued, then destroyed before inspection.
        controller.ObserveForeground(null);
        controller.ObserveForeground(Palette);

        Assert.IsFalse(Invoke(controller, command).Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void SynchronizationFailureDoesNotFallBackToOlderHistory(string command)
    {
        var windows = new FakeWindows { Foreground = Palette };
        using var controller = new WindowController(windows, () => new TargetCapture(null, "Tracking is catching up"));
        controller.ObserveForeground(App);

        var result = Invoke(controller, command);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("Tracking is catching up", result.Error);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("AlmostMaximize")]
    [DataRow("Maximize")]
    [DataRow("Minimize")]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize0")]
    [DataRow("SetSize75")]
    [DataRow("SetSize100")]
    public void TheCapturedTargetIsNotReplacedByALaterHistoryUpdate(string command)
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
            Assert.IsTrue(Invoke(controller, command).Succeeded);
            Assert.AreEqual(selected.Identity, windows.AppliedTarget);
        }
    }

    [TestMethod]
    [DataRow("Maximize", true)]
    [DataRow("Minimize", false)]
    public void StateCommandsUseTheRememberedTargetWithoutRequiringGeometry(string command, bool maximize)
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with { WorkArea = null };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsTrue(Invoke(controller, command).Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
        Assert.AreEqual(maximize ? WindowState.Maximized : WindowState.Minimized, windows.AppliedState);
        Assert.IsNull(windows.AppliedBounds);
        Assert.AreEqual(1, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("IncreaseSize", 352, 274, 896, 652)]
    [DataRow("DecreaseSize", 448, 326, 704, 548)]
    public void ResizesTheRememberedApplicationUsingItsVisibleBounds(
        string command, int x, int y, int width, int height)
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsTrue(Invoke(controller, command).Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
        Assert.AreEqual(new WindowRectangle(x, y, width, height), windows.AppliedBounds);
        Assert.IsNull(windows.AppliedState);
    }

    [TestMethod]
    public void RelativeResizeUsesTheLatestGeometryRatherThanTheObservedGeometry()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with
        {
            WorkArea = new WindowRectangle(-2560, -200, 2560, 1400),
            VisibleBounds = new WindowRectangle(-2400, 0, 1600, 1000),
        };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsTrue(controller.IncreaseSize().Succeeded);
        Assert.AreEqual(new WindowRectangle(-2464, -35, 1728, 1070), windows.AppliedBounds);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IncreasingToTheCeilingUsesTrueMaximization(bool alreadyMaximized)
    {
        var current = App with
        {
            VisibleBounds = alreadyMaximized ? null : new WindowRectangle(0, 40, 1900, 1020),
            State = alreadyMaximized ? WindowState.Maximized : WindowState.Normal,
        };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        Assert.IsTrue(controller.IncreaseSize().Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
        Assert.AreEqual(WindowState.Maximized, windows.AppliedState);
        Assert.IsNull(windows.AppliedBounds);
    }

    [TestMethod]
    public void IncreasingToTheCeilingReportsNativeMaximizeFailure()
    {
        var current = App with { VisibleBounds = App.WorkArea };
        var expected = WindowActionResult.Failure("Maximization is unavailable");
        var windows = new FakeWindows { Foreground = current, NextResult = expected };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        Assert.AreEqual(expected, controller.IncreaseSize());
        Assert.AreEqual(WindowState.Maximized, windows.AppliedState);
        Assert.IsNull(windows.AppliedBounds);
    }

    [TestMethod]
    public void OneDimensionAtTheCeilingDoesNotPrematurelyMaximizeTheOther()
    {
        var current = App with { VisibleBounds = new WindowRectangle(0, 40, 1920, 832) };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        Assert.IsTrue(controller.IncreaseSize().Succeeded);
        Assert.AreEqual(new WindowRectangle(0, 40, 1920, 884), windows.AppliedBounds);
        Assert.IsNull(windows.AppliedState);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DecreasingAMaximizedWindowStartsAtTheWorkAreaNotItsRestoreBounds(bool hasVisibleBounds)
    {
        var current = App with { State = WindowState.Maximized, VisibleBounds = hasVisibleBounds ? App.VisibleBounds : null };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        Assert.IsTrue(controller.DecreaseSize().Succeeded);
        Assert.AreEqual(new WindowRectangle(48, 66, 1824, 988), windows.AppliedBounds);
        Assert.IsNull(windows.AppliedState);
    }

    [TestMethod]
    public void DecreasingAtTenPercentDoesNotShrinkFurther()
    {
        var current = App with { VisibleBounds = new WindowRectangle(400, 300, 192, 104) };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        Assert.IsTrue(controller.DecreaseSize().Succeeded);
        Assert.AreEqual(current.VisibleBounds, windows.AppliedBounds);
    }

    [TestMethod]
    [DataRow("IncreaseSize", false)]
    [DataRow("DecreaseSize", false)]
    [DataRow("IncreaseSize", true)]
    [DataRow("DecreaseSize", true)]
    [DataRow("Center", false)]
    [DataRow("Center", true)]
    [DataRow("SetSize75", false)]
    [DataRow("SetSize75", true)]
    public void PlacementRejectsMissingOrInvalidVisibleBounds(string command, bool invalid)
    {
        var current = App with { VisibleBounds = invalid ? new WindowRectangle(0, 0, 0, 100) : null };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        var result = Invoke(controller, command);
        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "visible bounds");
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    [DataRow("SetSize75")]
    public void PlacementRejectsAnInvalidWorkArea(string command)
    {
        var current = App with { WorkArea = new WindowRectangle(0, 0, 0, 1040) };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        var result = Invoke(controller, command);
        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "work area");
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow("IncreaseSize")]
    [DataRow("DecreaseSize")]
    [DataRow("Center")]
    public void PlacementDoesNotUseAMinimizedWindowsIconBounds(string command)
    {
        var current = App with { State = WindowState.Minimized };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        var result = Invoke(controller, command);
        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error, "Restore the target");
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void CenterUsesMoveOnlyPlacementForTheRememberedApplication()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);
        controller.ObserveForeground(Palette);

        Assert.IsTrue(controller.Center().Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
        Assert.AreEqual(new WindowRectangle(560, 260, 800, 600), windows.AppliedPosition);
        Assert.IsNull(windows.AppliedBounds);
        Assert.IsNull(windows.AppliedState);
        Assert.AreEqual(1, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CenterIsANoOpForMaximizedWindowsEvenWithoutGeometry(bool hasGeometry)
    {
        var current = App with
        {
            State = WindowState.Maximized,
            VisibleBounds = hasGeometry ? App.VisibleBounds : null,
            WorkArea = hasGeometry ? App.WorkArea : null,
        };
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);
        // Use the latest maximized state, not the earlier observation of a normal window.
        controller.ObserveForeground(App);

        Assert.IsTrue(controller.Center().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void CenterNoOpStillRequiresAValidTargetIdentity()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with
        {
            State = WindowState.Maximized,
            Identity = App.Identity with { ProcessId = 999 },
        };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsFalse(controller.Center().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void CenterDoesNotMoveAnAlreadyCenteredWindow()
    {
        var current = App with { VisibleBounds = new WindowRectangle(560, 260, 800, 600) };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        Assert.IsTrue(controller.Center().Succeeded);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    public void CenterUsesTheLatestWorkAreaAndSize()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with
        {
            WorkArea = new WindowRectangle(-2560, -200, 2560, 1400),
            VisibleBounds = new WindowRectangle(-2400, 0, 1600, 1000),
        };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsTrue(controller.Center().Succeeded);
        Assert.AreEqual(new WindowRectangle(-2080, 0, 1600, 1000), windows.AppliedPosition);
        Assert.IsNull(windows.AppliedBounds);
    }

    [TestMethod]
    [DataRow(1, 790, 595, 19, 10)]
    [DataRow(9, 713, 553, 173, 94)]
    [DataRow(50, 320, 340, 960, 520)]
    [DataRow(75, 80, 210, 1440, 780)]
    [DataRow(99, 0, 50, 1901, 1030)]
    public void SetSizeAppliesAbsoluteDimensionsToTheRememberedApplication(
        int percentage, int x, int y, int width, int height)
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsTrue(controller.SetSize(percentage).Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
        Assert.AreEqual(new WindowRectangle(x, y, width, height), windows.AppliedBounds);
        Assert.IsNull(windows.AppliedState);
        Assert.AreEqual(1, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(100, true)]
    public void SetSizeEndpointsUseNativeStateChangesWithoutRequiringGeometry(int percentage, bool maximize)
    {
        var current = App with { WorkArea = null, VisibleBounds = null };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        Assert.IsTrue(controller.SetSize(percentage).Succeeded);
        Assert.AreEqual(App.Identity, windows.AppliedTarget);
        Assert.AreEqual(maximize ? WindowState.Maximized : WindowState.Minimized, windows.AppliedState);
        Assert.IsNull(windows.AppliedBounds);
        Assert.AreEqual(1, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(101)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void SetSizeRejectsInvalidPercentagesBeforeCapturingATarget(int percentage)
    {
        var windows = new FakeWindows { Foreground = App };
        using var controller = new WindowController(windows, () => throw new InvalidOperationException("Must not capture a target"));

        var result = controller.SetSize(percentage);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(WindowSizePercentage.InvalidInputMessage, result.Error);
        Assert.AreEqual(0, windows.ApplyCount);
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void SetSizeRestoresNonNormalWindowsAtTheMonitorCenter(bool maximized, bool hasVisibleBounds)
    {
        var current = App with
        {
            State = maximized ? WindowState.Maximized : WindowState.Minimized,
            VisibleBounds = hasVisibleBounds ? App.VisibleBounds : null,
        };
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsTrue(controller.SetSize(75).Succeeded);
        Assert.AreEqual(new WindowRectangle(240, 170, 1440, 780), windows.AppliedBounds);
        Assert.IsNull(windows.AppliedState);
    }

    [TestMethod]
    public void SetSizeUsesTheLatestMonitorAndWindowGeometry()
    {
        var windows = new FakeWindows { Foreground = Palette };
        windows.Existing[App.Identity.Handle] = App with
        {
            WorkArea = new WindowRectangle(-2560, -200, 2560, 1400),
            VisibleBounds = new WindowRectangle(-2400, 0, 1600, 1000),
        };
        using var controller = new WindowController(windows);
        controller.ObserveForeground(App);

        Assert.IsTrue(controller.SetSize(75).Succeeded);
        Assert.AreEqual(new WindowRectangle(-2560, -25, 1920, 1050), windows.AppliedBounds);
    }

    [TestMethod]
    public void OnlyOneHundredRequestsMaximizationEvenWhenPixelRoundingFillsTheWorkArea()
    {
        var current = App with
        {
            WorkArea = new WindowRectangle(0, 0, 2, 2),
            VisibleBounds = new WindowRectangle(0, 0, 2, 2),
        };
        var windows = new FakeWindows { Foreground = current };
        windows.Existing[App.Identity.Handle] = current;
        using var controller = new WindowController(windows);

        Assert.IsTrue(controller.SetSize(99).Succeeded);
        Assert.AreEqual(current.WorkArea, windows.AppliedBounds);
        Assert.IsNull(windows.AppliedState);
    }

    private static WindowActionResult Invoke(WindowController controller, string command) => command switch
    {
        "AlmostMaximize" => controller.AlmostMaximize(),
        "Maximize" => controller.Maximize(),
        "Minimize" => controller.Minimize(),
        "IncreaseSize" => controller.IncreaseSize(),
        "DecreaseSize" => controller.DecreaseSize(),
        "Center" => controller.Center(),
        "SetSize0" => controller.SetSize(0),
        "SetSize75" => controller.SetSize(75),
        "SetSize100" => controller.SetSize(100),
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };

    private sealed class FakeWindows : IWindowSystem
    {
        internal WindowSnapshot? Foreground { get; init; }
        internal Dictionary<nint, WindowSnapshot> Existing { get; } = [];
        internal WindowIdentity? AppliedTarget { get; private set; }
        internal WindowRectangle? AppliedBounds { get; private set; }
        internal WindowRectangle? AppliedPosition { get; private set; }
        internal WindowState? AppliedState { get; private set; }
        internal int ApplyCount { get; private set; }
        internal WindowActionResult NextResult { get; init; } = WindowActionResult.Applied;

        public WindowActionResult ApplyState(WindowIdentity target, WindowState state, CancellationToken cancellationToken)
        {
            Assert.IsFalse(cancellationToken.IsCancellationRequested);
            AppliedTarget = target;
            AppliedState = state;
            ApplyCount++;
            return NextResult;
        }

        public WindowActionResult ApplyPosition(WindowIdentity target, WindowRectangle bounds, CancellationToken cancellationToken)
        {
            Assert.IsFalse(cancellationToken.IsCancellationRequested);
            AppliedTarget = target;
            AppliedPosition = bounds;
            ApplyCount++;
            return NextResult;
        }

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
