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
    public void AddsInvisibleResizeBordersOutsideTheRequestedVisibleRectangle()
    {
        var actual = WindowLayout.IncludeInvisibleFrame(
            new WindowRectangle(192, 104, 1536, 832),
            new WindowRectangle(93, 100, 814, 607),
            new WindowRectangle(100, 100, 800, 600));

        Assert.AreEqual(new WindowRectangle(185, 104, 1550, 839), actual);
    }
}
