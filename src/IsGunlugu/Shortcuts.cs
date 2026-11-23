using System.Globalization;
using System.Windows.Input;

namespace IsGunlugu;

/// <summary>One shortcut: its keys as separate caps, and what it does split around the searched word so it can be bold.</summary>
public sealed class ShortcutRow(string group, string[] keys, string action, string note = "")
{
    public string Group { get; } = group;
    public IList<string> Keys { get; } = keys;
    public string Action { get; } = action;

    /// <summary>A side remark, shown small under the action instead of in brackets.</summary>
    public string Note { get; } = note;
    public bool HasNote => Note.Length > 0;

    public string Before { get; private init; } = action;
    public string Hit { get; private init; } = "";
    public string After { get; private init; } = "";

    /// <summary>The row with <paramref name="query"/> picked out, or null when neither its keys nor its text hold it.</summary>
    public ShortcutRow? Matching(string query, CultureInfo culture)
    {
        var compare = culture.CompareInfo;
        var at = compare.IndexOf(Action, query, CompareOptions.IgnoreCase);
        if (at >= 0)
            return new ShortcutRow(Group, Keys.ToArray(), Action, Note) { Before = Action.Substring(0, at), Hit = Action.Substring(at, query.Length), After = Action.Substring(at + query.Length) };
        return Keys.Any(k => compare.IndexOf(k, query, CompareOptions.IgnoreCase) >= 0) || compare.IndexOf(Group, query, CompareOptions.IgnoreCase) >= 0
            || compare.IndexOf(Note, query, CompareOptions.IgnoreCase) >= 0 ? this : null;
    }
}

public sealed class ShortcutSection(string title, IList<ShortcutRow> rows, bool showTitle)
{
    public string Title { get; } = title.ToUpper(new CultureInfo("tr-TR"));
    public IList<ShortcutRow> Rows { get; } = rows;
    public bool ShowTitle { get; } = showTitle;
}

/// <summary>A tab over the shortcuts; while searching it says how many it holds, and fades when none.</summary>
public sealed class ShortcutTab(string name, bool isOn, int found, bool searching, ICommand pick)
{
    public string Name { get; } = name;
    public bool IsOn { get; } = isOn;
    public string Found { get; } = searching && found > 0 ? found.ToString() : "";
    public bool IsEmpty { get; } = searching && found == 0;
    public ICommand PickCommand { get; } = pick;
}

/// <summary>
/// The shortcuts window (F1, or Settings › Yardım): tabs by subject, and a search over all of them at the top.
/// Opened from Settings, closing it goes back there.
/// </summary>
public sealed partial class MainViewModel
{
    private static readonly string[] ShortcutGroups = { "Liste", "Seçili iş", "Taşıma", "Sayfalar", "Yazarken", "Genel" };

    private static readonly IList<ShortcutRow> AllShortcuts = new List<ShortcutRow>
    {
        new("Liste", new[] { "↑", "↓" }, "Önceki / sonraki iş"),
        new("Liste", new[] { "Tab", "Shift+Tab" }, "Tamamlanmamış işler arasında geç"),
        new("Liste", new[] { "→", "←" }, "Alt işlere in / ana işe çık"),
        new("Liste", new[] { "Home", "End" }, "İlk / son iş"),
        new("Liste", new[] { "Esc" }, "Seçimi bırak"),

        new("Seçili iş", new[] { "Boşluk" }, "Tikle ya da tiki kaldır"),
        new("Seçili iş", new[] { "F2" }, "Yerinde düzenle"),
        new("Seçili iş", new[] { "Enter" }, "Sağ panelde aç"),
        new("Seçili iş", new[] { "Del" }, "Sil, önce sorar", "Ctrl+Z geri alır"),
        new("Seçili iş", new[] { "Alt+↑", "Alt+↓" }, "Sırada yukarı / aşağı taşı"),
        new("Seçili iş", new[] { "Ctrl+tık", "Shift+tık" }, "Fareyle seç, seçilenleri birlikte sürükle"),
        new("Seçili iş", new[] { "Soldaki noktalar" }, "Sürükle: sırala ya da başka güne taşı. Tıkla: menü"),

        new("Taşıma", new[] { "M" }, "Taşıma menüsü: Bugüne, Yarına, Tarihsize, Gün seç"),
        new("Taşıma", new[] { "Ctrl+Shift+←", "Ctrl+Shift+→" }, "Bir gün öne / sonraya taşı"),
        new("Taşıma", new[] { "Shift+↑", "Shift+↓" }, "Birden çok iş seç, birlikte taşı"),
        new("Taşıma", new[] { "Shift+Enter" }, "Seçili işi seçime ekle ya da çıkar", "Arka arkaya olmayan işleri tek tek seçer"),
        new("Taşıma", new[] { "←", "→", "↑", "↓" }, "Gün seçerken: takvimde gün değiştir", "Enter taşır, Esc vazgeçer"),
        new("Taşıma", new[] { "Takvime sürükle" }, "Bitmiş ya da bitmemiş işi o güne taşı"),

        new("Sayfalar", new[] { "Ctrl+↑", "Ctrl+↓" }, "Önceki / sonraki sayfa: Bugün, Tamamlanmamış, Gelecek, Tarihsiz, Tümü"),
        new("Sayfalar", new[] { "Ctrl+1 … 6" }, "Bugün, Tamamlanmamış, Gelecek, Tarihsiz, Tümü, Takvim"),
        new("Sayfalar", new[] { "Ctrl+←", "Ctrl+→" }, "Önceki / sonraki gün, boş günler dahil", "Takvimde: hafta, ay ya da yıl"),
        new("Sayfalar", new[] { "Alt+←", "Alt+→" }, "İş olan önceki / sonraki gün"),
        new("Sayfalar", new[] { "Ctrl+Tab" }, "İşler ile Takvim arasında geç"),

        new("Yazarken", new[] { "Enter" }, "İş ekle. Açıklamada: tamam. Alt işte: alt işi ekle"),
        new("Yazarken", new[] { "Ctrl+Enter" }, "Tikli ekle. Düzenlerken: kaydet ve tikle"),
        new("Yazarken", new[] { "Alt+Enter" }, "Alt satır: - ile başlayan alt iş, düz satır açıklama", "Shift+Enter da olur"),
        new("Yazarken", new[] { "Esc" }, "Ekleme kutusunu, düzenlemeyi ya da aramayı kapat"),
        new("Yazarken", new[] { "Ctrl+A", "Ctrl+A" }, "İki kez: bütün satırları seç", "Shift+↑/↓ ya da satırlar arasında sürüklemek de seçer; sonra kopyala, sil ya da üstüne yaz"),
        new("Yazarken", new[] { "Ctrl+N" }, "Ekleme kutusunu aç"),
        new("Yazarken", new[] { GlobalHotKey.Keys }, "Her yerden, uygulama tepsideyken de iş ekle"),

        new("Genel", new[] { "Ctrl+F" }, "Ara"),
        new("Genel", new[] { "Ctrl+Z" }, "Geri al"),
        new("Genel", new[] { "F5" }, "Dosyayı yeniden yükle"),
        new("Genel", new[] { "F1" }, "Bu pencere"),
    };

