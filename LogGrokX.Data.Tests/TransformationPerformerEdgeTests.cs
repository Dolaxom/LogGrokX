using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Data.Tests;

[TestClass]
public class TransformationPerformerEdgeTests
{
    [TestMethod]
    public void UnknownTransformationLeavesInputUntouched()
    {
        var performer = new TransformationPerformer(new[] { @"(?<NoSuchTransform>.+)" });

        Assert.AreEqual("abc", performer.Transform("abc"));
    }
}
