# EZsound 0.5 — User Interface Redesign

**EZsound** is a high-performance Windows Volume Mixer engineered for gaming, content creation, and multitasking workflows. It enables per-application volume adjustment for games, media players, voice communication tools, and system audio without alt-tabbing or opening native Windows settings.

![.NET 9](https://img.shields.io/badge/.NET-9.0-purple) ![Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue) ![WPF](https://img.shields.io/badge/UI-WPF%20%2B%20WinForms-green) ![Version](https://img.shields.io/badge/Version-0.5.0-indigo)

---

## Changelog v0.5: User Interface Redesign

EZsound 0.5 introduces a comprehensive visual overhaul centered on a minimalist, modern desktop software architecture. All user interface components have been rebuilt to match commercial Windows application standards while preserving 100% of the underlying audio engine, real-time session discovery, and overlay functionality.

- **Minimal Dark Palette** — Replaced legacy blue-tinted borders with neutral graphite tones:
  - Surface Background: `#111217`
  - Cards & Panels: `#181A23` (elevated `#202230` on hover)
  - Borders: `#282B3D` / `#303348` (1px hairline)
  - Accents: Royal Lavender (`#8875FF`), Electric Indigo (`#9B83FF`), and Cyan (`#64C8FF`).
- **Custom Sliders** — Rebuilt from scratch:
  - 4px track with an active gradient fill (`#64C8FF` to `#8875FF`).
  - 14px circular thumb with hover illumination and scale feedback.
  - Complete removal of default Windows/WPF slider artifacts.
- **Minimalist Custom Scrollbars** — Replaced standard OS scrollbars with a 5px pill thumb (`#282B3D`, transitioning to `#8875FF` on hover) designed to integrate seamlessly into dark surfaces.
- **Unified Application Cards** — Standardized layout across all audio session cards:
  - 10px border radius with smooth background state transitions.
  - Standardized 28x28px icon containers with rounded backing.
  - Segoe UI Variable typography featuring high-contrast primary text (`#F2F2F7`), muted secondary metadata (`#9295A8`), and monospace percentage readouts (`#64C8FF`).
- **Streamlined Master Audio Wave Visualizer** — Refined geometry featuring thinner wave lines (1.3px primary crest in a cyan-to-purple gradient, 0.7px secondary wave, and low-opacity ribbon fill) aligned with the card grid.
- **Quick Access Hotkey Badge** — Added an interactive pill indicator in the header displaying `Ctrl + Shift + M`.
- **Synchronized Overlay & Main Window Styling** — Unified card hierarchy, control templates, and visual proportions across both the desktop mixer and the floating overlay.

---

## Features

### Audio Session Control (Core Engine)
- **Individual Volume Sliders** — Real-time volume control for every active application via the Windows Core Audio API (`ISimpleAudioVolume`).
- **Individual Mute** — Independent mute/unmute toggles per application.
- **Master Volume Panel** — Dedicated slider and mute control for system-wide output (`IAudioEndpointVolume`).
- **Real-Time Session Detection** — Sub-50ms discovery of newly launched games and applications via `OnSessionCreated` and dedicated `SessionEventClient` listeners.
- **Application Icons** — High-resolution icons extracted directly from running process executables via `ProcessHelper` (`QueryFullProcessImageName` fallback).
- **Zero Audio Duplication** — Shared singleton audio service prevents redundant COM resource allocation and unnecessary CPU overhead.

### Gaming Overlay & Quick Access
- **Floating Gaming Overlay** — Compact (390px wide), borderless, semi-transparent window (96% opacity dark surface `#F7111217`).
- **Adaptive Height** — Dynamically adjusts vertical height based on active audio sessions (up to a 600px maximum with smooth scrolling).
- **Global Hotkey (`Ctrl + Shift + M`)** — Instant overlay toggle from within any application or game via the native Win32 `RegisterHotKey` API.
- **System Tray Integration** — Operates silently in the background with a system tray icon, double-click summon action, and a right-click context menu.
- **Always on Top & Pin Toggle** — Keep the overlay pinned above other windows via the header pin control or tray menu.
- **Window Position Memory** — Persists screen coordinates across sessions with multi-monitor boundary validation.
- **Minimize to Tray on Close** — Closing the window minimizes the application to the system tray; full termination is executed exclusively via the tray "Exit" command.
- **Fade Transitions** — 130ms fade-in and 100ms fade-out animations upon opening and closing.
- **Escape to Hide** — Pressing `Escape` immediately dismisses the overlay.

### Master Audio Wave Visualizer
- **WASAPI Loopback Capture** — Direct system audio loopback capture from the default render endpoint without microphone access or audio recording.
- **60 FPS Hardware-Accelerated Rendering** — Implemented via `DrawingContext` and `StreamGeometry` using pre-allocated frozen pens and brushes. Rendering automatically suspends when the window is hidden or minimized (0% idle CPU/GPU utilization).
- **Dynamic Response** — Maintains a minimal resting waveform during silence and scales dynamically in response to real-time audio transients and volume peaks.

---

## Global Hotkey & Quick Access

| Action | Shortcut / Trigger |
|--------|---------------------|
| **Toggle Gaming Overlay** | `Ctrl + Shift + M` (Global) |
| **Hide Overlay** | `Escape` or Close button |
| **Open from Tray** | Double-click EZsound tray icon |
| **Context Menu** | Right-click tray icon (Open, Toggle Overlay, Always on Top, Wave Visualizer, Exit) |
| **Toggle Wave Visualizer** | Wave toggle button in Header or Tray Menu |

---

## System Requirements

- **Operating System:** Windows 10 or Windows 11 (64-bit)
- **Runtime:** .NET 9.0 Desktop Runtime ([Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/9.0))

---

## Quick Start

### 1. Run Pre-built Executable

The compiled release binary is located at:

```text
d:\EZsound\publish\EZsound.exe
```

Execute `EZsound.exe` to launch the application. The overlay will initialize on screen, and the background service icon will appear in the system tray.

### 2. Build from Source

Requires the .NET 9.0 SDK.

```bash
# Navigate to the repository directory
cd d:\EZsound

# Restore NuGet dependencies
dotnet restore src/EZsound/EZsound.csproj

# Run in Development mode
dotnet run --project src/EZsound/EZsound.csproj
```

### 3. Build Release Package

```bash
# Publish Framework-Dependent Release build
dotnet publish src/EZsound/EZsound.csproj -c Release -r win-x64 --self-contained false -o publish

# Publish Self-Contained Single-File build (no external runtime required)
dotnet publish src/EZsound/EZsound.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone
```

---

## Architecture & Components

```text
src/EZsound/
├── Models/
│   ├── AudioSessionData.cs              # Data model for audio session metadata
│   └── AppSettings.cs                   # JSON configuration persistence (AlwaysOnTop, Visualizer, Position, Hotkey)
├── Services/
│   ├── IAudioService.cs                 # Core Audio WASAPI service interface
│   ├── AudioService.cs                  # NAudio / WASAPI real-time session manager
│   ├── IAudioCaptureService.cs          # Master loopback capture interface
│   ├── AudioCaptureService.cs           # WASAPI Loopback Capture & RMS/Peak processor
│   ├── IHotkeyService.cs                # Global hotkey service interface
│   ├── HotkeyService.cs                 # Win32 RegisterHotKey / Message-only window implementation
│   ├── ITrayIconService.cs              # System tray service interface
│   ├── TrayIconService.cs               # NotifyIcon & context menu controller
│   ├── IWindowManagementService.cs      # Window orchestration interface
│   └── WindowManagementService.cs       # Overlay and main window positioning and animations
├── ViewModels/
│   ├── BaseViewModel.cs                 # INotifyPropertyChanged base implementation
│   ├── RelayCommand.cs                  # ICommand implementation
│   ├── MainViewModel.cs                 # Master session orchestration and loopback lifecycle
│   ├── MasterVolumeViewModel.cs         # Master volume state and commands
│   ├── AudioSessionViewModel.cs         # Per-application volume state and commands
│   └── OverlayViewModel.cs              # Floating overlay view model
├── Views/
│   ├── MainWindow.xaml / .cs            # Full-size desktop mixer window
│   ├── OverlayWindow.xaml / .cs         # Compact floating gaming overlay
│   └── MasterWaveVisualizer.xaml / .cs  # 60 FPS audio waveform visualizer control
├── Helpers/
│   ├── IconHelper.cs                    # Process executable icon extraction and caching
│   └── ProcessHelper.cs                 # Win32 QueryFullProcessImageName process resolver
├── Converters/
│   └── Converters.cs                    # WPF value converters (Visibility, Opacity)
├── Resources/
│   └── Theme.xaml                       # Color palette, custom sliders, scrollbars, and button templates
├── App.xaml / App.xaml.cs               # Application lifecycle, dependency wiring, and exception handling
└── EZsound.csproj                       # .NET 9.0-windows project configuration (WPF + WindowsForms, v0.5.0)
```

---

## Technical Notes & Limitations

### Windowed & Borderless Windowed Modes
The EZsound Floating Overlay is engineered to render over applications running in **Borderless Windowed** (Windowed Fullscreen) or **Standard Windowed** modes. Pressing `Ctrl + Shift + M` summons the overlay, and pressing `Ctrl + Shift + M` or `Escape` dismisses it and returns focus to the active application.

### Exclusive Fullscreen (DirectX / Vulkan)
EZsound operates strictly as a standard desktop application and **does not** utilize DLL injection, graphics API hooking, or process memory modification. Standard Windows GUI windows (including WPF) cannot render above exclusive DirectX/Vulkan fullscreen swapchains without invasive hooks that may trigger anti-cheat systems (such as Easy Anti-Cheat, BattlEye, or Vanguard).

**Recommendation:** Run games in **Borderless Windowed** mode to ensure uninterrupted overlay functionality without impacting game stability or anti-cheat compliance.

---

## License

MIT License