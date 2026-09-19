using LogGrokX.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class IndexKeyEdgeTests
{
    [TestMethod]
    public void EqualsObjectHandlesBoxedAndForeignValues()
    {
        var meta = TestHelpers.CreateMeta(@"^(?'A'\w+) (?'B'\w+)$");
        var first = TestHelpers.CreateIndexKey(meta, "alpha beta", false, out _);
        var same = TestHelpers.CreateIndexKey(meta, "alpha beta", false, out _);
        var different = TestHelpers.CreateIndexKey(meta, "alpha gamma", false, out _);

        Assert.IsTrue(first.Equals((object)same));
        Assert.IsFalse(first.Equals((object)different));
        Assert.IsFalse(first.Equals("alpha beta"));
        Assert.IsFalse(first.Equals(null));
    }

    [TestMethod]
    public void IndexKeyNumEqualsObjectHandlesBoxedAndForeignValues()
    {
        var first = new IndexKeyNum { KeyNum = 1 };
        var same = new IndexKeyNum { KeyNum = 1 };
        var different = new IndexKeyNum { KeyNum = 2 };

        Assert.IsTrue(first.Equals((object)same));
        Assert.IsFalse(first.Equals((object)different));
        Assert.IsFalse(first.Equals("1"));
        Assert.IsFalse(first.Equals(null));
    }
}
