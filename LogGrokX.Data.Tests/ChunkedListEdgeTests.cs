using System;
using System.Collections;
using System.Collections.Generic;
using LogGrokX.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class ChunkedListEdgeTests
{
    [TestMethod]
    public void UnsupportedMembersThrow()
    {
        var list = new ChunkedList<int>(4);
        list.Add(1);

        Assert.IsFalse(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Contains(1));
        Assert.Throws<NotSupportedException>(() => list.CopyTo(new int[1], 0));
        Assert.Throws<NotSupportedException>(() => list.Remove(1));
        Assert.Throws<NotSupportedException>(() => list.IndexOf(1));
        Assert.Throws<NotSupportedException>(() => list.Insert(0, 1));
        Assert.Throws<NotSupportedException>(() => list.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => { list[0] = 5; });
    }

    [TestMethod]
    public void ClearResetsCount()
    {
        var list = new ChunkedList<int>(2);
        list.Add(1);
        list.Add(2);
        list.Add(3);

        list.Clear();

        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void NonGenericEnumeratorYieldsItems()
    {
        var list = new ChunkedList<int>(2);
        list.Add(1);
        list.Add(2);
        list.Add(3);

        var values = new List<int>();
        var enumerator = ((IEnumerable)list).GetEnumerator();
        while (enumerator.MoveNext())
            values.Add((int)enumerator.Current!);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, values);
    }
}
