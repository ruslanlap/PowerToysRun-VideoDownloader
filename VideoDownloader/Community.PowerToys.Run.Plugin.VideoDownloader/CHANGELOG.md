# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

## [1.0.6] - 2024-06-11
### 🐛 Fixed
- **Settings Save Bug:** Fixed issue where settings for video/audio quality and format were not saved unless the download folder was changed ([#4](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/issues/4)). Now all fields are reliably saved.
- **Quality Selection:** Fixed issue where plugin was auto-downloading lowest resolution and not accepting quality parameters ([#3](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/issues/3)).
- **Settings UI:** Fixed issue where full list of settings was not available in plugin settings ([#5](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/issues/5)).
- **Download Location:** Added ability to change download location through plugin settings ([#8](https://github.com/ruslanlap/PowerToysRun-VideoDownloader/issues/8)).

### 🔄 Changed
- **Settings UI:** All settings fields (except checkboxes) are now text fields for maximum compatibility with PowerToys settings panel.
- **Input Validation:** Added validation for all text fields to ensure only allowed values are accepted.

### ✨ Added
- **Input validation and user hints** for all settings fields.
- **Tooltips/descriptions** to all text fields with allowed values for easier configuration.

### 📝 Release Notes
This release focuses on improving the user experience and fixing several critical issues:
1. All settings are now properly saved and loaded
2. Quality selection works as expected
3. Full settings UI is available
4. Download location can be customized
5. Better input validation and user guidance

### 🚀 Quick Start
1. Download the ZIP file for your platform (x64 or ARM64)
2. Extract to `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\`
3. Restart PowerToys
4. Start using with `Alt+Space` then type `dl <URL>`

### 🔍 Quick Commands
| Command | Description |
|---------|-------------|
| `dl <URL>` | Download a video in the best quality |


### ⚠️ Important Notes
- This project is created for educational purposes only
- Use of this software for downloading videos is at your own risk and responsibility
- Please respect copyright laws and terms of service of the platforms you download from
- Muxed streams are deprecated by YouTube and may not be available for all videos

---

## [1.0.5] - 2025-06-03
- See previous release notes on GitHub. 