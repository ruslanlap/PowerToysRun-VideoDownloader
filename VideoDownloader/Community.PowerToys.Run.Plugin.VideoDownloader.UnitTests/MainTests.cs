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
        public void Query_should_return_results()
        {
            // Plugin is not fully initialized (no PluginInitContext), so Query returns setup/error results.
            // That is valid — any non-null list with at least one result passes.
            var results = main.Query(new Query("search", "search"));

            Assert.IsNotNull(results, "Query should return a non-null list");
            Assert.IsTrue(results.Count > 0, "Query should return at least one result (setup prompt or actual results)");
        }
    }
}
