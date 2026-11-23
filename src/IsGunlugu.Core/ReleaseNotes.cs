using System.Globalization;
using System.Text.RegularExpressions;

namespace IsGunlugu.Core;

public sealed class ReleaseSection(string title, IList<string> items)
{
    public string Title { get; } = title;
    public IList<string> Items { get; } = items;
}

/// <summary>
/// One item on the screen: a short head in bold and the rest faint under it, split at the first ":", ";" or full stop when what
/// comes before is short enough to be a head (a time like 14:00 is no split). Otherwise the item stays one line.
/// </summary>
public sealed class ReleaseItem(string head, string detail)
{
    private const int LongestHead = 80;
    private static readonly CultureInfo Turkish = new("tr-TR");
    private static readonly Regex Split = new(@"[:;](?!\d)|\.(?=\s+\p{Lu})");

    public string Head { get; } = head;
    public string Detail { get; } = detail;
    public bool HasDetail => Detail.Length > 0;

    public static ReleaseItem Of(string item)
    {
        var match = Split.Match(item);
        if (!match.Success || match.Index < 3 || match.Index > LongestHead) return new ReleaseItem(item, "");
        var rest = item.Substring(match.Index + 1).Trim();
        if (rest.Length == 0) return new ReleaseItem(item, "");
        return new ReleaseItem(item.Substring(0, match.Index).Trim(), char.ToUpper(rest[0], Turkish) + rest.Substring(1));
    }
}

public sealed class ReleaseNote(string version, DateTime? date, IList<ReleaseSection> sections)
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    public string Version { get; } = version;
    public DateTime? Date { get; } = date;
    public IList<ReleaseSection> Sections { get; } = sections;

    public string Title => Date is DateTime day ? $"{Version} · {day.ToString("d MMMM yyyy", Turkish)}" : Version;
}

/// <summary>Reads a Keep a Changelog file. Unreleased work is left out: it is not in the program yet.</summary>
public static class ReleaseNotes
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    private static readonly Regex VersionHeader = new(@"^##\s+\[?(?<version>[^\]\s]+)\]?(\s+-\s+(?<date>\d{4}-\d{2}-\d{2}))?\s*$");

    /// <summary>"[0.8.5.3]" at the end of an item: the build it came with, for the file's readers, not the screen.</summary>
    private static readonly Regex BuildTag = new(@"\s*\[\d+\.\d+\.\d+\.\d+\]\s*$");

    private static readonly Dictionary<string, string> SectionTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Added"] = "Yeni",
        ["Changed"] = "Değişen",
        ["Deprecated"] = "Kalkacak",
        ["Removed"] = "Kaldırılan",
        ["Fixed"] = "Düzeltilen",
        ["Security"] = "Güvenlik",
    };

    public static IList<ReleaseNote> Parse(string text)
    {
        var notes = new List<ReleaseNote>();
        List<ReleaseSection>? sections = null;
        List<string>? items = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r', ' ');
            var header = VersionHeader.Match(line);
            if (header.Success)
            {
                items = null;
                sections = null;

                var version = header.Groups["version"].Value;
                if (version.Equals("Unreleased", StringComparison.OrdinalIgnoreCase)) continue;

                sections = new List<ReleaseSection>();
                notes.Add(new ReleaseNote(version, ParseDate(header.Groups["date"].Value), sections));
            }
            else if (line.StartsWith("### ") && sections is not null)
            {
                var name = line.Substring(4).Trim();
                items = new List<string>();
                sections.Add(new ReleaseSection(SectionTitles.TryGetValue(name, out var title) ? title : name, items));
            }
            else if (line.StartsWith("- ") && items is not null)
            {
                items.Add(Capitalize(BuildTag.Replace(line.Substring(2), "").Trim()));
            }
        }

        return notes;
    }

    private static DateTime? ParseDate(string text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    // The file keeps entries lowercase as the format asks; the screen shows them as sentences.
    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Turkish) + text.Substring(1);
}
