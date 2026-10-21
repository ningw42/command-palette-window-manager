using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowManager.Windowing;

namespace WindowManager.Tests;

[TestClass]
public sealed class WindowSizePercentageTests
{
    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("1", 1)]
    [DataRow("9", 9)]
    [DataRow("75", 75)]
    [DataRow("99", 99)]
    [DataRow("100", 100)]
    [DataRow(" 75 ", 75)]
    [DataRow("\t075\r\n", 75)]
    [DataRow("000", 0)]
    public void ParsesWholePercentages(string input, int expected)
    {
        Assert.IsTrue(WindowSizePercentage.TryParse(input, out var percentage));
        Assert.AreEqual(expected, percentage);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("-1")]
    [DataRow("-0")]
    [DataRow("+75")]
    [DataRow("101")]
    [DataRow("75.0")]
    [DataRow("75,0")]
    [DataRow("75%")]
    [DataRow("1e2")]
    [DataRow("0x4B")]
    [DataRow("7 5")]
    [DataRow("75\n25")]
    [DataRow("75\0")]
    [DataRow("2147483648")]
    [DataRow("99999999999999999999999999")]
    [DataRow("１２")]
    [DataRow("١٢")]
    public void RejectsInvalidInputWithoutTreatingItAsZero(string? input)
    {
        Assert.IsFalse(WindowSizePercentage.TryParse(input, out _));
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("fr-FR")]
    [DataRow("ar-SA")]
    public void ParsingDoesNotDependOnTheCurrentCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            for (var expected = 0; expected <= 100; expected++)
            {
                Assert.IsTrue(WindowSizePercentage.TryParse(expected.ToString(CultureInfo.InvariantCulture), out var percentage));
                Assert.AreEqual(expected, percentage);
            }

            Assert.IsFalse(WindowSizePercentage.TryParse("75,5", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
