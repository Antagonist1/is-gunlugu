using System.Globalization;
using System.Windows.Input;
using IsGunlugu.Core;

namespace IsGunlugu;

public enum CalendarScale { Week, Month, Year }

/// <summary>One task on a day of the week page, coloured by how far it got. It can be dragged to another day.</summary>
public sealed class WeekLine(Entry entry, DateTime date, bool isPartlyDone, bool isInDetail = false)
{
    public Entry Entry { get; } = entry;
    public DateTime Date { get; } = date;
    public string Text { get; } = entry.Text;
    public bool IsOpen { get; } = entry.HasOpenWork;
    public bool IsPartlyDone { get; } = isPartlyDone;
    public bool IsDropped { get; } = entry.IsDropped;
    /// <summary>The task open in the panel on the right.</summary>
    public bool IsInDetail { get; } = isInDetail;
    public bool IsDone => !IsOpen;
    public TimeChip? Chip { get; } = TimeChip.Of(entry, date, DateTime.Now);
}

/// <summary>One day of the week page: the date on the left, the work in full on the right.</summary>
public sealed class WeekDayCard(DateTime date, bool isToday, IList<WeekLine> lines, string name, ICommand open) : ObservableObject
{
    private bool _isDropTarget;

    /// <summary>A task dragged from another day is over this one.</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set { _isDropTarget = value; Notify(); }
    }

    public DateTime Date { get; } = date;
    public int Day { get; } = date.Day;
    public string DayName { get; } = date.ToString("ddd", new CultureInfo("tr-TR")).ToUpper(new CultureInfo("tr-TR"));
    public bool IsWeekend { get; } = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public bool IsToday { get; } = isToday;
    public IList<WeekLine> Lines { get; } = lines;
    public bool HasWork => Lines.Count > 0;
    public string Name { get; } = name;
    public ICommand OpenCommand { get; } = open;
}

/// <summary>A square on the year page: a day coloured by its work, or an empty place before the 1st.</summary>
public sealed class YearDayCell
{
    public static readonly YearDayCell Blank = new();

    private YearDayCell() => IsBlank = true;

    public YearDayCell(DateTime date, int heatLevel, bool hasOpen, bool isToday, string name, ICommand open)
    {
        Day = date.Day;
        HeatLevel = heatLevel;
        HasOpen = hasOpen;
        IsToday = isToday;
        IsWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        Name = name;
        OpenCommand = open;
    }

    public bool IsBlank { get; }
    public int Day { get; }
    public int HeatLevel { get; }
    public bool HasOpen { get; }
    public bool IsToday { get; }
    public bool IsWeekend { get; }
    public string Name { get; } = "";
    public ICommand? OpenCommand { get; }
}

/// <summary>One small month on the year page.</summary>
public sealed class YearMonthCard(string name, bool isCurrent, IList<YearDayCell> cells, ICommand open)
{
    public string Name { get; } = name;
    public bool IsCurrent { get; } = isCurrent;
    public IList<YearDayCell> Cells { get; } = cells;
    public ICommand OpenCommand { get; } = open;
}

/// <summary>
/// The calendar page at three sizes, as on the phone: a week with the work written out, a month, and the year as
/// twelve small months. The arrows and Alt+Left/Right step by the size shown.
/// </summary>
public sealed partial class MainViewModel
{
    private CalendarScale _calendarScale = CalendarScale.Month;
    private DateTime _visibleWeek;
    private IList<WeekDayCard> _weekCards = new List<WeekDayCard>();
    private IList<YearMonthCard> _yearCards = new List<YearMonthCard>();

    public ICommand ShowWeekScaleCommand { get; private set; } = null!;
    public ICommand ShowMonthScaleCommand { get; private set; } = null!;
    public ICommand ShowYearScaleCommand { get; private set; } = null!;
    public ICommand CalendarNowCommand { get; private set; } = null!;
    public ICommand CopyWeekSummaryCommand { get; private set; } = null!;

    public bool IsWeekScale => IsCalendarView && _calendarScale == CalendarScale.Week;
    public bool IsMonthScale => IsCalendarView && _calendarScale == CalendarScale.Month;
    public bool IsYearScale => IsCalendarView && _calendarScale == CalendarScale.Year;

    public string CalendarNowText => _calendarScale switch
    {
        CalendarScale.Week => "Bu hafta",
        CalendarScale.Year => "Bu yıl",
        _ => "Bu ay",
    };

