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
        private static VideoInfoWindow _currentInfoWindow = null;

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
                    results.Add(new Result { Title = $"🎵 Download Audio from {domain}", SubTitle = $"Format: {_settings.AudioFormat.ToUpper()}. Safe filename handling enabled.", IcoPath = _iconPath, Action = _ => { DownloadAudio(search); return true; } });
                    results.Add(new Result { Title = $"🎥 Download Video instead", SubTitle = $"Quality: {_settings.DefaultVideoQuality}. Safe filename handling enabled.", IcoPath = _iconPath, Action = _ => { DownloadVideo(search); return true; } });
                }
                else
                {
                    results.Add(new Result { Title = $"🎥 Download Video from {domain}", SubTitle = $"Quality: {_settings.DefaultVideoQuality}. Safe filename handling enabled.", IcoPath = _iconPath, Action = _ => { DownloadVideo(search); return true; } });
                    results.Add(new Result { Title = $"🎵 Download Audio Only", SubTitle = $"Format: {_settings.AudioFormat.ToUpper()}. Safe filename handling enabled.", IcoPath = _iconPath, Action = _ => { DownloadAudio(search); return true; } });
                }

                var qualityOptions = new[] { "1080p", "720p", "480p", "360p", "best" };
                foreach (var quality in qualityOptions.Where(q => q != _settings.DefaultVideoQuality))
                {
                    results.Add(new Result { Title = $"🎬 Download in {quality}", SubTitle = $"Download video in {quality} quality with safe filename handling", IcoPath = _iconPath, Action = _ => { DownloadWithQuality(search, quality); return true; } });
                }

                // Only show video info once - removed duplicate logic
                results.Add(new Result { Title = $"ℹ️ Show Available Formats", SubTitle = "Display all available video/audio formats", IcoPath = _iconPath, Action = _ => { ShowVideoFormats(search); return true; } });
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
                    if (_settings.ShowNotifications && !_settings.MinimalNotifications)
                    {
                        _context.API.ShowMsg("⏳ Starting Download...", "Audio download initiated", _iconPath);
                    }

                    var ffmpegPath = GetFfmpegExecutablePath();
                    var ffmpegDir = Path.GetDirectoryName(ffmpegPath);
                    var outputTemplate = GetSafeOutputTemplate("audio");

                    var commandParts = new List<string>
                    {
                        File.Exists(ffmpegPath) ? "--ffmpeg-location" : "",
                        File.Exists(ffmpegPath) ? $"\"{ffmpegDir}\"" : "",
                        "-f", "bestaudio/best",
                        "-x",
                        "--audio-format", _settings.AudioFormat,
                        "--audio-quality", _settings.AudioQuality.ToString(),
                        "--no-overwrites", // Always prevent overwriting - this fixes filename conflicts
                        "--restrict-filenames", // Use safe ASCII filenames
                        "--windows-filenames", // Ensure Windows compatibility
                        "-o", $"\"{outputTemplate}\""
                    };

                    if (_settings.EmbedMetadata)
                    {
                        commandParts.AddRange(new[] { "--embed-metadata", "--add-metadata" });
                    }

                    commandParts.Add($"\"{url}\"");
                    var command = BuildYtDlpCommand(commandParts);

                    Debug.WriteLine($"Audio download - Command: {command}");
                    
                    // Always show CMD window like v1.05 - this fixes error visibility
                    var success = RunYtDlpCommandVisible(command);
                    
                    Debug.WriteLine($"Audio download - Success: {success}, AutoOpenFolder: {_settings.AutoOpenFolder}");
                    
                    if (success && _settings.AutoOpenFolder)
                    {
                        Debug.WriteLine("Auto-opening folder after successful audio download");
                        Task.Delay(1000).ContinueWith(_ => OpenDownloadFolder()); // Small delay to ensure file is written
                    }
                }
                catch (Exception e)
                {
                    _context.API.ShowMsg("❌ Download Error", e.Message, _iconPath);
                    Debug.WriteLine($"Audio download exception: {e}");
                }
            });
        }

        private void DownloadWithQuality(string url, string quality)
        {
            Task.Run(() =>
            {
                try
                {
                    if (_settings.ShowNotifications && !_settings.MinimalNotifications)
                    {
                        _context.API.ShowMsg("⏳ Starting Download...", $"Video download in {quality} quality initiated", _iconPath);
                    }

                    var format = GetQualityFormat(quality);
                    var ffmpegPath = GetFfmpegExecutablePath();
                    var ffmpegDir = Path.GetDirectoryName(ffmpegPath);
                    var outputTemplate = GetSafeOutputTemplate(quality);

                    var commandParts = new List<string>
                    {
                        File.Exists(ffmpegPath) ? "--ffmpeg-location" : "",
                        File.Exists(ffmpegPath) ? $"\"{ffmpegDir}\"" : "",
                        "-f", $"\"{format}\"",
                        "--merge-output-format", _settings.VideoFormat,
                        "--no-overwrites", // Always prevent overwriting - this fixes filename conflicts
                        "--restrict-filenames", // Use safe ASCII filenames
                        "--windows-filenames", // Ensure Windows compatibility
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
                    
                    Debug.WriteLine($"Video download - Command: {command}");
                    
                    // Always show CMD window like v1.05 - this fixes error visibility  
                    var success = RunYtDlpCommandVisible(command);
                    
                    Debug.WriteLine($"Video download - Success: {success}, AutoOpenFolder: {_settings.AutoOpenFolder}");
                    
                    if (success && _settings.AutoOpenFolder)
                    {
                        Debug.WriteLine("Auto-opening folder after successful video download");
                        Task.Delay(1000).ContinueWith(_ => OpenDownloadFolder()); // Small delay to ensure file is written
                    }
                }
                catch (Exception e)
                {
                    _context.API.ShowMsg("❌ Download Error", e.Message, _iconPath);
                    Debug.WriteLine($"Video download exception: {e}");
                }
            });
        }

        // Renamed from ShowVideoInfo to avoid confusion and simplified
        private void ShowVideoFormats(string url)
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

                    Debug.WriteLine($"Show formats - Command: {command}");
                    
                    // Always show in visible CMD window like v1.05
                    RunYtDlpCommandVisible(command);
                }
                catch (Exception e)
                {
                    _context.API.ShowMsg("❌ Information Error", e.Message, _iconPath);
                    Debug.WriteLine($"Video info exception: {e}");
                }
            });
        }

        private string BuildYtDlpCommand(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private string GetQualityFormat(string quality) => quality.ToLower() switch
        {
            "1080p" => "bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]",
            "720p" => "bestvideo[height<=720][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=720]+bestaudio/best[height<=720]",
            "480p" => "bestvideo[height<=480][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=480]+bestaudio/best[height<=480]",
            "360p" => "bestvideo[height<=360][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=360]+bestaudio/best[height<=360]",
            _ => "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best",
        };

        private string GetSafeOutputTemplate(string quality = "")
        {
            var template = _settings.CustomFilenameTemplate;
            
            if (string.IsNullOrWhiteSpace(template))
            {
                // Enhanced filename template with conflict resolution
                var titlePart = "%(title).100B"; // Shorter title to avoid long paths
                var qualityPart = "";
                var idPart = "";
                var datePart = "";
                
                if (_settings.IncludeQualityInFilename && !string.IsNullOrEmpty(quality) && quality != "audio")
                {
                    qualityPart = "_[%(height)sp]";
                }
                else if (quality == "audio")
                {
                    qualityPart = "_[Audio]";
                }
                
                // Always include video ID to prevent conflicts - this was missing in v1.08
                if (_settings.UseVideoIdInFilename)
                {
                    idPart = "_[%(id)s]";
                }
                else
                {
                    // If user doesn't want video ID, use date+time for uniqueness
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    datePart = $"_{timestamp}";
                }
                
                // Build safe template that prevents conflicts
                template = $"{titlePart}{qualityPart}{idPart}{datePart}.%(ext)s";
            }

            var fullPath = Path.Combine(_settings.DownloadPath, template);
            Debug.WriteLine($"Output template: {fullPath}");
            return fullPath;
        }

        private void OpenDownloadFolder()
        {
            try
            {
                Debug.WriteLine($"OpenDownloadFolder called - Path: {_settings.DownloadPath}");
                
                if (!Directory.Exists(_settings.DownloadPath))
                {
                    Debug.WriteLine("Download directory doesn't exist, creating it");
                    Directory.CreateDirectory(_settings.DownloadPath);
                }
                
                Debug.WriteLine("Starting explorer process");
                var processInfo = new ProcessStartInfo 
                { 
                    FileName = _settings.DownloadPath, 
                    UseShellExecute = true 
                };
                
                var process = Process.Start(processInfo);
                if (process != null)
                {
                    Debug.WriteLine("Successfully started explorer process");
                }
                else
                {
                    Debug.WriteLine("Failed to start explorer process - returned null");
                    // Alternative method using explorer.exe directly
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{_settings.DownloadPath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to open download folder: {e.Message}");
                Debug.WriteLine($"Stack trace: {e.StackTrace}");
                _context.API.ShowMsg("❌ Cannot Open Folder", $"Path: {_settings.DownloadPath}\nError: {e.Message}", _iconPath);
                
                // Try alternative method with explorer.exe
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{_settings.DownloadPath}\"",
                        UseShellExecute = true
                    });
                    Debug.WriteLine("Successfully opened folder using explorer.exe fallback");
                }
                catch (Exception fallbackException)
                {
                    Debug.WriteLine($"Fallback method also failed: {fallbackException.Message}");
                }
            }
        }

        private (bool success, string output, string error) RunYtDlpCommandWithFullOutput(string arguments, bool showWindow = false)
        {
            try
            {
                ProcessStartInfo startInfo;
                if (showWindow)
                {
                    // For visible windows, use cmd.exe to keep window open after completion
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/k \"\"{GetYtDlpExecutablePath()}\" {arguments} && echo. && echo Download completed! Press any key to close... && pause > nul\"",
                        WorkingDirectory = _settings.DownloadPath,
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                }
                else
                {
                    // For hidden processes, direct execution
                    startInfo = new ProcessStartInfo
                    {
                        FileName = GetYtDlpExecutablePath(),
                        Arguments = arguments,
                        WorkingDirectory = _settings.DownloadPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                }

                using var process = Process.Start(startInfo);
                
                if (showWindow)
                {
                    // For visible windows, just wait for completion
                    process?.WaitForExit();
                    var exitCode = process?.ExitCode ?? -1;
                    return (exitCode == 0, "", "");
                }
                else
                {
                    // For hidden processes, capture output
                    var output = process?.StandardOutput.ReadToEnd() ?? "";
                    var error = process?.StandardError.ReadToEnd() ?? "";
                    process?.WaitForExit();
                    
                    var exitCode = process?.ExitCode ?? -1;
                    var success = exitCode == 0;
    
                    return (success, output, error);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"yt-dlp command exception: {e}");
                return (false, "", e.Message);
            }
        }

        // Simplified command execution - always visible like v1.05
        private bool RunYtDlpCommandVisible(string arguments)
        {
            try
            {
                Debug.WriteLine($"Running yt-dlp command: {arguments}");
                
                // Simple, reliable execution like v1.05 - always show window
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"\"{GetYtDlpExecutablePath()}\" {arguments}\"",
                    WorkingDirectory = _settings.DownloadPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    Debug.WriteLine("Successfully started yt-dlp process with visible window");
                    return true;
                }
                else
                {
                    Debug.WriteLine("Failed to start yt-dlp process");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"yt-dlp command exception: {e}");
                _context.API.ShowMsg("❌ Command Failed", $"Failed to run yt-dlp: {e.Message}", _iconPath);
                return false;
            }
        }

        private bool RunYtDlpCommand(string arguments)
        {
            var (success, output, error) = RunYtDlpCommandWithFullOutput(arguments);
            if (!success)
            {
                var errorMsg = GetFriendlyErrorMessage(error);
                _context.API.ShowMsg("❌ Download Failed", errorMsg, _iconPath);
                Debug.WriteLine($"yt-dlp failed: {error}");
            }
            return success;
        }

        private string RunYtDlpCommandWithOutput(string arguments)
        {
            var (success, output, error) = RunYtDlpCommandWithFullOutput(arguments);
            
            if (!success)
            {
                Debug.WriteLine($"yt-dlp command failed: {error}");
                return "";
            }
            
            return output;
        }

        private string GetFriendlyErrorMessage(string errorOutput)
        {
            if (string.IsNullOrEmpty(errorOutput))
                return "Unknown error occurred. Check the command window for details.";

            var error = errorOutput.ToLower();
            
            // Check for common error patterns and provide helpful messages
            if (error.Contains("file exists") || error.Contains("already exists"))
            {
                return "File already exists. Using unique video ID naming should prevent this.";
            }
            else if (error.Contains("network") || error.Contains("timeout") || error.Contains("connection") || error.Contains("http"))
            {
                return "Network error. Check your internet connection and try again.";
            }
            else if (error.Contains("format") || error.Contains("quality") || error.Contains("not available") || error.Contains("no such format"))
            {
                return "Requested format/quality not available. Try 'Video Information' to see available formats.";
            }
            else if (error.Contains("private") || error.Contains("permission") || error.Contains("access") || error.Contains("forbidden"))
            {
                return "Video is private or requires authentication. Cannot download.";
            }
            else if (error.Contains("geo") || error.Contains("location") || error.Contains("region") || error.Contains("blocked"))
            {
                return "Video is geo-blocked in your region or unavailable.";
            }
            else if (error.Contains("live") || error.Contains("stream"))
            {
                return "Live streams require special handling. Try again when stream ends.";
            }
            else if (error.Contains("age") || error.Contains("sign") || error.Contains("login"))
            {
                return "Video requires age verification or login. Cannot download automatically.";
            }
            else if (error.Contains("copyright") || error.Contains("removed") || error.Contains("deleted"))
            {
                return "Video has been removed or is copyright protected.";
            }
            else if (error.Contains("ffmpeg") || error.Contains("postprocess"))
            {
                return "Post-processing error. Video downloaded but format conversion failed.";
            }
            
            // For other errors, show first meaningful line
            var lines = errorOutput.Split('\n');
            var meaningfulLine = lines.FirstOrDefault(line => 
                !string.IsNullOrWhiteSpace(line) && 
                !line.Trim().StartsWith("[") &&
                line.Length > 10
            );
            
            if (!string.IsNullOrEmpty(meaningfulLine))
            {
                return meaningfulLine.Length > 120 ? meaningfulLine.Substring(0, 120) + "..." : meaningfulLine;
            }
            
            return "Download failed. Check the command window for detailed error information.";
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
                        Key = "PreventFileOverwrites",
                        DisplayLabel = "Prevent File Overwrites",
                        DisplayDescription = "Never overwrite existing files - use safe filenames with unique IDs",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.PreventFileOverwrites
                    },
                    new()
                    {
                        Key = "CustomFilenameTemplate",
                        DisplayLabel = "Custom Filename Template",
                        DisplayDescription = "yt-dlp output template (e.g., %(title).200B [%(id)s].%(ext)s). Leave empty for safe default.",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                        TextValue = _settings.CustomFilenameTemplate
                    },
                    new()
                    {
                        Key = "ShowNotifications",
                        DisplayLabel = "Show Download Notifications",
                        DisplayDescription = "Display status notifications during download process",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.ShowNotifications
                    },
                    new()
                    {
                        Key = "MinimalNotifications",
                        DisplayLabel = "Minimal Notifications",
                        DisplayDescription = "Show only essential notifications (start and completion)",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.MinimalNotifications
                    },
                    new()
                    {
                        Key = "IncludeQualityInFilename",
                        DisplayLabel = "Include Quality in Filename",
                        DisplayDescription = "Add quality suffix to filenames (e.g., video_720p.mp4)",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.IncludeQualityInFilename
                    },
                    new()
                    {
                        Key = "UseVideoIdInFilename",
                        DisplayLabel = "Use Video ID in Filename",
                        DisplayDescription = "Include unique video ID to prevent filename conflicts (recommended)",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.UseVideoIdInFilename
                    },
                    new()
                    {
                        Key = "ShowCommandWindow",
                        DisplayLabel = "Show Command Window During Downloads",
                        DisplayDescription = "Display yt-dlp command window to see download progress and errors",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.ShowCommandWindow
                    },
                    new()
                    {
                        Key = "AutoOpenFolder",
                        DisplayLabel = "Auto-Open Download Folder",
                        DisplayDescription = "Automatically open the download folder after successful downloads",
                        PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                        Value = _settings.AutoOpenFolder
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

                // Validation for DefaultVideoQuality
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

                // Validation for VideoFormat
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

                // Validation for AudioFormat
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

                // Validation for AudioQuality
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

                var preventOverwritesOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "PreventFileOverwrites");
                if (preventOverwritesOption != null && preventOverwritesOption.Value != _settings.PreventFileOverwrites)
                {
                    _settings.PreventFileOverwrites = preventOverwritesOption.Value;
                    Debug.WriteLine($"Prevent file overwrites updated to: {_settings.PreventFileOverwrites}");
                }

                var customTemplateOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "CustomFilenameTemplate");
                if (customTemplateOption != null && customTemplateOption.TextValue != _settings.CustomFilenameTemplate)
                {
                    _settings.CustomFilenameTemplate = customTemplateOption.TextValue?.Trim() ?? "";
                    Debug.WriteLine($"Custom filename template updated to: {_settings.CustomFilenameTemplate}");
                }

                var showNotificationsOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ShowNotifications");
                if (showNotificationsOption != null && showNotificationsOption.Value != _settings.ShowNotifications)
                {
                    _settings.ShowNotifications = showNotificationsOption.Value;
                    Debug.WriteLine($"Show notifications updated to: {_settings.ShowNotifications}");
                }

                var minimalNotificationsOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "MinimalNotifications");
                if (minimalNotificationsOption != null && minimalNotificationsOption.Value != _settings.MinimalNotifications)
                {
                    _settings.MinimalNotifications = minimalNotificationsOption.Value;
                    Debug.WriteLine($"Minimal notifications updated to: {_settings.MinimalNotifications}");
                }

                var includeQualityOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "IncludeQualityInFilename");
                if (includeQualityOption != null && includeQualityOption.Value != _settings.IncludeQualityInFilename)
                {
                    _settings.IncludeQualityInFilename = includeQualityOption.Value;
                    Debug.WriteLine($"Include quality in filename updated to: {_settings.IncludeQualityInFilename}");
                }

                var useVideoIdOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "UseVideoIdInFilename");
                if (useVideoIdOption != null && useVideoIdOption.Value != _settings.UseVideoIdInFilename)
                {
                    _settings.UseVideoIdInFilename = useVideoIdOption.Value;
                    Debug.WriteLine($"Use video ID in filename updated to: {_settings.UseVideoIdInFilename}");
                }

                var showCommandWindowOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ShowCommandWindow");
                if (showCommandWindowOption != null && showCommandWindowOption.Value != _settings.ShowCommandWindow)
                {
                    _settings.ShowCommandWindow = showCommandWindowOption.Value;
                    Debug.WriteLine($"Show command window updated to: {_settings.ShowCommandWindow}");
                }

                var autoOpenFolderOption = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "AutoOpenFolder");
                if (autoOpenFolderOption != null && autoOpenFolderOption.Value != _settings.AutoOpenFolder)
                {
                    _settings.AutoOpenFolder = autoOpenFolderOption.Value;
                    Debug.WriteLine($"Auto-open folder updated to: {_settings.AutoOpenFolder}");
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
        public bool PreventFileOverwrites { get; set; } = true; // New setting to prevent overwrites
        public string CustomFilenameTemplate { get; set; } = "";
        public bool ShowNotifications { get; set; } = true;
        public bool MinimalNotifications { get; set; } = false;
        public bool IncludeQualityInFilename { get; set; } = true;
        public bool UseVideoIdInFilename { get; set; } = true; // Use video ID for unique filenames
        public bool ShowCommandWindow { get; set; } = true; // Show command window during downloads
        public bool AutoOpenFolder { get; set; } = true; // Automatically open download folder after successful download
    }
}