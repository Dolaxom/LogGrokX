using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class MergedLineOrderTests
{
    private static long At(int seconds) => new DateTime(2024, 1, 1).AddSeconds(seconds).Ticks;

    private static TimeIndex Timed(params int[] seconds)
    {
        var index = new TimeIndex();
        foreach (var second in seconds)
            index.Add(At(second));
        return index;
    }

    [TestMethod]
    public void MergesTimedSourcesByTicks()
    {
        var sources = new List<MergeSource>
        {
            new(2, Timed(10, 30)),
            new(2, Timed(20, 40))
        };

        var result = MergedLineOrder.Build(sources);

        CollectionAssert.AreEqual(
            new[] { (0, 0), (1, 0), (0, 1), (1, 1) },
            result.Select(r => (r.SourceIndex, r.LineNumber)).ToArray());
    }

    [TestMethod]
    public void InterleavesThreeSourcesInOrder()
    {
        var sources = new List<MergeSource>
        {
            new(3, Timed(1, 4, 7)),
            new(3, Timed(2, 5, 8)),
            new(3, Timed(3, 6, 9))
        };

        var result = MergedLineOrder.Build(sources);

        CollectionAssert.AreEqual(
            new[] { (0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (0, 2), (1, 2), (2, 2) },
            result.Select(r => (r.SourceIndex, r.LineNumber)).ToArray());
    }

    [TestMethod]
    public void UntimedSourceIsPlacedAfterTimedSources()
    {
        var sources = new List<MergeSource>
        {
            new(2, new TimeIndex()),
            new(2, Timed(5, 6))
        };

        var result = MergedLineOrder.Build(sources);

        CollectionAssert.AreEqual(
            new[] { (1, 0), (1, 1), (0, 0), (0, 1) },
            result.Select(r => (r.SourceIndex, r.LineNumber)).ToArray());
        Assert.AreEqual(-1, result[2].Ticks);
        Assert.AreEqual(-1, result[3].Ticks);
    }

    [TestMethod]
    public void NonMonotonicSourceFallsBackToSequentialOrder()
    {
        var nonMonotonic = new TimeIndex();
        nonMonotonic.Add(At(10));
        nonMonotonic.Add(At(5));

        var sources = new List<MergeSource>
        {
            new(2, nonMonotonic),
            new(1, Timed(1))
        };

        var result = MergedLineOrder.Build(sources);

        CollectionAssert.AreEqual(
            new[] { (1, 0), (0, 0), (0, 1) },
            result.Select(r => (r.SourceIndex, r.LineNumber)).ToArray());
    }

    [TestMethod]
    public void EmptyAndMissingSourcesAreSkipped()
    {
        var sources = new List<MergeSource>
        {
            new(0, new TimeIndex()),
            new(1, Timed(3))
        };

        var result = MergedLineOrder.Build(sources);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1, result[0].SourceIndex);
    }

    [TestMethod]
    public void BuildTimeIndexAlignsWithMergedLines()
    {
        var sources = new List<MergeSource>
        {
            new(2, Timed(10, 30)),
            new(2, Timed(20, 40))
        };

        var lines = MergedLineOrder.Build(sources);
        var index = MergedLineOrder.BuildTimeIndex(lines);

        Assert.AreEqual(4, index.Count);
        Assert.IsTrue(index.HasTime);
        Assert.IsTrue(index.IsMonotonic);
        CollectionAssert.AreEqual(
            new[] { At(10), At(20), At(30), At(40) },
            Enumerable.Range(0, index.Count).Select(index.GetTicksAt).ToArray());
    }

    [TestMethod]
    public void BuildTimeIndexStaysUntimedWhenNoSourceHasTime()
    {
        var sources = new List<MergeSource>
        {
            new(2, new TimeIndex()),
            new(1, new TimeIndex())
        };

        var lines = MergedLineOrder.Build(sources);
        var index = MergedLineOrder.BuildTimeIndex(lines);

        Assert.AreEqual(3, index.Count);
        Assert.IsFalse(index.HasTime);
    }
}