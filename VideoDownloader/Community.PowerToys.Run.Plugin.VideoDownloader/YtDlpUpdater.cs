using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Community.PowerToys.Run.Plugin.VideoDownloader
{
    /// <summary>
    /// Downloads and installs yt-dlp updates: fetches the latest GitHub release metadata,
    /// picks the asset for the current architecture, verifies the download against the
    /// SHA-256 digest published in the release metadata, and replaces the existing binary
    /// atomically with rollback to the previous binary on any failure.
    /// </summary>
    public static class YtDlpUpdater
    {
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";

        public sealed class ReleaseAsset
        {
            public string Name { get; init; }

            public string DownloadUrl { get; init; }

            /// <summary>Lowercase hex SHA-256 from the release metadata digest; null when the release has none.</summary>
            public string Sha256 { get; init; }
        }

        public sealed class ReleaseInfo
        {
            public string TagName { get; init; }

            public ReleaseAsset[] Assets { get; init; }
        }

        /// <summary>Maps the process architecture to the yt-dlp release asset name.</summary>
        public static string SelectAssetName(Architecture architecture) =>
            architecture == Architecture.X86 ? "yt-dlp_x86.exe" : "yt-dlp.exe";

        /// <summary>Picks the release asset for the given architecture; throws when the release has none.</summary>
        public static ReleaseAsset SelectAsset(ReleaseInfo release, Architecture architecture)
        {
            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            var name = SelectAssetName(architecture);
            var asset = Array.Find(release.Assets ?? Array.Empty<ReleaseAsset>(), a => a?.Name == name);
            return asset ?? throw new InvalidOperationException($"Release {release.TagName} has no asset '{name}'.");
        }

        /// <summary>Parses the GitHub releases/latest JSON response.</summary>
        public static ReleaseInfo ParseRelease(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            string tag = null;
            if (root.TryGetProperty("tag_name", out var tagElement) && tagElement.ValueKind == JsonValueKind.String)
            {
                tag = tagElement.GetString();
            }

            ReleaseAsset[] assets = Array.Empty<ReleaseAsset>();
            if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
            {
                assets = ParseAssets(assetsElement);
            }

            return new ReleaseInfo { TagName = tag, Assets = assets };
        }

        private static ReleaseAsset[] ParseAssets(JsonElement assetsElement)
        {
            var assets = new ReleaseAsset[assetsElement.GetArrayLength()];
            var i = 0;
            foreach (var element in assetsElement.EnumerateArray())
            {
                assets[i++] = new ReleaseAsset
                {
                    Name = GetStringOrNull(element, "name"),
                    DownloadUrl = GetStringOrNull(element, "browser_download_url"),
                    Sha256 = ParseDigest(GetStringOrNull(element, "digest")),
                };
            }

            return assets;
        }

        private static string GetStringOrNull(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        /// <summary>Converts a GitHub digest field ("sha256:hex") to plain lowercase hex; other algorithms yield null.</summary>
        public static string ParseDigest(string digest) =>
            digest != null
            && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? digest["sha256:".Length..].Trim().ToLowerInvariant()
            : null;

        /// <summary>Computes the lowercase hex SHA-256 of a stream without loading it into memory.</summary>
        public static string ComputeSha256(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }

        /// <summary>
        /// Streams the asset to <paramref name="tempPath"/> while hashing it,
        /// reporting downloaded bytes via <paramref name="progress"/>. Returns the file's SHA-256.
        /// </summary>
        public static async Task<string> DownloadToFileAsync(
            HttpClient client,
            string url,
            string tempPath,
            IProgress<long> progress,
            CancellationToken cancellationToken = default)
        {
            using var response = await (client ?? throw new ArgumentNullException(nameof(client)))
                .GetAsync(url ?? throw new ArgumentNullException(nameof(url)), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var target = File.Create(tempPath ?? throw new ArgumentNullException(nameof(tempPath)));
            using var sha256 = SHA256.Create();

            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                sha256.TransformBlock(buffer, 0, read, null, 0);
                total += read;
                progress?.Report(total);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha256.Hash).ToLowerInvariant();
        }

        /// <summary>
        /// Full install: download to a temp file, verify SHA-256 against the release metadata,
        /// pre-validate the binary, atomically replace the target, post-validate, roll back on failure.
        /// Returns the version reported by the installed binary.
        /// </summary>
        /// <param name="validateBinary">Receives the binary path, returns its version string; null/empty means the binary is broken.</param>
        public static async Task<string> InstallAsync(
            HttpClient client,
            ReleaseAsset asset,
            string targetPath,
            Func<string, string> validateBinary,
            IProgress<string> status = null,
            CancellationToken cancellationToken = default)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrEmpty(asset.Sha256))
            {
                throw new InvalidOperationException($"Release metadata for '{asset.Name}' has no SHA-256 digest; refusing to install.");
            }

            if (string.IsNullOrEmpty(asset.DownloadUrl))
            {
                throw new InvalidOperationException($"Release asset '{asset.Name}' has no download URL.");
            }

            // Must keep the .exe extension: pre-validation runs the downloaded file,
            // and Windows only executes .exe/.bat/.com regardless of content.
            var tempPath = targetPath + ".download.exe";
            var backupPath = targetPath + ".backup";

            try
            {
                status?.Report($"Downloading {asset.Name}…");
                var actualSha256 = await DownloadToFileAsync(client, asset.DownloadUrl, tempPath, null, cancellationToken).ConfigureAwait(false);

                status?.Report("Verifying SHA-256…");
                if (!string.Equals(actualSha256, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"SHA-256 mismatch for '{asset.Name}': expected {asset.Sha256}, got {actualSha256}. Download discarded.");
                }

                status?.Report("Testing downloaded binary…");
                var version = validateBinary?.Invoke(tempPath);
                if (string.IsNullOrWhiteSpace(version))
                {
                    throw new InvalidOperationException("Downloaded yt-dlp binary failed to run; keeping the current version.");
                }

                // Back up the current binary, then replace it atomically (same-volume move).
                if (File.Exists(targetPath))
                {
                    File.Copy(targetPath, backupPath, true);
                }

                status?.Report("Installing…");
                File.Move(tempPath, targetPath, true);

                // Post-flight check: if the installed binary no longer runs, roll back.
                var installedVersion = validateBinary?.Invoke(targetPath);
                if (string.IsNullOrWhiteSpace(installedVersion))
                {
                    Rollback(targetPath, backupPath);
                    throw new InvalidOperationException("Installed yt-dlp failed to start; rolled back to the previous version.");
                }

                TryDelete(backupPath);
                status?.Report($"Updated to {installedVersion.Trim()}");
                return installedVersion.Trim();
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        private static void Rollback(string targetPath, string backupPath)
        {
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, targetPath, true);
                TryDelete(backupPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best effort cleanup; a leftover temp/backup file is harmless.
            }
        }
    }
}
