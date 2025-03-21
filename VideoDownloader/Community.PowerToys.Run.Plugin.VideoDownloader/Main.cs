using ManagedCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Wox.Plugin;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using System.Reflection;
using Wox.Plugin.Logger;
using System.Windows.Controls;
using System.Linq;

namespace Community.PowerToys.Run.Plugin.VideoDownloader
{
    // Implements IPlugin, IContextMenu for additional actions, and IPluginI18n for localization.
    public class Main : IPlugin, IContextMenu, IPluginI18n, IDisposable
    {
        public static string PluginID => "9B6621426ABD46EC9C8B30F165866711";
        public string Name => "VideoDownloader";
        public string Description => "Download videos from URLs (supports YouTube with restricted content)";

        private PluginInitContext Context { get; set; }
        private string IconPath { get; set; }
        private bool Disposed { get; set; }
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly YoutubeClient _youtubeClient = new YoutubeClient();
        private string _downloadFolder;
        private CancellationTokenSource _downloadCancellationTokenSource;
        private string _pluginDirectory;
        private string _ytDlpPath;
        private string _cookiesFilePath;
        private bool _ytDlpAvailable;

        // Settings
        private bool _openExplorerAfterDownload = true;
        private bool _preferYtDlp = false;
        private bool _autoInstallYtDlp = true;
        private string _preferredQuality = "best"; // Default quality

        // Resource strings – these should eventually be moved into .resx files.
        private const string DownloadStartedMessage = "Download started";
        private const string DownloadCompleteTitle = "Download Complete!";
        private const string DownloadCompleteMessage = "Video saved to {0}";
        private const string DownloadFailedTitle = "Download Failed";
        private const string InvalidUrlMessage = "Enter valid video URL";
        private const string InvalidUrlSubTitle = "Example: https://youtu.be/abc123 or https://example.com/video.mp4";
        private const string YtDlpMissingMessage = "yt-dlp is required for restricted content";
        private const string YtDlpInstallingMessage = "Installing yt-dlp...";
        private const string YtDlpDownloadFailedMessage = "Failed to download yt-dlp";

        // Icon glyphs for context menu
        private const string DownloadIcon = "\uE896";    // Download icon
        private const string CopyIcon = "\uE8C8";        // Clipboard icon
        private const string FolderIcon = "\uE838";      // Folder icon
        private const string SettingsIcon = "\uE713";    // Settings icon
        private const string CookieIcon = "\uE753";      // Cookie icon
        private const string QualityIcon = "\uE9E9";     // HD icon
        private const string YoutubeIcon = "\uF5B1";     // Video icon 

        // Quality options for context menu
        private readonly Dictionary<string, string> _qualityOptions = new Dictionary<string, string>
        {
            { "best", "Best Quality" },
            { "1080p", "1080p" },
            { "720p", "720p" },
            { "480p", "480p" },
            { "360p", "360p" },
            { "audio", "Audio Only" }
        };

        public List<Result> Query(Query query)
        {
            var search = query.Search.Trim();
            // Return a friendly message if the URL is missing or invalid.
            if (string.IsNullOrWhiteSpace(search) || !Uri.IsWellFormedUriString(search, UriKind.Absolute))
            {
                return new List<Result>
                {
                    new Result
                    {
                        QueryTextDisplay = search,
                        IcoPath = IconPath,
                        Title = InvalidUrlMessage,
                        SubTitle = InvalidUrlSubTitle,
                    }
                };
            }

            // Return a single result that will start the download on selection.
            return new List<Result>
            {
                new Result
                {
                    QueryTextDisplay = search,
                    IcoPath = IconPath,
                    Title = "Download video",
                    SubTitle = $"URL: {search} | Quality: {_qualityOptions[_preferredQuality]}",
                    ToolTipData = new ToolTipData("Download Video", 
                        $"Supports YouTube (including restricted content) and direct links{(_ytDlpAvailable ? "" : " - yt-dlp not installed")}"),
                    Action = context =>
                    {
                        // Fire-and-forget the download task.
                        Task.Run(() => DownloadVideoAsync(search));
                        return true;
                    },
                    ContextData = search,
                }
            };
        }

