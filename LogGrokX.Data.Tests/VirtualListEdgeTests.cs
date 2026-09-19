using System;
using System.Collections;
using System.Collections.Generic;
using LogGrokX.Data.Virtualization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class VirtualListEdgeTests
{
    [TestMethod]
    public void GenericIndexerSetThrows()
    {
        var list = new VirtualList<int, string>(new Provider(3), i => $"i{i}");

        Assert.Throws<NotSupportedException>(() => { list[0] = "x"; });
    }

    [TestMethod]
    public void NonGenericIndexerSetThrows()
    {
        var list = new VirtualList<int, string>(new Provider(3), i => $"i{i}");
        IList nonGeneric = list;

        Assert.Throws<NotSupportedException>(() => { nonGeneric[0] = "x"; });
    }

    [TestMethod]
    public void NonGenericEnumeratorYieldsItems()
    {
        var list = new VirtualList<int, string>(new Provider(3), i => $"i{i}");

        var values = new List<string>();
        var enumerator = ((IEnumerable)list).GetEnumerator();
        while (enumerator.MoveNext())
            values.Add((string)enumerator.Current!);

        CollectionAssert.AreEqual(new[] { "i0", "i1", "i2" }, values);
    }

    private sealed class Provider : IItemProvider<int>
    {
        public Provider(int count) => Count = count;

        public int Count { get; }

        public void Fetch(int start, Span<int> values)
        {
            for (var i = 0; i < values.Length; i++)
                values[i] = start + i;
        }
    }
}
