using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IsGunlugu;

/// <summary>Small drawn pieces shared by the tour and the user guide. Colours follow the theme.</summary>
internal static class GuideParts
{
    public const string TickData = "M 3,8.5 L 6.5,12 L 13,4.5";
    public const string DashData = "M 3.5,8 L 12.5,8";

    private static readonly Regex Markup = new(@"\*\*(.+?)\*\*|\[\[(.+?)\]\]|`(.+?)`|~(.+?)~", RegexOptions.Compiled);
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    public enum State { Open, Partial, Done }

    public static T Paint<T>(this T element, DependencyProperty property, string brush) where T : FrameworkElement
    {
        element.SetResourceReference(property, brush);
        return element;
    }

    /// <summary>A text with **bold**, ~a button's name~, `file text` and [[a key]] inside it.</summary>
    public static TextBlock Text(string markup, double size = 14, string brush = "InkBrush", bool semiBold = false)
    {
        var block = new TextBlock { FontSize = size, TextWrapping = TextWrapping.Wrap, LineHeight = Math.Round(size * 1.5) };
        block.Paint(TextBlock.ForegroundProperty, brush);
        if (semiBold) block.FontWeight = FontWeights.SemiBold;

        var from = 0;
        foreach (Match match in Markup.Matches(markup))
        {
            if (match.Index > from) block.Inlines.Add(new Run(markup.Substring(from, match.Index - from)));
            if (match.Groups[1].Success) block.Inlines.Add(new Run(match.Groups[1].Value) { FontWeight = FontWeights.SemiBold });
            else if (match.Groups[2].Success) block.Inlines.Add(new InlineUIContainer(Key(match.Groups[2].Value)) { BaselineAlignment = BaselineAlignment.Center });
            else if (match.Groups[3].Success) block.Inlines.Add(CodeRun(match.Groups[3].Value, size));
            else block.Inlines.Add(new Run(match.Groups[4].Value) { FontStyle = FontStyles.Italic });
            from = match.Index + match.Length;
        }
        if (from < markup.Length) block.Inlines.Add(new Run(markup.Substring(from)));
        return block;
    }

    private static Run CodeRun(string text, double size)
    {
        var run = new Run(text) { FontFamily = Mono, FontSize = size - 0.5 };
        run.SetResourceReference(TextElement.BackgroundProperty, "SidebarBrush");
        return run;
    }

    public static TextBlock MonoText(string text, double size = 13)
    {
        var block = new TextBlock { Text = text, FontFamily = Mono, FontSize = size };
        return block.Paint(TextBlock.ForegroundProperty, "InkBrush");
    }

