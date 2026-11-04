# Noctis Windows 7 SP1 Port Checkpoint

## Shift Information
- **Shift Index**: 2
- **Elapsed Time**: ~16 minutes
- **Timestamp**: 2026-11-05 02:43

## Completed in this Block
- **Phase 2: Zero-Regression Windows 7 Engine Refactor**
  - **Framework & Language Retargeting**: Retargeted `src/Noctis/Noctis.csproj`, `src/Noctis.Core/Noctis.Core.csproj`, `src/Noctis.Core.Server/Noctis.Core.Server.csproj`, `src/Noctis.UI/Noctis.UI.csproj`, `src/Noctis.Plugins.Abstractions/Noctis.Plugins.Abstractions.csproj`, `tests/Noctis.Tests/Noctis.Tests.csproj`, and `tools/Noctis.PluginCheck/Noctis.PluginCheck.csproj` to `net6.0-windows` with `<SupportedOSPlatformVersion>7.0</SupportedOSPlatformVersion>` and `<LangVersion>latest</LangVersion>`.
  - **Polyfilling**: Integrated `PolySharp` across projects to polyfill modern C# 10/11/12/13 features (UTF-8 literals, init/required properties, compiler feature attributes).
  - **Deployment Configuration**: Configured self-contained deployment properties (`PublishSingleFile=false`, `IncludeNativeLibrariesForSelfExtract=true`) in `Noctis.csproj`.
  - **Direct3D 11 & ANGLE Hardware Acceleration**: Configured `Win32PlatformOptions` in `src/Noctis/Program.cs` (`BuildAvaloniaApp`) with `DirectX11` and `AngleEgl` rendering modes and `LowLatencyDxgiSwapChain` / `RedirectionSurface` composition modes, tailored for Intel HD 4400 (Haswell GT2) and D3D11-capable GPUs.
  - **Windows 7 Aero Glass Bridge**: Created `src/Noctis/Helpers/Win7AeroGlassBridge.cs` implementing P/Invoke into `dwmapi.dll` (`DwmExtendFrameIntoClientArea`). Hooked into `MainWindow.Opened` and theme/glass change events to extend the DWM frosted glass across the entire client area with `#28141414` dark translucent tinted brushes for 100% visual parity with screenshot-4.png without flat or opaque fallbacks.
  - **UWP/SMTC Isolation**: Guarded Windows 10-specific WinRT references in `SmtcService.cs` under `#if WINDOWS10_0_17763_0_OR_GREATER`, cleanly routing Windows 7 media controls through the native `TaskbarIntegrationService` (`ITaskbarList3` COM interface).
- **Phase 3: GitHub Actions Cloud Compilation**
  - Created `.github/workflows/build-win7.yml` running on `windows-latest` with .NET 6.0.x and 8.0.x SDKs.
  - Builds and publishes self-contained win-x64 release package into `./dist/noctis-win7`.
  - Compresses distribution into `Noctis-v1.6.7-Win7-x64.zip`.
  - Uploads artifact via `actions/upload-artifact@v4`.
- **Phase 4: Dispatch & Report**
  - Pushed `feat/win7-lossless-v1.6.7` branch to `majordark8139/Noctis`.
  - GitHub Actions compilation workflow triggered and executing on `windows-latest`.

## GitHub Actions Compilation & Artifacts
- **Workflow Run**: [Build Windows 7 SP1 x64 #41913989637](https://github.com/majordark8139/Noctis/actions/runs/41913989637)
- **Workflow Artifacts**: [Noctis-v1.6.7-Win7-x64 Artifact Link](https://github.com/majordark8139/Noctis/actions/runs/41913989637/artifacts)

## Windows 7 Host System Requirements
To run Noctis v1.6.7 on Windows 7 SP1 with 100% functional and visual parity:
1. **Operating System**: Windows 7 SP1 x64 (64-bit).
2. **Platform Update (KB2670838)**:
   - Mandatory update providing DirectX 11.1 runtime, DXGI 1.2, and Direct2D components required for Avalonia's Direct3D 11 hardware acceleration on Intel HD 4400.
3. **Universal C Runtime (KB2999226)**:
   - Required by the .NET 6 runtime host, native libVLC media binaries, and SkiaSharp graphic renderers.
4. **Desktop Window Manager (DWM) Aero Glass**:
   - DWM composition must be enabled (stock Windows 7 Aero theme) so `DwmExtendFrameIntoClientArea` can render the real-time blurred frosted glass through Noctis's dark acrylic tint.

## Git Status
- **Branch**: `feat/win7-lossless-v1.6.7`
- **Head Commit**: `3f54c515 feat(win7): port engine to Windows 7 SP1 x64 with native Aero Glass bridge and D3D11 acceleration`
- **Remote**: `origin/feat/win7-lossless-v1.6.7` (synchronized)
