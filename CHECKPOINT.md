# Noctis Windows 7 SP1 Port Checkpoint

## Shift Information
- **Shift Index**: 9 (Ghost Dropdowns Fixed, Output Device Recursion Guard, Window-Scoped Overlay Popups & 50% UI Scaling)
- **Status**: **100% COMPLETE & VERIFIED ON CI**
- **Timestamp**: 2026-11-07 16:50 IST

---

## Executive Summary & Deliverables

1. **Ghost Dropdowns Resolved (`ComboBoxDropDownAnimator.cs`)**:
   - **Root Cause**: When clicking dropdowns further down in a `ScrollViewer` (like "Interface Size"), Avalonia's `BringIntoView` mechanism slightly shifted the scroll position to fit the popup on screen. `ComboBoxDropDownAnimator`'s `LayoutHandler` detected `box.TranslatePoint` moving relative to `state.Anchor` and mistook this initial open layout shift for user scrolling, instantly slamming the dropdown closed on the very first frame.
   - **Fix**: Added `state.OpenedTick = Environment.TickCount64`. During the initial 350ms opening phase, `state.Anchor` dynamically synchronizes with any layout/scroll adjustment. Once opened and settled, user scrolling still dismisses the dropdown as intended.
   - **Result**: All dropdowns across the entire application now open smoothly and remain open and responsive.

2. **Output Device Stack Overflow Crash Fixed (`SettingsView.axaml.cs` & `SettingsViewModel.cs`)**:
   - **Root Cause**: `OutputDeviceCombo.DropDownOpened` was synchronously triggering `RefreshOutputDevicesCommand`, which rebuilt `OutputDeviceOptions`. Changing `ItemsSource` while the ComboBox was opening triggered re-entrant `DropDownOpened` cycles, creating an infinite recursive call stack until Windows NT threw `A new guard page for the stack cannot be created`.
   - **Fix**:
     - Added an `isRefreshingOutputDevices` guard in `SettingsView.axaml.cs` and deferred the refresh via `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)` so it never disrupts the ComboBox's active opening transition.
     - In `SettingsViewModel.RebuildOutputDeviceOptions`, checked `OutputDeviceOptions.SequenceEqual(newOptions)` so `ItemsSource` is never replaced if the device list is identical.
   - **Result**: Clicking "Output Device" opens the endpoint list cleanly without errors or crashing.

3. **MiniPlayer Menu Clipping vs Tooltip White Box Borders**:
   - **Root Cause**: `Win32PlatformOptions.OverlayPopups = true` was a blunt global setting that forced all popups across all windows into the window's visual tree. While this solved the white square corners on rounded tooltips in `MainWindow`, it trapped `MiniPlayerWindow`'s 248px menu inside its tiny 160x140px window bounds, clipping it at the edges.
   - **Fix**:
     - Removed `OverlayPopups = true` from `Program.cs`.
     - In `App.axaml.cs`, added a dynamic popup handler:
       - Popups/Tooltips belonging to `MainWindow` and standard dialogs use `ShouldUseOverlayLayer = true` (eliminating Win32 square corners around rounded pill tooltips).
       - Popups belonging to `MiniPlayerWindow` use `ShouldUseOverlayLayer = false` (spawning native floating popups that extend cleanly beyond the mini player's compact bounds without clipping).
   - **Result**: Both features now work together at the same time: no white borders in MainWindow, and no clipping in MiniPlayer.

4. **Extended Interface Size Options (50% to 200%)**:
   - Extended `AppSettings.UiScaleChoices` to: `50%`, `60%`, `70%`, `75%`, `80%`, `85%`, `90%`, `100% (default)`, `110%`, `125%`, `150%`, `175%`, `200%`.
   - Updated test assertions in `UiScaleTests.cs`.

---

## Verified CI Release Build
- **Workflow Run**: [GitHub Actions Run #42367446987](https://github.com/majordark8139/Noctis/actions/runs/42367446987)
- **Release Package**: `Noctis-v1.6.7-Win7-x64.zip` (247.2 MB)
- **Direct Artifact Download**: [Noctis-v1.6.7-Win7-x64 (Artifact 13572391861)](https://github.com/majordark8139/Noctis/actions/runs/42367446987/artifacts/13572391861)
- **Commit**: `b0567d4b`
- **Branch**: `feat/win7-lossless-v1.6.7`
