using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;
using Noctis.Services;

namespace Noctis.Helpers;

/// <summary>
/// Native Windows 7 Aero Glass Bridge.
/// Ensures proper DWM composition and clean, solid rendering on Windows NT 6.1 (Major &lt; 10).
/// Prevents the window background from bleeding through desktop Explorer/wallpaper while
/// allowing Noctis's in-app Liquid Glass panels (sidebar, playback bar island, menus) to frost smoothly.
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
    /// Applies Windows 7 DWM window chrome integration and ensures solid background surfaces.
    /// </summary>
    public static void Apply(Window window, bool glassEnabled = true)
    {
        if (!IsWindows7OrLower) return;

        try
        {
            if (window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
                return;

            // Clear any window-level transparency hints so Avalonia doesn't punch transparent holes to the desktop
            window.ClearValue(Window.TransparencyLevelHintProperty);

            // Clean up any direct resource overrides so theme resources apply cleanly
            window.Resources.Remove("AppWindowBackgroundBrush");
            window.Resources.Remove("AppMainBackground");
        }
        catch (Exception ex)
        {
            DebugLogger.Warn(DebugLogger.Category.UI, "Win7AeroGlassBridge", ex.Message);
        }
    }
}
