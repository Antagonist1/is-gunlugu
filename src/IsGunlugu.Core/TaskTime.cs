using System.Globalization;
using System.Text.RegularExpressions;

namespace IsGunlugu.Core;

/// <summary>
/// A task's time and up to two reminders before it, kept at the end of its line: "🚨Toplantı ⏰[14:00 30dk 15dk]".
/// Readable in Notepad; the phone reads it through this same code, so a title edited there keeps it.
/// </summary>
public sealed class TaskTime
{
    public const int MaxReminders = 2;
    public static readonly TimeSpan Longest = TimeSpan.FromHours(24);

    private static readonly Regex AtEnd = new(@"\s*⏰\[(?<at>\d{1,2}:\d{2})(?<before>(?:\s+[^\s\]]+)*)\s*\]\s*$");
    private static readonly Regex SpanPattern = new(@"^(?:(?<h>\d{1,2})sa)?(?:(?<m>\d{1,4})dk)?$", RegexOptions.IgnoreCase);

    public TaskTime(TimeSpan at, IEnumerable<TimeSpan> before)
    {
        At = at;
        Before = before.Where(b => b >= TimeSpan.Zero && b <= Longest).Distinct()
            .OrderByDescending(b => b).Take(MaxReminders).ToList();
    }

    public TimeSpan At { get; }

    /// <summary>How long before the time each reminder rings, the earliest first.</summary>
    public IList<TimeSpan> Before { get; }

    public string AtText => At.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>The mark from a line's end, or null when there is none or it does not read as a time.</summary>
    public static TaskTime? Read(string text)
    {
        var match = AtEnd.Match(text);
        if (!match.Success || !TryTime(match.Groups["at"].Value, out var at)) return null;

        var before = new List<TimeSpan>();
        foreach (var word in match.Groups["before"].Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            if (TrySpan(word, out var span)) before.Add(span);
        return new TaskTime(at, before);
    }

    /// <summary>Moves a valid mark at the end of a task's title into <see cref="Entry.Alarm"/>.</summary>
    internal static void Split(Entry entry)
    {
        var match = AtEnd.Match(entry.Text);
        if (!match.Success || Read(match.Value) is null) return;
        entry.Alarm = match.Value;
        entry.Text = entry.Text.Substring(0, match.Index);
    }

    public string Write() =>
        " ⏰[" + string.Join(" ", new[] { AtText }.Concat(Before.Select(Span)).ToArray()) + "]";

    /// <summary>A span as written in the file: 30dk, 1sa, 1sa30dk, 0dk.</summary>
    public static string Span(TimeSpan span)
    {
        var hours = (int)span.TotalHours;
        return hours == 0 ? $"{span.Minutes}dk" : span.Minutes == 0 ? $"{hours}sa" : $"{hours}sa{span.Minutes}dk";
    }

    /// <summary>A span as shown: "30 dk önce", "1 sa 30 dk önce", "1 gün önce", "Tam saatinde".</summary>
    public static string SpanText(TimeSpan span)
    {
        if (span == TimeSpan.Zero) return "Tam saatinde";
        if (span == Longest) return "1 gün önce";
        var hours = (int)span.TotalHours;
        var parts = new List<string>();
        if (hours > 0) parts.Add($"{hours} sa");
        if (span.Minutes > 0) parts.Add($"{span.Minutes} dk");
        return string.Join(" ", parts.ToArray()) + " önce";
    }

    /// <summary>"30 dk sonra", "1 sa sonra", "Şimdi": how far the time is, when a reminder rings.</summary>
    public static string LeftText(TimeSpan span) =>
        span == TimeSpan.Zero ? "Şimdi" : SpanText(span).Replace(" önce", " sonra");

    public static bool TryTime(string text, out TimeSpan at)
    {
        at = default;
        var parts = text.Split(':');
        if (parts.Length != 2 || parts[1].Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m)) return false;
        if (h > 23 || m > 59) return false;
        at = new TimeSpan(h, m, 0);
        return true;
    }

    private static bool TrySpan(string word, out TimeSpan span)
    {
        span = default;
        var match = SpanPattern.Match(word);
        if (!match.Success || word.Length == 0) return false;
        var h = match.Groups["h"].Success ? int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture) : 0;
        var m = match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        span = new TimeSpan(h, m, 0);
        return true;
    }
}
