using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class IndexEdgeTests
{
    [TestMethod]
    public void IsEmptyAndDispose()
    {
        var index = new Index.Index(4);
        Assert.IsTrue(index.IsEmpty);

        for (var i = 0; i < 50; i++)
            index.Add(i);

        Assert.IsFalse(index.IsEmpty);
        Assert.AreEqual(50, index.Count);

        index.Dispose();
    }

    [TestMethod]
    public void GrowsThroughMultipleChunks()
    {
        var index = new Index.Index(2);
        for (var i = 0; i < 10000; i++)
            index.Add(i);

        Assert.AreEqual(10000, index.Count);
        Assert.IsTrue(index.GetEnumerableFromValue(9990).SequenceEqual(Enumerable.Range(9990, 10)));
    }
}
