using System;
using System.Collections;
using System.Collections.Generic;
using LogGrokX.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class ListLazyAdapterTests
{
    [TestMethod]
    public void ConvertsLazilyAndCaches()
    {
        var calls = 0;
        var adapter = new ListLazyAdapter<int, string>(new List<int> { 1, 2 },
            i => { calls++; return $"n{i}"; });

        Assert.AreEqual(2, adapter.Count);
        Assert.IsTrue(adapter.IsReadOnly);
        Assert.AreEqual("n1", adapter[0]);
        Assert.AreEqual("n1", adapter[0]);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void ResizesWhenSourceGrows()
    {
        var source = new List<int> { 1 };
        var adapter = new ListLazyAdapter<int, string>(source, i => $"n{i}");

        Assert.AreEqual("n1", adapter[0]);

        source.Add(2);
        Assert.AreEqual("n2", adapter[1]);
    }

    [TestMethod]
    public void EnumeratorsYieldConvertedValues()
    {
        var adapter = new ListLazyAdapter<int, string>(new List<int> { 1, 2 }, i => $"n{i}");

        var generic = new List<string>();
        foreach (var value in adapter)
            generic.Add(value);
        CollectionAssert.AreEqual(new[] { "n1", "n2" }, generic);

        var nonGeneric = new List<object?>();
        var enumerator = ((IEnumerable)adapter).GetEnumerator();
        while (enumerator.MoveNext())
            nonGeneric.Add(enumerator.Current);
        CollectionAssert.AreEqual(new object?[] { "n1", "n2" }, nonGeneric);
    }

    [TestMethod]
    public void UnsupportedMembersThrow()
    {
        var adapter = new ListLazyAdapter<int, string>(new List<int> { 1 }, i => $"n{i}");

        Assert.Throws<NotSupportedException>(() => { adapter[0] = "x"; });
        Assert.Throws<NotSupportedException>(() => adapter.Add("x"));
        Assert.Throws<NotSupportedException>(() => adapter.Clear());
        Assert.Throws<NotSupportedException>(() => adapter.Contains("x"));
        Assert.Throws<NotSupportedException>(() => adapter.CopyTo(new string[1], 0));
        Assert.Throws<NotSupportedException>(() => adapter.Remove("x"));
        Assert.Throws<NotSupportedException>(() => adapter.IndexOf("x"));
        Assert.Throws<NotSupportedException>(() => adapter.Insert(0, "x"));
        Assert.Throws<NotSupportedException>(() => adapter.RemoveAt(0));
    }
}
