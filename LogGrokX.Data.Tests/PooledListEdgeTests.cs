using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class PooledListEdgeTests
{
    [TestMethod]
    public void AllocateSpanGrowsPooledArray()
    {
        using var list = new PooledList<int>(2);

        var span = list.AllocateSpan(1000);
        span[999] = 42;

        Assert.AreEqual(42, list[999]);
    }

    [TestMethod]
    public void IndexOfHandlesNullElements()
    {
        using var list = new PooledList<string?>();
        list.Add(null);
        list.Add("a");

        Assert.AreEqual(0, list.IndexOf(null));
        Assert.AreEqual(1, list.IndexOf("a"));
        Assert.AreEqual(-1, list.IndexOf("missing"));
    }

    [TestMethod]
    public void NonGenericEnumeratorYieldsItems()
    {
        using var list = new PooledList<int>();
        list.Add(1);
        list.Add(2);

        var values = new List<int>();
        var enumerator = ((IEnumerable)list).GetEnumerator();
        while (enumerator.MoveNext())
            values.Add((int)enumerator.Current!);

        CollectionAssert.AreEqual(new[] { 1, 2 }, values);
    }
}
