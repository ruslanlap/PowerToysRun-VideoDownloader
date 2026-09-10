using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Community.PowerToys.Run.Plugin.VideoDownloader.UnitTests
{
    /// <summary>
    /// Tests for the #57 Premiere Pro compatibility feature:
    /// format-sort flag, post-download transcode discovery, and settings persistence.
    /// </summary>
    [TestClass]
    public class PremiereTranscodeTests
    {
        private Main _plugin;
        private VideoDownloaderSettings _settings;
        private string _tempDir;

        [TestInitialize]
        public void TestInitialize()
        {
            _plugin = new Main();

            // Use reflection to access private _settings field (same pattern as GetSafeOutputTemplateBugTests)
            var settingsField = typeof(Main).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance);
            _settings = (VideoDownloaderSettings)settingsField.GetValue(_plugin);

            _tempDir = Path.Combine(Path.GetTempPath(), "vd-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _settings.DownloadPath = _tempDir;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        private string CallFindRecentDownload(DateTime startedAt)
        {
            var method = typeof(Main).GetMethod("FindRecentDownload", BindingFlags.NonPublic | BindingFlags.Instance);
            return (string)method.Invoke(_plugin, new object[] { startedAt });
        }

        [TestMethod]
        public void TranscodeToPremiereSafe_DefaultsToTrue()
        {
            var fresh = new VideoDownloaderSettings();
            Assert.IsTrue(fresh.TranscodeToPremiereSafe,
                "TranscodeToPremiereSafe must default to true so existing users get the fix without touching settings");
        }

        [TestMethod]
        public void Settings_JsonRoundTrip_PersistsTranscodeFlag()
        {
            _settings.TranscodeToPremiereSafe = false;
            var json = JsonSerializer.Serialize(_settings);
            StringAssert.Contains(json, "TranscodeToPremiereSafe",
                "Serialized settings must carry the new key for persistence");

            var restored = JsonSerializer.Deserialize<VideoDownloaderSettings>(json);
            Assert.IsFalse(restored.TranscodeToPremiereSafe);
        }

        [TestMethod]
        public void AdditionalOptions_ContainsTranscodeToggle_DefaultTrue()
        {
            var options = _plugin.AdditionalOptions;
            var toggle = FirstOrDefault(options, "TranscodeToPremiereSafe");
            Assert.IsNotNull(toggle, "Settings UI must expose the TranscodeToPremiereSafe checkbox");
            Assert.IsTrue(toggle.Value);
        }

        private static Microsoft.PowerToys.Settings.UI.Library.PluginAdditionalOption FirstOrDefault(
            System.Collections.Generic.IEnumerable<Microsoft.PowerToys.Settings.UI.Library.PluginAdditionalOption> options, string key)
        {
            foreach (var option in options)
            {
                if (option.Key == key) return option;
            }
            return null;
        }

        [TestMethod]
        public void FindRecentDownload_ReturnsNewestMp4SinceStart()
        {
            var startedAt = DateTime.UtcNow.AddMinutes(-1);
            var old = Path.Combine(_tempDir, "old.mp4");
            var recent = Path.Combine(_tempDir, "recent.mp4");
            File.WriteAllText(old, "old");
            File.WriteAllText(recent, "recent");
            File.SetLastWriteTimeUtc(old, startedAt.AddMinutes(-10)); // pre-existing file
            File.SetLastWriteTimeUtc(recent, startedAt.AddSeconds(30));

            var found = CallFindRecentDownload(startedAt);
            Assert.AreEqual(recent, found, "Must pick the newest mp4 written after the download started");
        }

        [TestMethod]
        public void FindRecentDownload_IncludesMkv()
        {
            var startedAt = DateTime.UtcNow.AddMinutes(-1);
            var mkv = Path.Combine(_tempDir, "video.mkv");
            File.WriteAllText(mkv, "mkv");
            File.SetLastWriteTimeUtc(mkv, startedAt.AddSeconds(30));

            var found = CallFindRecentDownload(startedAt);
            Assert.AreEqual(mkv, found);
        }

        [TestMethod]
        public void FindRecentDownload_IgnoresPreExistingAndTmpFiles()
        {
            var startedAt = DateTime.UtcNow.AddMinutes(-1);
            var preExisting = Path.Combine(_tempDir, "pre-existing.mp4");
            File.WriteAllText(preExisting, "old");
            File.SetLastWriteTimeUtc(preExisting, startedAt.AddMinutes(-10));

            var leftoverTmp = Path.Combine(_tempDir, "leftover.tmp.mp4");
            File.WriteAllText(leftoverTmp, "tmp");
            File.SetLastWriteTimeUtc(leftoverTmp, startedAt.AddSeconds(30));

            var found = CallFindRecentDownload(startedAt);
            Assert.IsNull(found, "Old files and .tmp.mp4 leftovers must not be picked up");
        }

        [TestMethod]
        public void FindRecentDownload_ReturnsNull_WhenDirectoryMissing()
        {
            _settings.DownloadPath = Path.Combine(Path.GetTempPath(), "vd-tests-missing-" + Guid.NewGuid().ToString("N"));
            var found = CallFindRecentDownload(DateTime.UtcNow.AddMinutes(-1));
            Assert.IsNull(found);
        }
    }
}
