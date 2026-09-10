# Changelog

## [1.3.0] - 2026-09-10

### Added
- **Premiere Pro H.264/AAC compatibility** (#57): Videos downloaded in AV1 or VP9 format are automatically re-encoded to H.264/AAC MP4 after download, making them directly importable in Adobe Premiere Pro without manual conversion.
- New setting **"Transcode AV1/VP9 to H.264 (Premiere Pro)"** (enabled by default) — prefer H.264 sort order during download AND re-encode incompatible files post-download.
- `*.webm` files now included in post-download scan.

### Fixed
- **Data-loss prevention**: transcode now uses atomic `File.Move(overwrite:true)` — the original file is never deleted before the new file is confirmed written (P1-A).
- **Correct file targeting**: transcode writes to a `.transcoding.mp4` temp sibling and moves atomically; eliminates the race where a wrong file could be picked up (P1-B).
- **Visible-mode race**: `RunYtDlpInTerminal` now calls `WaitForExit()` and returns the real exit code instead of returning immediately after `Process.Start` (P1-C).
- **Audio codec check**: transcode is also triggered when audio is not AAC-family (e.g. Opus, Vorbis) even if video is already H.264 (P2-b).
- **MKV → MP4 collision**: respects `PreventFileOverwrites` with auto-incrementing suffix `(1)`, `(2)` … (P1-B).
- `-S sort` flag is now gated on the "Transcode" setting so users who disable it get unmodified yt-dlp format selection (P2-c).

### Tests
- Re-enabled unit test CI step (was disabled in 1.2.6).
- Added 6 new unit tests covering all P1/P2 fixes and regression guards.

---

## [1.2.6] - 2026-09-09

### Changed
- Internal: updated dictionary API; CI test step added.

## [1.2.5] - 2026-09-05

### Changed
- Subtitle download improvements and settings persistence fixes.
