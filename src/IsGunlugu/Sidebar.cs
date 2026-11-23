using IsGunlugu.Core;
using System.Windows.Input;

namespace IsGunlugu;

public enum SidebarSection { Tasks, Calendar }

/// <summary>
/// The left panel in two tiers: a narrow strip of sections (İşler, Takvim) and beside it the list of the chosen one,
/// with the file picker on top. On a narrow window only the strip stays; a section then opens its list over the
/// middle for one choice. The rule: the calendar is the overview, a day's work is a list, so a day opens in İşler.
/// </summary>
public sealed partial class MainViewModel
{
    private SidebarSection _section = SidebarSection.Tasks;
    private bool _isRailOnly;
    private bool _isSectionOverlayOpen;

    public SidebarSection Section
    {
        get => _section;
        set
        {
            if (_section == value) return;
            _section = value;
            Notify();
            Notify(nameof(IsTasksSection));
            Notify(nameof(IsCalendarSection));
        }
    }

    public bool IsTasksSection => _section == SidebarSection.Tasks;
    public bool IsCalendarSection => _section == SidebarSection.Calendar;

    /// <summary>Work left over from past days: the orange dot on İşler, seen from every section.</summary>
    public bool HasOverdueWork => OpenCount > 0;

    /// <summary>The old single-column sidebar instead of the two tiers. It never goes narrow, so it is the window that asks.</summary>
    public bool IsClassicSidebar
    {
        get => _settings.ClassicSidebar;
        set
        {
            if (_settings.ClassicSidebar == value) return;
            _settings.ClassicSidebar = value;
            if (!_pathsFromCommandLine) _settings.Save();
            if (value) IsSectionOverlayOpen = false;
            Notify();
            Notify(nameof(IsNewSidebar));
        }
    }

    public bool IsNewSidebar => !IsClassicSidebar;

    /// <summary>Plain text in the add box, the edit box and Win+Shift+N, as before; otherwise the visual box.</summary>
    public bool IsTextWriting
    {
        get => _settings.TextWriting;
        set
        {
            if (_settings.TextWriting == value) return;
            _settings.TextWriting = value;
            if (!_pathsFromCommandLine) _settings.Save();
            Notify();
            Notify(nameof(IsVisualWriting));
            Notify(nameof(ComposerKeyHint));
            Notify(nameof(ComposerHasNote));
            Notify(nameof(ComposerNoteChipTip));
            Notify(nameof(ComposerSubTaskChipTip));
            Notify(nameof(ComposerSummary));
            Notify(nameof(HasComposerSummary));
        }
    }

    public bool IsVisualWriting => !IsTextWriting;

    public ICommand UseVisualWritingCommand { get; private set; } = null!;
    public ICommand UseTextWritingCommand { get; private set; } = null!;

    public ICommand UseNewSidebarCommand { get; private set; } = null!;
    public ICommand UseClassicSidebarCommand { get; private set; } = null!;

    /// <summary>Set by the window: too narrow for the list beside the strip.</summary>
    public bool IsRailOnly
    {
        get => _isRailOnly;
        set
        {
            if (_isRailOnly == value) return;
            _isRailOnly = value;
            Notify();
            if (!value) IsSectionOverlayOpen = false;
        }
    }

    /// <summary>On a narrow window, the chosen section's list lying over the middle.</summary>
    public bool IsSectionOverlayOpen
    {
        get => _isSectionOverlayOpen;
        set
        {
            if (_isSectionOverlayOpen == value) return;
            _isSectionOverlayOpen = value;
            Notify();
        }
    }

    /// <summary>
    /// "tasks" or "calendar"; "calendar" also opens the calendar page. On a narrow window a second press on the open
    /// section closes its list.
    /// </summary>
    public ICommand ShowSectionCommand { get; private set; } = null!;

    /// <summary>＋ Yeni iş in every list. The add box lives under the lists, so the calendar gives way to today first.</summary>
    public ICommand NewTaskCommand { get; private set; } = null!;

