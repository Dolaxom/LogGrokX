using System.Collections.Generic;
using LogGrokX.MergedView;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Tests
{
    [TestClass]
    public class MergedSchemaTests
    {
        private static IReadOnlyList<IReadOnlyList<string>> FieldSets(params string[][] sets)
        {
            var result = new List<IReadOnlyList<string>>();
            foreach (var set in sets)
                result.Add(set);
            return result;
        }

        [TestMethod]
        public void BuildFieldsAlignsSameFieldNamesIgnoringCase()
        {
            var fields = MergedSchema.BuildFields(FieldSets(
                new[] { "Time", "Thread", "Severity", "Component", "Message" },
                new[] { "time", "process_thread", "severity", "component", "message" }));

            CollectionAssert.AreEqual(
                new[] { "Time", "Thread", "Severity", "Component", "Message", "process_thread" },
                new List<string>(fields));
        }

        [TestMethod]
        public void BuildFieldsPrefersUppercaseFieldName()
        {
            var fields = MergedSchema.BuildFields(FieldSets(
                new[] { "time", "message" },
                new[] { "Time", "Message" }));

            CollectionAssert.AreEqual(new[] { "Time", "Message" }, new List<string>(fields));
        }

        [TestMethod]
        public void BuildFieldsMapsPlainTextToMessage()
        {
            var fields = MergedSchema.BuildFields(FieldSets(
                new[] { "Text" },
                new[] { "Time", "Level", "Message" }));

            CollectionAssert.AreEqual(new[] { "Message", "Time", "Level" }, new List<string>(fields));
        }

        [TestMethod]
        public void BuildFieldsKeepsTextWhenNoMessageFormatExists()
        {
            var fields = MergedSchema.BuildFields(FieldSets(new[] { "Text" }));

            CollectionAssert.AreEqual(new[] { "Text" }, new List<string>(fields));
        }

        [TestMethod]
        public void BuildFieldsMergesLevelAndSeverity()
        {
            var fields = MergedSchema.BuildFields(FieldSets(
                new[] { "Time", "Level", "Message" },
                new[] { "Time", "Severity", "Message" }));

            CollectionAssert.AreEqual(new[] { "Time", "Severity", "Message" }, new List<string>(fields));
        }

        [TestMethod]
        public void BuildFieldsPrefersSeverityRegardlessOfOrder()
        {
            var fields = MergedSchema.BuildFields(FieldSets(
                new[] { "Severity", "Message" },
                new[] { "Level", "Message" }));

            CollectionAssert.AreEqual(new[] { "Severity", "Message" }, new List<string>(fields));
        }

        [TestMethod]
        public void FindSourceFieldIndexIsCaseInsensitive()
        {
            Assert.AreEqual(0, MergedSchema.FindSourceFieldIndex(new[] { "time", "message" }, "Time"));
            Assert.AreEqual(1, MergedSchema.FindSourceFieldIndex(new[] { "time", "message" }, "Message"));
            Assert.AreEqual(-1, MergedSchema.FindSourceFieldIndex(new[] { "time" }, "Severity"));
        }

        [TestMethod]
        public void FindSourceFieldIndexMapsTextToMessage()
        {
            Assert.AreEqual(0, MergedSchema.FindSourceFieldIndex(new[] { "Text" }, "Message"));
            Assert.AreEqual(0, MergedSchema.FindSourceFieldIndex(new[] { "Message" }, "Text"));
        }

        [TestMethod]
        public void FindSourceFieldIndexMapsLevelToSeverity()
        {
            Assert.AreEqual(1, MergedSchema.FindSourceFieldIndex(new[] { "Time", "Level" }, "Severity"));
            Assert.AreEqual(1, MergedSchema.FindSourceFieldIndex(new[] { "Time", "Severity" }, "Level"));
        }
    }
}