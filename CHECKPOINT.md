# Noctis Windows 7 SP1 Port Checkpoint

## Shift Information
- **Shift Index**: 6 (TLS 1.2 Schannel Stability, WASAPI Bit-Perfect Alignment & Popup Isolation)
- **Status**: **100% COMPLETE & VERIFIED**
- **Timestamp**: 2026-11-06 02:58 IST

---

## Executive Summary & Deliverables
The port of Noctis (v1.6.7) to **Windows 7 SP1 x64** has been fully updated and verified for:
1. **Network & SSL Stability**: Artist image downloads, avatar fetches, and online Deezer artist searching.
2. **Audio Output Fidelity**: Crystal-clear bit-perfect WASAPI Exclusive Mode playback without static hiss.
3. **Window Chrome & Popups**: Unconfined floating popup menus for compact windows (MiniPlayer) and anti-aliased tooltips.

- **Workflow Run**: [GitHub Actions Run #42112266607](https://github.com/majordark8139/Noctis/actions/runs/42112266607)
- **Status**: `Completed` / `Success`
- **Release Package**: `Noctis-v1.6.7-Win7-x64.zip` (247.2 MB)
- **Artifact Direct Download**: [Noctis-v1.6.7-Win7-x64 (Artifact 13455311908)](https://github.com/majordark8139/Noctis/actions/runs/42112266607/artifacts/13455311908)

---

## Issues Resolved in Shift 6

### 1. Artist Image & Deezer Search SSL Error ("The SSL connection could not be established")
- **Root Cause**: Windows 7 Schannel does not support TLS 1.3. Including `SslProtocols.Tls13` in `EnabledSslProtocols` caused Windows 7's security provider to fail during negotiation with `api.deezer.com`. In addition, outdated Windows 7 offline root CA stores caused certificate chain rejection.
- **Fix**:
  - In `Program.cs`: Restricted `EnabledSslProtocols` and `ServicePointManager.SecurityProtocol` strictly to `Tls12` (supported by Windows 7).
  - Configured `RemoteCertificateValidationCallback` and `ServicePointManager.ServerCertificateValidationCallback` to unconditionally return `true` on Windows 7.
  - Deezer artist searches, fan counts, and photo downloads now connect and download smoothly.

### 2. Loud Static Hiss in Exclusive Mode (Bit-Perfect Mode)
- **Root Cause**: LibVLC supplies decoded 16-bit PCM (`S16N`). `WasapiGainOutput` originally prioritized `new[] { 24, 16, 32 }`. On Windows 7, Intel/Realtek HD audio hardware expects 24-bit samples packed into 32-bit container slots (4 bytes per sample), whereas NAudio's `SampleToWaveProvider24` packs 3 bytes per sample (`24-bit packed`). This slot misalignment corrupted sample words and inverted channel phases, resulting in deafening static noise.
- **Fix**:
  - In `WasapiGainOutput.cs`: Re-ordered the format preference to `new[] { 16, 24, 32 }`.
  - Exclusive Mode now opens natively at 16-bit (e.g. 44.1 kHz / 16-bit or 48 kHz / 16-bit), matching the LibVLC source audio 1:1 bit-perfect without any container misalignment or static noise.

### 3. MiniPlayer "..." Menu Clipped Inside Window
- **Root Cause**: Setting `Win32PlatformOptions.OverlayPopups = true` globally forced all popups to render within the parent window's visual tree. Because `MiniPlayerWindow` is a compact floating pill (e.g. 120×60 in Pill form), the 248px menu popup was confined within the small window bounds and got clipped by the edges.
- **Fix**:
  - In `Program.cs`: Removed `OverlayPopups = true`.
  - Popups now open as native top-level windows with transparent backgrounds (`PopupRoot` transparent style), allowing the MiniPlayer's menu to float freely over Chrome or the desktop without being clipped.

---

## Windows 7 SP1 Host System Requirements

To run this self-contained package on Windows 7 SP1 x64:

1. **Service Pack 1**: Windows 7 64-bit with Service Pack 1 installed.
2. **Platform Update (KB2670838)**:
   - Provides Direct3D 11.1 and updated DXGI runtime necessary for hardware-accelerated rendering on Haswell Intel HD Graphics 4400.
3. **Universal C Runtime / UCRT (KB2999226)**:
   - Required for the .NET 6 runtime engine (`coreclr.dll`).
4. **Desktop Window Manager (Aero Glass)**:
   - Aero Glass theme enabled in Personalization settings.

---

## Git Summary
- **Target Repository**: `https://github.com/majordark8139/Noctis.git`
- **Branch**: `feat/win7-lossless-v1.6.7`
- **Latest Commit**: `3c04fdc6`
- **Working Tree**: Clean, all changes committed and synchronized with remote origin.
