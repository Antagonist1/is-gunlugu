using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>What the small time label after a task shows: "14:00", grey once the time is past or the task is done.</summary>
public sealed class TimeChip
{
    private TimeChip(string text, bool isPast, bool hasReminders, string tip)
    {
        Text = text;
        IsPast = isPast;
        HasReminders = hasReminders;
        Tip = tip;
    }

    public string Text { get; }
    public bool IsPast { get; }
    public bool HasReminders { get; }
    public string Tip { get; }

    /// <summary>The label for a task under <paramref name="date"/>, or null when it has no time.</summary>
    public static TimeChip? Of(Entry entry, DateTime date, DateTime now)
    {
        if (entry.Time is not { } time) return null;
        var past = entry.Status != EntryStatus.Open || (date != Day.Undated && date.Date + time.At < now);
        var tip = time.Before.Count == 0 ? "Hatırlatma yok"
            : "Hatırlatma: " + string.Join(", ", time.Before.Select(TaskTime.SpanText).ToArray());
        return new TimeChip(time.AtText, past, time.Before.Count > 0, tip);
    }

    public bool LooksLike(TimeChip? other) =>
        other is not null && Text == other.Text && IsPast == other.IsPast && HasReminders == other.HasReminders;
}
