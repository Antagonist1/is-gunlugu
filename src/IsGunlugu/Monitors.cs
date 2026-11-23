using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace IsGunlugu;

/// <summary>
/// The monitors as Windows sees them. The app scales to each monitor on its own (app.manifest), so Windows speaks in
/// real pixels here; a monitor's work area is turned into window units with that monitor's own scale.
/// </summary>
internal static class Monitors
{
    /// <summary>A monitor's work area in its own window units, and its scale (1.25 for 125%).</summary>
    public sealed class Area(double left, double top, double width, double height, double scale)
    {
        public double Left { get; } = left;
        public double Top { get; } = top;
        public double Width { get; } = width;
        public double Height { get; } = height;
        public double Scale { get; } = scale;
    }

    /// <summary>The monitor the pointer is on.</summary>
    public static Area AtPointer() => GetCursorPos(out var p) ? Of(MonitorFromPoint(p, Nearest)) : Primary();

    /// <summary>The monitor holding most of a rectangle given in pixels, or the nearest one when none does.</summary>
    public static Area Holding(double left, double top, double width, double height)
    {
        var r = new Rect32 { Left = (int)left, Top = (int)top, Right = (int)(left + width), Bottom = (int)(top + height) };
        return Of(MonitorFromRect(ref r, Nearest));
    }

    /// <summary>
    /// The scale of the main monitor. Places are saved in its units, as they were before each monitor kept its own,
    /// so a place saved by an older version still opens where it was.
    /// </summary>
    public static double PrimaryScale => Primary().Scale;

    /// <summary>A window's scale where it stands now.</summary>
    public static double ScaleOf(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return handle == IntPtr.Zero ? PrimaryScale : Of(MonitorFromWindow(handle, Nearest)).Scale;
    }

    /// <summary>Where the window's frame stands, in pixels: left, top.</summary>
    public static Point PixelPlace(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return handle != IntPtr.Zero && GetWindowRect(handle, out var r) ? new Point(r.Left, r.Top) : new Point(window.Left, window.Top);
    }

    /// <summary>
    /// Moves a window's top left corner to a place in pixels. A move onto a monitor with another scale makes the
    /// window take that scale (and keep its size in window units); the second move puts the corner back exactly where
    /// it was asked, since the change of scale may shift it.
    /// </summary>
    public static void MoveTo(Window window, double left, double top)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        for (var i = 0; i < 2; i++)
        {
            SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(left), (int)Math.Round(top), 0, 0, NoSize | NoZOrder | NoActivate);
        }
    }

    private static Area Primary() => Of(MonitorFromPoint(new Point32(), Primary1));

    private static Area Of(IntPtr monitor)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            var main = SystemParameters.WorkArea;
            return new Area(main.Left, main.Top, main.Width, main.Height, 1);
        }

        var scale = 1.0;
        try
        {
            if (GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 && dpi > 0) scale = dpi / 96.0;
        }
        catch (Exception)
        {
            // Before Windows 8.1 there is no scale per monitor; every one has the system's.
            scale = SystemScale();
        }

        var w = info.Work;
        return new Area(w.Left / scale, w.Top / scale, (w.Right - w.Left) / scale, (w.Bottom - w.Top) / scale, scale);
    }

    private static double SystemScale()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        return screen > 0 && SystemParameters.PrimaryScreenWidth > 0 ? screen / SystemParameters.PrimaryScreenWidth : 1;
    }

    private const uint Nearest = 2;
    private const uint Primary1 = 1;
    private const uint NoSize = 0x0001;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32 { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32 { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public Rect32 Monitor; public Rect32 Work; public uint Flags; }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point32 point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point32 point, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref Rect32 rect, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect32 rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
}
