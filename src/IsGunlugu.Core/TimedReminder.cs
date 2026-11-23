using System.Globalization;

namespace IsGunlugu.Core;

/// <summary>
/// Which task reminders ring now. A reminder rings from its moment until ten minutes past the task's time, once;
/// of the ones missed while the computer was off only the latest comes, and the earlier ones are closed with it.
/// Done, given up and undated tasks stay silent.
/// </summary>
public static class TimedReminder
{
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    public sealed class Ring(Entry entry, DateTime date, DateTime taskAt, TimeSpan before, IList<string> keys)
    {
        public Entry Entry { get; } = entry;
        public DateTime Date { get; } = date;
        public DateTime TaskAt { get; } = taskAt;
        public TimeSpan Before { get; } = before;

        /// <summary>To keep as rung: this reminder and the earlier ones it stands for.</summary>
        public IList<string> Keys { get; } = keys;
    }

    public static IList<Ring> Due(IEnumerable<(DateTime date, Entry entry)> items, DateTime now, ISet<string> rung, string file)
    {
        var rings = new List<Ring>();
        foreach (var (date, entry) in items)
        {
            if (date == Day.Undated || entry.Status != EntryStatus.Open || entry.Time is not { } time) continue;

            var taskAt = date.Date + time.At;
            if (now >= taskAt + Grace) continue;

            // Before is earliest first, so the last one due is the one to show.
            var due = time.Before.Where(b => now >= taskAt - b).ToList();
            var keys = due.Select(b => Key(file, taskAt, b, entry.Text)).ToList();
            if (due.Count == 0 || keys.All(rung.Contains)) continue;
            rings.Add(new Ring(entry, date, taskAt, due[due.Count - 1], keys));
        }
        return rings;
    }

    public static string Key(string file, DateTime taskAt, TimeSpan before, string title) =>
        string.Join("|", file, taskAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), TaskTime.Span(before), title);
}
