# 🎥 PowerToys Run: Video Downloader

<div align="center">
<p align="center">
  <img src="assets/logo.png" width="428" alt="Plugin Logo" alt="logo">
</p>
  <h1>📥 Video Downloader for PowerToys Run</h1>
  <h3>Download videos from YouTube and 1000+ other sites directly from your keyboard</h3>

  <!-- Badges -->
  <a href="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/actions/workflows/build-and-release.yml">
    <img src="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/actions/workflows/build-and-release.yml/badge.svg" alt="Build Status">
  </a>
  <a href="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases/latest">
    <img src="https://img.shields.io/github/v/release/ruslanlap/PowerToysRun-VideoDownloader?label=latest&style=flat-square" alt="Latest Release">
  </a>
  <a href="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases/latest">
    <img src="https://img.shields.io/badge/version-v1.0.5-blue?style=flat-square" alt="Version">
  </a>
  <img src="https://img.shields.io/maintenance/yes/2025?style=flat-square" alt="Maintenance">
  <img src="https://img.shields.io/badge/C%23-.NET%209-512BD4?style=flat-square" alt="C# .NET 9">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2B-0078d7?style=flat-square" alt="Windows 10+">
  <img src="https://img.shields.io/badge/Arch-x64%20%7C%20ARM64-0078d7?style=flat-square" alt="x64 | ARM64">
  <a href="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/stargazers">
    <img src="https://img.shields.io/github/stars/ruslanlap/PowerToysRun-VideoDownloader?style=flat-square" alt="GitHub stars">
  </a>
  <a href="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/issues">
    <img src="https://img.shields.io/github/issues/ruslanlap/PowerToysRun-VideoDownloader?style=flat-square" alt="GitHub issues">
  </a>
  <a href="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases/latest">
    <img src="https://img.shields.io/github/downloads/ruslanlap/PowerToysRun-VideoDownloader/total?style=flat-square" alt="GitHub all releases">
  </a>
  <a href="https://opensource.org/licenses/MIT">
    <img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="License">
  </a>
  <img src="https://img.shields.io/github/last-commit/ruslanlap/PowerToysRun-VideoDownloader?style=flat-square" alt="Last Commit">
  <img src="https://img.shields.io/github/commit-activity/m/ruslanlap/PowerToysRun-VideoDownloader?style=flat-square" alt="Commit Activity">
  <img src="https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square" alt="PRs Welcome">
  <img src="https://img.shields.io/badge/PowerToys-Run%20Plugin-0078d7?style=flat-square" alt="PowerToys Run Plugin">
  <img src="https://img.shields.io/badge/YouTube-Compatible-FF0000?style=flat-square&logo=youtube" alt="YouTube Compatible">
  <a href="https://twitter.com/intent/tweet?text=Check%20out%20this%20awesome%20PowerToys%20Run%20Video%20Downloader%20plugin!&url=https://github.com/ruslanlap/PowerToysRun-VideoDownloader">
    <img src="https://img.shields.io/twitter/url?style=social&url=https%3A%2F%2Fgithub.com%2Fruslanlap%2FPowerToysRun-VideoDownloader" alt="Tweet">
  </a>
</div>

<div align="center">
  <a href="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases/latest">
    <img src="https://img.shields.io/badge/%E2%AC%87%EF%B8%8F_DOWNLOAD_LATEST_RELEASE-blue?style=for-the-badge&logo=github" alt="Download Latest Release">
  </a>
</div>

<div align="center">
  <img src="https://img.shields.io/badge/Supported%20Sites-1000%2B-success?style=flat-square" alt="Supported Sites">
  <img src="https://img.shields.io/badge/Code%20Quality-A-brightgreen?style=flat-square" alt="Code Quality">
  <img src="https://img.shields.io/badge/Windows%2011-Compatible-0078d7?style=flat-square&logo=windows" alt="Windows 11 Compatible">
  <img src="https://img.shields.io/badge/PowerToys-v0.75%2B-0078d7?style=flat-square" alt="PowerToys v0.75+">
</div>

---

## 🌟 Features

- 🚀 **One-Click Downloads** - Download videos with a single command
- 🎥 **Multiple Formats** - Supports both video (MP4) and audio-only (MP3) downloads
- 🔍 **Smart URL Detection** - Automatically recognizes video URLs from various platforms
- ⚡ **Lightning Fast** - Built with performance in mind
- 🎨 **Dark/Light Theme** - Seamlessly integrates with your system theme
- 📂 **Custom Download Folder** - Choose where to save your downloads
- 🛠️ **No Dependencies** - Auto-downloads required components
- 🌍 **1000+ Sites** - Works with YouTube, Vimeo, and many more via yt-dlp

## 🔔 Quick Start

