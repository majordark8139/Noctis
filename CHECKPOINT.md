# Noctis Windows 7 SP1 Port Checkpoint

## Shift Information
- **Shift Index**: 10 (MiniPlayer Menu Native Rounded Clipping, Fullscreen Restore Fix, & Dedicated Stop Button with Exclusive Mode Tree Toggle)
- **Status**: **100% COMPLETE & VERIFIED ON CI**
- **Timestamp**: 2026-11-07 19:20 IST

---

## Executive Summary & Deliverables

1. **MiniPlayer Menu Zero White Corners & Zero Clipping (`Win7AeroGlassBridge.cs` & `MiniPlayerWindow.axaml.cs`)**:
   - **Root Cause**: When `MiniPlayerWindow` popups are native Win32 windows (`ShouldUseOverlayLayer = false`), Windows 7 paints default white background pixels into the 4 empty corner gaps outside the `CornerRadius="18"` card. When forced into the overlay layer (`ShouldUseOverlayLayer = true`), the menu is clipped by the mini player's compact 140px height.
   - **Fix**: Kept `MorePopup` as a native floating Win32 popup (zero clipping anywhere on the screen). Added `Win7AeroGlassBridge.ApplyPopup`:
     - Calls `DwmExtendFrameIntoClientArea(hwnd, -1)` to tell Windows 7 DWM to enable per-pixel alpha composition on the popup window.
     - Calls `SetWindowRgn` with `CreateRoundRectRgn(0, 0, w, h, 36, 36)` dynamically on `MenuCard.SizeChanged` and `MorePopup.Opened`.
   - **Result**: The OS physically cuts away the 4 rectangular corner wedges outside the rounded card. There are zero white corners and zero clipping.

2. **Fullscreen -> MiniPlayer -> Windowed Caption Bar Edge Case (`MainWindow.axaml.cs`)**:
   - **Root Cause**: In `ToggleMiniPlayer()`, `_restoreFullScreenAfterMini` was only set for `OperatingSystem.IsMacOS()`. On Windows, when `MainWindow` entered MiniPlayer while in Fullscreen (`F11`), `Hide()` left the window in a borderless fullscreen window style. Upon double-clicking to restore, the window opened in windowed coordinates but lacked its caption buttons and Aero frame until toggled again with `F11`.
   - **Fix**: Expanded the fullscreen check to Windows: if `WindowState == WindowState.FullScreen`, it sets `_restoreFullScreenAfterMini = true` and steps to `WindowState.Normal` before hiding. Upon closing the MiniPlayer, `OnMiniPlayerClosed` automatically posts `ToggleFullScreen()` on Windows as well, seamlessly restoring fullscreen mode.

3. **Dedicated Stop Button for WASAPI Exclusive Mode Release**:
   - **Audio Engine**: Utilized `IAudioPlayer.Stop()` in `VlcAudioPlayer.cs` which halts playback, cleans up active media handles, and releases the WASAPI Exclusive Mode device lock so other system applications and DAC devices are immediately freed.
   - **Player & Transport Bar**:
     - Added `StopIcon` geometry in `Icons.axaml`.
     - Added `StopCommand` to `PlayerViewModel.cs` setting `State = PlaybackState.Stopped` and resetting position.
     - Added `Stop` button to the bottom player island (`PlaybackBarView.axaml`) between `PlayPause` and `Next`.
     - Automatically included in `LyricsView.axaml` (which embeds `PlaybackBarView`).
     - Added `Stop` menu option to `MiniPlayerWindow.axaml` More menu.
     - Dynamically widened `ExtraTransportWidth` in `PlaybackBarView.axaml.cs` by 36px when visible.
   - **Settings Tree Toggle (`SettingsView.axaml` & `SettingsViewModel.cs`)**:
     - Added tree toggle under **Settings → Player → Player Island Buttons**:
       - Parent: **Stop button** (`PlaybackBarShowStopButton`) — unconditionally shows the button everywhere.
       - Child: **Only in Exclusive Mode** (`PlaybackBarStopButtonExclusiveOnly`) — glides open via `controls:CollapsibleContent` when the parent toggle is enabled; restricts the Stop button to show only when WASAPI Exclusive Mode is active/enabled to save UI space.

---

## Verified CI Release Build
- **Workflow Run**: [GitHub Actions Run #42378337664](https://github.com/majordark8139/Noctis/actions/runs/42378337664)
- **Release Package**: `Noctis-v1.6.7-Win7-x64.zip` (235.8 MB)
- **Direct Artifact Download**: [Noctis-v1.6.7-Win7-x64 (Artifact 13576393376)](https://github.com/majordark8139/Noctis/actions/runs/42378337664/artifacts/13576393376)
- **Commit**: `e246c655`
- **Branch**: `feat/win7-lossless-v1.6.7`
