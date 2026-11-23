using System.Globalization;
using System.Windows.Input;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>
/// The evening reminder in the settings: on or off, and the time, half an hour at a time. The app's timer asks
/// <see cref="TakeDayEndReminder"/> every minute; it answers once a day, when today still has open work.
/// </summary>
public sealed partial class MainViewModel
{
    public ICommand EarlierDayEndCommand { get; private set; } = null!;
    public ICommand LaterDayEndCommand { get; private set; } = null!;

    public bool DayEndOn
    {
        get => _settings.DayEndReminder ?? true;
        set
        {
            _settings.DayEndReminder = value;
            if (!_pathsFromCommandLine) _settings.Save();
            Notify();
        }
    }

    public string DayEndTimeText => DayEndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private TimeSpan DayEndTime =>
        TimeSpan.TryParseExact(_settings.DayEndTime ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out var at) ? at : DayEndReminder.DefaultTime;

    private void InitDayEnd()
    {
        EarlierDayEndCommand = new RelayCommand(() => SetDayEndTime(DayEndReminder.Step(DayEndTime, -1)));
        LaterDayEndCommand = new RelayCommand(() => SetDayEndTime(DayEndReminder.Step(DayEndTime, 1)));
    }

    private void SetDayEndTime(TimeSpan at)
    {
        _settings.DayEndTime = at.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        if (!_pathsFromCommandLine) _settings.Save();
        Notify(nameof(DayEndTimeText));
    }

    /// <summary>Title and text for the tray when the reminder is due now, else null. Marks the day as reminded.</summary>
    public string[]? TakeDayEndReminder(DateTime now)
    {
        if (!DayEndOn || !HasFile) return null;

        var open = TodayOpenEntries;
        DateTime? shownOn = DateTime.TryParseExact(_settings.DayEndShownOn ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;
        if (!DayEndReminder.IsDue(now, DayEndTime, shownOn, open.Count)) return null;

        _settings.DayEndShownOn = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!_pathsFromCommandLine) _settings.Save();
        return new[]
        {
            $"Bugün {open.Count} iş açık kaldı",
            DayEndReminder.Message(open.Select(e => e.Text).ToList()) + "\nBugünün işleri için tıkla.",
        };
    }
}
