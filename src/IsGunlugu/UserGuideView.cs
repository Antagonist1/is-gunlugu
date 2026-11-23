using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static IsGunlugu.GuideParts;

namespace IsGunlugu;

/// <summary>
/// The user guide, drawn with the window's own controls. The browser control that .NET Framework offers is the old
/// Internet Explorer, which cannot lay the web page out, and anything newer would break the single exe.
/// Everything is built when first opened, so it costs nothing until then.
/// </summary>
public sealed partial class UserGuideView : UserControl
{
    private const double SideWidth = 250;

    private readonly Grid _root = new();
    private readonly List<Watched> _watched = new();
    private readonly List<Button> _links = new();
    private ScrollViewer _scroll = new();
    private StackPanel _pages = new();
    private WindowMap _map = new();
    private bool _builtDark;
    private bool _built;

    public UserGuideView()
    {
        Content = _root;
        Focusable = false;
        IsVisibleChanged += (_, e) =>
        {
            if (!(bool)e.NewValue) return;
            Build();
            Dispatcher.BeginInvoke(new Action(() => _scroll.Focus()), System.Windows.Threading.DispatcherPriority.Input);
        };
    }

    /// <summary>The part names lit on the map, for the section at the reading line.</summary>
    public IEnumerable<string> LitParts => _map.LitParts;

    public int SectionCount => _links.Count;

    public ScrollViewer Scroller => _scroll;

    public int ShotCount { get; private set; }

    public int MissingShots { get; private set; }

    /// <summary>The section whose title is on the left as the current one.</summary>
    public string CurrentSection => _links.FirstOrDefault(l => Equals(l.Tag, true))?.Content as string ?? "";

    public void GoToSection(int index)
    {
        var target = _watched.First(w => w.Link == index && w.IsSection).Element;
        _scroll.ScrollToVerticalOffset(TopOf(target));
    }

    private static bool ThemeIsDark()
    {
        // The theme's own colour decides, so the pictures always match what is on screen.
        var window = Application.Current.TryFindResource("WindowBrush") as SolidColorBrush;
        return window is not null && window.Color.R < 128;
    }