    private void InitSidebar()
    {
        UseNewSidebarCommand = new RelayCommand(() => IsClassicSidebar = false);
        UseClassicSidebarCommand = new RelayCommand(() => IsClassicSidebar = true);
        UseVisualWritingCommand = new RelayCommand(() => IsTextWriting = false);
        UseTextWritingCommand = new RelayCommand(() => IsTextWriting = true);
        ShowSectionCommand = new ParameterCommand(p =>
        {
            var section = p as string == "calendar" ? SidebarSection.Calendar : SidebarSection.Tasks;
            if (_isRailOnly) IsSectionOverlayOpen = !(_isSectionOverlayOpen && _section == section);
            Section = section;
            // Takvim opens the big calendar in the middle; while a day is being picked the page under it stays.
            if (section == SidebarSection.Calendar && !IsPickingDay && !IsCalendarView) Navigate(ViewMode.Calendar);
            // İşler from the calendar goes straight to today's list, not to the calendar left in the middle.
            else if (section == SidebarSection.Tasks && !IsPickingDay && IsCalendarView) ShowDay(_today);
        });
        NewTaskCommand = new RelayCommand(() =>
        {
            IsSectionOverlayOpen = false;
            if (IsCalendarView) ShowDay(_today);
            OpenComposerCommand.Execute(null);
        });
        ToggleMiniYearCommand = new RelayCommand(() =>
        {
            _isMiniYearOpen = !_isMiniYearOpen;
            Notify(nameof(IsMiniYearOpen));
            Rebuild();
        });
        PreviousPaneCommand = new RelayCommand(() => ShiftPane(-1));
        NextPaneCommand = new RelayCommand(() => ShiftPane(1));
        BackToCalendarCommand = new RelayCommand(() =>
        {
            _visibleMonth = _returnMonth;
            Navigate(ViewMode.Calendar);
        });
    }

    /// <summary>Picking a day needs the small calendar in sight; once picked or given up, a list opened for it closes.</summary>
    private void ShowSectionForPicking()
    {
        if (IsPickingDay)
        {
            // The small calendar lives under İşler.
            Section = SidebarSection.Tasks;
            if (_isRailOnly) IsSectionOverlayOpen = true;
        }
        else
        {
            IsSectionOverlayOpen = false;
        }
    }

    // ---- The small calendar's year, the calendar list's months, and the way back from a day ----

    private bool _isMiniYearOpen;
    private int? _paneYear;
    private DateTime? _paneWeekMonth;
    private int? _paneYearCenter;
    private IList<PaneWeek> _paneWeeks = new List<PaneWeek>();
    private IList<MonthChip> _paneYears = new List<MonthChip>();
    private bool _calendarReturn;
    private DateTime _returnMonth;
    private IList<MonthChip> _miniMonths = new List<MonthChip>();
    private IList<MonthChip> _paneMonths = new List<MonthChip>();
    private string _paneSummaryTitle = "";
    private string _paneSummary = "";

    /// <summary>The small calendar shows twelve months instead of one month's days, to jump far quickly.</summary>
    public bool IsMiniYearOpen => _isMiniYearOpen;

    public string MiniHeaderText => _isMiniYearOpen ? _visibleMonth.Year.ToString(Turkish) : MonthTitle;

    public ICommand ToggleMiniYearCommand { get; private set; } = null!;

    public IList<MonthChip> MiniMonths
    {
        get => _miniMonths;
        private set { _miniMonths = value; Notify(); }
    }

    /// <summary>The year of the twelve months beside the big calendar; it follows the calendar until its own arrows move it.</summary>
    public int CalendarPaneYear => _paneYear ?? CalendarShownMonth.Year;

    public IList<MonthChip> CalendarPaneMonths
    {
        get => _paneMonths;
        private set { _paneMonths = value; Notify(); }
    }

    public string CalendarPaneSummaryTitle
    {
        get => _paneSummaryTitle;
        private set { _paneSummaryTitle = value; Notify(); }
    }

    public string CalendarPaneSummary
    {
        get => _paneSummary;
        private set { _paneSummary = value; Notify(); }
    }

