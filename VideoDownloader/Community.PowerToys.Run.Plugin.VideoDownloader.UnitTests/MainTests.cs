using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Wox.Plugin;
using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.VideoDownloader.UnitTests
{
    [TestClass]
    public class MainTests
    {
        private Main main;

        [TestInitialize]
        public void TestInitialize()
        {
            main = new Main();
        }

        [TestMethod]
        [Ignore("Requires PluginInitContext (GetYtDlpExecutablePath needs _context.CurrentPluginMetadata). Use integration tests for full Query coverage.")]
        public void Query_should_return_results()
        {
            var results = main.Query(new Query("search", "search"));
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
        }
    }
}
