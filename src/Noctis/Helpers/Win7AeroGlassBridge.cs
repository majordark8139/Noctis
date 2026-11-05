using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Noctis.Services;

namespace Noctis.Helpers;

/// <summary>
/// Native Windows 7 Aero Glass Bridge.
/// Extends DWM Aero Glass into the client area via dwmapi.dll on Windows NT 6.1 (Environment.OSVersion.Version.Major &lt; 10).
/// Composites a solid-tinted dark ground (84% opacity) so native DWM frosted glass shines through while keeping text
/// and cards crisp and preventing desktop windows/Explorer from bleeding through.
/// </summary>
public static class Win7AeroGlassBridge
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    public static extern int DwmIsCompositionEnabled(out bool pfEnabled);

    /// <summary>
    /// Detects Windows NT 6.1 (or earlier Windows versions where Major &lt; 10).
    /// </summary>
    public static bool IsWindows7OrLower =>
        OperatingSystem.IsWindows() && Environment.OSVersion.Version.Major < 10;

    /// <summary>
    /// Applies the native DWM Aero Glass extension into the client area and configures
    /// window transparency and dark frosted glass tinted brushes based on Liquid Glass state.
    /// </summary>
    public static void Apply(Window window, bool glassEnabled = true)
    {
        if (!IsWindows7OrLower) return;

        try
        {
            if (window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
                return;

            var margins = new MARGINS
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1
            };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            var baseColor = Color.Parse("#1A1A1A");
            if (window.TryFindResource("AppMainBackgroundColor", out var resColor) && resColor is Color c)
                baseColor = c;

            if (glassEnabled)
            {
                // Enable transparency levels
                window.TransparencyLevelHint = new[]
                {
                    WindowTransparencyLevel.Transparent,
                    WindowTransparencyLevel.Blur,
                    WindowTransparencyLevel.None
                };
                window.Background = Brushes.Transparent;

                // Apply solid-tinted dark ground (84% opacity) so the DWM blur shines through
                // without showing desktop Explorer text or wallpapers behind the content.
                var darkTint = Color.FromArgb(0xD6, baseColor.R, baseColor.G, baseColor.B);
                var darkBrush = new SolidColorBrush(darkTint);
                window.Resources["AppWindowBackgroundBrush"] = darkBrush;
                window.Resources["AppMainBackground"] = darkBrush;
            }
            else
            {
                window.ClearValue(Window.TransparencyLevelHintProperty);
                var opaqueBrush = new SolidColorBrush(baseColor);
                window.Background = opaqueBrush;
                window.Resources["AppWindowBackgroundBrush"] = opaqueBrush;
                window.Resources["AppMainBackground"] = opaqueBrush;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Warn(DebugLogger.Category.UI, "Win7AeroGlassBridge", ex.Message);
        }
    }
}