    /// <summary>The month whose weeks the calendar list shows; it follows the calendar until its own arrows move it.</summary>
    private DateTime PaneWeekMonth => _paneWeekMonth ?? CalendarShownMonth;

    /// <summary>The middle of the nine years: this year, until the list's arrows move it.</summary>
    private int PaneYearCenter => _paneYearCenter ?? _today.Year;

    /// <summary>The weeks of a month, each as a row of seven days.</summary>
    public IList<PaneWeek> CalendarPaneWeeks
    {
        get => _paneWeeks;
        private set { _paneWeeks = value; Notify(); }
    }

    /// <summary>Nine years, three by three, around the middle one.</summary>
    public IList<MonthChip> CalendarPaneYears
    {
        get => _paneYears;
        private set { _paneYears = value; Notify(); }
    }

    // The list beside the big calendar follows its size: weeks for Hafta, months for Ay, years for Yıl.
    public bool IsPaneWeeks => _calendarScale == CalendarScale.Week;
    public bool IsPaneMonths => _calendarScale == CalendarScale.Month;
    public bool IsPaneYears => _calendarScale == CalendarScale.Year;

    public string CalendarPaneLabel => _calendarScale switch
    {
        CalendarScale.Week => "HAFTALAR",
        CalendarScale.Year => "YILLAR",
        _ => "AYLAR",
    };

    public string CalendarPaneHeader => _calendarScale switch
    {
        CalendarScale.Week => PaneWeekMonth.ToString("MMMM yyyy", Turkish),
        CalendarScale.Year => $"{PaneYearCenter - 4}–{PaneYearCenter + 4}",
        _ => CalendarPaneYear.ToString(Turkish),
    };

    public string PanePreviousTip => _calendarScale switch
    {
        CalendarScale.Week => "Önceki ay",
        CalendarScale.Year => "Önceki yıllar",
        _ => "Önceki yıl",
    };

    public string PaneNextTip => _calendarScale switch
    {
        CalendarScale.Week => "Sonraki ay",
        CalendarScale.Year => "Sonraki yıllar",
        _ => "Sonraki yıl",
    };

    public ICommand PreviousPaneCommand { get; private set; } = null!;
    public ICommand NextPaneCommand { get; private set; } = null!;

    /// <summary>A day opened from the big calendar keeps the way back to the same month or week.</summary>
    public bool ShowBackToCalendar => _calendarReturn && _mode is ViewMode.Day or ViewMode.Today;

    public ICommand BackToCalendarCommand { get; private set; } = null!;

    private DateTime CalendarShownMonth => _calendarScale == CalendarScale.Week ? FirstOfMonth(_visibleWeek.AddDays(3)) : _visibleMonth;

    /// <summary>The month arrows step a year while the small calendar shows its twelve months.</summary>
    private int MiniStep => _isMiniYearOpen ? 12 : 1;

    /// <summary>A day of the big calendar was opened: the list belongs to İşler, with the way back kept.</summary>
    private void NoteDayFromCalendar()
    {
        if (_mode != ViewMode.Calendar) return;
        _calendarReturn = true;
        _returnMonth = _visibleMonth;
        Section = SidebarSection.Tasks;
    }

    /// <summary>The list's own arrows: a month of weeks, a year of months, or a row of three years.</summary>
    private void ShiftPane(int step)
    {
        switch (_calendarScale)
        {
            case CalendarScale.Week:
                _paneWeekMonth = PaneWeekMonth.AddMonths(step);
                break;
            case CalendarScale.Year:
                _paneYearCenter = PaneYearCenter + 3 * step;
                break;
            default:
                _paneYear = CalendarPaneYear + step;
                break;
        }
        Rebuild();
    }

    /// <summary>The list's arrows have moved it away; a new size starts it again from the calendar.</summary>
    private void ResetPaneLists()
    {
        _paneYear = null;
        _paneWeekMonth = null;
        _paneYearCenter = null;
    }

