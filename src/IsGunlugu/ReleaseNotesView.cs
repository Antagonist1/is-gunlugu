using System.Globalization;
using System.Windows.Input;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>A version in the list on the left: its number, its day, and whether it is the one running.</summary>
public sealed class ReleaseTab(string version, string day, bool isOn, ICommand pick)
{
    public string Version { get; } = version;
    public string Day { get; } = day;
    public bool IsOn { get; } = isOn;
    public ICommand PickCommand { get; } = pick;
}

/// <summary>One part of a version's notes on the right: YENİ · 4, its colour, its items as head and faint detail.</summary>
public sealed class ReleaseSectionView(string title, string kind, IList<ReleaseItem> items)
{
    public string Title { get; } = title;
    public string Kind { get; } = kind;
    public IList<ReleaseItem> Items { get; } = items;
}

/// <summary>
/// The release notes window, laid out as Settings: the versions on the left, the picked one's notes on the right in
/// white cards on the grey page. It opens on the version running.
/// </summary>
public sealed partial class MainViewModel
{
    private string? _releaseVersion;

    private ReleaseNote? ShownRelease =>
        ReleaseNoteList.FirstOrDefault(n => n.Version == _releaseVersion)
        ?? ReleaseNoteList.FirstOrDefault(n => n.Version == AppVersion)
        ?? ReleaseNoteList.FirstOrDefault();

    public IList<ReleaseTab> ReleaseTabs => ReleaseNoteList.Select(n => new ReleaseTab(
        n.Version,
        (n.Date?.ToString("d MMMM yyyy", Turkish) ?? "") + (n.Version == AppVersion ? " · bu sürüm" : ""),
        n == ShownRelease,
        new RelayCommand(() => PickRelease(n.Version)))).ToList();

    public string ReleaseHeading => ShownRelease is { } note
        ? note.Version + (note.Date is { } day ? " · " + day.ToString("d MMMM yyyy", Turkish) : "")
        : "";

    public IList<ReleaseSectionView> ReleaseSections => ShownRelease?.Sections
        .Where(s => s.Items.Count > 0)
        .Select(s => new ReleaseSectionView(
            s.Title.ToUpper(Turkish) + " · " + s.Items.Count.ToString(CultureInfo.InvariantCulture),
            s.Title switch { "Yeni" => "new", "Değişen" => "changed", "Düzeltilen" => "fixed", _ => "other" },
            s.Items.Select(ReleaseItem.Of).ToList()))
        .ToList() ?? new List<ReleaseSectionView>();

    /// <summary>Opening the window shows the version running again.</summary>
    private void ResetReleaseNotes()
    {
        _releaseVersion = null;
        NotifyRelease();
    }

    private void PickRelease(string version)
    {
        _releaseVersion = version;
        NotifyRelease();
    }

    private void NotifyRelease()
    {
        Notify(nameof(ReleaseTabs));
        Notify(nameof(ReleaseHeading));
        Notify(nameof(ReleaseSections));
    }
}
