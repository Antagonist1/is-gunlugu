using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace IsGunlugu;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _plain;
    private Icon? _badged;
    private int _count;

    /// <summary>What a click on the last note does; null opens today.</summary>
    private Action? _balloonClick;

    public TrayIcon(Action showToday, Action open, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Bugünün işleri", null, (_, _) => showToday());
        menu.Items.Add("Uygulamayı aç", null, (_, _) => open());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Icon = _plain = LoadIcon(),
            Text = "İş Günlüğü",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) showToday();
        };
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) open();
        };
        _icon.BalloonTipClicked += (_, _) => (_balloonClick ?? showToday)();
    }

    /// <summary>The evening note: a Windows notification; a click on it opens today's tasks.</summary>
    public void ShowReminder(string title, string text, Action? onClick = null)
    {
        _balloonClick = onClick;
        _icon.ShowBalloonTip(10000, title, text, Forms.ToolTipIcon.Info);
    }

    public void ShowHint()
    {
        _balloonClick = null;
        _icon.ShowBalloonTip(
        4000, "İş Günlüğü burada çalışıyor", "Bugünün işleri için simgeye tıkla, uygulamayı açmak için çift tıkla. Kapatmak için sağ tıklayıp Çıkış'ı seç.", Forms.ToolTipIcon.None);
    }

    /// <summary>Today's open work as a number over the icon's lower right, up to 99; none when nothing is left.</summary>
    public void SetOpenCount(int count)
    {
        if (count == _count) return;
        _count = count;
        _icon.Text = count == 0 ? "İş Günlüğü" : $"İş Günlüğü · bugün {count} açık iş";

        var old = _badged;
        _badged = count == 0 ? null : DrawBadge(_plain, count > 99 ? "99" : count.ToString());
        _icon.Icon = _badged ?? _plain;
        Release(old);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        Release(_badged);
    }

    /// <summary>
    /// With open work the tray shows our own calendar icon with the count where the tick is, in a narrow typeface so
    /// the digits come out as large as the tray square allows; two digits fit too.
    /// </summary>
    private static Icon DrawBadge(Icon plain, string text)
    {
        // Drawn large: Windows shrinks it to the tray's size, which on a scaled screen is bigger than 16.
        const int size = 64;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(IconColor(plain)))
            {
                // The app icon's calendar: a rounded body and two tabs on top.
                FillRounded(g, brush, new RectangleF(3, 8, 58, 53), 12);
                FillRounded(g, brush, new RectangleF(14, 1.5f, 8.5f, 14), 4.25f);
                FillRounded(g, brush, new RectangleF(41.5f, 1.5f, 8.5f, 14), 4.25f);
            }

            // The digits as shapes, scaled to fill the body under the tabs with a thin margin.
            using var path = new GraphicsPath();
            using (var family = NarrowFamily())
                path.AddString(text, family, (int)FontStyle.Bold, 100, PointF.Empty, StringFormat.GenericTypographic);
            var bounds = path.GetBounds();
            var room = new RectangleF(9, 19, 46, 36);
            var scale = Math.Min(room.Width / bounds.Width, room.Height / bounds.Height);
            using var place = new Matrix();
            place.Translate(room.X + room.Width / 2, room.Y + room.Height / 2);
            place.Scale(scale, scale);
            place.Translate(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2));
            path.Transform(place);
            g.FillPath(Brushes.White, path);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>The teal of the app icon itself, read from its body, so the two always match.</summary>
    private static Color IconColor(Icon plain)
    {
        using var source = plain.ToBitmap();
        var color = source.GetPixel(source.Width / 8, source.Height * 2 / 3);
        return color.A > 200 ? Color.FromArgb(255, color) : Color.FromArgb(0x16, 0x85, 0x8A);
    }

    /// <summary>Bahnschrift (in Windows 10 and 11) is narrow, so digits grow taller in the same square.</summary>
    private static FontFamily NarrowFamily()
    {
        foreach (var name in new[] { "Bahnschrift SemiBold SemiConden", "Bahnschrift SemiCondensed", "Bahnschrift" })
        {
            try { return new FontFamily(name); }
            catch (ArgumentException) { }
        }
        return new FontFamily("Segoe UI");
    }

    private static void FillRounded(Graphics g, Brush brush, RectangleF box, float radius)
    {
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(box.X, box.Y, d, d, 180, 90);
        path.AddArc(box.Right - d, box.Y, d, d, 270, 90);
        path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
        path.AddArc(box.X, box.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    // Icon.FromHandle does not own the handle, so it is given back here.
    private static void Release(Icon? icon)
    {
        if (icon is null) return;
        var handle = icon.Handle;
        icon.Dispose();
        DestroyIcon(handle);
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static Icon LoadIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(StartupRegistration.ExePath) ?? SystemIcons.Application;
        }
        catch (ArgumentException)
        {
            return SystemIcons.Application;
        }
    }
}
