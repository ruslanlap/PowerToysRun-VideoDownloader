using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;

namespace Community.PowerToys.Run.Plugin.VideoDownloader.UnitTests
{
    /// <summary>
    /// Test cases that demonstrate the current broken behavior in GetSafeOutputTemplate method.
    /// These tests document the bugs before they are fixed.
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
            
            // Use reflection to access private _settings field
            var settingsField = typeof(Main).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance);
            _settings = (VideoDownloaderSettings)settingsField.GetValue(_plugin);
            
            // Set a test download path
            _settings.DownloadPath = @"C:\TestDownloads";
        }

        private string CallGetSafeOutputTemplate(string quality = "")
        {
            // Use reflection to call private GetSafeOutputTemplate method
            var method = typeof(Main).GetMethod("GetSafeOutputTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
            var result = (string)method.Invoke(_plugin, new object[] { quality });
            
            // Return just the filename part for easier testing
            return Path.GetFileName(result);
        }

        [TestMethod]
        public void BUG_AudioQualityIncludedWhenSettingIsFalse()
        {
            // Arrange: User wants NO quality information in filenames
            _settings.IncludeQualityInFilename = false;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            // Act: Download audio
            var template = CallGetSafeOutputTemplate("audio");

            // Assert: Should NOT include [Audio] but currently DOES (this is the bug)
            Assert.IsTrue(template.Contains("[Audio]"), 
                "BUG DEMONSTRATION: Audio quality '[Audio]' is included even when IncludeQualityInFilename is false. " +
                "Expected: no quality info. Actual: " + template);
        }

        [TestMethod]
        public void BUG_TimestampAlwaysAddedWhenVideoIdDisabled()
        {
            // Arrange: User wants to allow overwrites and no video ID
            _settings.IncludeQualityInFilename = false;
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = false; // User WANTS to allow overwrites

            // Act: Download video
            var template = CallGetSafeOutputTemplate("720p");

            // Assert: Should NOT include timestamp but currently DOES (this is the bug)
            Assert.IsTrue(template.Contains("_20"), // Timestamp starts with year 20xx
                "BUG DEMONSTRATION: Timestamp is added even when PreventFileOverwrites is false. " +
                "Expected: no timestamp. Actual: " + template);
        }

        [TestMethod]
        public void BUG_SettingsNotIndependent_QualityAndTimestamp()
        {
            // Arrange: User wants quality but no video ID and allows overwrites
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = false; // Should NOT add timestamp

            // Act: Download video
            var template = CallGetSafeOutputTemplate("1080p");

            // Assert: Should be "title_[1080p].ext" but currently includes unwanted timestamp
            Assert.IsTrue(template.Contains("[1080p]") || template.Contains("%(height)sp"), 
                "Quality should be included when IncludeQualityInFilename is true");
            Assert.IsTrue(template.Contains("_20"), // Timestamp bug
                "BUG DEMONSTRATION: Timestamp added even when not needed. " +
                "Expected: title_[quality].ext. Actual: " + template);
        }

        [TestMethod]
        public void CurrentBehavior_AllSettingsEnabled()
        {
            // Arrange: All settings enabled (this should work correctly)
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            // Act: Download video
            var template = CallGetSafeOutputTemplate("720p");

            // Assert: This combination should work correctly
            Assert.IsTrue(template.Contains("%(title)"), "Should include title");
            Assert.IsTrue(template.Contains("%(height)sp") || template.Contains("[720p]"), "Should include quality");
            Assert.IsTrue(template.Contains("%(id)s"), "Should include video ID");
            Assert.IsFalse(template.Contains("_20"), "Should NOT include timestamp when video ID is used");
        }

        [TestMethod]
        public void CurrentBehavior_AudioWithAllSettings()
        {
            // Arrange: All settings enabled for audio
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            // Act: Download audio
            var template = CallGetSafeOutputTemplate("audio");

            // Assert: Audio with all settings should work
            Assert.IsTrue(template.Contains("%(title)"), "Should include title");
            Assert.IsTrue(template.Contains("[Audio]"), "Should include audio quality marker");
            Assert.IsTrue(template.Contains("%(id)s"), "Should include video ID");
        }

        [TestMethod]
        public void CurrentBehavior_CustomTemplateBypassesLogic()
        {
            // Arrange: Custom template should bypass all logic
            _settings.CustomFilenameTemplate = "custom_%(title)s.%(ext)s";
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;

            // Act: Download video
            var template = CallGetSafeOutputTemplate("1080p");

            // Assert: Should use custom template exactly
            Assert.AreEqual("custom_%(title)s.%(ext)s", template, 
                "Custom template should be used without modification");
        }

        [TestMethod]
        public void BUG_QualityLogicFlawedForVideoDownloads()
        {
            // Arrange: Quality enabled, test different quality values
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            // Act & Assert: Test various quality values
            var template720p = CallGetSafeOutputTemplate("720p");
            var templateBest = CallGetSafeOutputTemplate("best");
            var templateEmpty = CallGetSafeOutputTemplate("");

            // These should all include quality information when IncludeQualityInFilename is true
            Assert.IsTrue(template720p.Contains("%(height)sp"), 
                "720p should include height placeholder: " + template720p);
            
            // BUG: "best" and empty quality don't get quality info even when setting is true
            Console.WriteLine($"Best quality template: {templateBest}");
            Console.WriteLine($"Empty quality template: {templateEmpty}");
        }

        [TestMethod]
        public void DocumentCurrentBehaviorMatrix()
        {
            // This test documents the current behavior for all setting combinations
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
            Console.WriteLine("IncludeQuality | UseVideoId | PreventOverwrites | Quality | Template");
            Console.WriteLine("---------------|------------|-------------------|---------|----------");

            foreach (var testCase in testCases)
            {
                _settings.IncludeQualityInFilename = testCase.IncludeQuality;
                _settings.UseVideoIdInFilename = testCase.UseVideoId;
                _settings.PreventFileOverwrites = testCase.PreventOverwrites;

                var template = CallGetSafeOutputTemplate(testCase.Quality);
                
                Console.WriteLine($"{testCase.IncludeQuality,-14} | {testCase.UseVideoId,-10} | {testCase.PreventOverwrites,-17} | {testCase.Quality,-7} | {template}");
            }

            // This test always passes - it's just for documentation
            Assert.IsTrue(true, "Documentation test");
        }
    }
}