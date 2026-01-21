using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;

namespace Community.PowerToys.Run.Plugin.VideoDownloader.UnitTests
{
    /// <summary>
    /// Tests for custom template precedence handling in GetSafeOutputTemplate method.
    /// Validates Requirements 4.1 and 4.2.
    /// </summary>
    [TestClass]
    public class CustomTemplatePrecedenceTests
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
        public void CustomTemplate_WhenProvided_ShouldUseWithoutModification()
        {
            // Arrange: Custom template provided
            _settings.CustomFilenameTemplate = "custom_%(title)s.%(ext)s";
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            // Act: Generate template
            var template = CallGetSafeOutputTemplate("1080p");

            // Assert: Should use custom template exactly without any modifications
            Assert.AreEqual("custom_%(title)s.%(ext)s", template, 
                "Custom template should be used without modification, ignoring all other settings");
        }

        [TestMethod]
        public void CustomTemplate_WhenEmpty_ShouldFallbackToGenerated()
        {
            // Arrange: Empty custom template
            _settings.CustomFilenameTemplate = "";
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            // Act: Generate template
            var template = CallGetSafeOutputTemplate("1080p");

            // Assert: Should generate template based on settings
            Assert.IsTrue(template.Contains("%(title)"), "Should include title from generated template");
            Assert.IsTrue(template.Contains("%(height)sp"), "Should include quality from generated template");
            Assert.IsTrue(template.Contains("%(id)s"), "Should include video ID from generated template");
            Assert.IsFalse(template.Contains("_20"), "Should not include timestamp when video ID is used");
        }

        [TestMethod]
        public void CustomTemplate_WhenWhitespace_ShouldFallbackToGenerated()
        {
            // Arrange: Whitespace custom template
            _settings.CustomFilenameTemplate = "   \t\n  ";
            _settings.IncludeQualityInFilename = false;
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = false;

            // Act: Generate template
            var template = CallGetSafeOutputTemplate("720p");

            // Assert: Should generate minimal template based on settings
            Assert.IsTrue(template.Contains("%(title)"), "Should include title from generated template");
            Assert.IsFalse(template.Contains("%(height)sp"), "Should not include quality when setting is false");
            Assert.IsFalse(template.Contains("%(id)s"), "Should not include video ID when setting is false");
            Assert.IsFalse(template.Contains("_20"), "Should not include timestamp when overwrites allowed");
        }

        [TestMethod]
        public void CustomTemplate_WhenNull_ShouldFallbackToGenerated()
        {
            // Arrange: Null custom template
            _settings.CustomFilenameTemplate = null;
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = true;

            // Act: Generate template for audio
            var template = CallGetSafeOutputTemplate("audio");

            // Assert: Should generate template with audio quality and timestamp
            Assert.IsTrue(template.Contains("%(title)"), "Should include title from generated template");
            Assert.IsTrue(template.Contains("[Audio]"), "Should include audio quality marker");
            Assert.IsFalse(template.Contains("%(id)s"), "Should not include video ID when setting is false");
            Assert.IsTrue(template.Contains("_20"), "Should include timestamp for uniqueness when video ID disabled");
        }

        [TestMethod]
        public void CustomTemplate_IgnoresAllOtherSettings()
        {
            // Arrange: Custom template with all other settings enabled
            _settings.CustomFilenameTemplate = "simple.%(ext)s";
            _settings.IncludeQualityInFilename = true;
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            // Act: Generate template
            var template = CallGetSafeOutputTemplate("audio");

            // Assert: Should use only custom template, ignoring all settings
            Assert.AreEqual("simple.%(ext)s", template, 
                "Custom template should completely bypass all other filename settings");
        }

        [TestMethod]
        public void CustomTemplate_WithComplexPattern_ShouldUseExactly()
        {
            // Arrange: Complex custom template
            _settings.CustomFilenameTemplate = "%(uploader)s - %(title).50B [%(id)s].%(ext)s";
            _settings.IncludeQualityInFilename = false;
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = false;

            // Act: Generate template
            var template = CallGetSafeOutputTemplate("1080p");

            // Assert: Should use complex template exactly
            Assert.AreEqual("%(uploader)s - %(title).50B [%(id)s].%(ext)s", template, 
                "Complex custom template should be used exactly as provided");
        }
    }
}