        /// <summary>
        /// Downloads the video given the URL by dispatching to the appropriate download method.
        /// </summary>
        /// <param name="url">The video URL.</param>
        private async Task DownloadVideoAsync(string url)
        {
            // Cancel any ongoing download if needed.
            _downloadCancellationTokenSource?.Cancel();
            _downloadCancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _downloadCancellationTokenSource.Token;

            try
            {
                // Immediately notify the user that the download has started.
                Context.API.ShowMsg(DownloadStartedMessage, $"Downloading video from: {url}");

                if (IsYouTubeUrl(url))
                {
                    if (_preferYtDlp && _ytDlpAvailable)
                    {
                        await DownloadFromYouTubeWithYtDlpAsync(url, token);
                    }
                    else
                    {
                        try
                        {
                            await DownloadFromYouTubeAsync(url, token);
                        }
                        catch (Exception ex) when (IsRestrictedContentException(ex))
                        {
                            if (_ytDlpAvailable)
                            {
                                // Fallback to yt-dlp for restricted content
                                Context.API.ShowMsg("Restricted Content", "Trying alternative download method...");
                                await DownloadFromYouTubeWithYtDlpAsync(url, token);
                            }
                            else if (_autoInstallYtDlp)
                            {
                                Context.API.ShowMsg(YtDlpInstallingMessage, "Required for restricted content");
                                if (await InstallYtDlpAsync(token))
                                {
                                    await DownloadFromYouTubeWithYtDlpAsync(url, token);
                                }
                                else
                                {
                                    throw new Exception(YtDlpDownloadFailedMessage);
                                }
                            }
                            else
                            {
                                // Propagate original exception if yt-dlp is not available
                                throw new Exception($"{ex.Message}\n\n{YtDlpMissingMessage}");
                            }
                        }
                    }
                }
                else
                {
                    await DownloadFromDirectUrlAsync(url, token);
                }
                OnDownloadSucceeded();
            }
            catch (Exception ex)
            {
                OnDownloadFailed(ex);
            }
        }

        /// <summary>
        /// Downloads a YouTube video using YoutubeExplode with quality selection.
        /// </summary>
        private async Task DownloadFromYouTubeAsync(string url, CancellationToken token)
        {
            // Get video details and stream manifest.
            var videoInfo = await _youtubeClient.Videos.GetAsync(url, token);
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoInfo.Id, token);

            // Select stream based on quality preference
            IStreamInfo streamInfo = null;

            if (_preferredQuality == "audio")
            {
                // Audio only - get best audio
                streamInfo = streamManifest.GetAudioStreams().GetWithHighestBitrate();
                if (streamInfo == null)
                {
                    throw new Exception("No suitable audio streams found for this video.");
                }
            }
            else if (_preferredQuality == "best")
            {
                // Try to get a muxed stream first
                streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
            }
            else
            {
                // Try to get the specific quality
                int height = int.Parse(_preferredQuality.Replace("p", ""));

                // First try muxed streams
                streamInfo = streamManifest.GetMuxedStreams()
                    .Where(s => s.VideoResolution.Height <= height)
                    .OrderByDescending(s => s.VideoResolution.Height)
                    .FirstOrDefault();

                // If no muxed stream found, we'll need to download video and audio separately
                if (streamInfo == null)
                {
                    var videoStream = streamManifest.GetVideoStreams()
                        .Where(s => s.VideoResolution.Height <= height)
                        .OrderByDescending(s => s.VideoResolution.Height)
                        .FirstOrDefault();

                    var audioStream = streamManifest.GetAudioStreams().GetWithHighestBitrate();

                    if (videoStream != null && audioStream != null)
                    {
                        // Build a safe file name from the video title
                        string fileName = SanitizeFileName(videoInfo.Title) + ".mp4";
                        string filePath = Path.Combine(_downloadFolder, fileName);

                        // Download separate streams and mux them
                        await DownloadVideoAndAudioAsync(videoStream, audioStream, filePath, token);
                        return;
                    }
                }
            }