    /// <summary>A week of the calendar list: the big calendar shows that week.</summary>
    private void JumpCalendarToWeek(DateTime monday)
    {
        // A week reaching into the month before stays in the list it was picked from.
        _paneWeekMonth = PaneWeekMonth;
        _visibleWeek = monday;
        _visibleMonth = FirstOfMonth(monday.AddDays(3));
        if (_mode != ViewMode.Calendar) Navigate(ViewMode.Calendar);
        else Rebuild();
    }

    /// <summary>A year of the calendar list: the big calendar shows that year.</summary>
    private void JumpCalendarToYear(int year)
    {
        _visibleMonth = new DateTime(year, _visibleMonth.Month, 1);
        if (_mode != ViewMode.Calendar) Navigate(ViewMode.Calendar);
        else Rebuild();
    }

    private static string WeekRange(DateTime monday)
    {
        var sunday = monday.AddDays(6);
        return monday.Month == sunday.Month
            ? $"{monday.Day}–{sunday.ToString("d MMMM", Turkish)}"
            : $"{monday.ToString("d MMMM", Turkish)} – {sunday.ToString("d MMMM", Turkish)}";
    }

    /// <summary>A month of the calendar list: the big calendar goes there, at the size it shows.</summary>
    private void JumpCalendarToMonth(DateTime month)
    {
        _paneYear = null;
        switch (_calendarScale)
        {
            case CalendarScale.Week:
                _visibleWeek = CalendarNav.WeekFor(month, _today);
                _visibleMonth = month;
                break;
            case CalendarScale.Year:
                OpenMonth(month);
                return;
            default:
                _visibleMonth = month;
                break;
        }
        if (_mode != ViewMode.Calendar) Navigate(ViewMode.Calendar);
        else Rebuild();
    }

    private void BuildSidebarCalendars(List<DayEntries> byDate)
    {
        var openMonths = new HashSet<DateTime>(byDate
            .Where(d => d.Date != Day.Undated && d.Entries.Any(e => e.HasOpenWork))
            .Select(d => FirstOfMonth(d.Date)));
        var thisMonth = FirstOfMonth(_today);

        IList<MonthChip> Months(int year, DateTime shown, Action<DateTime> pick) =>
            Enumerable.Range(1, 12).Select(m =>
            {
                var month = new DateTime(year, m, 1);
                return new MonthChip(month.ToString("MMM", Turkish), month == shown, month == thisMonth, openMonths.Contains(month),
                    new RelayCommand(() => pick(month)), month > thisMonth);
            }).ToList();

        MiniMonths = Months(_visibleMonth.Year, _visibleMonth, month =>
        {
            _visibleMonth = month;
            _isMiniYearOpen = false;
            Notify(nameof(IsMiniYearOpen));
            Rebuild();
        });

        var yearWide = _calendarScale == CalendarScale.Year;
        var shown = CalendarShownMonth;
        CalendarPaneMonths = Months(CalendarPaneYear, yearWide ? default : shown, JumpCalendarToMonth);

        var weekWide = _calendarScale == CalendarScale.Week;
        BuildPaneWeeks(byDate);
        var openYears = new HashSet<int>(openMonths.Select(m => m.Year));
        CalendarPaneYears = CalendarNav.YearsAround(PaneYearCenter).Select(year =>
            new MonthChip(year.ToString(Turkish), yearWide && year == _visibleMonth.Year, year == _today.Year, openYears.Contains(year),
                new RelayCommand(() => JumpCalendarToYear(year)), year > _today.Year)).ToList();

        var inPeriod = byDate
            .Where(d => d.Date != Day.Undated && (
                weekWide ? d.Date >= _visibleWeek && d.Date < _visibleWeek.AddDays(7)
                : yearWide ? d.Date.Year == _visibleMonth.Year
                : FirstOfMonth(d.Date) == shown))
            .SelectMany(d => d.Entries)
            .ToList();
        var dropped = inPeriod.Count(e => e.IsDropped);
        CalendarPaneSummaryTitle = weekWide ? $"{CalendarNav.WeekNumber(_visibleWeek)}. hafta · {WeekRange(_visibleWeek)}"
            : yearWide ? _visibleMonth.Year.ToString(Turkish)
            : shown.ToString("MMMM yyyy", Turkish);
        var progressed = weekWide ? ProgressedIn(_visibleWeek, _visibleWeek.AddDays(6))
            : yearWide ? ProgressedIn(new DateTime(_visibleMonth.Year, 1, 1), new DateTime(_visibleMonth.Year, 12, 31))
            : ProgressedIn(shown, shown.AddMonths(1).AddDays(-1));
        CalendarPaneSummary = $"{inPeriod.Count(e => e.IsFinished)} iş bitti"
            + (progressed.Count > 0 ? $" · {progressed.Count} ilerledi" : "")
            + $" · {inPeriod.Count(e => e.HasOpenWork && !progressed.Contains(e))} açık"
            + (dropped > 0 ? $" · {dropped} iptal" : "");

        Notify(nameof(CalendarPaneYear));
        Notify(nameof(IsPaneWeeks));
        Notify(nameof(IsPaneMonths));
        Notify(nameof(IsPaneYears));
        Notify(nameof(CalendarPaneLabel));
        Notify(nameof(CalendarPaneHeader));
        Notify(nameof(PanePreviousTip));
        Notify(nameof(PaneNextTip));
        Notify(nameof(MiniHeaderText));
        Notify(nameof(ShowBackToCalendar));
    }

