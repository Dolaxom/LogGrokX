using System.Collections.Generic;
using LogGrokX.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class CountIndexTests
{
    [TestMethod]
    public void AddAtGranularityCapturesSnapshots()
    {
        var indices = new Dictionary<IndexKeyNum, LogGrokX.Data.Index.Index>();
        var countIndex = new CountIndex<LogGrokX.Data.Index.Index>(indices);
        var key = new IndexKeyNum { KeyNum = 1 };
        var index = new LogGrokX.Data.Index.Index(16);
        index.Add(0);
        indices[key] = index;

        countIndex.Add(0, indices);
        Assert.AreEqual(1, countIndex.Counts.Count);

        countIndex.Add(CountIndex<LogGrokX.Data.Index.Index>.Granularity, indices);
        Assert.AreEqual(2, countIndex.Counts.Count);

        countIndex.Finish(indices);
        Assert.AreEqual(2, countIndex.Counts.Count);
    }
}
