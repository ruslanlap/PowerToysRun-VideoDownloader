using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.VideoDownloader.UnitTests
{
    /// <summary>
    /// Tests for the enhanced timestamp-based uniqueness fallback mechanism.
    /// Validates Requirements 2.3 and 3.2: collision-resistant timestamp generation.
    /// </summary>
    [TestClass]
    public class TimestampUniquenessTests
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

        private string CallGenerateCollisionResistantTimestamp()
        {
            // Use reflection to call private GenerateCollisionResistantTimestamp method
            var method = typeof(Main).GetMethod("GenerateCollisionResistantTimestamp", BindingFlags.NonPublic | BindingFlags.Instance);
            return (string)method.Invoke(_plugin, new object[] { });
        }

        [TestMethod]
        public void TimestampFormat_ShouldFollowExpectedPattern()
        {
            // Act
            var timestamp = CallGenerateCollisionResistantTimestamp();

            // Assert: Should match pattern yyyyMMdd_HHmmss_fff_xxxx
            var pattern = @"^\d{8}_\d{6}_\d{3}_[0-9A-Z]{4}$";
            Assert.IsTrue(Regex.IsMatch(timestamp, pattern), 
                $"Timestamp '{timestamp}' should match pattern yyyyMMdd_HHmmss_fff_xxxx");

            // Verify components
            var parts = timestamp.Split('_');
            Assert.AreEqual(4, parts.Length, "Timestamp should have 4 parts separated by underscores");
            
            // Date part (yyyyMMdd)
            Assert.AreEqual(8, parts[0].Length, "Date part should be 8 characters");
            Assert.IsTrue(int.TryParse(parts[0], out _), "Date part should be numeric");
            
            // Time part (HHmmss)
            Assert.AreEqual(6, parts[1].Length, "Time part should be 6 characters");
            Assert.IsTrue(int.TryParse(parts[1], out _), "Time part should be numeric");
            
            // Milliseconds part (fff)
            Assert.AreEqual(3, parts[2].Length, "Milliseconds part should be 3 characters");
            Assert.IsTrue(int.TryParse(parts[2], out _), "Milliseconds part should be numeric");
            
            // Random suffix (xxxx)
            Assert.AreEqual(4, parts[3].Length, "Random suffix should be 4 characters");
            Assert.IsTrue(Regex.IsMatch(parts[3], @"^[0-9A-Z]{4}$"), "Random suffix should be alphanumeric uppercase");
        }

        [TestMethod]
        public void TimestampGeneration_ShouldBeCollisionResistant()
        {
            // Act: Generate multiple timestamps rapidly
            var timestamps = new HashSet<string>();
            const int iterations = 1000;
            
            for (int i = 0; i < iterations; i++)
            {
                var timestamp = CallGenerateCollisionResistantTimestamp();
                timestamps.Add(timestamp);
            }

            // Assert: All timestamps should be unique
            Assert.AreEqual(iterations, timestamps.Count, 
                $"All {iterations} timestamps should be unique. Found {timestamps.Count} unique values.");
        }

        [TestMethod]
        public void TimestampGeneration_ShouldBeReasonablyCurrentTime()
        {
            // Act
            var beforeGeneration = DateTime.UtcNow;
            var timestamp = CallGenerateCollisionResistantTimestamp();
            var afterGeneration = DateTime.UtcNow;

            // Parse the timestamp to verify it's current
            var datePart = timestamp.Substring(0, 8);
            var timePart = timestamp.Substring(9, 6);
            
            var year = int.Parse(datePart.Substring(0, 4));
            var month = int.Parse(datePart.Substring(4, 2));
            var day = int.Parse(datePart.Substring(6, 2));
            var hour = int.Parse(timePart.Substring(0, 2));
            var minute = int.Parse(timePart.Substring(2, 2));
            var second = int.Parse(timePart.Substring(4, 2));

            var timestampDateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

            // Assert: Timestamp should be within the generation window
            Assert.IsTrue(timestampDateTime >= beforeGeneration.AddSeconds(-1), 
                "Timestamp should not be before generation started");
            Assert.IsTrue(timestampDateTime <= afterGeneration.AddSeconds(1), 
                "Timestamp should not be after generation completed");
        }

        [TestMethod]
        public void TimestampInTemplate_ShouldOnlyAppearWhenNeeded()
        {
            // Test Case 1: Should include timestamp when video ID disabled and overwrites prevented
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = true;
            _settings.IncludeQualityInFilename = false;

            var template1 = CallGetSafeOutputTemplate("720p");
            Assert.IsTrue(ContainsTimestamp(template1), 
                "Template should contain timestamp when UseVideoIdInFilename=false and PreventFileOverwrites=true");

            // Test Case 2: Should NOT include timestamp when video ID is enabled
            _settings.UseVideoIdInFilename = true;
            _settings.PreventFileOverwrites = true;

            var template2 = CallGetSafeOutputTemplate("720p");
            Assert.IsFalse(ContainsTimestamp(template2), 
                "Template should NOT contain timestamp when UseVideoIdInFilename=true");

            // Test Case 3: Should NOT include timestamp when overwrites are allowed
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = false;

            var template3 = CallGetSafeOutputTemplate("720p");
            Assert.IsFalse(ContainsTimestamp(template3), 
                "Template should NOT contain timestamp when PreventFileOverwrites=false");
        }

        [TestMethod]
        public void TimestampInTemplate_ShouldWorkWithAllQualityTypes()
        {
            // Arrange: Settings that require timestamp
            _settings.UseVideoIdInFilename = false;
            _settings.PreventFileOverwrites = true;
            _settings.IncludeQualityInFilename = true;

            // Test different quality types
            var qualities = new[] { "720p", "1080p", "480p", "best", "audio", "" };

            foreach (var quality in qualities)
            {
                // Act
                var template = CallGetSafeOutputTemplate(quality);

                // Assert
                Assert.IsTrue(ContainsTimestamp(template), 
                    $"Template for quality '{quality}' should contain timestamp: {template}");
                
                // Verify timestamp format in template
                var timestampMatch = Regex.Match(template, @"\[(\d{8}_\d{6}_\d{3}_[0-9A-Z]{4})\]");
                Assert.IsTrue(timestampMatch.Success, 
                    $"Template should contain properly formatted timestamp in brackets for quality '{quality}': {template}");
            }
        }

        [TestMethod]
        public void TimestampGeneration_ShouldBeConsistentFormat()
        {
            // Act: Generate multiple timestamps and verify they all follow the same format
            var timestamps = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                timestamps.Add(CallGenerateCollisionResistantTimestamp());
                // Small delay to ensure different milliseconds
                System.Threading.Thread.Sleep(1);
            }

            // Assert: All should follow the same pattern
            var pattern = @"^\d{8}_\d{6}_\d{3}_[0-9A-Z]{4}$";
            foreach (var timestamp in timestamps)
            {
                Assert.IsTrue(Regex.IsMatch(timestamp, pattern), 
                    $"Timestamp '{timestamp}' should match consistent format pattern");
            }

            // Verify they're all different (collision resistance)
            var uniqueTimestamps = timestamps.Distinct().Count();
            Assert.AreEqual(timestamps.Count, uniqueTimestamps, 
                "All generated timestamps should be unique");
        }

        [TestMethod]
        public void TimestampGeneration_ShouldUseUTCTime()
        {
            // This test verifies that the timestamp uses UTC time by checking
            // that the generated timestamp is close to the current UTC time
            
            // Act
            var utcBefore = DateTime.UtcNow;
            var timestamp = CallGenerateCollisionResistantTimestamp();
            var utcAfter = DateTime.UtcNow;

            // Parse timestamp components
            var parts = timestamp.Split('_');
            var datePart = parts[0]; // yyyyMMdd
            var timePart = parts[1]; // HHmmss

            // Convert to DateTime
            var year = int.Parse(datePart.Substring(0, 4));
            var month = int.Parse(datePart.Substring(4, 2));
            var day = int.Parse(datePart.Substring(6, 2));
            var hour = int.Parse(timePart.Substring(0, 2));
            var minute = int.Parse(timePart.Substring(2, 2));
            var second = int.Parse(timePart.Substring(4, 2));

            var timestampDateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

            // Assert: Should be within reasonable range of UTC time
            var timeDifference = Math.Abs((timestampDateTime - utcBefore).TotalSeconds);
            Assert.IsTrue(timeDifference < 2, 
                $"Timestamp should be within 2 seconds of UTC time. Difference: {timeDifference} seconds");
        }

        /// <summary>
        /// Helper method to check if a template contains a timestamp pattern
        /// </summary>
        private bool ContainsTimestamp(string template)
        {
            // Look for timestamp pattern in brackets: [yyyyMMdd_HHmmss_fff_xxxx]
            var timestampPattern = @"\[\d{8}_\d{6}_\d{3}_[0-9A-Z]{4}\]";
            return Regex.IsMatch(template, timestampPattern);
        }
    }
}