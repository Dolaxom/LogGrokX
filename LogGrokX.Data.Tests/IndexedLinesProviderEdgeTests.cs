using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class IndexedLinesProviderEdgeTests
{
    private const string Format = @"^(?'Level'[A-Z]+) (?'Message'.*)$";

    [TestMethod]
    public void ProviderFindsAndClampsIndices()
    {
        var path = TestHelpers.WriteTempFile("INFO a\r\nERROR b\r\nWARN c");
        try
        {
            var meta = TestHelpers.CreateMeta(Format, new[] { "Level" });
            var model = TestHelpers.LoadFile(path, meta);

            Assert.AreEqual(3, model.LineProvider.Count);
            Assert.AreEqual(meta, model.MetaInformation);
            Assert.IsNotNull(model.LineParser);

            var provider = model.Indexer.GetIndexedLinesProvider(new Dictionary<int, IEnumerable<string>>());

            Assert.AreEqual(3, provider.Count);
            Assert.AreEqual(0, provider.GetIndexByValue(0));
            Assert.AreEqual(1, provider.GetIndexByValue(1));
            Assert.AreEqual(2, provider.GetIndexByValue(20000));

            var values = new int[3];
            provider.Fetch(0, values);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, values);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