    private void BuildPaneWeeks(List<DayEntries> byDate)
    {
        var month = PaneWeekMonth;
        var worked = DayWork.WorkedByDay(_document, _today);
        var open = new HashSet<DateTime>(byDate.Where(d => d.Entries.Any(e => e.HasOpenWork)).Select(d => d.Date));
        var weekWide = _calendarScale == CalendarScale.Week;

        CalendarPaneWeeks = CalendarNav.WeeksOfMonth(month).Select(monday =>
        {
            var days = Enumerable.Range(0, 7).Select(i =>
            {
                var date = monday.AddDays(i);
                var done = worked.TryGetValue(date, out var count) ? count : 0;
                return new PaneDay(date.Day, date.Month != month.Month, date > _today, date == _today,
                    done switch { 0 => 0, <= 2 => 1, <= 4 => 2, _ => 3 }, open.Contains(date));
            }).ToList();
            var number = CalendarNav.WeekNumber(monday);
            return new PaneWeek(number, days, weekWide && monday == _visibleWeek, $"{number}. hafta, {WeekRange(monday)}",
                new RelayCommand(() => JumpCalendarToWeek(monday)));
        }).ToList();
    }
}

/// <summary>One month of a twelve-month grid in the sidebar, or one year of the nine.</summary>
public sealed class MonthChip(string label, bool isShown, bool isThisMonth, bool hasOpenWork, ICommand pick, bool isFuture = false)
{
    public string Label { get; } = label;
    public bool IsShown { get; } = isShown;
    public bool IsThisMonth { get; } = isThisMonth;
    public bool HasOpenWork { get; } = hasOpenWork;
    public ICommand PickCommand { get; } = pick;

    /// <summary>Still ahead of today: faded, though it can be opened.</summary>
    public bool IsFuture { get; } = isFuture;
}

/// <summary>A week in the calendar list: its number in the year and its seven days, Monday first.</summary>
public sealed class PaneWeek(int number, IList<PaneDay> days, bool isShown, string name, ICommand pick)
{
    public int Number { get; } = number;
    public IList<PaneDay> Days { get; } = days;

    /// <summary>The week the big calendar shows.</summary>
    public bool IsShown { get; } = isShown;

    public string Name { get; } = name;
    public ICommand PickCommand { get; } = pick;
}

/// <summary>A day in a week of the calendar list, green by the work finished on it as in the big calendar.</summary>
public sealed class PaneDay(int day, bool isOutside, bool isFuture, bool isToday, int heatLevel, bool hasOpen)
{
    public int Day { get; } = day;

    /// <summary>Belongs to the month before or after.</summary>
    public bool IsOutside { get; } = isOutside;

    public bool IsFuture { get; } = isFuture;
    public bool IsToday { get; } = isToday;
    public int HeatLevel { get; } = heatLevel;
    public bool HasOpen { get; } = hasOpen;
}