    private string _shortcutTab = ShortcutGroups[0];
    private string _shortcutQuery = "";
    private bool _helpFromSettings;

    public bool IsHelpOpen
    {
        get => _isHelpOpen;
        set
        {
            if (_isHelpOpen == value) return;
            _isHelpOpen = value;
            Notify();
            if (value)
            {
                ShortcutQuery = "";
                return;
            }
            if (_helpFromSettings)
            {
                _helpFromSettings = false;
                IsSettingsOpen = true;
            }
        }
    }

    /// <summary>Settings › Yardım › Kısayollar: the window shows over the screen, and closing it brings Settings back.</summary>
    public void OpenHelpFromSettings()
    {
        _helpFromSettings = true;
        IsSettingsOpen = false;
        IsHelpOpen = true;
    }

    public string ShortcutQuery
    {
        get => _shortcutQuery;
        set
        {
            _shortcutQuery = value ?? "";
            Notify();
            NotifyShortcuts();
        }
    }

    public bool IsShortcutSearching => ShortcutQuery.Trim().Length > 0;

    public IList<ShortcutTab> ShortcutTabs
    {
        get
        {
            var found = Found();
            return ShortcutGroups.Select(name => new ShortcutTab(name, !IsShortcutSearching && name == _shortcutTab,
                found.Count(r => r.Group == name), IsShortcutSearching, new RelayCommand(() => PickShortcutTab(name)))).ToList();
        }
    }

    public IList<ShortcutSection> ShortcutSections
    {
        get
        {
            if (!IsShortcutSearching)
                return new[] { new ShortcutSection(_shortcutTab, AllShortcuts.Where(r => r.Group == _shortcutTab).ToList(), showTitle: false) };
            var found = Found();
            return ShortcutGroups.Select(g => new ShortcutSection(g, found.Where(r => r.Group == g).ToList(), showTitle: true))
                .Where(s => s.Rows.Count > 0).ToList();
        }
    }

    public string ShortcutResult
    {
        get
        {
            if (!IsShortcutSearching) return "";
            var count = Found().Count;
            return count == 0 ? "Sonuç yok" : $"{count} sonuç";
        }
    }

    /// <summary>Ctrl+Tab in the window: the next tab (the search is left).</summary>
    public void NextShortcutTab(int step)
    {
        var at = Array.IndexOf(ShortcutGroups, _shortcutTab);
        PickShortcutTab(ShortcutGroups[(at + step + ShortcutGroups.Length) % ShortcutGroups.Length]);
    }

    private void PickShortcutTab(string name)
    {
        _shortcutTab = name;
        if (IsShortcutSearching) ShortcutQuery = "";
        else NotifyShortcuts();
    }

    private List<ShortcutRow> Found()
    {
        var query = ShortcutQuery.Trim();
        return query.Length == 0 ? new List<ShortcutRow>() : AllShortcuts.Select(r => r.Matching(query, Turkish)).OfType<ShortcutRow>().ToList();
    }

    private void NotifyShortcuts()
    {
        Notify(nameof(IsShortcutSearching));
        Notify(nameof(ShortcutTabs));
        Notify(nameof(ShortcutSections));
        Notify(nameof(ShortcutResult));
    }
}
