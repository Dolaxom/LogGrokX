using System.Collections.Generic;
using System.Linq;
using LogGrokX.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class CollectionUtilsGenericTests
{
    [TestMethod]
    public void GenericMergeSortedRequeuesNonContiguousCursors()
    {
        var cursors = new List<IEnumerator<int>>
        {
            new List<int> { 1, 5, 9 }.GetEnumerator(),
            new List<int> { 2, 10 }.GetEnumerator()
        };
        foreach (var cursor in cursors)
            cursor.MoveNext();

        var result = CollectionUtils.MergeSorted(cursors, (a, b) => b == a + 1).ToList();

        CollectionAssert.AreEqual(new[] { 1, 2, 5, 9, 10 }, result);
    }
}
