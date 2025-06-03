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
            var results = main.Query(new Query("search", "search"));

            Assert.IsNotNull(results.First());
        }

        // Additional test methods can be added here as needed
    }
}
