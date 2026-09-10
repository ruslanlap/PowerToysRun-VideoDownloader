using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;

namespace Community.PowerToys.Run.Plugin.VideoDownloader.UnitTests
{
    /// <summary>
    /// Tests for GetSafeOutputTemplate correct behavior (bugs fixed in 1.2.x).
    /// </summary>
    [TestClass]
    public class GetSafeOutputTemplateBugTests
    {
        private Main _plugin;
        private VideoDownloaderSettings _settings;

        [TestInitialize]
        public void TestInitialize()
        {
            _plugin = new Main();
            var settingsField = typeof(Main).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance);
            _settings = (VideoDownloaderSettings)settingsField.GetValue(_plugin);
            _settings.DownloadPath = @"C:\TestDownloads";
        }

        private string CallGetSafeOutputTemplate(string quality = "")
        {
            var method = typeof(Main).GetMethod("GetSafeOutputTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
            var result = (string)method.Invoke(_plugin, new object[] { quality });
            return Path.GetFileName(result);
        }

        [TestMethod]
        public void AudioQuality_WhenSettingIsFalse_NotIncluded()
        {
            // Bug was: [Audio] appeared even when IncludeQualityInFilename=false. Fixed in 1.2.x.
            _settings.IncludeQualityInFilename = false;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            var template = CallGetSafeOutputTemplate("audio");

            Assert.IsFalse(template.Contains("[Audio]"),
                "Audio quality marker should NOT appear when IncludeQualityInFilename is false. Actual: " + template);
        }

        [TestMethod]
        public void Timestamp_WhenVideoIdDisabledAndOverwritesAllowed_NotAdded()
        {
            // Bug was: timestamp added even when PreventFileOverwrites=false. Fixed in 1.2.x.
            _settings.IncludeQualityInFilename = false;
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = false;

            var template = CallGetSafeOutputTemplate("720p");

            Assert.IsFalse(template.Contains("_20"),
                "Timestamp should NOT appear when PreventFileOverwrites is false. Actual: " + template);
        }

        [TestMethod]
        public void QualityAndTimestamp_AreIndependent()
        {
            // Bug was: timestamp added even when PreventFileOverwrites=false. Fixed in 1.2.x.
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = false;

            var template = CallGetSafeOutputTemplate("1080p");

            Assert.IsTrue(template.Contains("[1080p]") || template.Contains("%(height)sp"),
                "Quality should be included when IncludeQualityInFilename is true. Actual: " + template);
            Assert.IsFalse(template.Contains("_20"),
                "Timestamp should NOT appear when PreventFileOverwrites is false. Actual: " + template);
        }

        [TestMethod]
        public void CurrentBehavior_AllSettingsEnabled()
        {
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            var template = CallGetSafeOutputTemplate("720p");

            Assert.IsTrue(template.Contains("%(title)"), "Should include title");
            Assert.IsTrue(template.Contains("%(height)sp") || template.Contains("[720p]"), "Should include quality");
            Assert.IsTrue(template.Contains("%(id)s"), "Should include video ID");
            Assert.IsFalse(template.Contains("_20"), "Should NOT include timestamp when video ID is used");
        }

        [TestMethod]
        public void CurrentBehavior_AudioWithAllSettings()
        {
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            var template = CallGetSafeOutputTemplate("audio");

            Assert.IsTrue(template.Contains("%(title)"), "Should include title");
            Assert.IsTrue(template.Contains("[Audio]"), "Should include audio quality marker");
            Assert.IsTrue(template.Contains("%(id)s"), "Should include video ID");
        }

        [TestMethod]
        public void CurrentBehavior_CustomTemplateBypassesLogic()
        {
            _settings.CustomFilenameTemplate = "custom_%(title)s.%(ext)s";
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;

            var template = CallGetSafeOutputTemplate("1080p");

            Assert.AreEqual("custom_%(title)s.%(ext)s", template,
                "Custom template should be used without modification");
        }

        [TestMethod]
        public void BUG_QualityLogicFlawedForVideoDownloads()
        {
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            var template720p = CallGetSafeOutputTemplate("720p");
            var templateBest = CallGetSafeOutputTemplate("best");
            var templateEmpty = CallGetSafeOutputTemplate("");

            Assert.IsTrue(template720p.Contains("%(height)sp"),
                "720p should include height placeholder: " + template720p);

            Console.WriteLine($"Best quality template: {templateBest}");
            Console.WriteLine($"Empty quality template: {templateEmpty}");
        }

        [TestMethod]
        public void DocumentCurrentBehaviorMatrix()
        {
            var testCases = new[]
            {
                new { IncludeQuality = false, UseVideoId = false, PreventOverwrites = false, Quality = "720p", Description = "Minimal settings" },
                new { IncludeQuality = false, UseVideoId = false, PreventOverwrites = true, Quality = "720p", Description = "Only prevent overwrites" },
                new { IncludeQuality = true, UseVideoId = false, PreventOverwrites = false, Quality = "720p", Description = "Only quality" },
                new { IncludeQuality = true, UseVideoId = true, PreventOverwrites = false, Quality = "720p", Description = "Quality + Video ID" },
                new { IncludeQuality = false, UseVideoId = false, PreventOverwrites = false, Quality = "audio", Description = "Audio minimal" },
                new { IncludeQuality = false, UseVideoId = false, PreventOverwrites = true, Quality = "audio", Description = "Audio with overwrite prevention" },
            };

            Console.WriteLine("Current Behavior Matrix:");
            foreach (var testCase in testCases)
            {
                _settings.IncludeQualityInFilename = testCase.IncludeQuality;
                _settings.UseVideoIdInFilename = testCase.UseVideoId;
                _settings.PreventFileOverwrites = testCase.PreventOverwrites;
                var template = CallGetSafeOutputTemplate(testCase.Quality);
                Console.WriteLine($"{testCase.Description}: {template}");
            }

            Assert.IsTrue(true, "Documentation test");
        }
    }
}