    private void Build()
    {
        var dark = ThemeIsDark();
        if (_built && dark == _builtDark) return;

        _built = true;
        _builtDark = dark;
        _watched.Clear();
        _links.Clear();
        ShotCount = 0;
        MissingShots = 0;
        _root.Children.Clear();
        _root.ColumnDefinitions.Clear();
        _root.Paint(Panel.BackgroundProperty, "WindowBrush");
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SideWidth) });
        _root.ColumnDefinitions.Add(new ColumnDefinition());

        // The empty space below lets the last sections scroll up to the top when picked from the list.
        _pages = new StackPanel { Margin = new Thickness(40, 30, 56, 480), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        _scroll = new ScrollViewer { Content = _pages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = true, FocusVisualStyle = null };
        _scroll.ScrollChanged += (_, _) => UpdateReadingPlace();
        Grid.SetColumn(_scroll, 1);
        _root.Children.Add(_scroll);

        var side = BuildSide();
        WriteContent();
        ((Panel)side).Children.Add(BuildLinks());
        _root.Children.Add(new Border { Child = side }.Paint(Border.BackgroundProperty, "SidebarBrush"));

        var close = new Button
        {
            Style = (Style)Application.Current.FindResource("IconButton"),
            Content = Application.Current.FindResource("IconClose"),
            ToolTip = "Kapat (Esc)",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 24, 0),
        };
        System.Windows.Automation.AutomationProperties.SetName(close, "Kılavuzu kapat");
        close.SetBinding(System.Windows.Controls.Primitives.ButtonBase.CommandProperty, new Binding(nameof(MainViewModel.CloseUserGuideCommand)));
        Grid.SetColumn(close, 1);
        _root.Children.Add(close);

        _root.UpdateLayout();
        UpdateReadingPlace();
    }

    private DockPanel BuildSide()
    {
        var side = new DockPanel { Margin = new Thickness(18, 22, 12, 12) };

        var brand = new TextBlock { Text = "İş Günlüğü Kılavuzu", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 0, 0, 12) };
        brand.Paint(TextBlock.ForegroundProperty, "InkBrush");
        DockPanel.SetDock(brand, Dock.Top);
        side.Children.Add(brand);

        _map = new WindowMap { Margin = new Thickness(6, 0, 6, 6) };
        DockPanel.SetDock(_map, Dock.Top);
        side.Children.Add(_map);

        var hint = new TextBlock { Text = "Renkli yer, okuduğun bölümün pencerede bulunduğu yer.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 0, 6, 12) };
        hint.Paint(TextBlock.ForegroundProperty, "MutedBrush");
        DockPanel.SetDock(hint, Dock.Top);
        side.Children.Add(hint);
        return side;
    }

    private ScrollViewer BuildLinks()
    {
        var list = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
        foreach (var link in _links) list.Children.Add(link);
        return new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
    }

    /// <summary>A heading with a place in the contents list.</summary>
    private void Section(string title, params string[] parts)
    {
        var index = _links.Count;
        var link = new Button { Style = (Style)Application.Current.FindResource("NavButton"), Content = title, Padding = new Thickness(0), Tag = false };
        link.Click += (_, _) => GoToSection(index);
        _links.Add(link);

        var heading = new TextBlock { Text = title, FontSize = index == 0 ? 34 : 26, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, index == 0 ? 0 : 54, 0, 14) };
        heading.Paint(TextBlock.ForegroundProperty, "InkBrush");
        _pages.Children.Add(heading);
        _watched.Add(new Watched(heading, parts, index, true));
    }

    /// <summary>One of the screens: a small map of where it is, its name and what it does.</summary>
    private void Screen(string title, string text, string[] parts, UIElement? picture = null, params UIElement[] after)
    {
        var map = new WindowMap(parts) { Width = 136, VerticalAlignment = VerticalAlignment.Top };
        var words = new StackPanel { Margin = new Thickness(18, 0, 0, 0) };
        words.Children.Add(Text(title, 18, semiBold: true));
        var body = Text(text, 15);
        body.Margin = new Thickness(0, 4, 0, 0);
        words.Children.Add(body);

        var head = new DockPanel { Margin = new Thickness(0, 26, 0, 12) };
        head.Children.Add(map);
        head.Children.Add(words);
        _pages.Children.Add(head);
        _watched.Add(new Watched(head, parts, _links.Count - 1, false));

        if (picture is not null) _pages.Children.Add(picture);
        foreach (var piece in after) _pages.Children.Add(piece);
    }

    private void Add(UIElement piece) => _pages.Children.Add(piece);

    private void P(string text) => Add(Spaced(Text(text, 15), 0, 10));

    private void Lead(string text) => Add(Spaced(Text(text, 17, "MutedBrush"), 0, 16));

    private void Sub(string title) => Add(Spaced(Text(title, 18, semiBold: true), 16, 8));

    private static T Spaced<T>(T element, double above, double below) where T : FrameworkElement
    {
        element.Margin = new Thickness(0, above, 0, below);
        return element;
    }

    private static FrameworkElement Note(string text)
    {
        var note = new Border { Child = Text(text, 14.5), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(14, 2, 0, 2), Margin = new Thickness(0, 6, 0, 12) };
        return note.Paint(Border.BorderBrushProperty, "AccentBrush");
    }

    /// <summary>A table: the first column fits its text, the others share the rest. A cell is text or a drawing.</summary>
    private static FrameworkElement Table(string[] head, params object[][] rows)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 14) };
        for (var c = 0; c < head.Length; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = c == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });

        void AddRow(object[] cells, bool isHead, int at)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var line = new Border { BorderThickness = new Thickness(0, 0, 0, 1) };
            line.Paint(Border.BorderBrushProperty, isHead ? "InputBorderBrush" : "HoverBrush");
            Grid.SetRow(line, at);
            Grid.SetColumnSpan(line, head.Length);
            grid.Children.Add(line);

            for (var c = 0; c < cells.Length; c++)
            {
                FrameworkElement cell = cells[c] as FrameworkElement ?? Text((string)cells[c], isHead ? 13.5 : 14.5, isHead ? "MutedBrush" : "InkBrush", isHead);
                cell.Margin = new Thickness(0, isHead ? 0 : 7, 18, 7);
                cell.VerticalAlignment = VerticalAlignment.Top;
                if (cell is TextBlock text && c == 0) text.TextWrapping = TextWrapping.NoWrap;
                if (cell is not TextBlock) cell.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetRow(cell, at);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        AddRow(head.Cast<object>().ToArray(), true, 0);
        for (var r = 0; r < rows.Length; r++) AddRow(rows[r], false, r + 1);
        return grid;
    }

    private static object[] R(params object[] cells) => cells;

    private static FrameworkElement StateCell(State state, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(StatusBox(state));
        var text = Text(label, 14.5);
        text.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(text);
        return row;
    }

    /// <summary>Numbered circles joined by a line, for things done in order.</summary>
    private static FrameworkElement Steps(params string[] steps)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
        for (var i = 0; i < steps.Length; i++)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            if (i < steps.Length - 1)
            {
                var line = new Border { Width = 1.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 30, 0, 0) };
                row.Children.Add(line.Paint(Border.BackgroundProperty, "InputBorderBrush"));
            }

            var number = new TextBlock { Text = (i + 1).ToString(), FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            number.Paint(TextBlock.ForegroundProperty, "AccentBrush");
            var circle = new Border { Child = number, Width = 30, Height = 30, CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1.5), VerticalAlignment = VerticalAlignment.Top };
            circle.Paint(Border.BorderBrushProperty, "AccentBrush").Paint(Border.BackgroundProperty, "WindowBrush");
            row.Children.Add(circle);

            var text = Text(steps[i], 15);
            text.Margin = new Thickness(12, 3, 0, 16);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            stack.Children.Add(row);
        }
        return stack;
    }

    /// <summary>What each numbered marker on a picture is.</summary>
    private static FrameworkElement Legend(params string[] items)
    {
        var columns = new TwoColumns { Margin = new Thickness(0, 12, 0, 12) };
        var split = items.Length > 3 ? (items.Length + 1) / 2 : items.Length;
        var first = new StackPanel();
        var second = new StackPanel();
        for (var i = 0; i < items.Length; i++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var marker = Pin(i + 1);
            marker.VerticalAlignment = VerticalAlignment.Top;
            marker.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(marker);
            row.Children.Add(Text(items[i], 14.5));
            (i < split ? first : second).Children.Add(row);
        }
        columns.Children.Add(first);
        if (second.Children.Count > 0) columns.Children.Add(second);
        return columns;
    }

    private static Border Pin(int number)
    {
        var label = new TextBlock { Text = number.ToString(), FontSize = 12.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.Paint(TextBlock.ForegroundProperty, "OnAccentBrush");
        var pin = new Border { Child = label, Width = 24, Height = 24, CornerRadius = new CornerRadius(12) };
        return pin.Paint(Border.BackgroundProperty, "AccentBrush");
    }

    /// <summary>A picture of the window in the current theme, with numbered markers at x,y in percent.</summary>
    private FrameworkElement Shot(string name, double maxWidth = double.PositiveInfinity, params double[] pins)
    {
        ShotCount++;
        var picture = new ShotPanel { Margin = new Thickness(0, 4, 0, 12), HorizontalAlignment = HorizontalAlignment.Left };
        var source = LoadPicture(name + (_builtDark ? "-d" : "-l"));
        if (source is null)
        {
            MissingShots++;
            return picture;
        }

        picture.Ratio = (double)source.PixelWidth / source.PixelHeight;
        // The pictures were taken at one and a half times their size.
        picture.MaxPictureWidth = Math.Min(maxWidth, source.PixelWidth / 1.5);
        var image = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Background = new ImageBrush(source) { Stretch = Stretch.Fill },
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        picture.Children.Add(image.Paint(Border.BorderBrushProperty, "InputBorderBrush"));

        for (var i = 0; i + 1 < pins.Length; i += 2)
        {
            var pin = Pin(i / 2 + 1);
            ShotPanel.SetAt(pin, new Point(pins[i], pins[i + 1]));
            picture.Children.Add(pin);
        }
        return picture;
    }

    private static BitmapImage? LoadPicture(string name)
    {
        using var stream = typeof(UserGuideView).Assembly.GetManifestResourceStream("guide." + name + ".png");
        if (stream is null) return null;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private double TopOf(FrameworkElement element) => element.TransformToAncestor(_pages).Transform(new Point(0, 0)).Y;

    private void UpdateReadingPlace()
    {
        if (_watched.Count == 0 || !_scroll.IsVisible) return;

        // The reading line sits a third of the way down; at the very end the last section counts.
        var line = _scroll.VerticalOffset + _scroll.ViewportHeight * 0.35;
        var current = _watched[0];
        foreach (var item in _watched)
        {
            if (TopOf(item.Element) <= line) current = item;
        }
        if (_scroll.ScrollableHeight > 0 && _scroll.VerticalOffset >= _scroll.ScrollableHeight - 1) current = _watched[_watched.Count - 1];

        _map.Show(current.Parts);
        for (var i = 0; i < _links.Count; i++) _links[i].Tag = i == current.Link;
    }

    private sealed class Watched(FrameworkElement element, string[] parts, int link, bool isSection)
    {
        public FrameworkElement Element { get; } = element;
        public string[] Parts { get; } = parts;
        public int Link { get; } = link;
        public bool IsSection { get; } = isSection;
    }

    /// <summary>Keeps a picture's proportions at any width and sets its markers on top.</summary>
    private sealed class ShotPanel : Panel
    {
        private static readonly DependencyProperty AtProperty = DependencyProperty.RegisterAttached("At", typeof(Point?), typeof(ShotPanel));

        public double Ratio { get; set; } = 1.5;
        public double MaxPictureWidth { get; set; } = double.PositiveInfinity;

        public static void SetAt(UIElement element, Point at) => element.SetValue(AtProperty, at);

        protected override Size MeasureOverride(Size available)
        {
            var width = Math.Min(available.Width, MaxPictureWidth);
            if (double.IsInfinity(width)) width = 600;
            var size = new Size(width, width / Ratio);
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(child.GetValue(AtProperty) is null ? size : new Size(double.PositiveInfinity, double.PositiveInfinity));
            }
            return size;
        }

        protected override Size ArrangeOverride(Size final)
        {
            foreach (UIElement child in InternalChildren)
            {
                if (child.GetValue(AtProperty) is Point at)
                {
                    var size = child.DesiredSize;
                    child.Arrange(new Rect(new Point(final.Width * at.X / 100 - size.Width / 2, final.Height * at.Y / 100 - size.Height / 2), size));
                }
                else
                {
                    child.Arrange(new Rect(final));
                }
            }
            return final;
        }
    }

    /// <summary>Two columns side by side when there is room, one under the other when not.</summary>
    private sealed class TwoColumns : Panel
    {
        private const double Gap = 24;
        private const double NarrowBelow = 560;

        protected override Size MeasureOverride(Size available)
        {
            var count = InternalChildren.Count;
            if (count == 0) return new Size();

            var sideBySide = count == 2 && available.Width >= NarrowBelow;
            var width = sideBySide ? (available.Width - Gap) / 2 : available.Width;
            double tallest = 0, total = 0;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                tallest = Math.Max(tallest, child.DesiredSize.Height);
                total += child.DesiredSize.Height;
            }
            return new Size(available.Width, sideBySide ? tallest : total);
        }

        protected override Size ArrangeOverride(Size final)
        {
            var count = InternalChildren.Count;
            var sideBySide = count == 2 && final.Width >= NarrowBelow;
            var width = sideBySide ? (final.Width - Gap) / 2 : final.Width;
            double x = 0, y = 0;
            foreach (UIElement child in InternalChildren)
            {
                child.Arrange(new Rect(x, y, width, child.DesiredSize.Height));
                if (sideBySide) x += width + Gap;
                else y += child.DesiredSize.Height;
            }
            return final;
        }
    }
}
