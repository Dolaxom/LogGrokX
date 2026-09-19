using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class LoaderEdgeTests
{
    [TestMethod]
    public void FaultedLoadIsObservedAndDisposed()
    {
        var path = TestHelpers.WriteTempFile("hello");
        var logFile = new LogFile(path, 0);
        _ = logFile.Encoding;
        File.Delete(path);

        var loader = new Loader(logFile, new LineDataConsumerMock(new LineIndexMock()), NullLogger.Instance);
        loader.Dispose();

        Assert.IsFalse(loader.IsLoading);
    }
}

[TestClass]
public class MaskedStreamEdgeTests
{
    [TestMethod]
    public void ReadWithUnalignedBufferOffset()
    {
        var original = Encoding.UTF8.GetBytes("masked data");
        var masked = new byte[original.Length];
        for (var i = 0; i < original.Length; i++)
            masked[i] = (byte)(original[i] ^ 0x33);

        var path = TestHelpers.WriteTempBytes(masked);
        try
        {
            var logFile = new LogFile(path, 0x33);
            using var stream = logFile.OpenForSequentialRead();
            var buffer = new byte[original.Length + 1];

            var read = stream.Read(buffer, 1, original.Length);

            Assert.AreEqual(original.Length, read);
            CollectionAssert.AreEqual(original, buffer[1..]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

[TestClass]
public class TimeIndexUpperBoundTests
{
    [TestMethod]
    public void FindLineRangeWithUpperBoundBelowAll()
    {
        var index = new TimeIndex();
        index.Add(new DateTime(2024, 1, 1).Ticks);
        index.Add(new DateTime(2024, 1, 2).Ticks);

        var range = index.FindLineRange(0, 0);

        Assert.IsNotNull(range);
        Assert.AreEqual(0, range.Value.StartLine);
        Assert.AreEqual(0, range.Value.EndLine);
    }
}
