using System.Globalization;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>
/// Reminders before a task's time (⏰ in the file): the app's minute timer asks <see cref="TakeTimedReminder"/>,
/// which answers with the tray note to show, and remembers what it showed on this computer.
/// </summary>
public sealed partial class MainViewModel
{
    public const string DefaultReminders = "30dk 15dk";

    public bool TimedRemindersOn
    {
        get => _settings.TimedReminders ?? true;
        set
        {
            _settings.TimedReminders = value;
            if (!_pathsFromCommandLine) _settings.Save();
            Notify();
        }
    }

    /// <summary>What a task newly given a time gets, earliest first.</summary>
    public IList<TimeSpan> ReminderDefaults
    {
        get => TaskTime.Read($"⏰[00:00 {_settings.ReminderDefaults ?? DefaultReminders}]")!.Before;
        set
        {
            _settings.ReminderDefaults = string.Join(" ", new TaskTime(TimeSpan.Zero, value).Before.Select(TaskTime.Span).ToArray());
            if (!_pathsFromCommandLine) _settings.Save();
            Notify();
        }
    }

    /// <summary>
    /// "toplantı 14:00" typed for <paramref name="date"/>: the time leaves the title and goes into its mark, with the
    /// usual reminders, none when the time is already past today.
    /// </summary>
    public string WithTime(string text, DateTime date)
    {
        if (TimeInText.Find(text) is not { } found) return text;
        var past = date == _today && date + found.At <= DateTime.Now;
        var mark = new TaskTime(found.At, past ? new List<TimeSpan>() : ReminderDefaults).Write();
        var lineEnd = found.Text.IndexOf('\n');
        return lineEnd < 0 ? found.Text + mark : found.Text.Substring(0, lineEnd).TrimEnd('\r') + mark + found.Text.Substring(lineEnd);
    }

    /// <summary>The note to show now, or null: title, text, and what a click on it does.</summary>
    public (string Title, string Text, Action Open)? TakeTimedReminder(DateTime now)
    {
        if (!TimedRemindersOn || !HasFile || _store is null) return null;

        var rung = new HashSet<string>(_settings.RungReminders ?? new List<string>());
        var items = _document.Days.SelectMany(d => d.Entries.Select(e => (d.Date, e)));
        var rings = TimedReminder.Due(items, now, rung, _store.Path);
        if (rings.Count == 0) return null;

        // Kept a week: older ones can no longer ring, as their tasks' times are past.
        var weekAgo = now.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var kept = (_settings.RungReminders ?? new List<string>())
            .Where(k => string.CompareOrdinal(k.Split('|').ElementAtOrDefault(1) ?? "", weekAgo) >= 0).ToList();
        kept.AddRange(rings.SelectMany(r => r.Keys).Where(k => !kept.Contains(k)));
        _settings.RungReminders = kept;
        if (!_pathsFromCommandLine) _settings.Save();

        if (rings.Count > 1)
        {
            var titles = DayEndReminder.Message(rings.Select(r => r.Entry.Text).ToList());
            return ($"{rings.Count} iş yaklaşıyor", titles + "\nBugünün işleri için tıkla.", () => ShowDay(_today));
        }

        var ring = rings[0];
        var title = ring.Entry.Text.Trim();
        if (title.Length > 40) title = title.Substring(0, 40).TrimEnd() + "…";
        var at = ring.TaskAt.ToString("HH:mm", CultureInfo.InvariantCulture);
        var left = TaskTime.LeftText(ring.Before);
        return ($"{title} · {at}", $"{left}. İşi açmak için tıkla.", () =>
        {
            ShowDay(ring.Date);
            if (_document.Days.Any(d => d.Entries.Contains(ring.Entry))) OpenDetail(ring.Entry);
        });
    }
}
