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

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [DllImport("user32.dll")]
    public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);

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

    /// <summary>
    /// Applies DWM composition frame extension and clips native popup windows to rounded bounds on Windows 7.
    /// Eliminates white square corner wedges outside rounded cards while preserving native floating behavior.
    /// </summary>
    public static void ApplyPopup(TopLevel topLevel, double width, double height, double cornerRadius = 18)
    {
        if (!IsWindows7OrLower) return;
        try
        {
            if (topLevel.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
                return;

            // 1. Extend DWM frame into client area to enable per-pixel alpha transparency in DWM
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // 2. Set window region to physically cut away the 4 rectangular corner gaps
            if (width > 0 && height > 0)
            {
                var scaling = topLevel.RenderScaling;
                var w = (int)Math.Ceiling(width * scaling);
                var h = (int)Math.Ceiling(height * scaling);
                var d = (int)Math.Round(cornerRadius * 2 * scaling);
                var rgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, d, d);
                if (rgn != IntPtr.Zero)
                {
                    SetWindowRgn(hwnd, rgn, true);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Warn(DebugLogger.Category.UI, "Win7AeroGlassBridge", ex.Message);
        }
    }
}
