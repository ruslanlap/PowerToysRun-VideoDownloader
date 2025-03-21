# PowerToys Run: VideoDownloader Plugin

<div align="center">
  <img src="VideoDownloader/Community.PowerToys.Run.Plugin.VideoDownloader/Images/logo.png" alt="VideoDownloader Logo" width="128" height="128">
  
  <h1>VideoDownloader for PowerToys Run</h1>
  <h3>Download videos from popular platforms directly from PowerToys Run</h3>
  
  ![GitHub release (latest by date)](https://img.shields.io/github/v/release/ruslanlap/PowerToysRun-VideoDownloader)
  ![GitHub issues](https://img.shields.io/github/issues/ruslanlap/PowerToysRun-VideoDownloader)
  ![GitHub pull requests](https://img.shields.io/github/issues-pr/ruslanlap/PowerToysRun-VideoDownloader)
  ![GitHub last commit](https://img.shields.io/github/last-commit/ruslanlap/PowerToysRun-VideoDownloader)
  <br>
  ![Build and Release](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/workflows/Build%20and%20Release/badge.svg)
  ![Maintenance](https://img.shields.io/maintenance/yes/2025)
  ![Code Size](https://img.shields.io/github/languages/code-size/ruslanlap/PowerToysRun-VideoDownloader)
  ![GitHub Repo stars](https://img.shields.io/github/stars/ruslanlap/PowerToysRun-VideoDownloader?style=social)
  <br>
  ![.NET Version](https://img.shields.io/badge/.NET-9.0-512BD4)
  ![C#](https://img.shields.io/badge/C%23-8.0-239120)
  ![PowerToys Compatible](https://img.shields.io/badge/PowerToys-Compatible-blue)
  ![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
  [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
  <br>
  <a href="https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases/latest">
    <img src="https://img.shields.io/badge/⬇️_Download-Latest_Release-blue?style=for-the-badge&logo=github" alt="Download Latest Release">
  </a>
</div>

## 📋 Overview

PowerToysRun-VideoDownloader is a plugin for [Microsoft PowerToys Run](https://github.com/microsoft/PowerToys) that allows you to quickly download videos from various platforms (primarily YouTube) directly from your PowerToys Run interface. Simply type `dl` followed by a video URL to get started.

<div align="center">
  <img src="icon.png" alt="PowerToys" width="200">
</div>

### ✨ Features

- 🎯 Download videos from YouTube (including restricted content)
- 🔄 Support for multiple video quality options
- 💻 Compatible with x64 and ARM64 architectures
- 🚀 Fast downloads using YoutubeExplode and yt-dlp
- 📂 Customizable download location
- 🔔 Notifications for download progress and completion

## 🚀 Installation

### Prerequisites

- [Microsoft PowerToys](https://github.com/microsoft/PowerToys/releases) installed
- Windows 10 version 10.0.22621.0 or higher
- .NET 9.0 Runtime

### Installation Steps

1. Download the latest release for your architecture:
   - [x64 version](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases/latest/download/VideoDownloader-latest-x64.zip)
   - [ARM64 version](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/releases/latest/download/VideoDownloader-latest-arm64.zip)

2. Extract the ZIP file to:
   ```
   %LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\
   ```

3. Restart PowerToys

## 🔧 Usage

1. Open PowerToys Run (default: <kbd>Alt</kbd> + <kbd>Space</kbd>)
2. Type `dl` followed by a space and a video URL:
   ```
   dl https://www.youtube.com/watch?v=dQw4w9WgXcQ
   ```
3. Press <kbd>Enter</kbd> to download with default settings or access additional options

### Advanced Options

Right-click on a result to access additional options:
- Select video quality
- Choose download location
- Copy video information
- Open in browser

## 📁 Data Folder Structure

```
%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\VideoDownloader\
├── Community.PowerToys.Run.Plugin.VideoDownloader.dll
├── YoutubeExplode.dll
├── Images/
│   ├── videodownloader.dark.png
│   └── videodownloader.light.png
└── plugin.json
```

## 🛠️ Building from Source

### Prerequisites

- Visual Studio 2022 or later
- .NET 9.0 SDK

### Build Steps

1. Clone the repository:
   ```
   git clone https://github.com/ruslanlap/PowerToysRun-VideoDownloader.git
   ```

2. Open the solution in Visual Studio

3. Build the solution:
   ```
   dotnet build -c Release
   ```

4. Find the output in the `bin/Release` directory

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgements

- [Microsoft PowerToys](https://github.com/microsoft/PowerToys) team
- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) library
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) project
