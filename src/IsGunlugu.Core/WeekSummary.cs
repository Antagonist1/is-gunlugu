using System.Globalization;
using System.Text;

namespace IsGunlugu.Core;

/// <summary>
/// A week's finished work as plain text, day by day, to paste into a mail or a timesheet: finished tasks with
/// their finished sub-tasks, partly done tasks with their count. Open work, notes and empty days stay out.
/// </summary>
public static class WeekSummary
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    public static string Build(NoteDocument document, DateTime monday) => Build(document, monday, DateTime.Today);

    /// <summary>
    /// Each day lists what was finished on it: whole tasks, and tasks with sub-tasks finished that day, with those
    /// sub-tasks. A task worked on over several days shows on each of them, with how far along it was that evening.
    /// </summary>
    public static string Build(NoteDocument document, DateTime monday, DateTime today)
    {
        var days = DaysWithWork(document, monday, today);
        if (days.Count == 0) return "";

        var text = new StringBuilder().Append(Title(monday)).Append(" · biten işler\r\n");
        foreach (var day in days)
        {
            text.Append("\r\n").Append(day.Key.ToString("dddd, d MMMM", Turkish)).Append("\r\n");
            foreach (var work in day.Value)
            {
                var tasks = work.Entry.ActiveSubTasks;
                text.Append("- ").Append(work.Entry.Text);
                if (!work.EntryDoneHere && tasks.Count > 0) text.Append($" ({DayWork.DoneBy(work.Entry, work.Home, day.Key, today)}/{tasks.Count})");
                text.Append("\r\n");
                foreach (var task in work.DoneHere) text.Append("  - ").Append(task.Text).Append("\r\n");
            }
        }
        return text.ToString();
    }

    /// <summary>How many tasks the summary names.</summary>
    public static int Count(NoteDocument document, DateTime monday) => DaysWithWork(document, monday, DateTime.Today).Sum(d => d.Value.Count);

    private static List<KeyValuePair<DateTime, List<WorkElsewhere>>> DaysWithWork(NoteDocument document, DateTime monday, DateTime today) =>
        Enumerable.Range(0, 7)
            .Select(i => monday.Date.AddDays(i))
            .Select(date => new KeyValuePair<DateTime, List<WorkElsewhere>>(date, DayWork.On(document, date, today)))
            .Where(d => d.Value.Count > 0)
            .ToList();

    private static string Title(DateTime monday)
    {
        var sunday = monday.AddDays(6);
        if (monday.Year != sunday.Year) return $"{monday.ToString("d MMMM yyyy", Turkish)} – {sunday.ToString("d MMMM yyyy", Turkish)}";
        if (monday.Month != sunday.Month) return $"{monday.ToString("d MMMM", Turkish)} – {sunday.ToString("d MMMM yyyy", Turkish)}";
        return $"{monday.Day} – {sunday.ToString("d MMMM yyyy", Turkish)}";
    }
}
