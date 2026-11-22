# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.5.0] - 2026-11-23

### Added
- Dark UI design with high-contrast typography, rounded corners, and responsive layout.
- Real-time animated equalizer wave visualizer reacting to master audio output via WASAPI loopback.
- Dynamic audio session discovery and removal using `IAudioSessionNotification` and `IAudioSessionEvents`.
- Floating gaming overlay with smooth fade transitions and repositioning support.
- Global system-wide hotkey (`Ctrl + Shift + M`) using a Win32 message-only window.
- System tray integration with single-click overlay toggle and context menu controls.
- Automatic settings persistence in `%APPDATA%\EZsound\settings.json` with debounced position saves.
- Robust 32-bit and 64-bit process metadata resolution via `QueryFullProcessImageName` fallback.
- Log rotation mechanism in `%APPDATA%\EZsound\debug.log` capped at 1 MB.

### Changed
- Refactored architecture so `MainWindow` and `OverlayWindow` share a single `MainViewModel` instance.
- Untracked build outputs and created standard .NET `.gitignore`.

## [0.4.0] - 2026-11-22

### Added
- Master audio wave visualizer component driven by NAudio WASAPI loopback capture.
- Visualizer enable/disable toggle in application settings.

## [0.3.0] - 2026-11-22

### Added
- Dynamic audio session monitoring via Windows Core Audio `IAudioSessionManager2`.
- Automatic detection of new audio streams without requiring application restart.
- Process executable path extraction fallback for elevated applications.

## [0.2.0] - 2026-11-22

### Added
- Floating game overlay window with topmost pinning and coordinate saving.
- Global hotkey registration for overlay toggle (`Ctrl + Shift + M`).
- Windows notification area (tray) icon and background execution support.

## [0.1.0] - 2026-11-22

### Added
- Initial prototype application for Windows per-application volume and mute control via NAudio.