<div style="float: right; margin: 0 0 20px 20px;">
  <img src="assets/logo.png" width="100" alt="Plugin Logo">
</div>

1. Download the latest release from the [Releases page](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases/latest)
2. Extract the ZIP file
3. Copy the extracted folder to `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\`
4. Restart PowerToys
5. Press `Alt+Space` to open PowerToys Run
6. Type `dl [URL]` to download a video URL, and hit Enter!

---

## 🎬 Demo

![Demo](https://raw.githubusercontent.com/ruslanlap/PowerToysRun-VideoDownloader/main/demo.gif)

## 📚 Usage

<div style="display: flex; align-items: center; margin-bottom: 20px;">
  <div style="flex: 2;">
    <h3>Basic Commands</h3>
    <ul>
      <li><code>dl [URL]</code> - Download a video in the best quality</li>
      <li><code>dl --audio [URL]</code> - Download audio only (MP3)</li>
      <li><code>dl --quality [quality] [URL]</code> - Download with specific quality (e.g., <code>--quality 720p</code>)</li>
      <li><code>dl --list-formats [URL]</code> - Show available formats</li>
    </ul>
  </div>
  <div style="flex: 1; text-align: center;">
    <img src="assets/videodownloader.dark.png" width="100" alt="Plugin Icon">
  </div>
</div>

### Examples

```
dl https://www.youtube.com/watch?v=dQw4w9WgXcQ
dl --audio https://www.youtube.com/watch?v=dQw4w9WgXcQ
dl --quality 1080p https://www.youtube.com/watch?v=dQw4w9WgXcQ
```

## ⚙️ Configuration

Access settings through PowerToys Settings → PowerToys Run → Plugin Manager → Video Downloader

### Available Settings:
- **Action Keyword**: Change from default `dl` if desired
- **Default Download Location**: Set your preferred download folder
- **Audio Format**: Choose between MP3, M4A, etc.
- **Video Format**: Choose between MP4, MKV, etc.
- **Auto-Open Folder**: Open download folder after completion

## 🛠️ Building from Source

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/)
- [PowerToys](https://github.com/microsoft/PowerToys)

### Build Steps
```bash
git clone https://github.com/ruslanlap/PowerToysRun-VideoDownloader.git
cd PowerToysRun-VideoDownloader
dotnet restore
dotnet build -c Release
```

The built plugin will be in `bin\Release\net9.0-windows10.0.22621.0`

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guidelines](CONTRIBUTING.md) before submitting a pull request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgements

- [Microsoft PowerToys](https://github.com/microsoft/PowerToys) team
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) for the amazing video downloader
- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) for YouTube support

## 🛠️ Troubleshooting

### Common Issues
- **Plugin not showing up**: Ensure you've extracted to the correct folder and restarted PowerToys
- **Download fails**: Check your internet connection and try again
- **Video not supported**: Some sites may have restrictions
- **yt-dlp not found**: The plugin will automatically download yt-dlp on first use. Make sure you have internet access
- **Slow downloads**: Try a lower quality setting or check your internet connection

### Getting Help
If you encounter any issues, please [open an issue](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/issues) with the following information:
- Video URL you're trying to download
- Command you used (if any)
- Any error messages received
- Screenshots if applicable

---


## 🎨 Assets

<div align="center">
  <h3>Plugin Icons</h3>
  <div style="display: flex; justify-content: center; gap: 20px; margin: 20px 0;">
    <div style="text-align: center;">
      <img src="assets/logo.png" alt="Plugin Logo" width="128">
      <p>Plugin Logo</p>
    </div>
    <div style="text-align: center;">
      <img src="assets/videodownloader.light.png" alt="Light Theme Icon" width="128">
      <p>Light Theme Icon</p>
    </div>
    <div style="text-align: center;">
      <img src="assets/videodownloader.dark.png" alt="Dark Theme Icon" width="128">
      <p>Dark Theme Icon</p>
    </div>
  </div>
  
  <p>All assets are available in the <code>assets/</code> directory of this repository.</p>
</div>

## 📋 Table of Contents
- [📝 Overview](#-overview)
- [✨ Features](#-features)
- [⚡ Quick Start](#-quick-start)
- [🚀 Usage](#-usage)
- [⚙️ Configuration](#️-configuration)
- [🛠️ Building from Source](#️-building-from-source)
- [🤝 Contributing](#-contributing)
- [❓ FAQ](#-faq)
- [📄 License](#-license)
- [🙏 Acknowledgements](#-acknowledgements)
- [🛠️ Troubleshooting](#-troubleshooting)

---

## 📝 Overview

**SpeedTest** is a PowerToys Run plugin that lets you check your internet speed instantly from your keyboard. Just type `spt` in PowerToys Run and launch a test—no browser required!

- **Plugin ID:** `5A0F7ED1D3F24B0A900732839D0E43DB`
- **Action Keyword:** `spt` or change to `speedtest`
- **Platform:** Windows 10/11 (x64, ARM64)
- **Tech:** C#/.NET, WPF, PowerToys Run API

## ✨ Features
- ⚡ One-command internet speed test from PowerToys Run
- 📊 Shows download, upload, ping, server info, and shareable result URL
- 🖼️ Modern WPF UI with real-time progress and results
- 🎨 Theme-aware (dark/light icons, adapts to system theme)
- 📝 Copy/share results instantly
- 🛠️ Robust error handling and informative messages
- 🧪 Automated tests and CI/CD (GitHub Actions)

## 🎬 Demo
<div align="center">
  <img src="SpeedTest/data/demo1.png" width="350" alt="Demo 1">
  <img src="SpeedTest/data/demo2.png" width="350" alt="Demo 2">
  <img src="SpeedTest/data/demo3.png" width="350" alt="Demo 3">
</div>

## ⚡ Easy Install
1. [Download the release (x64)](https://github.com/ruslanlap/PowerToysRun-SpeedTest/releases/download/v1.0.2/SpeedTest-1.0.2-x64.zip)
2. [Download the release (ARM64)](https://github.com/ruslanlap/PowerToysRun-SpeedTest/releases/download/v1.0.2/SpeedTest-1.0.2-ARM64.zip)
3. Extract to `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\`
4. Restart PowerToys
5. Press `Alt+Space`, type `spt`, and hit Enter! (or change to `speedtest` or any other keyword in the settings)

## 🚀 Usage     
- Open PowerToys Run (`Alt+Space`)    
- Type `spt` and select `Run Speed Test`
- View real-time progress and detailed results
- Click the result URL to view/share your result online

## 🖼️ Demo & Screenshots

![Video Downloader in Action](screenshots/demo.gif)

## 🛠️ Building from Source
- Requires .NET 9.0 SDK and Windows 10/11
- Clone the repo and open `VideoDownloader.sln` in Visual Studio
- Build the `Community.PowerToys.Run.Plugin.VideoDownloader` project (x64 or ARM64)
- Output: `VideoDownloader-x64.zip` or `VideoDownloader-arm64.zip` in the `publish` directory

## 📂 Project Structure
```
VideoDownloader/
├── Community.PowerToys.Run.Plugin.VideoDownloader/  # Plugin source code
├── tests/                                          # Unit & integration tests
├── publish/                                        # Build output
├── screenshots/                                    # Demo and documentation assets
└── .github/workflows/                              # CI/CD workflows
```

## 🤝 Contributing
Contributions are welcome! Please read our [Code of Conduct](CODE_OF_CONDUCT.md) and [Contributing Guide](CONTRIBUTING.md) before submitting a pull request.

### Contributors
- [ruslanlap](https://github.com/ruslanlap) - Project creator and maintainer

## ❓ FAQ
<details>
<summary><b>How do I change the download location?</b></summary>
<p>You can set a custom download folder in the plugin settings. Access it through PowerToys Settings → PowerToys Run → Plugin Manager → Video Downloader.</p>
</details>
<details>
<summary><b>Which video platforms are supported?</b></summary>
<p>The plugin supports YouTube, Vimeo, and 1000+ other sites through yt-dlp. See the <a href="https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md" target="_blank">full list of supported sites</a>.</p>
</details>
<details>
<summary><b>Does it work offline?</b></summary>
<p>No, an internet connection is required to download videos.</p>
</details>
<details>
<summary><b>Can I download videos in 4K quality?</b></summary>
<p>Yes, if the source video is available in 4K and the platform allows it. Use the <code>--quality 2160p</code> parameter.</p>
</details>

## 🤔 yt-dlp FAQ

<details>
<summary><b>What are the advantages of using yt-dlp over youtube-dl?</b></summary>
<p>yt-dlp offers additional features and options not available in youtube-dl. It also has an active development community that ensures that bugs are quickly fixed and new features are added.</p>
</details>

<details>
<summary><b>How do I install yt-dlp?</b></summary>
<p>The plugin includes yt-dlp and will automatically download it on first use. For manual installation, you can download the binary from the <a href="https://github.com/yt-dlp/yt-dlp/releases" target="_blank">official releases page</a>.</p>
</details>

<details>
<summary><b>Can I download videos in different formats?</b></summary>
<p>Yes, you can download videos in different formats using yt-dlp. You can specify the format using command-line options or by editing the configuration file.</p>
</details>

<details>
<summary><b>Is it legal to use yt-dlp to download YouTube videos?</b></summary>
<p>Some content on YouTube may be copyrighted, and downloading it without permission may be illegal. Downloading videos from YouTube is against YouTube's Terms of Service. Users are responsible for ensuring they have the right to download and use the content.</p>
</details>

<details>
<summary><b>Can I download entire playlists with yt-dlp?</b></summary>
<p>Yes, yt-dlp lets you download entire playlists by simply pasting the playlist URL. The plugin supports this functionality automatically.</p>
</details>

<details>
<summary><b>Does yt-dlp support subtitles?</b></summary>
<p>Yes, yt-dlp supports subtitles in various formats. The plugin can be configured to automatically download and embed subtitles in your preferred language.</p>
</details>

<details>
<summary><b>Can I download audio-only files?</b></summary>
<p>Yes, use the <code>--audio</code> flag to download audio-only files in MP3 format. For example: <code>dl --audio [URL]</code></p>
</details>

<details>
<summary><b>Is yt-dlp actively maintained?</b></summary>
<p>Yes, yt-dlp is actively maintained by a team of developers who regularly release updates and bug fixes. The plugin automatically checks for yt-dlp updates.</p>
</details>

<details>
<summary><b>Is there a GUI for yt-dlp?</b></summary>
<p>This plugin serves as a GUI for yt-dlp, integrated directly into PowerToys Run. For standalone GUI options, consider:
- <a href="https://github.com/kannagi0303/yt-dlp-gui" target="_blank">yt-dlp-gui</a>
- <a href="https://github.com/oleksis/youtube-dl-gui" target="_blank">youtube-dl-gui</a>
- <a href="https://github.com/kannagi0303/yt-dlp-gui" target="_blank">yt-dlp Web UI</a></p>
</details>

<details>
<summary><b>Can I use yt-dlp on mobile?</b></summary>
<p>This plugin is designed for Windows via PowerToys Run. For mobile use, you'll need to use the command-line version of yt-dlp with a terminal emulator like Termux on Android or a-Shell on iOS.</p>
</details>

## ☕ Support
Enjoying Video Downloader? ☕ Buy me a coffee to support development:

[![Buy me a coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-☕️-FFDD00?style=for-the-badge&logo=buy-me-a-coffee)](https://ruslanlap.github.io/ruslanlap_buymeacoffe/)

## 📄 License
MIT License. See [LICENSE](LICENSE).

## 🙏 Acknowledgements
- [Microsoft PowerToys](https://github.com/microsoft/PowerToys) team
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) - The best YouTube/Video downloader
- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) - YouTube video/playlist parsing
- All contributors and users!

---

## 🛠️ Troubleshooting

- **Plugin not showing up**  
  Make sure you extracted the plugin to the correct folder and restarted PowerToys.
- **Download fails**  
  Check your internet connection and try again. Some videos may have restrictions.
- **yt-dlp not found**  
  The plugin will automatically download yt-dlp on first use. Make sure you have internet access.
- **Slow downloads**  
  Try a lower quality setting or check your internet connection.

---

## 🔒 Security & Privacy

- The plugin does not store your download history.
- All downloads are performed directly by yt-dlp.
- No third-party APIs or data collection beyond what yt-dlp requires.

---

## 🧑‍💻 Tech Stack

- C# / .NET 9.0
- WPF (UI)
- PowerToys Run API
- yt-dlp (video downloading)
- YoutubeExplode (YouTube metadata)
- GitHub Actions (CI/CD)

---

## 📝 Changelog

See the [Releases](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases) page for the latest changes and updates.

---

## 🌐 Localization

<div style="display: flex; align-items: center;">
  <div style="flex: 3;">
    <p>Currently, the plugin UI is in English. Localization support is planned for future releases.</p>
    <p>Contributions for translations are welcome! If you'd like to help translate the plugin to your language, please check the <a href="CONTRIBUTING.md">Contributing Guidelines</a>.</p>
  </div>
  <div style="flex: 1; text-align: center;">
    <img src="assets/logo.png" width="80" alt="Plugin Logo">
  </div>
</div>

---

## 📸 Screenshots & Demo
<div align="center">
  <figure>
    <img src="assets/demo.png" width="600" alt="Video Downloader in Action">
    <figcaption>Downloading videos directly from PowerToys Run</figcaption>
  </figure>
  
  <div style="display: flex; justify-content: center; gap: 20px; margin: 20px 0;">
    <figure>
      <img src="assets/videodownloader.dark.png" width="128" alt="Dark Theme Icon">
      <figcaption>Dark Theme</figcaption>
    </figure>
    <figure>
      <img src="assets/videodownloader.light.png" width="128" alt="Light Theme Icon">
      <figcaption>Light Theme</figcaption>
    </figure>
  </div>
</div>

---

<div align="center">
  <sub>Made with ❤️ by <a href="https://github.com/ruslanlap">ruslanlap</a></sub>
</div>
