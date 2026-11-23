using System.Windows;

namespace IsGunlugu;

/// <summary>
/// The bar under the Bugün title, as on the phone: how many of today's tasks are finished, with "5/9" beside it.
/// A task moved on today without finishing adds its finished share in yellow. Shown on the Bugün page only, and
/// only when today has work.
/// </summary>
public sealed partial class MainViewModel
{
    public bool HasDayProgress => IsTodayView && TodayCounts()[1] > 0;

    public bool HasDayDone => TodayCounts()[0] > 0;

    public bool HasDayPart => TodayCounts()[2] > 0;

    /// <summary>" · 1 ilerledi" after the count, when a task moved on today.</summary>
    public string DayProgressedText => ProgressedOn(_today).Count is var n and > 0 ? $" · {n} ilerledi" : "";

    public string DayProgressText
    {
        get
        {
            var counts = TodayCounts();
            return $"{counts[0]}/{counts[1]}";
        }
    }

    public string DayProgressName
    {
        get
        {
            var counts = TodayCounts();
            return $"Bugünkü {counts[1]} işin {counts[0]} tanesi bitti" + (ProgressedOn(_today).Count is var n and > 0 ? $", {n} tanesi ilerledi" : "");
        }
    }

    public GridLength DayDoneWidth => new(Math.Max(TodayCounts()[0], 0.0001), GridUnitType.Star);

    public GridLength DayPartWidth => new(Math.Max(TodayCounts()[2], 0.0001), GridUnitType.Star);

    public GridLength DayLeftWidth
    {
        get
        {
            var counts = TodayCounts();
            return new GridLength(Math.Max(counts[1] - counts[0] - counts[2], 0.0001), GridUnitType.Star);
        }
    }

    /// <summary>
    /// Finished tasks, all of today's tasks, and the share of the tasks moved on today: for each, the sub-tasks
    /// finished today over all its sub-tasks in play. A task moved on today that sits under another day counts too.
    /// </summary>
    private double[] TodayCounts()
    {
        var entries = _document.Days.Where(d => d.Date == _today).SelectMany(d => d.Entries).ToList();
        var progressed = ProgressedOn(_today);
        var home = _document.Days.SelectMany(d => d.Entries.Select(e => new { e, d.Date })).Where(x => progressed.Contains(x.e)).ToDictionary(x => x.e, x => x.Date);
        var part = progressed.Sum(e => (double)e.ActiveSubTasks.Count(t => t.IsDone && t.DoneOn(home[e], _today) == _today) / Math.Max(1, e.ActiveSubTasks.Count));
        // What was given up is no longer part of the day's work, so the bar fills over the rest.
        var total = entries.Count(e => !e.IsDropped) + progressed.Count(e => !entries.Contains(e));
        return new[] { entries.Count(e => e.IsFinished), total, part };
    }

    private void NotifyDayProgress()
    {
        Notify(nameof(HasDayProgress));
        Notify(nameof(HasDayDone));
        Notify(nameof(DayProgressText));
        Notify(nameof(DayProgressName));
        Notify(nameof(DayDoneWidth));
        Notify(nameof(DayPartWidth));
        Notify(nameof(HasDayPart));
        Notify(nameof(DayProgressedText));
        Notify(nameof(DayLeftWidth));
    }
}
