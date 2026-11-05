# Noctis Windows 7 SP1 Port Checkpoint

## Shift Information
- **Shift Index**: 3 (End of Shift)
- **Total Elapsed Time**: 57 minutes (1-hour block: 17:00 - 17:57 IST)
- **Timestamp**: 2026-11-05 17:57 IST

## Completed in this Block
- **Polyfilled .NET 6 Core & Server APIs**:
  - `ServerCertificate.cs`: Replaced unsupported .NET 8 `X509CertificateLoader` with standard .NET 6 `X509Certificate2` PKCS#12 constructors.
  - `NoctisServer.cs`: Replaced .NET 8 `WebApplication.CreateSlimBuilder` with .NET 6 `WebApplication.CreateBuilder`.
  - `NoctisServer.cs`: Adapted `JsonNode.GetValueKind()` string extraction via `JsonValue.TryGetValue<string>`.
  - `NoctisServer.cs`: Adapted `TextReader.ReadToEndAsync` for .NET 6 signature without cancellation token argument.
  - Polyfilled stream hashing: replaced .NET 7+ `SHA256.HashData(Stream)` and `SHA256.HashDataAsync(Stream)` across `NoctisServer.Upload.cs`, `PluginCatalog.cs`, `PluginHost.cs`, `UpdateService.cs`, and `SettingsViewModel.cs` with standard `SHA256.Create().ComputeHash(stream)`.
  - Polyfilled `Net6StreamExtensions` globally in `namespace System.IO` so all assemblies resolve `ReadExactly` and `ReadAtLeast` seamlessly.
  - Guarded non-Windows `UnixFileMode` across `AutoUpdatePolicy.cs`, `UpdateService.cs`, `YtDlpTool.cs`, and `LocalApiTokenStore.cs`.
  - Verified: `src/Noctis.Core` and `src/Noctis.Core.Server` compile with **0 errors**.

- **Resolved Assembly Type-Unification Mismatch (`CS1705`)**:
  - Diagnosed that Avalonia 12 NuGet packages are compiled strictly against .NET 8 (`System.Runtime 8.0.0.0`), triggering a hard Roslyn compiler error `CS1705` when compiled for `net6.0-windows` and creating fatal runtime loading failures on Windows 7 SP1.
  - Aligned Avalonia to `11.3.18` (the official LTS line providing native `net6.0` runtime binaries). Removed experimental `Directory.Build.targets` assembly injection hacks.
  - Removed Avalonia 12-only package `Avalonia.HarfBuzz` to resolve `NU1605` package downgrade error.
  - Verified: `src/Noctis.Plugins.Abstractions` now compiles with **0 errors**.

## Build & Diagnostic Status
- **GitHub Actions Workflow**: Run [#42026729855](https://github.com/majordark8139/Noctis/actions/runs/42026729855)
- **Restore Step**: Successfully restored all dependencies for `net6.0-windows` on `win-x64` runner.
- **Projects Status**:
  - `Noctis.Plugins.Abstractions`: **0 errors (passed)**
  - `Noctis.Core`: **0 errors (passed)**
  - `Noctis.Core.Server`: **0 errors (passed)**
  - `Noctis.UI`: 6 isolated API differences queued for resolution.

## Git Status
- **Branch**: `feat/win7-lossless-v1.6.7`
- **Head Commit**: `2ea0f8b9` (`fix(deps): remove Avalonia.HarfBuzz (Avalonia 12 only package)`)
- **Remote**: `origin/feat/win7-lossless-v1.6.7` (synchronized)

## Immediate Next Steps Queued Up (Shift 4)
1. Fix the 6 isolated method calls in `src/Noctis.UI/`:
   - `BackdropSnapshot.cs`: replace `PngBitmapEncoderOptions` with Avalonia 11 bitmap export.
   - `BlurredBackdrop.cs`: polyfill `Stopwatch.GetElapsedTime`.
   - `GlassMenuFlyout.cs`: access popup via reflection/helper in Avalonia 11.
   - `PillDialogHost.cs`: adapt `IFocusManager.Focus` call for Avalonia 11.
   - `KaraokeRestingWords.cs`: provide both `dpiScaleX` and `dpiScaleY` to `LayoutHelper.RoundLayoutSizeUp`.
   - `MaskIcon.cs`: adapt `CopyPixels` overload for Avalonia 11.
2. Push to origin and verify clean compilation of `src/Noctis.UI` and `src/Noctis`.
3. Complete Phase 3 artifact packaging (`Noctis-v1.6.7-Win7-x64.zip`) and document system requirements.
