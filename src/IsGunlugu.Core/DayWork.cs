namespace IsGunlugu.Core;

/// <summary>A task that sits under another day but had something finished on the day asked about.</summary>
public sealed class WorkElsewhere(Entry entry, DateTime home, bool entryDoneHere, IList<SubTask> doneHere)
{
    public Entry Entry { get; } = entry;

    /// <summary>The day the task sits under now (<see cref="Day.Undated"/> for the undated ones).</summary>
    public DateTime Home { get; } = home;

    /// <summary>The task itself was finished on the day asked about.</summary>
    public bool EntryDoneHere { get; } = entryDoneHere;

    /// <summary>Its sub-tasks finished on the day asked about, in file order.</summary>
    public IList<SubTask> DoneHere { get; } = doneHere;
}

/// <summary>
/// What was done on a day, by the finish dates in the marks: a task moved on to a later day still counts on the
/// days its sub-tasks were finished. Marks without a date count on the day they sit under.
/// </summary>
public static class DayWork
{
    public static IList<WorkElsewhere> Elsewhere(NoteDocument document, DateTime date, DateTime today) =>
        document.Days
            .Where(day => day.Date != date.Date)
            .SelectMany(day => day.Entries.Select(entry => Work(entry, day.Date, date.Date, today)))
            .Where(work => work is not null)
            .Select(work => work!)
            .ToList();

    /// <summary>Everything worked on that day, wherever it sits now, in file order.</summary>
    public static List<WorkElsewhere> On(NoteDocument document, DateTime date, DateTime today) =>
        document.Days
            .SelectMany(day => day.Entries.Select(entry => Work(entry, day.Date, date.Date, today)))
            .Where(work => work is not null)
            .Select(work => work!)
            .ToList();

    /// <summary>How many tasks had the task itself or a sub-task finished on <paramref name="date"/>; each counts once.</summary>
    public static int Worked(NoteDocument document, DateTime date, DateTime today) =>
        document.Days.Sum(day => day.Entries.Count(entry => Work(entry, day.Date, date.Date, today) is not null));

    /// <summary>The <see cref="Worked"/> count of every day with work, in one pass over the file (for a calendar).</summary>
    public static Dictionary<DateTime, int> WorkedByDay(NoteDocument document, DateTime today)
    {
        var counts = new Dictionary<DateTime, int>();
        foreach (var day in document.Days)
        {
            foreach (var entry in day.Entries)
            {
                if (entry.Status == EntryStatus.Note) continue;

                var days = new HashSet<DateTime>();
                if (entry.Status == EntryStatus.Done) days.Add(entry.DoneOn(day.Date, today));
                foreach (var task in entry.ActiveSubTasks.Where(t => t.IsDone)) days.Add(task.DoneOn(day.Date, today));
                foreach (var date in days) counts[date] = counts.TryGetValue(date, out var n) ? n + 1 : 1;
            }
        }
        return counts;
    }

    /// <summary>
    /// Tasks still open that had a sub-task finished on a day, wherever they sit, by that day: the work that moved a
    /// task on without finishing it ("ilerledi" in the counts). One pass over the file.
    /// </summary>
    public static Dictionary<DateTime, List<Entry>> ProgressedByDay(NoteDocument document, DateTime today)
    {
        var map = new Dictionary<DateTime, List<Entry>>();
        foreach (var day in document.Days)
        {
            foreach (var entry in day.Entries)
            {
                if (entry.Status == EntryStatus.Note || entry.IsDropped || !entry.HasOpenWork) continue;
                foreach (var date in entry.ActiveSubTasks.Where(t => t.IsDone).Select(t => t.DoneOn(day.Date, today)).Distinct())
                {
                    if (!map.TryGetValue(date, out var list)) map[date] = list = new List<Entry>();
                    list.Add(entry);
                }
            }
        }
        return map;
    }

    /// <summary>How many of the task's sub-tasks in play were finished by the end of <paramref name="date"/>.</summary>
    public static int DoneBy(Entry entry, DateTime home, DateTime date, DateTime today) =>
        entry.ActiveSubTasks.Count(t => t.IsDone && t.DoneOn(home, today) <= date.Date);

    private static WorkElsewhere? Work(Entry entry, DateTime home, DateTime date, DateTime today)
    {
        if (entry.Status == EntryStatus.Note) return null;

        var entryDone = entry.Status == EntryStatus.Done && entry.DoneOn(home, today) == date;
        var subTasks = entry.ActiveSubTasks.Where(t => t.IsDone && t.DoneOn(home, today) == date).ToList();
        return entryDone || subTasks.Count > 0 ? new WorkElsewhere(entry, home, entryDone, subTasks) : null;
    }
}
