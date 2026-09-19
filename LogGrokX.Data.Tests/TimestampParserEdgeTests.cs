using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class TimestampParserEdgeTests
{
    [TestMethod]
    public void ParsesCustomDateFormat()
    {
        var expected = new DateTime(2024, 1, 2, 3, 4, 5).Ticks;

        Assert.IsTrue(TimestampParser.TryGetTicks("02.01.2024 03:04:05", "dd.MM.yyyy HH:mm:ss", out var ticks));
        Assert.AreEqual(expected, ticks);
    }

    [TestMethod]
    public void ParsesCustomTimeOnlyFormat()
    {
        var expected = new TimeSpan(9, 5, 3).Ticks;

        Assert.IsTrue(TimestampParser.TryGetTicks("9:05:03", "H:mm:ss", out var ticks));
        Assert.AreEqual(expected, ticks);
    }

    [TestMethod]
    public void RejectsDateWithWrongFractionSeparator()
    {
        Assert.IsFalse(TimestampParser.TryGetTicks("2024-01-02 03:04:05X678", "yyyy-MM-dd HH:mm:ss.fff", out _));
    }

    [TestMethod]
    public void RejectsDateWithNonDigitSeconds()
    {
        Assert.IsFalse(TimestampParser.TryGetTicks("2024-01-02 03:04:xx.678", "yyyy-MM-dd HH:mm:ss.fff", out _));
    }

    [TestMethod]
    public void RejectsDateWithNonDigitMilliseconds()
    {
        Assert.IsFalse(TimestampParser.TryGetTicks("2024-01-02 03:04:05.6x8", "yyyy-MM-dd HH:mm:ss.fff", out _));
    }

    [TestMethod]
    public void RejectsTimeWithWrongSeparator()
    {
        Assert.IsFalse(TimestampParser.TryGetTicks("12x34:56.789", "HH:mm:ss.fff", out _));
    }

    [TestMethod]
    public void RejectsTimeWithWrongFractionSeparator()
    {
        Assert.IsFalse(TimestampParser.TryGetTicks("12:34:56X789", "HH:mm:ss.fff", out _));
    }

    [TestMethod]
    public void RejectsTimeWithNonDigitSeconds()
    {
        Assert.IsFalse(TimestampParser.TryGetTicks("12:34:xx.789", "HH:mm:ss.fff", out _));
    }

    [TestMethod]
    public void RejectsTimeWithNonDigitMilliseconds()
    {
        Assert.IsFalse(TimestampParser.TryGetTicks("12:34:56.x89", "HH:mm:ss.fff", out _));
    }
}
