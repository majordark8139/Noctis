<div align="center">

<h1>
  <img src="src/Noctis/Assets/Icons/Noctis.ico" width="48" align="absmiddle" />&nbsp;Noctis
</h1>

**A music player that respects what's yours. Zero tracking, total control.**

[![Discord](https://img.shields.io/badge/DISCORD-JOIN%20SERVER-5865F2?style=for-the-badge&logo=discord&logoColor=white&labelColor=4F4F4F)](https://discord.gg/BNCDZQUVx7) &nbsp; [![Upstream](https://img.shields.io/badge/UPSTREAM-heartached%2FNoctis-blueviolet?style=for-the-badge)](https://github.com/heartached/Noctis) &nbsp; [![Win7 Release](https://img.shields.io/github/v/release/majordark8139/Noctis?color=E74856&label=WIN7%20EDITION&style=for-the-badge)](https://github.com/majordark8139/Noctis/releases) &nbsp; [![Support Upstream](https://img.shields.io/badge/SUPPORT-BUY%20ME%20A%20COFFEE-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black&labelColor=4F4F4F)](https://buymeacoffee.com/heartached)

[![Platform](https://img.shields.io/badge/Platform-Windows%207%20SP1%20%7C%20Windows%2010%2F11%20%7C%20macOS%20%7C%20Linux-blue.svg?style=for-the-badge)]()

</div>

---

> [!NOTE]
> ### 📢 Fork Disclaimer & Upstream Attribution
> This repository is a dedicated **Windows 7 SP1 (x64) & Bit-Perfect Lossless Edition** of Noctis, built upon **v1.6.7 and forwards**.
>
> - **Windows 7 Compatibility & Audio Upgrades**: This fork incorporates a self-contained .NET 6 backport, SkiaSharp per-pixel Aero Glass transparency rendering, Win32 round-rect border clipping, bundled LibVLC 3.0.21 x64 offline codecs, Deezer TLS 1.2 compatibility, extended interface scaling (down to 50%), and a dedicated **Bit-Perfect WASAPI Exclusive Mode** audio transport with dynamic DAC clock-locking and hardware lock release.
> - **Previous Versions (< 1.6.7) & Upstream Releases**: For all prior versions of Noctis (v1.0.0 through v1.6.6), upstream feature discussions, or official releases targeting Windows 10/11, macOS, and Linux, please visit the original upstream repository: [**heartached/Noctis**](https://github.com/heartached/Noctis).
> - All credit for Noctis's design, original architecture, and UI concept belongs to the original author, [**heartached**](https://github.com/heartached).

---

## Demo

https://github.com/majordark8139/Noctis/releases/download/v1.6.9.2/noctis-demo.mp4

> [!NOTE]
> The demonstration video above is quite laggy due to the overhead of real-time screen recording on low-end hardware. Noctis itself runs noticeably smoother in daily playback and navigation.
>
> **Test Machine Specs:**
> - **CPU**: Intel Core i3-4005U
> - **GPU**: Intel HD Graphics 4400 (64 MB VRAM)
> - **RAM**: 4 GB DDR3
> - **Storage**: Mechanical HDD
> - **Form Factor**: Laptop
> - **OS**: Windows 7 SP1 (64-bit)
>
> *(Direct video downloads: [MP4](https://github.com/majordark8139/Noctis/releases/download/v1.6.9.2/noctis-demo.mp4) · [MKV](https://github.com/majordark8139/Noctis/releases/download/v1.6.9.2/noctis-demo.mkv))*

---

## Screenshots

#### Artist pages

![Artist page](docs/images/screenshot-1.png)

#### Word-by-word karaoke lyrics

![Lyrics page](docs/images/screenshot-2.png)

#### Cover Flow

![Cover Flow](docs/images/screenshot-3.png)

#### Themes & accent colors

![Appearance settings](docs/images/screenshot-4.png)

---

## Install

### 🪟 Windows 7 SP1 (64-bit) — Bit-Perfect Lossless Edition

For **Windows 7 SP1 (x64)**, download the dedicated self-contained portable package from this repository:
- 📦 **[Noctis-v1.6.9.2-Win7-x64.zip](https://github.com/majordark8139/Noctis/releases/download/v1.6.9.2/Noctis-v1.6.9.2-Win7-x64.zip)** (from [Release v1.6.9.2](https://github.com/majordark8139/Noctis/releases/tag/v1.6.9.2))

> [!IMPORTANT]
> **Updating from v1.6.7 or v1.6.8**: The in-app updater in builds v1.6.7 and v1.6.8 cannot self-update due to an upstream installer mismatch (older builds expected an installer `.exe` and fail when downloading `.zip` archives). 
> **Upgrade instructions for v1.6.7 / v1.6.8:**
> 1. Download `Noctis-v1.6.9-Win7-x64.zip`.
> 2. Extract the contents directly into your existing Noctis folder, replacing/overwriting all files.
> 3. Launch `Noctis.exe` directly from your portable folder.
>
> **In-App Updating for Future Releases (v1.6.9+)**: Starting with **v1.6.9**, a native portable self-update engine is built directly into Noctis. Future updates (e.g., v1.7.0+) will automatically update in-place: clicking **Settings → About → Check for updates → Install & restart** will download the new release `.zip`, stage and extract it natively, cleanly replace files in your existing portable folder, and restart Noctis without any installer, without touching the C: drive, and without creating Start Menu shortcuts.

**Windows 7 Edition Highlights:**
- **Zero prerequisites**: Fully self-contained .NET 6 package — simply extract and run `Noctis.exe` with no .NET runtime or desktop frameworks required.
- **Multichannel (5.1 & 7.1) Surround Sound**: Native surround sound routing directly to your AV receiver or surround soundcard over WASAPI.
- **Native DSD Playback & Batch Conversion**: Hardware playback of `.dsf` and `.dff` files via `DsdPcmDecoder` synchronized directly into WASAPI Exclusive mode.
- **Bundled LibVLC 3.0.21 x64**: Native offline audio decoding with full format coverage (FLAC, ALAC, WAV, APE, Opus, AAC, MP3) without system codec dependencies.
- **SkiaSharp Per-Pixel Transparency**: Full Aero Glass integration with zero black box, white wedge, or window clipping artifacts.
- **Bit-Perfect Audio**: Direct WASAPI Exclusive Mode transport directly to your DAC with auto sample-rate matching (44.1 kHz up to 192 kHz).
- **Dedicated Stop Button**: Halts playback and immediately frees exclusive audio hardware locks for other apps.
- **High-DPI / Custom Scaling**: Expanded UI scaling range down to **50%** (50% to 150%) for customized display resolutions.

---

### 💻 Windows 10 & 11 (Upstream)

Via package manager from upstream:

```powershell
# winget
winget install heartached.Noctis

# Scoop (add the bucket once, then install)
scoop bucket add extras
scoop install extras/noctis
```

The Scoop package keeps its library and settings in Scoop's `persist\noctis\data`
folder, so it starts with its own library rather than an installer copy's.

Or download the installer or portable zip from upstream
[heartached/Noctis latest release](https://github.com/heartached/Noctis/releases/latest).

---

### 🍎 macOS & 🐧 Linux (Upstream)

Download the `.dmg` or AppImage from upstream
[heartached/Noctis latest release](https://github.com/heartached/Noctis/releases/latest).
Both ship self-contained with everything they need.

---

### 🌐 Self-Hosting (`noctis-server`)

`noctis-server` is the same library scanner and OpenSubsonic API without a window, for a NAS, a VPS or Docker. Grab `noctis-server-linux-x64.tar.gz` or `noctis-server-linux-arm64.tar.gz` from the
[upstream release](https://github.com/heartached/Noctis/releases/latest), or build the image:

```sh
docker build -t noctis-server .
docker run -d --name noctis -p 4747:4747 -v /path/to/music:/music:ro -v noctis-data:/data noctis-server
docker exec -it noctis /app/noctis-server user add alice
```

Options and client notes are in [docs/SELF-HOSTING.md](docs/SELF-HOSTING.md).

---

## Features

### Sound

- [x] Plays FLAC, ALAC, WAV, AIFF, APE, WavPack, MP3, AAC, OGG, Opus, WMA, M4A and M4B audiobooks
- [x] Bit-perfect WASAPI Exclusive Mode audio transport directly to DAC hardware with auto sample-rate matching (44.1 kHz to 192 kHz)
- [x] Dedicated hardware Stop button that resets playback and immediately frees exclusive DAC locks for other applications
- [x] Configurable Stop button display toggle in Player Island settings (with optional Exclusive-Mode-only display)
- [x] Parametric EQ with presets, and a saved preset per track if you want one
- [x] Gapless playback, crossfade and AutoMix transitions
- [x] ReplayGain and Sound Check volume leveling
- [x] Automatic BPM and musical key detection
- [x] Track Radio and Autoplay keep the music going when the queue runs out
- [x] Batch converter between formats (ffmpeg)
- [x] Audio CD playback
- [x] Multi-channel upmix, and pitch shift independent of playback speed
- [x] Spectrogram window and an audio visualizer (Bars, Mirror, Wave)

### Library

- [x] Songs, Albums, Artists, Folders and Playlists views
- [x] Albums split into Albums, Singles and EPs
- [x] Smart playlists, favorites, star ratings and play counts
- [x] Full metadata editor for artwork, lyrics and per-track options
- [x] Auto-tagging and cover art search using Deezer, MusicBrainz and Apple Music
- [x] Drag and drop import, watched folders, bulk edits
- [x] Playlist import from Exportify CSV, TuneMyMusic JSON and m3u files
- [x] Duplicate finder and file organizer
- [x] Command palette
- [x] Listening stats with a monthly and yearly Wrap
- [x] Artist pages with Top Songs, About, Similar Artists and fan counts from Deezer
- [x] Playlist and album import from TIDAL and Deezer links
- [x] Send to Folder and Open with Noctis file associations; Add from YouTube as an official plugin
- [x] Explicit content toggle, with an Explicit badge on albums
- [x] Group Artists By with editable separators; drag and drop for playlists and the sidebar
- [x] Rebindable keyboard shortcuts

### Lyrics

- [x] Word-by-word karaoke lyrics, Apple Music style
- [x] Reads plain text, LRC, word-level LRC, TTML and Lyricsfile sidecars
- [x] Auto-fetched from LRCLIB, NetEase, Musixmatch, lrc.red, Unison, Kugou and YouTube Music, cached offline
- [x] Lyrics panel you can keep open next to any page
- [x] Written By credits for the songwriters and producers
- [x] Built-in lyrics editor with `.lrc` export
- [x] Share lyrics as image cards or short clips
- [x] Lyrics Studio: speech-to-lyrics alignment on your own machine with Whisper
- [x] Video or GIF backgrounds on the lyrics page, per song or per album, and music videos in the artwork slot
- [x] Global lyrics offset and lyrics import from file

### Look and feel

- [x] Cover Flow browsing with Carousel, Cascade and Collage layouts
- [x] Now Playing artwork as a CD, a vinyl record or a cassette
- [x] Animated cover art
- [x] Ambient blurred backdrops on the lyrics page
- [x] Themes and accent colors, plus a custom theme editor
- [x] Liquid Glass translucent window mode
- [x] Windows 7 SP1 native Aero Glass title bar with per-pixel Skia transparency
- [x] Extended interface scaling options from 50% to 150% in 10% steps
- [x] Resizable mini player with search, queue, volume and karaoke lyrics (with zero-wedge rounded popups on Win7)
- [x] Kawarp warped background, album pages tinted from the cover, accent color that can follow the album art
- [x] Settings organized into 13 searchable pages

### Connect

- [x] Stream from Jellyfin, Navidrome, Airsonic, Gonic or any Subsonic server
- [x] Built-in Noctis Server: stream your library to Symfonium, substreamer, Feishin or any Subsonic client
- [x] Self-hosted `noctis-server` for a NAS, a VPS or Docker, see [docs/SELF-HOSTING.md](docs/SELF-HOSTING.md)
- [x] Discord Rich Presence
- [x] Scrobble to Last.fm and ListenBrainz
- [x] Web remote so you can control playback from your phone
- [x] Media keys on every platform, plus Windows taskbar controls
- [x] Artist images from Deezer
- [x] Sleep timer, tray icon and launch at login
- [x] Updates itself from GitHub releases
- [x] Song progress on the Windows taskbar

### Languages

- [x] English, Arabic, French, German, Japanese, Korean, Russian, Spanish, Turkish, Simplified Chinese and Traditional Chinese

<p align="center">
  <img src="docs/images/discord.png" width="380" alt="Discord Rich Presence" />
</p>

---

## Plugins

Noctis can be extended three ways, from safest to most powerful:

- **Content packs**: themes, lyrics presets and languages as plain JSON, with no code. They keep working in restricted mode. See [Content packs](docs/PLUGINS.md#content-packs).
- **Local API**: HTTP + JSON on 127.0.0.1 for Stream Deck buttons, OBS overlays and scripts in any language. Nothing runs inside Noctis. See the [Local API reference](docs/LOCAL-API.md).
- **Plugins**: .NET libraries built on the `Noctis.Plugins.Abstractions` SDK, with a `plugin.json` manifest and permissions, installed from a .zip. See the [plugin guide](docs/PLUGINS.md).

Official plugins install in one click from Settings › Plugins: A-B Loop, Add from YouTube, Album Roulette, Album Town, Chords, Lyric Motion, Mixxx and True Shuffle.

Community plugins start off (restricted mode) and ask for approval before they run. Sample plugins, a sample content pack and Local API examples are in [`samples/`](samples/).

---

## Build

### Building Windows 7 SP1 x64 (This Fork)

```bash
git clone https://github.com/majordark8139/Noctis.git
cd Noctis
dotnet publish src/Noctis/Noctis.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ./dist/noctis-win7
```

**Requirements:**
- .NET 6 SDK & .NET 8 SDK
- Windows (or Linux cross-build) host

### Building Upstream (Windows 10/11, macOS, Linux)

```bash
git clone https://github.com/heartached/Noctis.git
cd Noctis
dotnet run --project src/Noctis/Noctis.csproj
```

**Requirements:** .NET 10 SDK
Supported platforms: Windows 10/11 (x64), macOS 12+ (Intel and Apple Silicon), Linux (x64 and ARM64).

### Native dependency: libvlc

The released `.dmg`, AppImage and Windows downloads already carry everything they
need, and the `.deb` pulls libvlc in as a dependency. This matters if you are building
from source or running the Linux `.tar.gz`.

- **Windows:** bundled automatically via NuGet, nothing to install.
- **macOS:** install [VLC](https://www.videolan.org/vlc/), which Noctis loads from
  `/Applications/VLC.app`. Packaged release builds bundle their own copy of VLC
  inside the app, so a downloaded Noctis needs no VLC install.
  ```bash
  brew install --cask vlc
  ```
- **Linux:** install via your package manager. Noctis loads the runtime library
  (`libvlc.so.5`), so the `-dev` package is not needed (it works too). The released
  AppImage bundles libvlc and its plugins, so it runs without this.
  ```bash
  # Debian/Ubuntu
  sudo apt install libvlc5 vlc-plugin-base
  # Fedora
  sudo dnf install vlc-devel
  # Arch
  sudo pacman -S vlc
  ```

### Running a downloaded build (macOS / Linux)

The macOS and Linux artifacts on the upstream [Releases page](https://github.com/heartached/Noctis/releases)
are unsigned self-contained builds.

**macOS**, using the portable zip:
```bash
unzip Noctis-osx-arm64.zip
xattr -dr com.apple.quarantine Noctis.app   # remove Gatekeeper quarantine flag
open Noctis.app
```

**Linux**, using the `.deb` (Debian/Ubuntu-based distros). Noctis then appears in your
app menu and can be pinned to the dock; to update, install the next release's `.deb` the same way:
```bash
sudo apt install ./Noctis-linux-amd64.deb
```

Or the AppImage (needs glibc 2.35 or newer: Ubuntu 22.04, Debian 12 or later):
```bash
chmod +x Noctis-x86_64.AppImage
./Noctis-x86_64.AppImage
```

Or the portable tarball, which extracts without a top-level folder:
```bash
mkdir noctis && tar -xzf Noctis-linux-x64.tar.gz -C noctis
chmod +x noctis/Noctis
./noctis/Noctis
```

### Build the server

```bash
dotnet publish src/Noctis.Server/Noctis.Server.csproj -c Release -r linux-x64 --self-contained
docker build -t noctis-server .
```

---

## Star History

[![Star History Chart](https://star-history.dera.page/svg?repos=heartached/Noctis&type=Date)](https://star-history.dera.page/#heartached/Noctis&Date)

---

## Feedback & Issues

- **For Windows 7 SP1 compatibility, WASAPI Exclusive Mode, or issues specific to this fork:**
  Please open an issue on [majordark8139/Noctis Issues](https://github.com/majordark8139/Noctis/issues).

- **For general upstream features, suggestions, or previous versions (v1.0.0 through v1.6.6):**
  Please visit the original upstream project by heartached at [heartached/Noctis Issues](https://github.com/heartached/Noctis/issues).

---

## License

MIT, see [LICENSE](LICENSE)

Original work Copyright (c) heartached. Windows 7 backport & WASAPI enhancements maintained by majordark8139.

---

> [!WARNING]
> Windows may flag the portable executable as untrusted because it isn't code-signed with an expensive commercial certificate. This is normal for open-source indie software and the app is 100% safe to use.
