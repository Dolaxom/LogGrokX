using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class SearchEdgeTests
{
    private const string Format = @"^(?'Level'[A-Z]+) (?'Message'.*)$";

    [TestMethod]
    public async Task FindsMatchInVeryLongLine()
    {
        var longMessage = new string('x', 5000) + " needle";
        var path = TestHelpers.WriteTempFile("INFO short\r\nWARN " + longMessage);
        try
        {
            var model = TestHelpers.LoadFile(path, TestHelpers.CreateMeta(Format, new[] { "Level" }));
            var (progress, _, lineIndex) = LogGrokX.Data.Search.Search.CreateSearchIndex(
                model, new Regex("needle"), CancellationToken.None);

            await progress.Completion;

            Assert.AreEqual(1, lineIndex.Count);
            Assert.AreEqual(1, lineIndex.GetLine(0).sourceIndex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LineRangeOutsideDataCompletesEmpty()
    {
        var path = TestHelpers.WriteTempFile("INFO a\r\nERROR b");
        try
        {
            var model = TestHelpers.LoadFile(path, TestHelpers.CreateMeta(Format, new[] { "Level" }));
            var (progress, _, lineIndex) = LogGrokX.Data.Search.Search.CreateSearchIndex(
                model, new Regex("INFO"), CancellationToken.None, (100, 200));

            await progress.Completion;

            Assert.AreEqual(0, lineIndex.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ProgressValueIsReadable()
    {
        var progress = new LogGrokX.Data.Search.Search.Progress();
        progress.Value = 0.5;

        Assert.AreEqual(0.5, progress.Value);
    }

    [TestMethod]
    public async Task StartConsumersSwallowsCancellation()
    {
        var channel = Channel.CreateUnbounded<int>();
        var task = channel.StartConsumers(_ => Task.FromException(new OperationCanceledException()), 2);

        await task;
    }
}