            // If no stream found based on quality preference, fall back to best available
            if (streamInfo == null)
            {
                // Try to get any muxed stream
                streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();

                // If still no stream found, try separate video and audio
                if (streamInfo == null)
                {
                    var videoStream = streamManifest.GetVideoStreams().GetWithHighestVideoQuality();
                    var audioStream = streamManifest.GetAudioStreams().GetWithHighestBitrate();

                    if (videoStream == null || audioStream == null)
                    {
                        throw new Exception("No suitable streams found for this video.");
                    }

                    // Build a safe file name from the video title
                    string fileName = SanitizeFileName(videoInfo.Title) + ".mp4";
                    string filePath = Path.Combine(_downloadFolder, fileName);

                    // Download separate streams and mux them
                    await DownloadVideoAndAudioAsync(videoStream, audioStream, filePath, token);
                    return;
                }
            }

            // For a single stream, download directly
            string extension = _preferredQuality == "audio" ? ".mp3" : "." + streamInfo.Container;
            string streamFileName = SanitizeFileName(videoInfo.Title) + extension;
            string streamFilePath = Path.Combine(_downloadFolder, streamFileName);

            // Download the stream to file
            await _youtubeClient.Videos.Streams.DownloadAsync(streamInfo, streamFilePath, null, token);
        }

        /// <summary>
        /// Downloads separate video and audio streams and combines them.
        /// </summary>
        private async Task DownloadVideoAndAudioAsync(IVideoStreamInfo videoStream, IStreamInfo audioStream, string outputPath, CancellationToken token)
        {
            // Create temporary files for the streams
            string videoTempPath = Path.Combine(_downloadFolder, $"temp_video_{Guid.NewGuid()}.{videoStream.Container}");
            string audioTempPath = Path.Combine(_downloadFolder, $"temp_audio_{Guid.NewGuid()}.{audioStream.Container}");

            try
            {
                // Download video stream
                await _youtubeClient.Videos.Streams.DownloadAsync(videoStream, videoTempPath, null, token);

                // Download audio stream
                await _youtubeClient.Videos.Streams.DownloadAsync(audioStream, audioTempPath, null, token);

                // Use FFmpeg (via yt-dlp) to combine them if available
                if (_ytDlpAvailable)
                {
                    // Using yt-dlp's built-in FFmpeg to mux files
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _ytDlpPath,
                        Arguments = $"--remux-video mp4 --output \"{outputPath}\" \"{videoTempPath}\" --audio-file \"{audioTempPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = startInfo };
                    var tcs = new TaskCompletionSource<bool>();
                    process.Exited += (s, e) => tcs.TrySetResult(process.ExitCode == 0);
                    process.Start();
                    await tcs.Task;
                }
                else
                {
                    // If no yt-dlp available, just copy the video file
                    File.Copy(videoTempPath, outputPath, true);
                }
            }
            finally
            {
                // Clean up temp files
                try { if (File.Exists(videoTempPath)) File.Delete(videoTempPath); } catch { }
                try { if (File.Exists(audioTempPath)) File.Delete(audioTempPath); } catch { }
            }
        }


