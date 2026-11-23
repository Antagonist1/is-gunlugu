namespace IsGunlugu.Core;

/// <summary>
/// The evening note from the tray: once a day, from the chosen time until midnight, when today still has open work.
/// </summary>
public static class DayEndReminder
{
    public static readonly TimeSpan DefaultTime = new(17, 30, 0);

    private static readonly TimeSpan Earliest = new(12, 0, 0);
    private static readonly TimeSpan Latest = new(23, 45, 0);
    private const int MaxTitle = 40;

    public static bool IsDue(DateTime now, TimeSpan at, DateTime? shownOn, int openCount) =>
        openCount > 0 && now.TimeOfDay >= at && shownOn?.Date != now.Date;

    /// <summary>The time a step of a quarter of an hour later or earlier, kept between noon and a quarter to midnight.</summary>
    public static TimeSpan Step(TimeSpan at, int steps)
    {
        var next = at + TimeSpan.FromMinutes(15 * steps);
        return next < Earliest ? Earliest : next > Latest ? Latest : next;
    }

    /// <summary>"Rapor, Toplantı ve 1 iş daha": two names are enough to recall the day.</summary>
    public static string Message(IList<string> titles)
    {
        var named = titles.Take(2).Select(Cut).ToList();
        var rest = titles.Count - named.Count;
        if (rest > 0) return string.Join(", ", named.ToArray()) + $" ve {rest} iş daha";
        return named.Count == 2 ? $"{named[0]} ve {named[1]}" : named.FirstOrDefault() ?? "";
    }

    private static string Cut(string title)
    {
        var line = title.Trim();
        return line.Length <= MaxTitle ? line : line.Substring(0, MaxTitle).TrimEnd() + "…";
    }
}
