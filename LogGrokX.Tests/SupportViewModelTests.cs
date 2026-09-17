using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokX.Tests
{
    [TestClass]
    public class SupportViewModelTests
    {
        [TestMethod]
        public void DiagnosticsTextContainsVersionRuntimeAndArchitecture()
        {
            var viewModel = new SupportViewModel();

            StringAssert.Contains(viewModel.DiagnosticsText, viewModel.Version);
            StringAssert.Contains(viewModel.DiagnosticsText, viewModel.RuntimeVersion);
            StringAssert.Contains(viewModel.DiagnosticsText, viewModel.Architecture);
        }

        [TestMethod]
        public void CommitIsShortened()
        {
            var viewModel = new SupportViewModel();

            Assert.IsTrue(viewModel.Commit.Length <= 8, viewModel.Commit);
        }
    }
}