    /// <summary>The calendar is away from today's week, month or year, so the way back is worth showing.</summary>
    public bool IsCalendarAway => IsCalendarView && _calendarScale switch
    {
        CalendarScale.Week => _visibleWeek != CalendarNav.MondayOf(_today),
        CalendarScale.Year => _visibleMonth.Year != _today.Year,
        _ => _visibleMonth != FirstOfMonth(_today),
    };

    public IList<WeekDayCard> WeekCards
    {
        get => _weekCards;
        private set { _weekCards = value; Notify(); }
    }

    public IList<YearMonthCard> YearCards
    {
        get => _yearCards;
        private set { _yearCards = value; Notify(); }
    }

    private void InitCalendarScales()
    {
        _visibleWeek = CalendarNav.MondayOf(_today);
        if (Enum.IsDefined(typeof(CalendarScale), _settings.CalendarScale ?? "")) _calendarScale = (CalendarScale)Enum.Parse(typeof(CalendarScale), _settings.CalendarScale!);
        ShowWeekScaleCommand = new RelayCommand(() => SetCalendarScale(CalendarScale.Week));
        ShowMonthScaleCommand = new RelayCommand(() => SetCalendarScale(CalendarScale.Month));
        ShowYearScaleCommand = new RelayCommand(() => SetCalendarScale(CalendarScale.Year));
        CopyWeekSummaryCommand = new RelayCommand(CopyWeekSummary);
        CalendarNowCommand = new RelayCommand(() =>
        {
            _visibleWeek = CalendarNav.MondayOf(_today);
            _visibleMonth = FirstOfMonth(_today);
            Rebuild();
        });
    }

    /// <summary>A task dragged on the week page to another day. Finished work may move too; its day is a fact of the past.</summary>
    public void MoveFromWeek(Entry entry, DateTime to)
    {
        if (!_document.Days.Any(d => d.Entries.Contains(entry)) || DateOf(entry) == to) return;
        Mutate(doc => doc.MoveEntry(entry, to, null),
            to == _today ? "Bugüne alındı." : to == Tomorrow ? "Yarına taşındı." : $"{to.ToString("d MMMM dddd", Turkish)} gününe taşındı.");
    }

    /// <summary>The week on screen, finished work day by day, onto the clipboard for a mail or a timesheet.</summary>
    private void CopyWeekSummary()
    {
        var text = WeekSummary.Build(_document, _visibleWeek);
        if (text.Length == 0)
        {
            StatusText = "Bu haftada biten iş yok, kopyalanacak bir şey olmadı.";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(text);
            StatusText = $"Haftanın özeti panoya kopyalandı: {WeekSummary.Count(_document, _visibleWeek)} iş. Bir yere yapıştırabilirsin.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            StatusText = "Pano başka bir program tarafından kullanılıyor, birazdan tekrar dene.";
        }
    }

    private void SetCalendarScale(CalendarScale scale)
    {
        if (scale == CalendarScale.Week && _calendarScale != CalendarScale.Week) _visibleWeek = CalendarNav.WeekFor(_visibleMonth, _today);
        if (scale != CalendarScale.Week && _calendarScale == CalendarScale.Week) _visibleMonth = FirstOfMonth(_visibleWeek.AddDays(3));
        _calendarScale = scale;
        ResetPaneLists();
        RememberCalendarScale();
        if (_mode != ViewMode.Calendar) Navigate(ViewMode.Calendar);
        else Rebuild();
    }

    private void OpenMonth(DateTime month)
    {
        _visibleMonth = month;
        _calendarScale = CalendarScale.Month;
        RememberCalendarScale();
        Rebuild();
    }

    /// <summary>The next time the calendar opens, it opens at this size, also after a restart.</summary>
    private void RememberCalendarScale()
    {
        if (_settings.CalendarScale == _calendarScale.ToString()) return;
        _settings.CalendarScale = _calendarScale.ToString();
        if (!_pathsFromCommandLine) _settings.Save();
    }

    /// <summary>The arrows on the calendar page: a week, a month or a year at a time.</summary>
    private void ShiftCalendar(int step)
    {
        switch (_calendarScale)
        {
            case CalendarScale.Week:
                _visibleWeek = _visibleWeek.AddDays(7 * step);
                _visibleMonth = FirstOfMonth(_visibleWeek.AddDays(3));
                Rebuild();
                break;
            case CalendarScale.Year:
                ShiftMonth(12 * step);
                break;
            default:
                ShiftMonth(step);
                break;
        }
    }

