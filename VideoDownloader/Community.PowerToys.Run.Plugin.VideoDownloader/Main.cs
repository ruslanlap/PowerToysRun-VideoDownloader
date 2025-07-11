using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using ManagedCommon;
using Wox.Plugin;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Community.PowerToys.Run.Plugin.VideoDownloader
{
    public class Main : IPlugin, IReloadable, IDisposable, ISettingProvider
    {
        public static string PluginID => "B8F9B9F5C3E44A8B9F1F2E3D4C5B6A7B";
        public string Name => "VideoDownloader";
        public string Description => "Download videos from YouTube and other websites using yt-dlp";

        private PluginInitContext _context;
        private string _iconPath;
        private bool _disposed;
        private VideoDownloaderSettings _settings;
        private readonly string _settingsPath;
        private static readonly HttpClient _httpClient = new HttpClient();
        private static bool _isSetupRunning = false;

        public Main()
        {
            _settings = new VideoDownloaderSettings();
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "PowerToys", "PowerToys Run", "Settings", "Plugins",
                "Community.PowerToys.Run.Plugin.VideoDownloader", "settings.json");
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _iconPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images", "videodownloader.dark.png");
            LoadSettings();
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (!IsYtDlpAvailable() || !IsFfmpegAvailable())
            {
                results.Add(new Result
                {
                    Title = "📥 Setup Required",
                    SubTitle = "Click to automatically download yt-dlp and ffmpeg",
                    IcoPath = _iconPath,
                    Action = _ =>
                    {
                        Task.Run(SetupPluginAsync);
                        return true;
                    }
                });
                return results;
            }

            if (string.IsNullOrEmpty(query.Search))
            {
                results.Add(new Result { Title = "Video Downloader", SubTitle = "Enter a video URL to download", IcoPath = _iconPath });
                results.Add(new Result { Title = "📂 Open Download Folder", SubTitle = $"Current path: {_settings.DownloadPath}", IcoPath = _iconPath, Action = _ => { OpenDownloadFolder(); return true; } });
                results.Add(new Result { Title = "🔄 Update yt-dlp", SubTitle = "Force update yt-dlp to latest version", IcoPath = _iconPath, Action = _ => { Task.Run(() => UpdateYtDlp()); return true; } });
                return results;
            }

            var search = query.Search.Trim();
            if (IsValidUrl(search))
            {
                var domain = GetDomainFromUrl(search);

                if (_settings.AudioOnlyDefault)
                {
                    results.Add(new Result { Title = $"🎵 Download Audio from {domain}", SubTitle = $"Format: {_settings.AudioFormat.ToUpper()}. Folder will open automatically.", IcoPath = _iconPath, Action = _ => { DownloadAudio(search); return true; } });
                    results.Add(new Result { Title = $"🎥 Download Video instead", SubTitle = $"Quality: {_settings.DefaultVideoQuality}. Folder will open automatically.", IcoPath = _iconPath, Action = _ => { DownloadVideo(search); return true; } });
                }
                else
                {
                    results.Add(new Result { Title = $"🎥 Download Video from {domain}", SubTitle = $"Quality: {_settings.DefaultVideoQuality}. Folder will open automatically.", IcoPath = _iconPath, Action = _ => { DownloadVideo(search); return true; } });
                    results.Add(new Result { Title = $"🎵 Download Audio Only", SubTitle = $"Format: {_settings.AudioFormat.ToUpper()}. Folder will open automatically.", IcoPath = _iconPath, Action = _ => { DownloadAudio(search); return true; } });
                }

                var qualityOptions = new[] { "1080p", "720p", "480p", "360p", "best" };
                foreach (var quality in qualityOptions.Where(q => q != _settings.DefaultVideoQuality))
                {
                    results.Add(new Result { Title = $"🎬 Download in {quality}", SubTitle = $"Download video in {quality} quality", IcoPath = _iconPath, Action = _ => { DownloadWithQuality(search, quality); return true; } });
                }

                // Add video info option
                results.Add(new Result { Title = $"ℹ️ Video Information", SubTitle = "Show available formats and qualities", IcoPath = _iconPath, Action = _ => { ShowVideoInfo(search); return true; } });
            }
            else
            {
                results.Add(new Result { Title = "❌ Invalid URL", SubTitle = "Please enter a valid video link.", IcoPath = _iconPath });
            }

            return results;
        }

        private async Task SetupPluginAsync()
        {
            if (_isSetupRunning)
            {
                _context.API.ShowMsg("Setup already running", "Please wait for completion.", _iconPath);
                return;
            }

            _isSetupRunning = true;
            var pluginDir = _context.CurrentPluginMetadata.PluginDirectory;
            var ytDlpPath = GetYtDlpExecutablePath();
            var ffmpegPath = GetFfmpegExecutablePath();

            try
            {
                if (!File.Exists(ytDlpPath))
                {
                    _context.API.ShowMsg("Setup: Step 1/4", "Downloading yt-dlp.exe...", _iconPath);
                    var ytDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
                    var ytDlpBytes = await _httpClient.GetByteArrayAsync(ytDlpUrl);
                    await File.WriteAllBytesAsync(ytDlpPath, ytDlpBytes);
                }

                if (!File.Exists(ffmpegPath))
                {
                    _context.API.ShowMsg("Setup: Step 2/4", "Downloading ffmpeg archive (this may take several minutes)...", _iconPath);
                    var ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
                    var ffmpegZipPath = Path.Combine(pluginDir, "ffmpeg.zip");
                    
                    // Create HttpClient with longer timeout for large downloads
                    using var downloadClient = new HttpClient();
                    downloadClient.Timeout = TimeSpan.FromMinutes(10); // 10 minutes timeout
                    var ffmpegBytes = await downloadClient.GetByteArrayAsync(ffmpegUrl);
                    await File.WriteAllBytesAsync(ffmpegZipPath, ffmpegBytes);

                    _context.API.ShowMsg("Setup: Step 3/4", "Extracting ffmpeg...", _iconPath);
                    var tempExtractPath = Path.Combine(pluginDir, "ffmpeg_temp");
                    if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
                    ZipFile.ExtractToDirectory(ffmpegZipPath, tempExtractPath);

                    var ffmpegExeSource = Directory.GetFiles(tempExtractPath, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();

                    if (ffmpegExeSource != null)
                    {
                        File.Move(ffmpegExeSource, ffmpegPath);
                    }
                    else
                    {
                        throw new FileNotFoundException("ffmpeg.exe not found in archive.");
                    }

                    _context.API.ShowMsg("Setup: Step 4/4", "Cleaning up temporary files...", _iconPath);
                    File.Delete(ffmpegZipPath);
                    Directory.Delete(tempExtractPath, true);
                }

                _context.API.ShowMsg("✅ Setup Complete!", "Plugin is ready to use. Try entering a URL.", _iconPath);
            }
            catch (Exception e)
            {
                _context.API.ShowMsg("❌ Setup Error", e.Message, _iconPath);
            }
            finally
            {
                _isSetupRunning = false;
            }
        }

        private async Task UpdateYtDlp()
        {
            if (_isSetupRunning)
            {
                _context.API.ShowMsg("Update already running", "Please wait for completion.", _iconPath);
                return;
            }

            _isSetupRunning = true;
            var ytDlpPath = GetYtDlpExecutablePath();

            try
            {
                _context.API.ShowMsg("🔄 Updating yt-dlp", "Downloading latest version...", _iconPath);
                
                // Backup existing version
                var backupPath = ytDlpPath + ".backup";
                if (File.Exists(ytDlpPath))
                {
                    File.Copy(ytDlpPath, backupPath, true);
                }

                var ytDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
                var ytDlpBytes = await _httpClient.GetByteArrayAsync(ytDlpUrl);
                await File.WriteAllBytesAsync(ytDlpPath, ytDlpBytes);

                // Test the new version
                var testCommand = "--version";
                var versionOutput = RunYtDlpCommandWithOutput(testCommand);
                
                if (!string.IsNullOrEmpty(versionOutput))
                {
                    _context.API.ShowMsg("✅ yt-dlp Updated!", $"Version: {versionOutput.Trim()}", _iconPath);
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }
                else
                {
                    // Restore backup if update failed
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, ytDlpPath, true);
                        File.Delete(backupPath);
                    }
                    throw new Exception("Updated yt-dlp version failed to run");
                }
            }
            catch (Exception e)
            {
                _context.API.ShowMsg("❌ Update Failed", e.Message, _iconPath);
                Debug.WriteLine($"yt-dlp update failed: {e}");
            }
            finally
            {
                _isSetupRunning = false;
            }
        }

        private bool IsValidUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var result) && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        private string GetDomainFromUrl(string url) => new Uri(url).Host;
        private string GetYtDlpExecutablePath() => Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "yt-dlp.exe");
        private string GetFfmpegExecutablePath() => Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "ffmpeg.exe");
        private bool IsYtDlpAvailable() => File.Exists(GetYtDlpExecutablePath());
        private bool IsFfmpegAvailable() => File.Exists(GetFfmpegExecutablePath());

        private void DownloadVideo(string url) => DownloadWithQuality(url, _settings.DefaultVideoQuality);

        private void DownloadAudio(string url)
        {
            Task.Run(() =>
            {
                try
                {
                    _context.API.ShowMsg("⏳ Downloading...", "Audio download started", _iconPath);

                    var ffmpegDir = Path.GetDirectoryName(GetFfmpegExecutablePath());
                    var outputTemplate = GetSafeOutputTemplate();

                    var commandParts = new List<string>
                    {
                        "--ffmpeg-location", $"\"{ffmpegDir}\"",
                        "-f", "bestaudio/best",
                        "-x",
                        "--audio-format", _settings.AudioFormat,
                        "--audio-quality", _settings.AudioQuality.ToString(),
                        "--no-overwrites", // Always prevent overwrites
                        "-o", $"\"{outputTemplate}\""
                    };

                    if (_settings.EmbedMetadata)
                    {
                        commandParts.AddRange(new[] { "--embed-metadata", "--add-metadata" });
                    }

                    commandParts.Add($"\"{url}\"");
                    var command = BuildYtDlpCommand(commandParts);

                    var success = RunYtDlpCommand(command);
                    if (success)
                    {
                        _context.API.ShowMsg("✅ Audio Downloaded!", "Folder will open automatically", _iconPath);
                        OpenDownloadFolder();
                    }
                }
                catch (Exception e)
                {
                    _context.API.ShowMsg("❌ Download Error", e.Message, _iconPath);
                }
            });
        }

        private void DownloadWithQuality(string url, string quality)
        {
            Task.Run(() =>
            {
                try
                {
                    _context.API.ShowMsg("⏳ Downloading...", $"Video download in {quality} quality started", _iconPath);

                    var format = GetQualityFormat(quality);
                    var ffmpegDir = Path.GetDirectoryName(GetFfmpegExecutablePath());
                    var outputTemplate = GetSafeOutputTemplate();

                    var commandParts = new List<string>
                    {
                        "--ffmpeg-location", $"\"{ffmpegDir}\"",
                        "-f", $"\"{format}\"",
                        "--merge-output-format", _settings.VideoFormat,
                        "--no-overwrites", // Always prevent overwrites
                        "-o", $"\"{outputTemplate}\""
                    };

                    if (_settings.EmbedSubtitles)
                    {
                        commandParts.AddRange(new[] { "--embed-subs", "--write-auto-sub", "--sub-lang", "en,uk,ru" });
                    }

                    if (_settings.EmbedMetadata)
                    {
                        commandParts.AddRange(new[] { "--embed-metadata", "--add-metadata" });
                    }

                    commandParts.Add($"\"{url}\"");

                    var command = BuildYtDlpCommand(commandParts);
                    var success = RunYtDlpCommand(command);
                    if (success)
                    {
                        _context.API.ShowMsg("✅ Video Downloaded!", "Folder will open automatically", _iconPath);
                        OpenDownloadFolder();
                    }
                }
                catch (Exception e)
                {
                    _context.API.ShowMsg("❌ Download Error", e.Message, _iconPath);
                }
            });
        }

        private void ShowVideoInfo(string url)
        {
            Task.Run(() =>
            {
                try
                {
                    var command = BuildYtDlpCommand(new[]
                    {
                        "--list-formats",
                        "--no-download",
                        $"\"{url}\""
                    });

                    var output = RunYtDlpCommandWithOutput(command);
                    if (!string.IsNullOrEmpty(output))
                    {
                        var info = output.TrimEnd();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var window = new VideoInfoWindow(info);
                            window.Show();
                        });
                    }
                }
                catch (Exception e)
                {
                    _context.API.ShowMsg("❌ Information Error", e.Message, _iconPath);
                }
            });
        }

        private string BuildYtDlpCommand(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Where(x => !string.IsNullOrEmpty(x)));
        }

        private string GetQualityFormat(string quality) => quality.ToLower() switch
        {
            "1080p" => "bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]",
            "720p" => "bestvideo[height<=720][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=720]+bestaudio/best[height<=720]",
            "480p" => "bestvideo[height<=480][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=480]+bestaudio/best[height<=480]",
            "360p" => "bestvideo[height<=360][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=360]+bestaudio/best[height<=360]",
            _ => "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best",
        };

        private string GetOutputTemplate()
        {
            var template = _settings.CustomFilenameTemplate;
            if (string.IsNullOrWhiteSpace(template))
            {
                template = _settings.HandleDuplicateFilenames 
                    ? "%(title)s [%(id)s].%(ext)s"
                    : "%(title)s.%(ext)s";
            }
            return Path.Combine(_settings.DownloadPath, template);
        }

        private string GetSafeOutputTemplate()
        {
            var template = _settings.CustomFilenameTemplate;
            if (string.IsNullOrWhiteSpace(template))
            {
                // Always include ID to prevent filename conflicts
                template = "%(title)s [%(id)s].%(ext)s";
            }
            return Path.Combine(_settings.DownloadPath, template);
        }

        private void OpenDownloadFolder()
        {
            try
            {
                if (!Directory.Exists(_settings.DownloadPath))
                {
                    Directory.CreateDirectory(_settings.DownloadPath);
                }
                Process.Start(new ProcessStartInfo { FileName = _settings.DownloadPath, UseShellExecute = true });
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to open download folder: {e.Message}");
                _context.API.ShowMsg("❌ Cannot Open Folder", e.Message, _iconPath);
            }
        }

        private bool RunYtDlpCommand(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = GetYtDlpExecutablePath(),
                    Arguments = arguments,
                    WorkingDirectory = _settings.DownloadPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                var output = process?.StandardOutput.ReadToEnd();
                var error = process?.StandardError.ReadToEnd();
                process?.WaitForExit();
                
                var exitCode = process?.ExitCode ?? -1;
                if (exitCode != 0)
                {
                    var errorMsg = !string.IsNullOrEmpty(error) ? error : output ?? "Unknown error occurred";
                    var shortError = errorMsg.Length > 200 ? errorMsg.Substring(0, 200) + "..." : errorMsg;
                    
                    // Check for common errors and provide helpful messages
                    string helpfulMessage = "";
                    if (errorMsg.Contains("File exists"))
                    {
                        helpfulMessage = "\n\nTip: Try enabling 'Handle Duplicate Filenames' in settings or use a custom filename template.";
                    }
                    else if (errorMsg.Contains("network") || errorMsg.Contains("timeout"))
                    {
                        helpfulMessage = "\n\nTip: Check your internet connection and try again.";
                    }
                    else if (errorMsg.Contains("format") || errorMsg.Contains("quality"))
                    {
                        helpfulMessage = "\n\nTip: Try a different quality setting or check available formats.";
                    }
                    
                    _context.API.ShowMsg("❌ Download Failed", $"Exit code: {exitCode}\n{shortError}{helpfulMessage}", _iconPath);
                    Debug.WriteLine($"yt-dlp failed with exit code {exitCode}: {errorMsg}");
                    return false;
                }
                
                return true;
            }
            catch (Exception e)
            {
                _context.API.ShowMsg("❌ yt-dlp Error", $"Exception: {e.Message}", _iconPath);
                Debug.WriteLine($"yt-dlp exception: {e}");
                return false;
            }
        }

        private string RunYtDlpCommandWithOutput(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = GetYtDlpExecutablePath(),
                    Arguments = arguments,
                    WorkingDirectory = _settings.DownloadPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                var output = process?.StandardOutput.ReadToEnd();
                var error = process?.StandardError.ReadToEnd();
                process?.WaitForExit();
                
                var exitCode = process?.ExitCode ?? -1;
                if (exitCode != 0)
                {
                    var errorMsg = !string.IsNullOrEmpty(error) ? error : "Unknown error occurred";
                    _context.API.ShowMsg("❌ Command Failed", $"Exit code: {exitCode}\n{errorMsg.Substring(0, Math.Min(200, errorMsg.Length))}", _iconPath);
                    Debug.WriteLine($"yt-dlp command failed with exit code {exitCode}: {errorMsg}");
                    return "";
                }
                
                return output ?? "";
            }
            catch (Exception e)
            {
                _context.API.ShowMsg("❌ yt-dlp Error", $"Exception: {e.Message}", _iconPath);
                Debug.WriteLine($"yt-dlp exception: {e}");
                return "";
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    Debug.WriteLine($"Loading settings from: {_settingsPath}");
                    Debug.WriteLine($"Settings JSON: {json}");

                    var loadedSettings = JsonSerializer.Deserialize<VideoDownloaderSettings>(json);
                    if (loadedSettings != null)
                    {
                        _settings = loadedSettings;
                        Debug.WriteLine($"Settings loaded successfully - Quality: {_settings.DefaultVideoQuality}, VideoFormat: {_settings.VideoFormat}, AudioFormat: {_settings.AudioFormat}, AudioQuality: {_settings.AudioQuality}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Settings file not found at: {_settingsPath}");
                    _settings = new VideoDownloaderSettings();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to load settings: {e.Message}");
                _settings = new VideoDownloaderSettings();
            }

            // Ensure download directory exists
            if (!Directory.Exists(_settings.DownloadPath))
            {
                try 
                { 
                    Directory.CreateDirectory(_settings.DownloadPath); 
                } 
                catch (Exception e) 
                { 
                    Debug.WriteLine($"Failed to create download directory: {e.Message}"); 
                }
            }
        }

        private void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (dir != null && !Directory.Exists(dir)) 
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
                Debug.WriteLine($"Settings saved to: {_settingsPath}");
            }
            catch (Exception e) 
            { 
                Debug.WriteLine($"Failed to save settings: {e.Message}"); 
            }
        }

        public void ReloadData() => LoadSettings();
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing) { if (!_disposed) { _disposed = true; } }
        public Control CreateSettingPanel() => throw new NotImplementedException();

        public IEnumerable<PluginAdditionalOption> AdditionalOptions
        {
            get
            {
                var qualityOptions = new List<KeyValuePair<string, string>>
                {
                    new("1080p", "1080p"),
                    new("720p", "720p"),
                    new("480p", "480p"),
                    new("360p", "360p"),
                    new("best", "Best Available")
                };

                var videoFormatOptions = new List<KeyValuePair<string, string>>
                {
                    new("mp4", "MP4"),
                    new("mkv", "MKV"),
                    new("webm", "WebM"),
                    new("avi", "AVI")
                };

                var audioFormatOptions = new List<KeyValuePair<string, string>>
                {
                    new("mp3", "MP3"),
                    new("m4a", "M4A"),
                    new("flac", "FLAC"),
                    new("opus", "Opus"),
                    new("wav", "WAV")
                };

                var audioQualityOptions = new List<KeyValuePair<string, string>>
                {
                    new("0", "Best"),
                    new("1", "Very High"),
                    new("2", "High"),
                    new("3", "Medium"),
                    new("4", "Low")
                };

                // Calculate current indices based on saved settings - with better fallback
                int qualityIndex = qualityOptions.FindIndex(x => x.Key.Equals(_settings.DefaultVideoQuality, StringComparison.OrdinalIgnoreCase));
                if (qualityIndex == -1) qualityIndex = 0; // Default to 1080p

                int videoFormatIndex = videoFormatOptions.FindIndex(x => x.Key.Equals(_settings.VideoFormat, StringComparison.OrdinalIgnoreCase));
                if (videoFormatIndex == -1) videoFormatIndex = 0; // Default to MP4

                int audioFormatIndex = audioFormatOptions.FindIndex(x => x.Key.Equals(_settings.AudioFormat, StringComparison.OrdinalIgnoreCase));
                if (audioFormatIndex == -1) audioFormatIndex = 0; // Default to MP3

                int audioQualityIndex = audioQualityOptions.FindIndex(x => x.Key == _settings.AudioQuality.ToString());
                if (audioQualityIndex == -1) audioQualityIndex = 0; // Default to Best

                Debug.WriteLine($"Current settings - Quality: {_settings.DefaultVideoQuality} (index: {qualityIndex}), VideoFormat: {_settings.VideoFormat} (index: {videoFormatIndex}), AudioFormat: {_settings.AudioFormat} (index: {audioFormatIndex}), AudioQuality: {_settings.AudioQuality} (index: {audioQualityIndex})");

                return new List<PluginAdditionalOption>
                {
                    new()
                    {
                        Key = "DownloadPath",
                        DisplayLabel = "Download Folder",
                        DisplayDescription = "Folder where downloaded files will be saved",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                        TextValue = _settings.DownloadPath
                    },
                    new()
                    {
                        Key = "DefaultVideoQuality",
                        DisplayLabel = "Default Video Quality",
                        DisplayDescription = "Default video quality for downloads (1080p, 720p, 480p, 360p, best)",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                        TextValue = _settings.DefaultVideoQuality
                    },
                    new()
                    {
                        Key = "VideoFormat",
                        DisplayLabel = "Video Output Format",
                        DisplayDescription = "Container format for video files (mp4, mkv, webm, avi)",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                        TextValue = _settings.VideoFormat
                    },
                    new()
                    {
                        Key = "AudioFormat",
                        DisplayLabel = "Audio Output Format",
                        DisplayDescription = "Audio format for audio-only downloads (mp3, m4a, flac, opus, wav)",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                        TextValue = _settings.AudioFormat
                    },
                    new()
                    {
                        Key = "AudioQuality",
                        DisplayLabel = "Audio Quality",
                        DisplayDescription = "Quality level for audio downloads (0=Best, 1=Very High, 2=High, 3=Medium, 4=Low)",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                        TextValue = _settings.AudioQuality.ToString()
                    },
                    new()
                    {
                        Key = "AudioOnlyDefault",
                        DisplayLabel = "Download Audio Only by Default",
                        DisplayDescription = "Show audio download as the primary option",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.AudioOnlyDefault
                    },
                    new()
                    {
                        Key = "EmbedSubtitles",
                        DisplayLabel = "Embed Subtitles",
                        DisplayDescription = "Automatically download and embed subtitles",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.EmbedSubtitles
                    },
                    new()
                    {
                        Key = "EmbedMetadata",
                        DisplayLabel = "Embed Metadata",
                        DisplayDescription = "Include video metadata (title, description, etc.)",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.EmbedMetadata
                    },
                    new()
                    {
                        Key = "ShowDownloadWindow",
                        DisplayLabel = "Show Download Progress Window",
                        DisplayDescription = "Display console window during download",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.ShowDownloadWindow
                    },
                    new()
                    {
                        Key = "HandleDuplicateFilenames",
                        DisplayLabel = "Handle Duplicate Filenames",
                        DisplayDescription = "Prevent overwriting existing files with same name",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.HandleDuplicateFilenames
                    },
                    new()
                    {
                        Key = "CustomFilenameTemplate",
                        DisplayLabel = "Custom Filename Template",
                        DisplayDescription = "yt-dlp output template (e.g., %(title)s [%(id)s].%(ext)s). Leave empty for default.",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                        TextValue = _settings.CustomFilenameTemplate
                    }
                };
            }
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings?.AdditionalOptions == null) return;
            try
            {
                Debug.WriteLine("UpdateSettings called");

                // Update download path with validation
                var downloadPathOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "DownloadPath");
                if (downloadPathOption != null && !string.IsNullOrWhiteSpace(downloadPathOption.TextValue))
                {
                    var newPath = downloadPathOption.TextValue.Trim();
                    if (newPath != _settings.DownloadPath)
                    {
                        _settings.DownloadPath = newPath;
                        try
                        {
                            if (!Directory.Exists(_settings.DownloadPath))
                            {
                                Directory.CreateDirectory(_settings.DownloadPath);
                            }
                            Debug.WriteLine($"Download path updated to: {_settings.DownloadPath}");
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine($"Could not create directory: {e.Message}");
                        }
                    }
                }

                // Валідація для DefaultVideoQuality
                var allowedQualities = new[] { "1080p", "720p", "480p", "360p", "best" };
                var qualityOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "DefaultVideoQuality");
                if (qualityOption != null && !string.IsNullOrWhiteSpace(qualityOption.TextValue))
                {
                    var newQuality = qualityOption.TextValue.Trim().ToLower();
                    if (allowedQualities.Contains(newQuality))
                    {
                        if (newQuality != _settings.DefaultVideoQuality.ToLower())
                        {
                            _settings.DefaultVideoQuality = newQuality;
                            Debug.WriteLine($"Video quality updated to: {_settings.DefaultVideoQuality}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid video quality: {newQuality}. Allowed: {string.Join(", ", allowedQualities)}");
                    }
                }

                // Валідація для VideoFormat
                var allowedVideoFormats = new[] { "mp4", "mkv", "webm", "avi" };
                var videoFormatOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "VideoFormat");
                if (videoFormatOption != null && !string.IsNullOrWhiteSpace(videoFormatOption.TextValue))
                {
                    var newFormat = videoFormatOption.TextValue.Trim().ToLower();
                    if (allowedVideoFormats.Contains(newFormat))
                    {
                        if (newFormat != _settings.VideoFormat.ToLower())
                        {
                            _settings.VideoFormat = newFormat;
                            Debug.WriteLine($"Video format updated to: {_settings.VideoFormat}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid video format: {newFormat}. Allowed: {string.Join(", ", allowedVideoFormats)}");
                    }
                }

                // Валідація для AudioFormat
                var allowedAudioFormats = new[] { "mp3", "m4a", "flac", "opus", "wav" };
                var audioFormatOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "AudioFormat");
                if (audioFormatOption != null && !string.IsNullOrWhiteSpace(audioFormatOption.TextValue))
                {
                    var newFormat = audioFormatOption.TextValue.Trim().ToLower();
                    if (allowedAudioFormats.Contains(newFormat))
                    {
                        if (newFormat != _settings.AudioFormat.ToLower())
                        {
                            _settings.AudioFormat = newFormat;
                            Debug.WriteLine($"Audio format updated to: {_settings.AudioFormat}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid audio format: {newFormat}. Allowed: {string.Join(", ", allowedAudioFormats)}");
                    }
                }

                // Валідація для AudioQuality
                var allowedAudioQualities = new[] { "0", "1", "2", "3", "4" };
                var audioQualityOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "AudioQuality");
                if (audioQualityOption != null && !string.IsNullOrWhiteSpace(audioQualityOption.TextValue))
                {
                    var newQualityStr = audioQualityOption.TextValue.Trim();
                    if (allowedAudioQualities.Contains(newQualityStr))
                    {
                        if (int.TryParse(newQualityStr, out int newQuality))
                        {
                            if (newQuality != _settings.AudioQuality)
                            {
                                _settings.AudioQuality = newQuality;
                                Debug.WriteLine($"Audio quality updated to: {_settings.AudioQuality}");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid audio quality: {newQualityStr}. Allowed: {string.Join(", ", allowedAudioQualities)}");
                    }
                }

                // Update boolean settings
                var audioOnlyOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "AudioOnlyDefault");
                if (audioOnlyOption != null && audioOnlyOption.Value != _settings.AudioOnlyDefault)
                {
                    _settings.AudioOnlyDefault = audioOnlyOption.Value;
                    Debug.WriteLine($"Audio only default updated to: {_settings.AudioOnlyDefault}");
                }

                var embedSubsOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "EmbedSubtitles");
                if (embedSubsOption != null && embedSubsOption.Value != _settings.EmbedSubtitles)
                {
                    _settings.EmbedSubtitles = embedSubsOption.Value;
                    Debug.WriteLine($"Embed subtitles updated to: {_settings.EmbedSubtitles}");
                }

                var embedMetaOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "EmbedMetadata");
                if (embedMetaOption != null && embedMetaOption.Value != _settings.EmbedMetadata)
                {
                    _settings.EmbedMetadata = embedMetaOption.Value;
                    Debug.WriteLine($"Embed metadata updated to: {_settings.EmbedMetadata}");
                }

                var showWindowOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ShowDownloadWindow");
                if (showWindowOption != null && showWindowOption.Value != _settings.ShowDownloadWindow)
                {
                    _settings.ShowDownloadWindow = showWindowOption.Value;
                    Debug.WriteLine($"Show download window updated to: {_settings.ShowDownloadWindow}");
                }

                var handleDuplicatesOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "HandleDuplicateFilenames");
                if (handleDuplicatesOption != null && handleDuplicatesOption.Value != _settings.HandleDuplicateFilenames)
                {
                    _settings.HandleDuplicateFilenames = handleDuplicatesOption.Value;
                    Debug.WriteLine($"Handle duplicate filenames updated to: {_settings.HandleDuplicateFilenames}");
                }

                var customTemplateOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "CustomFilenameTemplate");
                if (customTemplateOption != null && customTemplateOption.TextValue != _settings.CustomFilenameTemplate)
                {
                    _settings.CustomFilenameTemplate = customTemplateOption.TextValue?.Trim() ?? "";
                    Debug.WriteLine($"Custom filename template updated to: {_settings.CustomFilenameTemplate}");
                }

                // Always save settings after any change
                SaveSettings();
                Debug.WriteLine("Settings update completed and saved");

                // Force reload to verify settings
                LoadSettings();
                Debug.WriteLine($"After reload - Quality: {_settings.DefaultVideoQuality}, VideoFormat: {_settings.VideoFormat}, AudioFormat: {_settings.AudioFormat}, AudioQuality: {_settings.AudioQuality}");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error updating settings: {e.Message}");
                Debug.WriteLine($"Stack trace: {e.StackTrace}");
            }
        }
    }

    public class VideoDownloaderSettings
    {
        public string DefaultVideoQuality { get; set; } = "1080p";
        public bool AudioOnlyDefault { get; set; } = false;
        public string DownloadPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "PowerToys-Downloader");
        public string VideoFormat { get; set; } = "mp4";
        public string AudioFormat { get; set; } = "mp3";
        public int AudioQuality { get; set; } = 0; // 0 = best quality
        public bool EmbedSubtitles { get; set; } = true;
        public bool EmbedMetadata { get; set; } = true;
        public bool ShowDownloadWindow { get; set; } = false;
        public bool HandleDuplicateFilenames { get; set; } = true;
        public string CustomFilenameTemplate { get; set; } = "";
    }
}