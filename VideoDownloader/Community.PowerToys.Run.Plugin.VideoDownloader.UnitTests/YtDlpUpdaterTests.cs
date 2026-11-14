using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Community.PowerToys.Run.Plugin.VideoDownloader;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.VideoDownloader.UnitTests
{
    [TestClass]
    public class YtDlpUpdaterTests
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly byte[] _payload;

            public FakeHandler(byte[] payload) => _payload = payload;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                var content = new ByteArrayContent(_payload);
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
            }
        }

        private static string TempPath()
        {
            var path = Path.Combine(Path.GetTempPath(), "ytdlp-test-" + Guid.NewGuid().ToString("N"), "yt-dlp.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }

        private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

        private const string ReleaseJson = """
        {
          "tag_name": "2026.11.10",
          "assets": [
            {
              "name": "yt-dlp.exe",
              "browser_download_url": "https://example.com/yt-dlp.exe",
              "digest": "sha256:3a5c7b9f01d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4"
            },
            {
              "name": "yt-dlp_x86.exe",
              "browser_download_url": "https://example.com/yt-dlp_x86.exe",
              "digest": "sha256:1111111111111111111111111111111111111111111111111111111111111111"
            },
            {
              "name": "yt-dlp.tar.gz",
              "browser_download_url": "https://example.com/yt-dlp.tar.gz",
              "digest": "sha256:2222222222222222222222222222222222222222222222222222222222222222"
            }
          ]
        }
        """;

        [TestMethod]
        public void ParseRelease_ReadsTagAndAssetDigests()
        {
            var release = YtDlpUpdater.ParseRelease(ReleaseJson);

            Assert.AreEqual("2026.11.10", release.TagName);
            Assert.AreEqual(3, release.Assets.Length);

            var asset = YtDlpUpdater.SelectAsset(release, Architecture.X64);
            Assert.AreEqual("yt-dlp.exe", asset.Name);
            Assert.AreEqual("https://example.com/yt-dlp.exe", asset.DownloadUrl);
            Assert.AreEqual("3a5c7b9f01d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4", asset.Sha256);

            var x86Asset = YtDlpUpdater.SelectAsset(release, Architecture.X86);
            Assert.AreEqual("yt-dlp_x86.exe", x86Asset.Name);
        }

        [TestMethod]
        public void ParseRelease_DigestMissing_YieldsNullSha()
        {
            var release = YtDlpUpdater.ParseRelease("""{"tag_name":"t","assets":[{"name":"yt-dlp.exe","browser_download_url":"u"}]}""");

            Assert.IsNull(release.Assets[0].Sha256);
        }

        [TestMethod]
        public void ParseDigest_RejectsNonSha256Algorithms()
        {
            Assert.IsNull(YtDlpUpdater.ParseDigest("md5:abc"));
            Assert.IsNull(YtDlpUpdater.ParseDigest(null));
            Assert.AreEqual("abc123", YtDlpUpdater.ParseDigest("sha256:ABC123"));
        }

        [TestMethod]
        public void SelectAssetName_MapsArchitecture()
        {
            Assert.AreEqual("yt-dlp_x86.exe", YtDlpUpdater.SelectAssetName(Architecture.X86));
            Assert.AreEqual("yt-dlp.exe", YtDlpUpdater.SelectAssetName(Architecture.X64));
            Assert.AreEqual("yt-dlp.exe", YtDlpUpdater.SelectAssetName(Architecture.Arm64));
        }

        [TestMethod]
        public void SelectAsset_ThrowsWhenAssetAbsent()
        {
            var release = YtDlpUpdater.ParseRelease("""{"tag_name":"t","assets":[{"name":"other.zip"}]}""");

            Assert.ThrowsException<InvalidOperationException>(
                () => YtDlpUpdater.SelectAsset(release, Architecture.X64));
        }

        [TestMethod]
        public void ComputeSha256_MatchesKnownVectors()
        {
            using var empty = new MemoryStream(Array.Empty<byte>());
            Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", YtDlpUpdater.ComputeSha256(empty));

            using var abc = new MemoryStream(Encoding.ASCII.GetBytes("abc"));
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", YtDlpUpdater.ComputeSha256(abc));
        }

        [TestMethod]
        public async Task InstallAsync_HappyPath_ReplacesBinaryAndReturnsVersion()
        {
            var payload = Encoding.ASCII.GetBytes("new-binary-content");
            var targetPath = TempPath();
            try
            {
                await File.WriteAllTextAsync(targetPath, "old-binary-content");

                using var client = new HttpClient(new FakeHandler(payload));
                var asset = new YtDlpUpdater.ReleaseAsset
                {
                    Name = "yt-dlp.exe",
                    DownloadUrl = "https://example.com/yt-dlp.exe",
                    Sha256 = ToHex(SHA256.HashData(payload)),
                };

                var version = await YtDlpUpdater.InstallAsync(client, asset, targetPath, _ => "2026.11.10");

                Assert.AreEqual("2026.11.10", version);
                Assert.AreEqual("new-binary-content", await File.ReadAllTextAsync(targetPath));
                Assert.IsFalse(File.Exists(targetPath + ".backup"));
                Assert.IsFalse(File.Exists(targetPath + ".download.exe"));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(targetPath), true);
            }
        }

        [TestMethod]
        public async Task InstallAsync_ShaMismatch_KeepsOldBinary()
        {
            var targetPath = TempPath();
            try
            {
                await File.WriteAllTextAsync(targetPath, "old-binary-content");

                using var client = new HttpClient(new FakeHandler(Encoding.ASCII.GetBytes("tampered")));
                var asset = new YtDlpUpdater.ReleaseAsset
                {
                    Name = "yt-dlp.exe",
                    DownloadUrl = "https://example.com/yt-dlp.exe",
                    Sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
                };

                var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => YtDlpUpdater.InstallAsync(client, asset, targetPath, _ => "v"));

                StringAssert.Contains(ex.Message, "SHA-256 mismatch");
                Assert.AreEqual("old-binary-content", await File.ReadAllTextAsync(targetPath));
                Assert.IsFalse(File.Exists(targetPath + ".download.exe"));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(targetPath), true);
            }
        }

        [TestMethod]
        public async Task InstallAsync_MissingDigest_RefusesToInstall()
        {
            var targetPath = TempPath();
            try
            {
                await File.WriteAllTextAsync(targetPath, "old-binary-content");

                using var client = new HttpClient(new FakeHandler(Encoding.ASCII.GetBytes("x")));
                var asset = new YtDlpUpdater.ReleaseAsset
                {
                    Name = "yt-dlp.exe",
                    DownloadUrl = "https://example.com/yt-dlp.exe",
                    Sha256 = null,
                };

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => YtDlpUpdater.InstallAsync(client, asset, targetPath, _ => "v"));

                Assert.AreEqual("old-binary-content", await File.ReadAllTextAsync(targetPath));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(targetPath), true);
            }
        }

        [TestMethod]
        public async Task InstallAsync_PostFlightValidationFails_RestoresBackup()
        {
            var payload = Encoding.ASCII.GetBytes("new-binary-content");
            var targetPath = TempPath();
            try
            {
                await File.WriteAllTextAsync(targetPath, "old-binary-content");

                using var client = new HttpClient(new FakeHandler(payload));
                var asset = new YtDlpUpdater.ReleaseAsset
                {
                    Name = "yt-dlp.exe",
                    DownloadUrl = "https://example.com/yt-dlp.exe",
                    Sha256 = ToHex(SHA256.HashData(payload)),
                };

                // Pre-flight passes (temp file), post-flight fails (installed file).
                string Validate(string path) => path.EndsWith(".download.exe") ? "2026.11.10" : null;

                var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => YtDlpUpdater.InstallAsync(client, asset, targetPath, Validate));

                StringAssert.Contains(ex.Message, "rolled back");
                Assert.AreEqual("old-binary-content", await File.ReadAllTextAsync(targetPath));
                Assert.IsFalse(File.Exists(targetPath + ".backup"));
                Assert.IsFalse(File.Exists(targetPath + ".download.exe"));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(targetPath), true);
            }
        }

        [TestMethod]
        public async Task InstallAsync_PreFlightValidationFails_NeverTouchesTarget()
        {
            var payload = Encoding.ASCII.GetBytes("new-binary-content");
            var targetPath = TempPath();
            try
            {
                await File.WriteAllTextAsync(targetPath, "old-binary-content");

                using var client = new HttpClient(new FakeHandler(payload));
                var asset = new YtDlpUpdater.ReleaseAsset
                {
                    Name = "yt-dlp.exe",
                    DownloadUrl = "https://example.com/yt-dlp.exe",
                    Sha256 = ToHex(SHA256.HashData(payload)),
                };

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => YtDlpUpdater.InstallAsync(client, asset, targetPath, _ => null));

                Assert.AreEqual("old-binary-content", await File.ReadAllTextAsync(targetPath));
                Assert.IsFalse(File.Exists(targetPath + ".backup"));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(targetPath), true);
            }
        }

        [TestMethod]
        public async Task DownloadToFileAsync_ReportsProgressAndHashesContent()
        {
            var payload = Encoding.ASCII.GetBytes("progressive-download-content");
            var tempPath = TempPath() + ".download.exe";
            try
            {
                using var client = new HttpClient(new FakeHandler(payload));
                long? lastReport = null;
                var progress = new Progress<long>(value => lastReport = value);

                var hash = await YtDlpUpdater.DownloadToFileAsync(client, "https://example.com/asset", tempPath, progress);
                await Task.Delay(50); // let Progress callbacks run

                Assert.AreEqual(ToHex(SHA256.HashData(payload)), hash);
                Assert.AreEqual(payload.Length, lastReport);
                Assert.IsTrue(File.Exists(tempPath));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(tempPath), true);
            }
        }
    }
}
