using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Noctis.Services;

namespace Noctis.Helpers;

/// <summary>
/// Native Windows 7 Aero Glass Bridge.
/// Extends DWM Aero Glass into the client area via dwmapi.dll on Windows NT 6.1 (Environment.OSVersion.Version.Major &lt; 10),
/// configuring window transparency and dark frosted tinted brushes (#25121212 to #35181818) so the native DWM frosted
/// glass shines through without flat or opaque fallbacks, matching screenshot-4.png.
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
    /// window transparency and dark frosted glass tinted brushes.
    /// </summary>
    public static void Apply(Window window)
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

            // Enable transparency levels
            window.TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.Transparent,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.None
            };
            window.Background = Brushes.Transparent;

            // Apply dark tinted brushes (#25121212 to #35181818) so native DWM frosted glass shines through
            var darkTint = Color.Parse("#28141414");
            var darkBrush = new SolidColorBrush(darkTint);
            window.Resources["AppWindowBackgroundBrush"] = darkBrush;
            window.Resources["AppMainBackground"] = darkBrush;
        }
        catch (Exception ex)
        {
            DebugLogger.Warn(DebugLogger.Category.UI, "Win7AeroGlassBridge", ex.Message);
        }
    }
}