        /// <summary>
        /// Downloads YouTube video using yt-dlp external process with quality selection.
        /// </summary>
        private async Task DownloadFromYouTubeWithYtDlpAsync(string url, CancellationToken token)
        {
            if (!_ytDlpAvailable)
            {
                throw new Exception(YtDlpMissingMessage);
            }

            // Create a temporary directory for output template
            string outputTemplate = Path.Combine(_downloadFolder, "%(title)s.%(ext)s");

            // Determine format based on quality preference
            string formatOption;

            switch (_preferredQuality)
            {
                case "1080p":
                    formatOption = "bestvideo[height<=1080]+bestaudio/best[height<=1080]";
                    break;
                case "720p":
                    formatOption = "bestvideo[height<=720]+bestaudio/best[height<=720]";
                    break;
                case "480p":
                    formatOption = "bestvideo[height<=480]+bestaudio/best[height<=480]";
                    break;
                case "360p":
                    formatOption = "bestvideo[height<=360]+bestaudio/best[height<=360]";
                    break;
                case "audio":
                    formatOption = "bestaudio";
                    break;
                default: // "best"
                    formatOption = "bestvideo+bestaudio/best";
                    break;
            }

            // Determine output format
            string outputFormat = _preferredQuality == "audio" ? "mp3" : "mp4";

            // Build process arguments
            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = $"-f \"{formatOption}\" --merge-output-format {outputFormat} --output \"{outputTemplate}\" \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Add cookies file if available
            if (!string.IsNullOrEmpty(_cookiesFilePath) && File.Exists(_cookiesFilePath))
            {
                startInfo.Arguments += $" --cookies \"{_cookiesFilePath}\"";
            }

            // For audio only, add extraction options
            if (_preferredQuality == "audio")
            {
                startInfo.Arguments += " -x --audio-format mp3";
            }

            using var process = new Process { StartInfo = startInfo };

            // Create a TaskCompletionSource to wait for process completion
            var tcs = new TaskCompletionSource<bool>();

            // Setup cancellation
            token.Register(() => {
                try { if (!process.HasExited) process.Kill(); } catch { }
            });

            // Buffer for capturing output
            var outputBuffer = new List<string>();
            var errorBuffer = new List<string>();

            // Handle output
            process.OutputDataReceived += (s, e) => {
                if (e.Data != null)
                {
                    outputBuffer.Add(e.Data);
                    // Log useful info like download progress
                    if (e.Data.Contains("%"))
                    {
                        Log.Info(e.Data, GetType());
                    }
                }
            };

            process.ErrorDataReceived += (s, e) => {
                if (e.Data != null)
                {
                    errorBuffer.Add(e.Data);
                    Log.Error(e.Data, GetType());
                }
            };

            process.Exited += (s, e) => tcs.TrySetResult(process.ExitCode == 0);

            // Start process and begin reading outputs
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to complete
            bool success = await tcs.Task;
            if (!success)
            {
                // If the process failed, throw an exception with the error output
                string errorMessage = string.Join(Environment.NewLine, errorBuffer);
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = "Unknown yt-dlp error";
                }
                throw new Exception($"yt-dlp failed: {errorMessage}");
            }
        }

        /// <summary>
        /// Downloads a video directly from a URL.
        /// </summary>
        private async Task DownloadFromDirectUrlAsync(string url, CancellationToken token)
        {
            string fileName = GetFilenameFromUrl(url);
            string filePath = Path.Combine(_downloadFolder, fileName);

            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs, token);
                }
            }
        }

        /// <summary>
        /// Downloads and installs yt-dlp to the plugin directory.
        /// </summary>
        private async Task<bool> InstallYtDlpAsync(CancellationToken token)
        {
            try
            {
                // URL for Windows binary
                string ytDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

                // Make sure the bin directory exists
                string binDir = Path.Combine(_pluginDirectory, "bin");
                if (!Directory.Exists(binDir))
                {
                    Directory.CreateDirectory(binDir);
                }

                // Download the file
                using (var response = await _httpClient.GetAsync(ytDlpUrl, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(_ytDlpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs, token);
                    }
                }

                _ytDlpAvailable = File.Exists(_ytDlpPath);
                return _ytDlpAvailable;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to install yt-dlp: {ex.Message}", GetType());
                return false;
            }
        }

        /// <summary>
        /// Checks if an exception is related to restricted content.
        /// </summary>
        private bool IsRestrictedContentException(Exception ex)
        {
            string message = ex.Message.ToLowerInvariant();
            return message.Contains("sign in") || 
                   message.Contains("unplayable") || 
                   message.Contains("restricted") || 
                   message.Contains("private") ||
                   message.Contains("unavailable") ||
                   message.Contains("403");
        }

        /// <summary>
        /// Called when a download completes successfully.
        /// </summary>
        private void OnDownloadSucceeded()
        {
            // For simplicity, we display the download folder.
            Context.API.ShowMsg(DownloadCompleteTitle, string.Format(DownloadCompleteMessage, _downloadFolder));
            if (_openExplorerAfterDownload)
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{_downloadFolder}\"");
                }
                catch (Exception ex)
                {
                    // Log error and notify user if Explorer cannot be opened.
                    Log.Error($"Could not open Explorer: {ex.Message}", GetType());
                    Context.API.ShowMsg("Error", "Could not open Explorer: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Called when a download fails.
        /// </summary>
        private void OnDownloadFailed(Exception ex)
        {
            Log.Error(ex.ToString(), GetType());
            // Display a user-friendly error message.
            Context.API.ShowMsg(DownloadFailedTitle, ex.Message);
        }

        /// <summary>
        /// Checks whether the URL is a YouTube URL.
        /// </summary>
        private bool IsYouTubeUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                       uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Extracts a file name from the URL. If no name is found, defaults to "video.mp4".
        /// </summary>
        private string GetFilenameFromUrl(string url)
        {
            var uri = new Uri(url);
            var path = uri.LocalPath.TrimEnd('/');
            var filename = Path.GetFileName(path);
            if (string.IsNullOrEmpty(filename))
                filename = "video";
            if (string.IsNullOrEmpty(Path.GetExtension(filename)))
                filename += ".mp4";
            return SanitizeFileName(filename);
        }

        /// <summary>
        /// Replaces illegal characters in file names.
        /// </summary>
        private string SanitizeFileName(string filename)
        {
            return Regex.Replace(filename, @"[\\/*?:""<>.|]", "_");
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());

            // Get the plugin directory
            _pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Set yt-dlp path
            _ytDlpPath = Path.Combine(_pluginDirectory, "bin", "yt-dlp.exe");
            _ytDlpAvailable = File.Exists(_ytDlpPath);

            // Check for cookies file
            _cookiesFilePath = Path.Combine(_pluginDirectory, "cookies.txt");

            // Create a download folder under the user's Videos directory.
            _downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "PowerToysDownloads");
            if (!Directory.Exists(_downloadFolder))
                Directory.CreateDirectory(_downloadFolder);

            // Load settings
            LoadSettings();
        }

        private void UpdateIconPath(Theme theme)
        {
            IconPath = (theme == Theme.Light || theme == Theme.HighContrastWhite)
                ? "Images/videodownloader.light.png"
                : "Images/videodownloader.dark.png";
        }

        private void OnThemeChanged(Theme current, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<ContextMenuResult>();
            if (selectedResult.ContextData is string url)
            {
                // Basic options
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Copy URL",
                    Glyph = CopyIcon,
                    Action = _ =>
                    {
                        try { Clipboard.SetText(url); } catch { }
                        return true;
                    }
                });

                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Open Download Folder",
                    Glyph = FolderIcon,
                    Action = _ =>
                    {
                        try { Process.Start("explorer.exe", _downloadFolder); } catch { }
                        return true;
                    }
                });

                // Add video quality options
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Video Quality Options",
                    Glyph = QualityIcon,
                    Action = _ => false, // This item is just a header
                });

                // Add each quality option as a separate menu item
                foreach (var quality in _qualityOptions)
                {
                    contextMenus.Add(new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = $"{(quality.Key == _preferredQuality ? "✓ " : "")}{quality.Value}",
                        Action = _ =>
                        {
                            _preferredQuality = quality.Key;
                            SaveSettings();
                            return false;
                        }
                    });
                }

                // YouTube-specific options
                if (IsYouTubeUrl(url))
                {
                    contextMenus.Add(new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = "Select Cookies File (for restricted videos)",
                        Glyph = CookieIcon,
                        Action = _ =>
                        {
                            Task.Run(() => SelectCookiesFileAsync());
                            return true;
                        }
                    });

                    contextMenus.Add(new ContextMenuResult
                    {
                        PluginName = Name,
                        Title = _ytDlpAvailable 
                            ? "Download using yt-dlp (for restricted videos)" 
                            : "Install yt-dlp (for restricted videos)",
                        Glyph = DownloadIcon,
                        Action = _ =>
                        {
                            Task.Run(async () => 
                            {
                                if (!_ytDlpAvailable && _autoInstallYtDlp)
                                {
                                    Context.API.ShowMsg(YtDlpInstallingMessage, "Required for restricted content");
                                    await InstallYtDlpAsync(CancellationToken.None);
                                }

                                if (_ytDlpAvailable)
                                {
                                    await DownloadVideoAsync(url);
                                }
                                else
                                {
                                    Context.API.ShowMsg(YtDlpMissingMessage, "Could not install yt-dlp");
                                }
                            });
                            return true;
                        }
                    });
                }
            }
            return contextMenus;
        }

        private async Task SelectCookiesFileAsync()
        {
            // This must be run on an STA thread for the dialog to work properly
            await Task.Run(() => {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Cookies files (*.txt)|*.txt|All files (*.*)|*.*",
                    Title = "Select YouTube cookies file"
                };

                if (dialog.ShowDialog() == true)
                {
                    _cookiesFilePath = dialog.FileName;
                    SaveSettings();
                    Context.API.ShowMsg("Cookies File Selected", $"Using: {_cookiesFilePath}");
                }
            });
        }

        /// <summary>
        /// Load settings from the plugin's data file
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                string settingsPath = Path.Combine(_pluginDirectory, "settings.txt");
                if (File.Exists(settingsPath))
                {
                    var lines = File.ReadAllLines(settingsPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();

                            switch (key)
                            {
                                case "OpenExplorerAfterDownload":
                                    _openExplorerAfterDownload = bool.Parse(value);
                                    break;
                                case "PreferYtDlp":
                                    _preferYtDlp = bool.Parse(value);
                                    break;
                                case "AutoInstallYtDlp":
                                    _autoInstallYtDlp = bool.Parse(value);
                                    break;
                                case "PreferredQuality":
                                    if (_qualityOptions.ContainsKey(value))
                                    {
                                        _preferredQuality = value;
                                    }
                                    break;
                                case "CookiesFilePath":
                                    if (File.Exists(value))
                                    {
                                        _cookiesFilePath = value;
                                    }
                                    break;
                                case "DownloadFolder":
                                    if (Directory.Exists(value))
                                    {
                                        _downloadFolder = value;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load settings: {ex.Message}", GetType());
            }
        }

        /// <summary>
        /// Save settings to the plugin's data file
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                string settingsPath = Path.Combine(_pluginDirectory, "settings.txt");
                var settings = new List<string>
                {
                    $"OpenExplorerAfterDownload={_openExplorerAfterDownload}",
                    $"PreferYtDlp={_preferYtDlp}",
                    $"AutoInstallYtDlp={_autoInstallYtDlp}",
                    $"PreferredQuality={_preferredQuality}",
                                        $"CookiesFilePath={_cookiesFilePath}",
                                        $"DownloadFolder={_downloadFolder}"
                                    };
                                    File.WriteAllLines(settingsPath, settings);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error($"Failed to save settings: {ex.Message}", GetType());
                                }
                            }

                            // IPluginI18n implementation for localization support.
                            public string GetTranslatedPluginTitle() => Name;
                            public string GetTranslatedPluginDescription() => Description;

                            public void Dispose()
                            {
                                Dispose(true);
                                GC.SuppressFinalize(this);
                            }

                            protected virtual void Dispose(bool disposing)
                            {
                                if (Disposed || !disposing)
                                    return;

                                Context.API.ThemeChanged -= OnThemeChanged;
                                _httpClient.Dispose();
                                _downloadCancellationTokenSource?.Dispose();
                                Disposed = true;
                            }
                        }
                    }