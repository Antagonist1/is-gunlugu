using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using static IsGunlugu.GuideParts;

namespace IsGunlugu;

/// <summary>
/// The small picture on each tour card. To give a step a picture, add a case with the step's title;
/// the pieces below (rows, keys, arrows, a file tree) cover most needs.
/// </summary>
internal static class GuideArt
{
    public static FrameworkElement? For(string stepTitle) => stepTitle switch
    {
        "Yeni iş yaz" => Panel(Arrow("Kutuya yaz", "Enter", "Bugün", main: true)),
        "Gün, alt iş, açıklama" => Panel(
            MockRow(State.Open, "Rapor gönder", badge: "0/2"),
            MockRow(State.Open, "Rakamları topla", 1),
            MockRow(State.Open, "Grafikleri güncelle", 1),
            Keys("Ctrl+Enter", "Tikli ekler")),
        "İşi bitir" => Panel(
            MockRow(State.Open, "Yapılmadı"),
            MockRow(State.Partial, "Yarım", badge: "1/3"),
            MockRow(State.Done, "Bitti")),
        "Satır düğmeleri" => Panel(Icons("IconPlus", "Alt iş", "IconEdit", "Düzenle", "IconDelete", "Sil")),
        "Soldaki noktalar" => Panel(
            Arrow("İş", "sürükle", "Bugün", main: true),
            Arrow("İş", "sürükle", "Gelecek: yarın"),
            Arrow("İş", "sürükle", "Tarihsiz")),
        "Geri al" => Panel(Arrow("Silindi", "Ctrl+Z", "Geri geldi", main: true)),
        "Bugün" => Panel(
            MockRow(State.Partial, "Önce yarım kalanlar", badge: "1/3"),
            MockRow(State.Open, "Sonra bitmemişler"),
            MockRow(State.Done, "En altta bitenler")),
        "İşler grubu" => Panel(Headings("İşler", "Tamamlanmamış", "Gelecek", "Tarihsiz", "Tümü")),
        "Tamamlanmamış" => Panel(Arrow("Bugün", "gün biterse", "Tamamlanmamış")),
        "Bugüne çek" => Panel(Arrow("Tamamlanmamış", "Bugüne al", "Bugün", main: true)),
        "Gelecek" => Panel(Arrow("Gelecek", "günü gelince", "Bugün", main: true)),
        "Tarihsiz" => Panel(Arrow("Tarihsiz", "tikleyince", "Bitirdiğin gün")),
        "Tümü" => Panel(Headings(null, "Bugün", "Önceki günlerden kalan", "Gelecek", "Tarihsiz")),
        "Toplu taşıma" => Panel(
            MockRow(State.Open, "Depo sayımı", selected: true),
            MockRow(State.Open, "Tedarikçiyle görüş", selected: true),
            MockRow(State.Open, "Seçilmeyen iş"),
            Keys("Ctrl+tık", "Tek tek", "Shift+tık", "Aralık")),
        "Takvim" => Panel(Heat()),
        "Küçük takvim" => Panel(Heat()),
        "Başka güne taşı" => Panel(Arrow("İş", "sürükle", "Takvimde bir gün", main: true)),
        "İleri tarihli iş" => Panel(Arrow("İleri bir gün", "tıkla, yaz", "Gelecek")),
        "Ara" => Panel(Search()),
        "Dosyalar" => Panel(Tree("Belgeler", "└ İş Günlüğü", "   ├ notlar.txt", "   └ ev.txt")),
        "Dosya grupları" => Panel(Tree("İş Günlüğü", "├ iş (grup = klasör)", "│ └ notlar.txt", "└ ev.txt")),
        "Not Defteri" => Panel(MonoText("🚨Faturaları kontrol et", 12.5), DownArrow(), MockRow(State.Open, "Faturaları kontrol et")),
        "Ayarlar" => Panel(Keys("Tek tık", "Bugünün işleri", "Çift tık", "Uygulama", "Sağ tık", "Çıkış")),
        "Kısayollar" => Panel(Keys("F1", "Bütün kısayollar", "Ctrl+N", "Yeni iş", "Ctrl+F", "Ara")),
        _ => null,
    };

