using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowManager.Windowing;

namespace WindowManager.Tests;

[TestClass]
public sealed class WindowLayoutTests
{
    [TestMethod]
    [DataRow(0, 0, 1920, 1040, 192, 104, 1536, 832)]
    [DataRow(0, 40, 1920, 1040, 192, 144, 1536, 832)]
    [DataRow(-2560, -200, 2560, 1400, -2304, -60, 2048, 1120)]
    [DataRow(1920, 0, 3840, 2080, 2304, 208, 3072, 1664)]
    [DataRow(0, 0, 1001, 777, 100, 78, 800, 621)]
    [DataRow(0, 0, 1, 1, 0, 0, 1, 1)]
    public void CentersEightyPercentInsideThePhysicalWorkArea(
        int x, int y, int width, int height, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var actual = WindowLayout.AlmostMaximized(new WindowRectangle(x, y, width, height));

        Assert.AreEqual(new WindowRectangle(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    [TestMethod]
    [DataRow(0, 1080)]
    [DataRow(1920, 0)]
    [DataRow(-1, 1080)]
    public void RejectsInvalidWorkAreas(int width, int height)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WindowLayout.AlmostMaximized(new WindowRectangle(0, 0, width, height)));
    }

    [TestMethod]
    [DataRow(true, 200, 200, 600, 400, 175, 180, 650, 440)]
    [DataRow(false, 200, 200, 600, 400, 225, 220, 550, 360)]
    [DataRow(true, 0, 0, 200, 160, 0, 0, 250, 200)]
    [DataRow(true, 900, 720, 100, 80, 850, 680, 150, 120)]
    [DataRow(true, 0, 0, 980, 400, 0, 0, 1000, 440)]
    [DataRow(true, 0, 0, 980, 780, 0, 0, 1000, 800)]
    [DataRow(false, 200, 200, 140, 100, 220, 210, 100, 80)]
    [DataRow(false, 200, 200, 100, 80, 200, 200, 100, 80)]
    [DataRow(false, -200, -200, 1400, 1200, 0, 0, 1000, 800)]
    [DataRow(true, -2000, 2000, 600, 400, 0, 360, 650, 440)]
    public void ResizesBothDimensionsAndClampsToTheWorkArea(
        bool increase, int x, int y, int width, int height,
        int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var actual = WindowLayout.ResizeByFivePercent(
            new WindowRectangle(x, y, width, height), new WindowRectangle(0, 0, 1000, 800), increase);

        Assert.AreEqual(new WindowRectangle(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    [TestMethod]
    public void ResizesInPhysicalPixelsOnANegativeCoordinateMonitor()
    {
        var actual = WindowLayout.ResizeByFivePercent(
            new WindowRectangle(-2400, 0, 1600, 1000), new WindowRectangle(-2560, -200, 2560, 1400), true);

        Assert.AreEqual(new WindowRectangle(-2464, -35, 1728, 1070), actual);
    }

    [TestMethod]
    public void RespectsATopTaskbarAndHighResolutionMonitor()
    {
        var actual = WindowLayout.ResizeByFivePercent(
            new WindowRectangle(1920, 40, 3000, 1600), new WindowRectangle(1920, 40, 3840, 2080), true);

        Assert.AreEqual(new WindowRectangle(1920, 40, 3192, 1704), actual);
    }

    [TestMethod]
    public void RoundsStepsToPixelsAndNeverRoundsTheMinimumBelowTenPercent()
    {
        var workArea = new WindowRectangle(0, 0, 1001, 777);
        Assert.AreEqual(new WindowRectangle(175, 81, 550, 439), WindowLayout.ResizeByFivePercent(
            new WindowRectangle(200, 100, 500, 400), workArea, true));
        Assert.AreEqual(new WindowRectangle(224, 119, 101, 78), WindowLayout.ResizeByFivePercent(
            new WindowRectangle(200, 100, 150, 116), workArea, false));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TinyWorkAreasStillProduceValidBounds(bool increase)
    {
        var workArea = new WindowRectangle(-1, 40, 1, 1);
        Assert.AreEqual(workArea, WindowLayout.ResizeByFivePercent(workArea, workArea, increase));
    }

    [TestMethod]
    [DataRow(0, 100, 1000, 800)]
    [DataRow(100, -1, 1000, 800)]
    [DataRow(100, 100, 0, 800)]
    [DataRow(100, 100, 1000, -1)]
    public void RelativeResizeRejectsInvalidRectangles(int width, int height, int workWidth, int workHeight)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WindowLayout.ResizeByFivePercent(
            new WindowRectangle(0, 0, width, height), new WindowRectangle(0, 0, workWidth, workHeight), true));
    }

    [TestMethod]
    [DataRow(1001, 777)]
    [DataRow(1920, 1040)]
    [DataRow(29, 11)]
    [DataRow(1, 1)]
    public void RepeatedResizingReachesAndStaysAtBothLimits(int width, int height)
    {
        var workArea = new WindowRectangle(-width, 40, width, height);
        var bounds = workArea;
        for (var i = 0; i < 40; i++) bounds = WindowLayout.ResizeByFivePercent(bounds, workArea, false);
        Assert.AreEqual((width + 9) / 10, bounds.Width);
        Assert.AreEqual((height + 9) / 10, bounds.Height);
        Assert.AreEqual(bounds, WindowLayout.ResizeByFivePercent(bounds, workArea, false));

        for (var i = 0; i < 40; i++) bounds = WindowLayout.ResizeByFivePercent(bounds, workArea, true);
        Assert.AreEqual(workArea, bounds);
        Assert.AreEqual(bounds, WindowLayout.ResizeByFivePercent(bounds, workArea, true));
    }

    [TestMethod]
    public void AddsInvisibleResizeBordersOutsideTheRequestedVisibleRectangle()
    {
        var actual = WindowLayout.IncludeInvisibleFrame(
            new WindowRectangle(192, 104, 1536, 832),
            new WindowRectangle(93, 100, 814, 607),
            new WindowRectangle(100, 100, 800, 600));

        Assert.AreEqual(new WindowRectangle(185, 104, 1550, 839), actual);
    }
}
