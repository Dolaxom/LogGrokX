using System;
using LogGrokX.Data.Monikers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class LogFormatEdgeTests
{
    [TestMethod]
    public void IsCorrectAcceptsValidTransformations()
    {
        var format = new LogFormat
        {
            Regex = @"(?'A'\w+)",
            Transformations = new[] { @"(?<Base64Decode>.+)" }
        };

        Assert.IsTrue(format.IsCorrect());
    }
}

[TestClass]
public class LogMetaInformationEdgeTests
{
    [TestMethod]
    public void ExposesTransformationsAndXorMask()
    {
        var format = new LogFormat
        {
            Regex = @"^(?'A'\w+)$",
            Transformations = new[] { "x" },
            XorMask = 0x2A
        };

        var meta = new LogMetaInformation(format);

        Assert.AreEqual(0x2A, meta.XorMask);
        CollectionAssert.AreEqual(new[] { "x" }, meta.Transformations);
    }
}

[TestClass]
public class RegexBasedLineParserEdgeTests
{
    [TestMethod]
    public void ParseThrowsWhenLineDoesNotMatch()
    {
        var parser = new RegexBasedLineParser(TestHelpers.CreateMeta(@"^(?'A'\w+)$"));

        Assert.Throws<InvalidOperationException>(() => parser.Parse("has spaces"));
    }

    [TestMethod]
    public void TryParseHandlesMissingOptionalGroup()
    {
        var meta = TestHelpers.CreateMeta(@"^(?'A'\w+)(?: (?'B'\w+))?$");
        var parser = new RegexBasedLineParser(meta);
        var placeholder = new int[LineMetaInformation.GetSizeInts(meta.ComponentCount)];
        var lineMeta = new LineMetaInformation(placeholder.AsSpan(), meta.ComponentCount);

        Assert.IsTrue(parser.TryParse("alpha", 0, "alpha".Length, lineMeta.ParsedLineComponents, out _));
        Assert.AreEqual(0, lineMeta.ParsedLineComponents.ComponentLength(1));
    }
}