    private void BuildCalendar(List<DayEntries> byDate)
    {
        var lookup = byDate.ToDictionary(d => d.Date, d => d.Entries);
        Groups = [];

        if (_calendarScale == CalendarScale.Week)
        {
            var days = Enumerable.Range(0, 7).Select(i => _visibleWeek.AddDays(i)).ToList();
            WeekCards = days.Select(date =>
            {
                var entries = lookup.TryGetValue(date, out var found) ? found : new List<Entry>();
                // Partly done first, then open, then finished, as on the Bugün page, even when the file was edited elsewhere.
                var lines = entries.OrderBy(e => e.StateRank).Select(e => new WeekLine(e, date, e.IsPartlyDone, IsInDetail(e))).ToList();
                return new WeekDayCard(date, date == _today, lines, date.ToString("d MMMM dddd", Turkish) + ", " + Counts(entries, ProgressedOn(date)), new RelayCommand(() => OpenDay(date)));
            }).ToList();

            var last = days[6];
            Title = _visibleWeek.Month == last.Month
                ? $"{_visibleWeek.Day} – {last.ToString("d MMMM yyyy", Turkish)}"
                : _visibleWeek.Year == last.Year
                    ? $"{_visibleWeek.ToString("d MMMM", Turkish)} – {last.ToString("d MMMM yyyy", Turkish)}"
                    : $"{_visibleWeek.ToString("d MMMM yyyy", Turkish)} – {last.ToString("d MMMM yyyy", Turkish)}";
            var inWeek = days.SelectMany(d => lookup.TryGetValue(d, out var found) ? found : new List<Entry>()).ToList();
            var weekProgressed = ProgressedIn(_visibleWeek, last);
            Subtitle = inWeek.Count == 0 && weekProgressed.Count == 0 ? "Bu haftada not yok" : Counts(inWeek, weekProgressed);
        }
        else if (_calendarScale == CalendarScale.Year)
        {
            var year = _visibleMonth.Year;
            var worked = DayWork.WorkedByDay(_document, _today);
            YearCards = Enumerable.Range(1, 12).Select(month =>
            {
                var first = new DateTime(year, month, 1);
                var cells = Enumerable.Repeat(YearDayCell.Blank, CalendarNav.LeadingBlanks(year, month)).ToList();
                for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
                {
                    var date = new DateTime(year, month, day);
                    var entries = lookup.TryGetValue(date, out var found) ? found : new List<Entry>();
                    var done = worked.TryGetValue(date, out var count) ? count : 0;
                    cells.Add(new YearDayCell(date, done switch { 0 => 0, <= 2 => 1, <= 4 => 2, _ => 3 },
                        entries.Any(e => e.HasOpenWork), date == _today, date.ToString("d MMMM dddd", Turkish) + ", " + Counts(entries, ProgressedOn(date)),
                        new RelayCommand(() => OpenDay(date))));
                }
                return new YearMonthCard(first.ToString("MMMM", Turkish), year == _today.Year && month == _today.Month, cells, new RelayCommand(() => OpenMonth(first)));
            }).ToList();

            Title = year.ToString(CultureInfo.InvariantCulture);
            var inYear = byDate.Where(d => d.Date.Year == year && d.Date != Day.Undated).SelectMany(d => d.Entries).ToList();
            Subtitle = inYear.Count == 0 ? "Bu yılda not yok" : Counts(inYear, ProgressedIn(new DateTime(year, 1, 1), new DateTime(year, 12, 31)));
        }
        else
        {
            var entries = byDate.Where(d => FirstOfMonth(d.Date) == _visibleMonth).SelectMany(d => d.Entries).ToList();
            Title = MonthTitle;
            Subtitle = entries.Count == 0 ? "Bu ayda not yok" : Counts(entries, ProgressedIn(_visibleMonth, _visibleMonth.AddMonths(1).AddDays(-1)));
        }
    }

    private void NotifyCalendarScale()
    {
        Notify(nameof(IsWeekScale));
        Notify(nameof(IsMonthScale));
        Notify(nameof(IsYearScale));
        Notify(nameof(CalendarNowText));
        Notify(nameof(IsCalendarAway));
    }
}