    private static Border Panel(params UIElement[] content)
    {
        var stack = new StackPanel();
        foreach (var piece in content)
        {
            if (piece is FrameworkElement element && stack.Children.Count > 0) element.Margin = new Thickness(element.Margin.Left, 5, element.Margin.Right, 0);
            stack.Children.Add(piece);
        }

        var panel = new Border { Child = stack, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 9, 10, 9), Margin = new Thickness(0, 8, 0, 10) };
        return panel.Paint(Border.BackgroundProperty, "SidebarBrush");
    }

    /// <summary>from → to, with the cause written above the arrow.</summary>
    private static FrameworkElement Arrow(string from, string cause, string to, bool main = false)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 70 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var start = Chip(from);
        start.VerticalAlignment = VerticalAlignment.Bottom;
        grid.Children.Add(start);

        var middle = new StackPanel { Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = cause, FontSize = 11.5, TextAlignment = TextAlignment.Center };
        middle.Children.Add(label.Paint(TextBlock.ForegroundProperty, "MutedBrush"));
        var line = new Path
        {
            Data = Geometry.Parse("M 0,4 L 100,4 M 94,0 L 100,4 L 94,8"),
            Stretch = Stretch.Fill,
            Height = 9,
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        middle.Children.Add(line.Paint(Shape.StrokeProperty, "InkBrush"));
        Grid.SetColumn(middle, 1);
        grid.Children.Add(middle);

        var end = Chip(to, main);
        end.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(end, 2);
        grid.Children.Add(end);
        return grid;
    }

    private static FrameworkElement DownArrow()
    {
        var arrow = Stroke("M 8,1 L 8,13 M 4,9 L 8,13 L 12,9", "MutedBrush");
        arrow.Width = 16;
        arrow.Height = 14;
        arrow.HorizontalAlignment = HorizontalAlignment.Left;
        return arrow;
    }

    /// <summary>Key, meaning, key, meaning...</summary>
    private static FrameworkElement Keys(params string[] pairs)
    {
        var stack = new StackPanel();
        for (var i = 0; i + 1 < pairs.Length; i += 2)
        {
            var meaning = pairs[i + 1];
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var cap = GuideParts.Key(pairs[i]);
            cap.MinWidth = 76;
            ((TextBlock)cap.Child).TextAlignment = TextAlignment.Center;
            row.Children.Add(cap);
            var text = new TextBlock { Text = meaning, FontSize = 13, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(text.Paint(TextBlock.ForegroundProperty, "InkBrush"));
            stack.Children.Add(row);
        }
        return stack;
    }

    /// <summary>Icon, label, icon, label...</summary>
    private static FrameworkElement Icons(params string[] pairs)
    {
        var grid = new UniformGrid { Rows = 1 };
        for (var i = 0; i + 1 < pairs.Length; i += 2)
        {
            var label = pairs[i + 1];
            var item = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var drawn = GuideParts.Icon(pairs[i], "InkBrush", 1.4);
            item.Children.Add(drawn);
            var text = new TextBlock { Text = label, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Center };
            item.Children.Add(text.Paint(TextBlock.ForegroundProperty, "MutedBrush"));
            grid.Children.Add(item);
        }
        return grid;
    }

    /// <summary>Collapsible headings as they look in the window; a first title holds the others in it.</summary>
    private static FrameworkElement Headings(string? parent, params string[] names)
    {
        var stack = new StackPanel();
        if (parent is not null) stack.Children.Add(Heading(parent, 0, true));
        foreach (var name in names) stack.Children.Add(Heading(name, parent is null ? 0 : 18, parent is null));
        return stack;
    }

    private static FrameworkElement Heading(string text, double indent, bool chevron)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(indent, 1, 0, 1) };
        if (chevron)
        {
            var icon = Icon("IconDown");
            icon.Margin = new Thickness(0, 0, 4, 0);
            icon.Width = 14;
            icon.Height = 14;
            icon.Stretch = Stretch.Uniform;
            row.Children.Add(icon);
        }
        var label = new TextBlock { Text = text, FontSize = 13, FontWeight = chevron ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(label.Paint(TextBlock.ForegroundProperty, "InkBrush"));
        return row;
    }

    /// <summary>What the calendar's colours mean.</summary>
    private static FrameworkElement Heat()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        void Add(string brush, string label)
        {
            var cell = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(cell.Paint(Border.BackgroundProperty, brush));
            var text = new TextBlock { Text = label, FontSize = 12.5, Margin = new Thickness(4, 0, 11, 0), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(text.Paint(TextBlock.ForegroundProperty, "InkBrush"));
        }
        Add("Heat1Brush", "1-2");
        Add("Heat2Brush", "3-4");
        Add("Heat3Brush", "5+");

        var dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(dot.Paint(Shape.FillProperty, "OpenBrush"));
        var open = new TextBlock { Text = "Kalan iş", FontSize = 12.5, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(open.Paint(TextBlock.ForegroundProperty, "InkBrush"));
        return row;
    }

    private static FrameworkElement Search()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Icon("IconSearch");
        icon.Margin = new Thickness(0, 0, 6, 0);
        row.Children.Add(icon);
        var word = new TextBlock { Text = "fatura", FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(word.Paint(TextBlock.ForegroundProperty, "InkBrush"));

        var box = new Border { Child = row, Padding = new Thickness(8, 3, 8, 3), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1) };
        box.Paint(Border.BackgroundProperty, "InputBrush").Paint(Border.BorderBrushProperty, "InputBorderBrush");

        var stack = new StackPanel();
        stack.Children.Add(box);
        var result = MockRow(State.Open, "Faturaları kontrol et");
        result.Margin = new Thickness(0, 5, 0, 0);
        stack.Children.Add(result);
        return stack;
    }

    private static FrameworkElement Tree(params string[] lines)
    {
        var stack = new StackPanel();
        foreach (var line in lines) stack.Children.Add(MonoText(line, 12.5));
        return stack;
    }
}
