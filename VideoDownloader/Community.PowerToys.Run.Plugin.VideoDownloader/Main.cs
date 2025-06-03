using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using YoutubeExplode;
using System.IO.Abstractions;

namespace Community.PowerToys.Run.Plugin.VideoDownloader
{
    // Define our own interfaces to replace Wox.Plugin
    public interface IPlugin
    {
        string Name { get; }
        string Description { get; }
        void Init(PluginInitContext context);
        List<Result> Query(Query query);
    }

    public interface IReloadable
    {
        void ReloadData();
    }

    public class PluginInitContext
    {
        public string CurrentPluginMetadata { get; set; }
        public Action<string, object> API { get; set; }
    }

    public class Query
    {
        public string Search { get; set; }
        public Query(string search)
        {
            Search = search;
        }
    }

    public class Result
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string IconPath { get; set; }
        public Action Action { get; set; }
    }

    public class Main : IPlugin, IReloadable, IDisposable
    {
        public static string PluginID => "B8F9B9F5C3E44A8B9F1F2E3D4C5B6A7B";
        public string Name => "VideoDownloader";
        public string Description => "Download videos from YouTube and other websites using yt-dlp";

        private PluginInitContext _context;
        private string _iconPathDark;
        private string _iconPathLight;
        private bool _disposed;
        private readonly ILogger<Main> _logger;
        private readonly YoutubeClient _youtubeClient;

        // Settings
        private string _downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        private string _audioFormat = "mp3";
        private string _videoFormat = "mp4";

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            // Check if yt-dlp is available on first use
            if (!IsYtDlpAvailable())
            {
                results.Add(new Result
                {
                    Title = "📥 Setup Video Downloader",
                    SubTitle = "First-time setup: Download yt-dlp.exe",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        SetupYtDlp();
                        return true;
                    }
                });

                if (!string.IsNullOrEmpty(query.Search))
                {
                    results.Add(new Result
                    {
                        Title = "⏳ Setting up downloader...",
                        SubTitle = "Click 'Setup Video Downloader' above first",
                        IcoPath = GetIconPath(),
                        Action = context => false
                    });
                }
                return results;
            }

            if (string.IsNullOrEmpty(query.Search))
            {
                results.Add(new Result
                {
                    Title = "Video Downloader",
                    SubTitle = "Type a URL to download video (e.g., dl https://youtube.com/...)",
                    IcoPath = GetIconPath(),
                    Action = context => false
                });

                results.Add(new Result
                {
                    Title = "📁  Open Download Folder",
                    SubTitle = $"Current: {_downloadPath}",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        OpenDownloadFolder();
                        return true;
                    }
                });

                results.Add(new Result
                {
                    Title = "⚙️  Change Download Folder",
                    SubTitle = "Click to select a new download location",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        ChangeDownloadFolder();
                        return true;
                    }
                });

                return results;
            }

            var searchTerms = query.Search.Trim();

            // Check if it's a URL
            if (IsValidUrl(searchTerms))
            {
                results.Add(new Result
                {
                    Title = $"🎥  Download Video: {GetDomainFromUrl(searchTerms)}",
                    SubTitle = $"Download to: {_downloadPath} | Format: {_videoFormat}",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        DownloadVideo(searchTerms);
                        return true;
                    }
                });

                results.Add(new Result
                {
                    Title = "🎵  Download Audio Only",
                    SubTitle = $"Extract audio to: {_downloadPath} | Format: {_audioFormat}",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        DownloadAudio(searchTerms);
                        return true;
                    }
                });

                results.Add(new Result
                {
                    Title = "📥  Download & Open Folder",
                    SubTitle = $"Download video and open {_downloadPath}",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        DownloadVideoAndOpenFolder(searchTerms);
                        return true;
                    }
                });

                results.Add(new Result
                {
                    Title = "🎧  Download Audio & Open Folder", 
                    SubTitle = $"Download audio and open {_downloadPath}",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        DownloadAudioAndOpenFolder(searchTerms);
                        return true;
                    }
                });

                results.Add(new Result
                {
                    Title = "⚙️  Choose Quality",
                    SubTitle = "Select video quality before download",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        ShowQualityOptions(searchTerms);
                        return true;
                    }
                });

                results.Add(new Result
                {
                    Title = "📁  Open Download Folder",
                    SubTitle = $"Open {_downloadPath}",
                    IcoPath = GetIconPath(),
                    Action = context =>
                    {
                        OpenDownloadFolder();
                        return true;
                    }
                });
            }
            else
            {
                results.Add(new Result
                {
                    Title = "❌  Invalid URL",
                    SubTitle = "Please provide a valid video URL (YouTube, Vimeo, etc.)",
                    IcoPath = GetIconPath(),
                    Action = context => false
                });
            }

            return results;
        }

        private string GetIconPath()
        {
            // Use dark icon by default, fallback to light if dark doesn't exist
            if (!string.IsNullOrEmpty(_iconPathDark) && File.Exists(_iconPathDark))
                return _iconPathDark;

            if (!string.IsNullOrEmpty(_iconPathLight) && File.Exists(_iconPathLight))
                return _iconPathLight;

            return _iconPathDark; // Return dark path even if file doesn't exist
        }

        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        private string GetDomainFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return "Unknown";
            }
        }

        private bool IsYtDlpAvailable()
        {
            var ytDlpPath = GetYtDlpExecutablePath();
            return File.Exists(ytDlpPath) && new FileInfo(ytDlpPath).Length > 1000000; // At least 1MB
        }

        private void SetupYtDlp()
        {
            Task.Run(() =>
            {
                var pluginDir = _context.CurrentPluginMetadata.PluginDirectory;
                var ytDlpPath = Path.Combine(pluginDir, "yt-dlp.exe");
                var escapedPath = ytDlpPath.Replace("'", "''"); // Escape for PowerShell

                try
                {
                    // Show progress window with properly escaped paths
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c echo Setting up Video Downloader... && echo. && echo Downloading yt-dlp.exe... && powershell -WindowStyle Normal -Command \"" +
                                   $"try {{ " +
                                   $"Write-Host 'Downloading from GitHub...' -ForegroundColor Green; " +
                                   $"Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile '{escapedPath}' -UseBasicParsing; " +
                                   $"if (Test-Path '{escapedPath}') {{ " +
                                   $"$size = [Math]::Round((Get-Item '{escapedPath}').Length / 1MB, 2); " +
                                   $"Write-Host 'Success! Downloaded yt-dlp.exe ($size MB)' -ForegroundColor Green; " +
                                   $"Write-Host 'Video Downloader is ready to use!' -ForegroundColor Cyan; " +
                                   $"}} else {{ throw 'Download failed' }} " +
                                   $"}} catch {{ " +
                                   $"Write-Host 'Download failed. Please check internet connection.' -ForegroundColor Red; " +
                                   $"Write-Host 'Manual download: https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -ForegroundColor Yellow " +
                                   $"}}; " +
                                   $"Write-Host 'Press any key to continue...'; " +
                                   $"$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')\"",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = pluginDir
                    };

                    Process.Start(startInfo);
                }
                catch
                {
                    // Fallback: create a simple batch file for download
                    var batchPath = Path.Combine(pluginDir, "setup.bat");
                    var batchContent = $@"@echo off
echo Setting up Video Downloader...
echo.
echo Downloading yt-dlp.exe...
powershell -Command ""Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile '{escapedPath}' -UseBasicParsing""
if exist ""{ytDlpPath}"" (
    echo Success! Video Downloader is ready.
) else (
    echo Download failed. Check internet connection.
    echo Manual download: https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe
)
pause
del ""%~f0""
";
                    File.WriteAllText(batchPath, batchContent);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = batchPath,
                        UseShellExecute = true,
                        WorkingDirectory = pluginDir
                    });
                }
            });
        }

        private void DownloadVideo(string url)
        {
            Task.Run(() => {
                RunYtDlpCommand($"-f best -o \"{Path.Combine(_downloadPath, "%(title)s.%(ext)s")}\" \"{url}\"");
                // Show notification with option to open folder
                ShowDownloadCompleteNotification("video");
            });
        }

        private void DownloadAudio(string url)
        {
            Task.Run(() => {
                RunYtDlpCommand($"-x --audio-format {_audioFormat} -o \"{Path.Combine(_downloadPath, "%(title)s.%(ext)s")}\" \"{url}\"");
                // Show notification with option to open folder
                ShowDownloadCompleteNotification("audio");
            });
        }

        private void DownloadVideoAndOpenFolder(string url)
        {
            Task.Run(() => {
                RunYtDlpCommand($"-f best -o \"{Path.Combine(_downloadPath, "%(title)s.%(ext)s")}\" \"{url}\"");
                // Wait a bit for download to start, then open folder
                Task.Delay(2000).ContinueWith(_ => OpenDownloadFolder());
            });
        }

        private void DownloadAudioAndOpenFolder(string url)
        {
            Task.Run(() => {
                RunYtDlpCommand($"-x --audio-format {_audioFormat} -o \"{Path.Combine(_downloadPath, "%(title)s.%(ext)s")}\" \"{url}\"");
                // Wait a bit for download to start, then open folder
                Task.Delay(2000).ContinueWith(_ => OpenDownloadFolder());
            });
        }

        private void ShowDownloadCompleteNotification(string type)
        {
            // Create a simple notification window that opens folder when clicked
            Task.Delay(3000).ContinueWith(_ => {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c echo {type} download started! && echo Press any key to open download folder... && pause && explorer \"{_downloadPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                try
                {
                    Process.Start(startInfo);
                }
                catch { /* Ignore errors */ }
            });
        }

        private void ShowQualityOptions(string url)
        {
            Task.Run(() => RunYtDlpCommand($"-F \"{url}\""));
        }

        private void ChangeDownloadFolder()
        {
            Task.Run(() =>
            {
                try
                {
                    // Open folder selection dialog using PowerShell
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-WindowStyle Hidden -Command \"" +
                                   $"Add-Type -AssemblyName System.Windows.Forms; " +
                                   $"$folder = New-Object System.Windows.Forms.FolderBrowserDialog; " +
                                   $"$folder.Description = 'Select Download Folder'; " +
                                   $"$folder.SelectedPath = '{_downloadPath.Replace("'", "''")}'; " +
                                   $"if ($folder.ShowDialog() -eq 'OK') {{ " +
                                   $"Write-Host $folder.SelectedPath " +
                                   $"}}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
                        {
                            _downloadPath = output;

                            // Show confirmation
                            var confirmInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/c echo Download folder changed to: && echo {_downloadPath} && pause",
                                UseShellExecute = true,
                                CreateNoWindow = false
                            };
                            Process.Start(confirmInfo);
                        }
                    }
                }
                catch
                {
                    // Fallback: just open current folder
                    OpenDownloadFolder();
                }
            });
        }

        private void OpenDownloadFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _downloadPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Fallback: use explorer
                try
                {
                    Process.Start("explorer.exe", $"\"{_downloadPath}\"");
                }
                catch { /* Ignore errors */ }
            }
        }

        private void RunYtDlpCommand(string arguments)
        {
            var ytDlpPath = GetYtDlpExecutablePath();

            try
            {
                // Try direct execution first
                var startInfo = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = _downloadPath
                };

                Process.Start(startInfo);
            }
            catch
            {
                // Fallback to cmd if direct execution fails
                try
                {
                    var quotedPath = $"\"{ytDlpPath}\"";
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe", 
                        Arguments = $"/k {quotedPath} {arguments}",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = _downloadPath
                    };

                    Process.Start(startInfo);
                }
                catch
                {
                    ShowInstallationDialog();
                }
            }
        }

        private string GetYtDlpExecutablePath()
        {
            // Priority order: plugin directory > PATH > system locations
            var pluginDir = _context.CurrentPluginMetadata.PluginDirectory;
            var localYtDlp = Path.Combine(pluginDir, "yt-dlp.exe");

            // 1. Check plugin directory first
            if (File.Exists(localYtDlp))
                return localYtDlp;

            // 2. Check if yt-dlp is in PATH (pip installation)
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                });

                if (process != null)
                {
                    process.WaitForExit(3000);
                    if (process.ExitCode == 0)
                        return "yt-dlp";
                }
            }
            catch { }

            // 3. Check common system locations
            var systemPaths = new[]
            {
                @"C:\yt-dlp\yt-dlp.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yt-dlp", "yt-dlp.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "yt-dlp", "yt-dlp.exe")
            };

            foreach (var path in systemPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // 4. Default to plugin directory (will be downloaded)
            return localYtDlp;
        }

        private void ShowInstallationDialog()
        {
            var pluginDir = _context.CurrentPluginMetadata.PluginDirectory;
            var ytDlpPath = Path.Combine(pluginDir, "yt-dlp.exe");

            if (!File.Exists(ytDlpPath))
            {
                var escapedPath = ytDlpPath.Replace("'", "''"); // Escape single quotes for PowerShell

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c echo Downloading yt-dlp.exe... && powershell -Command \"Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile '{escapedPath}' -UseBasicParsing\" && echo Download complete! && pause",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = pluginDir
                };

                Process.Start(startInfo);
            }
            else
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c echo yt-dlp.exe found but failed to run. Check if Windows Defender is blocking it. && pause",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Process.Start(startInfo);
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // Set up icon paths for both themes
            _iconPathDark = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images", "videodownloader.dark.png");
            _iconPathLight = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Images", "videodownloader.light.png");

            // Ensure download directory exists
            if (!Directory.Exists(_downloadPath))
            {
                try
                {
                    Directory.CreateDirectory(_downloadPath);
                }
                catch
                {
                    // Fallback to Downloads folder if MyVideos doesn't work
                    _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                }
            }
        }

        public void ReloadData()
        {
            // Reload plugin data if needed
            if (_context != null)
            {
                // Refresh settings or reinitialize if needed
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposed = true;
            }
        }
    }
}