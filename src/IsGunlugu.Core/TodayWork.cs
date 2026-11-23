namespace IsGunlugu.Core;

/// <summary>One line of the home-screen widget: work of today not finished yet.</summary>
public sealed class WorkLine(int index, string text, bool isPartlyDone, string progress)
{
    /// <summary>Position among the day's entries, so a tick finds its entry again.</summary>
    public int Index { get; } = index;
    public string Text { get; } = text;
    public bool IsPartlyDone { get; } = isPartlyDone;
    public string Progress { get; } = progress;
}

public sealed class WidgetFit(int shown, int more)
{
    public int Shown { get; } = shown;
    public int More { get; } = more;
}

/// <summary>What the home-screen widget lists, and how much of it fits.</summary>
public static class TodayWork
{
    // The widget's own measures, in dp: its padding, the heading, one tickable line and the "+N iş daha" line.
    private const double Padding = 8;
    private const double Heading = 32;
    private const double Line = 48;
    private const double MoreLine = 24;

    /// <summary>The widget's layout has this many places for lines.</summary>
    public const int MaxLines = 12;

    /// <summary>Today's work that is not done or only partly done, in the order the day lists it.</summary>
    public static IList<WorkLine> Open(NoteDocument doc, DateTime today) =>
        TodaysEntries(doc, today)
            .Select((entry, index) => new { entry, index })
            .Where(x => x.entry.HasOpenWork)
            .Select(x => new WorkLine(x.index, x.entry.Text, x.entry.IsPartlyDone, Progress(x.entry)))
            .ToList();

    /// <summary>The entry a tick was meant for: at its old place if it is still there, otherwise by its text.</summary>
    public static Entry? Find(NoteDocument doc, DateTime today, int index, string text)
    {
        var entries = TodaysEntries(doc, today);
        if (index >= 0 && index < entries.Count && entries[index].Text == text && entries[index].HasOpenWork) return entries[index];
        return entries.FirstOrDefault(e => e.Text == text && e.HasOpenWork);
    }

    /// <summary>Lines that fit at <paramref name="heightDp"/>; when not all do, one line's worth goes to the count of the rest.</summary>
    public static WidgetFit Fit(double heightDp, int count)
    {
        var room = heightDp - 2 * Padding - Heading;
        var lines = Math.Min(MaxLines, Math.Max(0, (int)Math.Floor(room / Line)));
        if (count <= lines) return new WidgetFit(count, 0);

        var shown = Math.Min(MaxLines, Math.Max(0, (int)Math.Floor((room - MoreLine) / Line)));
        return new WidgetFit(shown, count - shown);
    }

    private static List<Entry> TodaysEntries(NoteDocument doc, DateTime today) =>
        doc.Days.Where(d => d.Date == today.Date).SelectMany(d => d.Entries).ToList();

    private static string Progress(Entry entry)
    {
        var tasks = entry.ActiveSubTasks;
        return tasks.Count == 0 ? "" : $"{tasks.Count(t => t.IsDone)}/{tasks.Count}";
    }
}
