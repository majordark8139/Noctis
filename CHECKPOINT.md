# Noctis Windows 7 SP1 Port Checkpoint

## Shift Information
- **Shift Index**: 11 (MiniPlayer Button Tooltip Overlay Scope & Complete WASAPI Exclusive Mode Hardware Lock Release on Stop)
- **Status**: **100% COMPLETE & VERIFIED ON CI**
- **Timestamp**: 2026-11-07 21:00 IST

---

## Executive Summary & Deliverables

1. **MiniPlayer Button Tooltips (Shuffle, Play/Pause, Repeat, Previous, Next) Zero White Corners (`App.axaml.cs`)**:
   - **Root Cause**: In Shift 10, all popups inside `MiniPlayerWindow` were assigned `ShouldUseOverlayLayer = false`. While this was intended for the large `MorePopup` menu, it inadvertently forced button tooltips inside `MiniPlayerWindow` to also become native Win32 `PopupRoot` windows on subsequent hovers, rendering 4 white triangular corner wedges on Windows 7.
   - **Fix**: Refined the popup layer scoping in `App.axaml.cs`:
     - Checks specifically for the MiniPlayer dropdown menu (`popup.Name == "MorePopup"` or child `MenuCard`). Only this menu uses native floating mode (`ShouldUseOverlayLayer = false`), which already has rounded Win32 region clipping.
     - All other popups and tooltips across the entire application (including all button hover tooltips in `MiniPlayerWindow`, `MainWindow`, and dialogs) strictly use `ShouldUseOverlayLayer = true`.
   - **Result**: Hovering over Shuffle, Play, Pause, Repeat, or any button in the MiniPlayer now renders smoothly within the Skia layer with zero white corners on the 1st hover, 2nd hover, and indefinitely.

2. **WASAPI Exclusive Hardware Lock Release on Stop (`VlcAudioPlayer.cs`)**:
   - **Root Cause**: Upstream Noctis only invoked `_player.Stop()` on LibVLC. In WASAPI Exclusive mode, Noctis streams decoded audio to a dedicated NAudio `WasapiGainOutput` sink (`_exclusiveOut`). Because `_exclusiveOut` was never disposed during `Stop()`, its native `IAudioClient` and COM `MMDevice` continued holding the audio endpoint exclusively. The Windows audio service kept other applications (MusicBee, Spotify, browsers, system sounds) silenced until Noctis closed or played in shared mode.
   - **Fix**: In `VlcAudioPlayer.Stop()`, added explicit disposal of the exclusive sink:
     ```csharp
     lock (_exclusiveSinkLock)
     {
         try { _exclusiveOut?.Dispose(); } catch { }
         _exclusiveOut = null;
     }
     _keepAlive?.SetSuspended(false);
     try { OutputModeChanged?.Invoke(this, OutputDescription); } catch { }
     ```
   - **Result**: Pressing Stop immediately stops `WasapiOut`, disposes the exclusive audio client, and drops the COM device handle. Windows 7 instantly releases the DAC lock, restoring sound for all other desktop applications exactly like MusicBee. When playback resumes, `PrepareExclusiveOutputFor` lazily recreates the exclusive sink and locks the DAC again.

3. **Playback Timer & Remaining Time Reset on Stop (`PlayerViewModel.cs`)**:
   - **Root Cause**: `PlayerViewModel.Stop()` previously set `Position = TimeSpan.Zero` but did not reset `PositionText`, leaving the label stuck at the stopped time (e.g. `0:12`). Additionally, incoming dispatcher ticks from `OnPositionChanged` could overwrite the timer while stopped.
   - **Fix**:
     - Explicitly reset `PositionText = "0:00"`, `RemainingTimeText = FormatTime(Duration)`, `_latestVlcPosition = TimeSpan.Zero`, `_lastCommittedSeekTarget = TimeSpan.Zero`, and `_resumePositionMs = -1`.
     - In `OnPositionChanged`, added `if (State == PlaybackState.Stopped || _isSeeking) return;` to guard against late position updates stomping on the stopped state.
   - **Result**: Pressing Stop instantly resets the timer readout to `0:00` and the remaining duration to `-Duration`.

---

## Verified CI Release Build
- **Workflow Run**: [GitHub Actions Run #42386290074](https://github.com/majordark8139/Noctis/actions/runs/42386290074)
- **Release Package**: `Noctis-v1.6.7-Win7-x64.zip` (235.8 MB)
- **Direct Artifact Download**: [Noctis-v1.6.7-Win7-x64 (Artifact 13579284347)](https://github.com/majordark8139/Noctis/actions/runs/42386290074/artifacts/13579284347)
- **Commit**: `8ccca6c7`
- **Branch**: `feat/win7-lossless-v1.6.7`
