namespace IsGunlugu.Core;

/// <summary>
/// Where the main window opens: the wished size if the screen has room, otherwise the whole work area, so the title
/// bar never starts above the screen on a small or zoomed display.
/// </summary>
public sealed class WindowFit
{
    public double Left { get; private set; }
    public double Top { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }
    public double MinWidth { get; private set; }
    public double MinHeight { get; private set; }

    /// <summary>
    /// A monitor's work area as Windows gives it, in pixels, turned into the window's own units: left, top, width,
    /// height. <paramref name="scale"/> is the display scale, 1.25 for 125%.
    /// </summary>
    public static double[] AreaInDips(double left, double top, double width, double height, double scale) =>
        new[] { left / scale, top / scale, width / scale, height / scale };

    /// <summary>
    /// The place the window was left at, on the screen it now opens on: kept as it was when it fits, otherwise made
    /// small enough for this screen and moved inside it, so a window saved on an unplugged monitor is never lost.
    /// </summary>
    public static WindowFit Keep(double left, double top, double width, double height, double minWidth, double minHeight,
        double areaLeft, double areaTop, double areaWidth, double areaHeight)
    {
        var fit = new WindowFit
        {
            MinWidth = Math.Min(minWidth, areaWidth),
            MinHeight = Math.Min(minHeight, areaHeight),
        };
        fit.Width = Math.Min(Math.Max(width, fit.MinWidth), areaWidth);
        fit.Height = Math.Min(Math.Max(height, fit.MinHeight), areaHeight);
        fit.Left = Math.Max(areaLeft, Math.Min(left, areaLeft + areaWidth - fit.Width));
        fit.Top = Math.Max(areaTop, Math.Min(top, areaTop + areaHeight - fit.Height));
        return fit;
    }

    public static WindowFit Place(double width, double height, double minWidth, double minHeight,
        double areaLeft, double areaTop, double areaWidth, double areaHeight)
    {
        var fit = new WindowFit
        {
            MinWidth = Math.Min(minWidth, areaWidth),
            MinHeight = Math.Min(minHeight, areaHeight),
            Width = Math.Min(width, areaWidth),
            Height = Math.Min(height, areaHeight),
        };
        fit.Left = areaLeft + (areaWidth - fit.Width) / 2;
        fit.Top = areaTop + (areaHeight - fit.Height) / 2;
        return fit;
    }
}