    public static Border Key(string name)
    {
        var label = new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeights.SemiBold };
        label.Paint(TextBlock.ForegroundProperty, "InkBrush");
        var key = new Border
        {
            Child = label,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1, 1, 1, 2),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(1, 0, 1, 0),
        };
        key.Paint(Border.BackgroundProperty, "SidebarBrush");
        return key.Paint(Border.BorderBrushProperty, "InputBorderBrush");
    }

    public static Border StatusBox(State state)
    {
        var box = new Border { Width = 17, Height = 17, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1.75) };
        if (state == State.Open)
        {
            box.Paint(Border.BorderBrushProperty, "OpenBrush");
            return box;
        }

        var brush = state == State.Done ? "DoneBrush" : "PartialBrush";
        box.Paint(Border.BackgroundProperty, brush);
        box.Paint(Border.BorderBrushProperty, brush);
        var mark = Stroke(state == State.Done ? TickData : DashData, state == State.Done ? "WindowBrush" : "OnPartialBrush", 2.2);
        mark.Stretch = Stretch.Uniform;
        mark.Margin = new Thickness(1);
        box.Child = mark;
        return box;
    }

    /// <summary>A drawn line, the same way the window's icons are drawn.</summary>
    public static Path Stroke(string data, string brush, double thickness = 1.4)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        return path.Paint(Shape.StrokeProperty, brush);
    }

    public static Path Icon(string key, string brush = "MutedBrush", double thickness = 1.3)
    {
        var path = new Path
        {
            Data = (Geometry)Application.Current.FindResource(key),
            Width = 16,
            Height = 16,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        return path.Paint(Shape.StrokeProperty, brush);
    }

    /// <summary>A row like the ones in the list. Level 1 is a sub-task.</summary>
    public static FrameworkElement MockRow(State state, string text, int level = 0, string? badge = null, bool selected = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(level * 22, 0, 0, 0) };
        var box = StatusBox(state);
        box.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(box);

        var label = new TextBlock
        {
            Text = text,
            FontSize = level == 0 ? 13.5 : 13,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = state != State.Done && level == 0 ? FontWeights.SemiBold : FontWeights.Normal,
        };
        label.Paint(TextBlock.ForegroundProperty, state == State.Done ? "MutedBrush" : "InkBrush");
        row.Children.Add(label);

        if (badge is not null)
        {
            var count = new TextBlock { Text = badge, FontSize = 12, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(count.Paint(TextBlock.ForegroundProperty, "MutedBrush"));
        }

        var holder = new Border { Child = row, Padding = new Thickness(4, 3, 4, 3), CornerRadius = new CornerRadius(4) };
        return selected ? holder.Paint(Border.BackgroundProperty, "SelectedBrush") : holder;
    }

    /// <summary>A rounded label, as the node of a small diagram.</summary>
    public static Border Chip(string text, bool main = false)
    {
        var label = new TextBlock { Text = text, FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
        label.Paint(TextBlock.ForegroundProperty, main ? "OnAccentBrush" : "InkBrush");
        var chip = new Border { Child = label, CornerRadius = new CornerRadius(5), Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(1) };
        chip.Paint(Border.BackgroundProperty, main ? "AccentBrush" : "WindowBrush");
        return chip.Paint(Border.BorderBrushProperty, main ? "AccentBrush" : "InputBorderBrush");
    }

    /// <summary>The window's parts as they sit in a 1180 by 860 window, in percent of its size.</summary>
    /// <summary>
    /// Where the parts sit in the window, in percent, measured from the window at 1180 by 860: the strip on the left
    /// (İşler, Takvim, Ayarlar), the list beside it, the tasks in the middle and the add box under them. Dosyalar is the
    /// file button at the top of the list, where the file list opens.
    /// </summary>
    public static readonly WindowPart[] WindowParts =
    {
        new("FilesPanel", 6.19, 1.71, 18.9, 4.14),
        new("SearchBox", 6.19, 6.82, 18.9, 4.14),
        new("TodayNav", 5.84, 17.17, 19.59, 4.02),
        new("OpenNav", 5.84, 21.44, 19.59, 4.02),
        new("FutureNav", 5.84, 25.7, 19.59, 4.02),
        new("UndatedNav", 5.84, 29.96, 19.59, 4.02),
        new("AllNav", 5.84, 34.23, 19.59, 4.02),
        new("MiniCalendar", 6.19, 47.26, 18.9, 26.31),
        new("TaskGroupButton", 0.7, 6.33, 3.8, 5.85),
        new("CalendarNav", 0.7, 12.67, 3.8, 5.85),
        new("SettingsButton", 0.7, 92.69, 3.8, 5.85),
        new("ContentScroll", 28, 15.23, 70.5, 76),
        new("DraftBox", 29.2, 92.6, 67.7, 5),
        new("ShortcutsButton", 88.83, 94.09, 7.56, 2.68),
    };

    public sealed class WindowPart(string name, double x, double y, double w, double h)
    {
        public string Name { get; } = name;
        public double X { get; } = x;
        public double Y { get; } = y;
        public double W { get; } = w;
        public double H { get; } = h;
    }

    /// <summary>A drawing of the window that lights up some of its parts; Show changes which.</summary>
    public sealed class WindowMap : Viewbox
    {
        private const double HeightRatio = 860.0 / 1180.0;
        private readonly Dictionary<string, Rectangle> _parts = new();

        public WindowMap(params string[] lit)
        {
            Stretch = Stretch.Uniform;
            var canvas = new Canvas { Width = 100, Height = 100 * HeightRatio };
            var window = new Rectangle { Width = 100, Height = canvas.Height, RadiusX = 1.5, RadiusY = 1.5, StrokeThickness = 0.5 };
            window.Paint(Shape.FillProperty, "WindowBrush").Paint(Shape.StrokeProperty, "InputBorderBrush");
            canvas.Children.Add(window);

            // The strip and the list beside it, with a thin line between them as in the window.
            var side = new Rectangle { Width = 25.8, Height = canvas.Height - 0.6, RadiusX = 1.2, RadiusY = 1.2 };
            side.Paint(Shape.FillProperty, "SidebarBrush");
            Canvas.SetLeft(side, 0.3);
            Canvas.SetTop(side, 0.3);
            canvas.Children.Add(side);
            var seam = new Rectangle { Width = 0.25, Height = canvas.Height - 0.6 };
            seam.Paint(Shape.FillProperty, "InputBorderBrush");
            Canvas.SetLeft(seam, 5.1);
            Canvas.SetTop(seam, 0.3);
            canvas.Children.Add(seam);

            foreach (var part in WindowParts)
            {
                var rect = new Rectangle { Width = part.W, Height = Math.Max(0.8, part.H * HeightRatio - 0.5), RadiusX = 0.7, RadiusY = 0.7 };
                Canvas.SetLeft(rect, part.X);
                Canvas.SetTop(rect, part.Y * HeightRatio + 0.25);
                canvas.Children.Add(rect);
                _parts[part.Name] = rect;
            }
            Child = canvas;
            Show(lit);
        }

        public void Show(IEnumerable<string> lit)
        {
            var on = new HashSet<string>(lit);
            foreach (var pair in _parts)
            {
                var active = on.Contains(pair.Key);
                pair.Value.Paint(Shape.FillProperty, active ? "AccentBrush" : "InputBorderBrush");
                pair.Value.Opacity = active ? 1 : 0.55;
            }
        }

        public IEnumerable<string> LitParts => _parts.Where(p => p.Value.Opacity == 1).Select(p => p.Key);
    }
}
