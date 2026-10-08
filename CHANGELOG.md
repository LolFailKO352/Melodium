# 📋 Melodium – Changelog

All notable changes to **Melodium** are documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/).

---

## [1.8.0] – 2026-10-08

### 🚀 Update Experience & Changelog
- **Scrollable Changelog / Release Notes**:
  - When an update with extensive release notes is available, the changelog is now displayed inside a smoothly scrollable `ScrollViewer` container.
  - Users can easily scroll through long release notes both in the Settings view and directly within the top update notification banner (InfoBar).
  - Enabled text selection (`IsTextSelectionEnabled`) to easily copy text or open links from the release notes.
- **Update Preview in Notification Banner**:
  - The top update notification InfoBar now includes an instant preview of the changelog for the incoming version without having to open an external browser.

### 🛠️ Improvements & Adjustments
- Bumped project version to 1.8.0 across application manifests, WiX installer, and build metadata.

---

## [1.7.0] – 2026-10-05

### 🎧 Session Persistence & Cross-Device Resume
- **Automatic Session Restore**:
  - The app automatically saves the last playing track, position in seconds, and full active queue (including queue index) when closing, quitting, or minimizing to the tray.
  - On restart, the bottom playback bar and queue visually restore immediately to where you left off (without unprompted loud audio playback).
  - Pressing the *Play* button resumes playback seamlessly from the saved position.
- **Cross-Device Playback Resume (from YouTube Music account)**:
  - Similar to the official YouTube Music mobile app and web client, Melodium checks playback history from your Google/YouTube account upon login.
  - If you recently listened to music on a phone or another PC, the home screen displays an elegant Fluent banner showing track thumbnail, title, artist, and a *Resume* button to continue listening with one click.
  - The banner can be dismissed at any time via the close button.

### 🎨 Microsoft Store Fluent UI Home Screen
- **Hero Spotlight Carousel**:
  - Large interactive card featuring dynamic gradient backgrounds, category badges (*Featured*, *Ad-free*), track title, and a *Play Now* button.
  - Smoothly animated pill indicators and `<` / `>` arrow buttons to browse highlighted albums and tracks.
- **Dual Side-by-Side Sections**:
  - Left panel: *Popular Tracks* (quick ranked track list with direct playback).
  - Right panel: *Curated Picks* (compact grid of playlist and radio cards with rich artwork).
- **Microsoft Store-Style Header & Search**:
  - Centered header search bar with keyboard shortcut (Ctrl+E).
  - Profile avatar in the top-right corner providing direct access to account management and library.
  - Enhanced section headers with clickable `>` navigation arrows and mood/genre chips.

### ⚡ Core & Stability Optimizations
- Secure local persistence for player session directly in `LocalApplicationData` settings.
- New API for fetching account history via the Innertube `FEmusic_history` endpoint.

---

## [1.6.0] – 2026-09-20

### ✨ New Features
- **Karaoke Lyrics**:
  - Real-time synchronized lyrics with smooth auto-scrolling synced to the playback timestamp.
  - Multi-provider integration (LRCLIB, Musixmatch, YouTube timed lyrics).
- **System Tray & Background Mode**:
  - Background execution with Windows notification area (systray) icon.
  - Tray context menu with quick playback controls (*Play / Pause*, *Show / Hide*, *Exit*).
  - Setting option to toggle minimize/close to system tray.
- **Discord Rich Presence**:
  - Displays current track, artist, album, and elapsed time in Discord profile status.
- **Automatic Update Checker & Downloader**:
  - Built-in GitHub Releases updater to automatically discover new releases.
  - Direct download and installer launch straight from the application settings.
- **Advanced Localization**:
  - Runtime language switching with preference persistence and dynamic dictionary loading.

### 🛠️ Audio & Performance
- Hybrid audio engine with instant disk caching for 0ms latency on repeated plays.
- Intelligent background prefetching for the next track in the queue.
- Infinite Radio (Autoplay) when reaching the end of the active queue.
