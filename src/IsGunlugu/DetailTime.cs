using System.Globalization;
using System.Windows.Input;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>One reminder in the panel: "30 dk önce" over a faint "13:30", and a cross.</summary>
public sealed class DetailReminder(string text, string at, ICommand remove)
{
    public string Text { get; } = text;
    public string At { get; } = at;
    public ICommand RemoveCommand { get; } = remove;
}

/// <summary>A ready span to add with one click; filled while set, faint when two are set already.</summary>
public sealed class ReminderChoice(string text, bool isOn, bool canPick, ICommand pick)
{
    public string Text { get; } = text;
    public bool IsOn { get; } = isOn;
    public bool CanPick { get; } = canPick;
    public ICommand PickCommand { get; } = pick;
}

/// <summary>
/// The panel's SAAT and HATIRLAT parts for a task: its time, up to two reminders from ready spans or a custom one.
/// Each change writes the mark at the end of the task's line (⏰[14:00 30dk 15dk]).
/// </summary>
public sealed partial class MainViewModel
{
    private static readonly TimeSpan[] ReadySpans =
    {
        TimeSpan.Zero, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30), TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(24),
    };

    private bool _isCustomReminderOpen;
    private string _customHours = "0";
    private string _customMinutes = "45";

    public ICommand AddDetailTimeCommand { get; private set; } = null!;
    public ICommand RemoveDetailTimeCommand { get; private set; } = null!;
    public ICommand OpenCustomReminderCommand { get; private set; } = null!;
    public ICommand AddCustomReminderCommand { get; private set; } = null!;

    private void InitDetailTime()
    {
        // A task without a time gets the next whole hour with the usual reminders; the page is there to change it.
        OpenDetailTimeCommand = new RelayCommand(() =>
        {
            if (DetailTime == null) SetDetailTime(new TaskTime(TimeSpan.FromHours(Math.Min(23, DateTime.Now.Hour + 1)), ReminderDefaults), "Hatırlatıcı eklendi.");
            ShowDetailTimePage(true);
        });
        AddDetailTimeCommand = OpenDetailTimeCommand;
        RemoveDetailTimeCommand = new RelayCommand(() =>
        {
            SetDetailTime(null, "Hatırlatıcı kaldırıldı.");
            ShowDetailTimePage(false);
        });
        OpenCustomReminderCommand = new RelayCommand(() => IsCustomReminderOpen = !IsCustomReminderOpen);
        AddCustomReminderCommand = new RelayCommand(AddCustomReminder);
    }

    private bool _isDetailTimePage;

    public ICommand OpenDetailTimeCommand { get; private set; } = null!;

    /// <summary>The time page of the panel: its own page so the task's page stays short, ‹ or Esc goes back.</summary>
    public bool IsDetailTimePage => _isDetailTimePage && _detail != null && !IsDetailSubTask;

    /// <summary>The task's own page: title, description, sub-tasks, and the move buttons at the bottom.</summary>
    public bool IsDetailTaskPage => _detail != null && !IsDetailSubTask && !IsDetailTimePage;

    public bool IsDetailMainPage => !IsDetailTimePage;

    /// <summary>The task's page row: "Hatırlatıcı · 14:00" over "30 dk ve 15 dk önce hatırlatır", or a faint "Hatırlatıcı ekle".</summary>
    public string DetailTimeTitle => DetailTime is { } time ? "Hatırlatıcı · " + time.AtText : "Hatırlatıcı ekle";

    public string DetailTimeSummary => DetailTime is not { } time ? ""
        : time.Before.Count == 0 ? "Hatırlatma yok"
        : time.Before.Contains(TimeSpan.Zero)
            ? string.Join(" ve ", time.Before.Select(b => b == TimeSpan.Zero ? "tam saatinde" : TaskTime.SpanText(b)).ToArray()) + " hatırlatır"
            : string.Join(" ve ", time.Before.Select(TaskTime.SpanText).ToArray()).Replace(" önce ve ", " ve ") + " hatırlatır";

    private void ShowDetailTimePage(bool show)
    {
        _isDetailTimePage = show;
        IsCustomReminderOpen = false;
        Notify(nameof(IsDetailTimePage));
        Notify(nameof(IsDetailTaskPage));
        Notify(nameof(IsDetailMainPage));
        Notify(nameof(HasDetailBack));
    }

    /// <summary>‹ or Esc on one of the panel's own pages; false when already on the task's page.</summary>
    public bool DetailBack()
    {
        if (IsDetailTimePage)
        {
            ShowDetailTimePage(false);
            return true;
        }
        if (IsDetailSubTask)
        {
            ShowDetailSubTask(-1);
            return true;
        }
        return false;
    }

    private TaskTime? DetailTime => IsDetailSubTask ? null : _detail?.Time;

    /// <summary>A finished or given-up task gets no new reminder; one already set still shows, so it can be taken off.</summary>
    public bool ShowDetailTime => _detail != null && !IsDetailSubTask && (_detail.Status == EntryStatus.Open || _detail.Time != null);
    public bool HasDetailTime => DetailTime != null;
    public bool CanAddDetailTime => ShowDetailTime && !HasDetailTime;

    /// <summary>"14:00"; typed over, it is read as H:mm (a dot works too).</summary>
    public string DetailTimeText
    {
        get => DetailTime?.AtText ?? "";
        set
        {
            if (DetailTime is not { } time || value == time.AtText) return;
            if (!TaskTime.TryTime((value ?? "").Trim().Replace('.', ':'), out var at))
            {
                StatusText = "Saat 14:00 gibi yazılır.";
                Notify();
                return;
            }
            SetDetailTime(new TaskTime(at, time.Before), "Saat değişti.");
        }
    }

    /// <summary>↑/↓ in the time box: five minutes later or earlier, within the day.</summary>
    public void StepDetailTime(int steps)
    {
        if (DetailTime is not { } time) return;
        var minutes = ((int)time.At.TotalMinutes + steps * 5 + 24 * 60) % (24 * 60);
        SetDetailTime(new TaskTime(TimeSpan.FromMinutes(minutes), time.Before), "Saat değişti.");
    }

    public string DetailTimeNote => _detail != null && DetailDate == Day.Undated ? "Tarihsiz işte hatırlatma çalmaz." : "";
    public bool HasDetailTimeNote => HasDetailTime && DetailTimeNote.Length > 0;

    public IList<DetailReminder> DetailReminders => DetailTime is not { } time
        ? new List<DetailReminder>()
        : time.Before.Select(b => new DetailReminder(
            TaskTime.SpanText(b),
            (b >= time.At ? "önceki gün " : "") + DateTime.Today.Add(time.At).Subtract(b).ToString("HH:mm", CultureInfo.InvariantCulture),
            new RelayCommand(() => SetDetailTime(new TaskTime(time.At, time.Before.Where(x => x != b)), "Hatırlatma kaldırıldı.")))).ToList();

    public IList<ReminderChoice> ReminderChoices => Choices(DetailTime?.Before ?? new List<TimeSpan>(), ToggleDetailReminder);

    public bool IsCustomReminderOpen
    {
        get => _isCustomReminderOpen;
        set { _isCustomReminderOpen = value; Notify(); }
    }

    public string CustomHours { get => _customHours; set { _customHours = value ?? ""; Notify(); } }
    public string CustomMinutes { get => _customMinutes; set { _customMinutes = value ?? ""; Notify(); } }

    private IList<ReminderChoice> Choices(IList<TimeSpan> set, Action<TimeSpan> toggle) =>
        ReadySpans.Select(span => new ReminderChoice(
            span == TimeSpan.Zero ? "Tam saatinde" : TaskTime.SpanText(span).Replace(" önce", ""),
            set.Contains(span), set.Contains(span) || set.Count < TaskTime.MaxReminders,
            new RelayCommand(() => toggle(span)))).ToList();

    // ---- Settings › Davranış › Saatli işler: the two reminders a task newly given a time gets, stepped with ‹ › ----

    /// <summary>The steps of the arrows: none, then the ready spans from "at the time" to a day.</summary>
    private static readonly TimeSpan?[] DefaultSteps = new TimeSpan?[] { null }.Concat(ReadySpans.Select(s => (TimeSpan?)s)).ToArray();

    private TimeSpan? FirstDefault => ReminderDefaults.Count > 0 ? ReminderDefaults[0] : null;
    private TimeSpan? SecondDefault => ReminderDefaults.Count > 1 ? ReminderDefaults[1] : null;

    public string FirstReminderText => StepText(FirstDefault);
    public string SecondReminderText => StepText(SecondDefault);

    /// <summary>The second comes after the first, so it is there only once the first is.</summary>
    public bool CanSetSecondReminder => FirstDefault != null;

    public ICommand EarlierFirstReminderCommand => new RelayCommand(() => StepFirst(-1));
    public ICommand LaterFirstReminderCommand => new RelayCommand(() => StepFirst(1));
    public ICommand EarlierSecondReminderCommand => new RelayCommand(() => StepSecond(-1));
    public ICommand LaterSecondReminderCommand => new RelayCommand(() => StepSecond(1));

    private static string StepText(TimeSpan? span) => span is { } s ? TaskTime.SpanText(s) : "Yok";

    private static int StepOf(TimeSpan? span) => Array.IndexOf(DefaultSteps, span);

    private void StepFirst(int step)
    {
        var at = Math.Max(0, Math.Min(DefaultSteps.Length - 1, StepOf(FirstDefault) + step));
        var first = DefaultSteps[at];
        // The second has to stay later than the first (a shorter time before); otherwise it goes.
        var second = first is { } f && SecondDefault is { } s && s < f ? (TimeSpan?)s : null;
        SetDefaults(first, second);
    }

    private void StepSecond(int step)
    {
        if (FirstDefault is not { } first) return;
        var at = StepOf(SecondDefault) + step;
        // Only "Yok" and the spans shorter than the first one.
        while (at > 0 && at < DefaultSteps.Length && DefaultSteps[at] is { } s && s >= first) at += step > 0 ? 1 : -1;
        if (at < 0 || at >= DefaultSteps.Length) return;
        SetDefaults(first, DefaultSteps[at]);
    }

    private void SetDefaults(TimeSpan? first, TimeSpan? second)
    {
        ReminderDefaults = new[] { first, second }.Where(s => s != null).Select(s => s!.Value).ToList();
        Notify(nameof(FirstReminderText));
        Notify(nameof(SecondReminderText));
        Notify(nameof(CanSetSecondReminder));
    }

    /// <summary>Settings › Davranış: what a task newly given a time gets.</summary>
    public IList<ReminderChoice> DefaultReminderChoices => Choices(ReminderDefaults, span =>
    {
        var set = ReminderDefaults;
        if (set.Contains(span)) ReminderDefaults = set.Where(b => b != span).ToList();
        else if (set.Count < TaskTime.MaxReminders) ReminderDefaults = set.Concat(new[] { span }).ToList();
        Notify(nameof(DefaultReminderChoices));
    });

    private void ToggleDetailReminder(TimeSpan span)
    {
        if (DetailTime is not { } time) return;
        if (time.Before.Contains(span))
            SetDetailTime(new TaskTime(time.At, time.Before.Where(b => b != span)), "Hatırlatma kaldırıldı.");
        else if (time.Before.Count < TaskTime.MaxReminders)
            SetDetailTime(new TaskTime(time.At, time.Before.Concat(new[] { span })), "Hatırlatma eklendi.");
    }

    private void AddCustomReminder()
    {
        if (DetailTime is not { } time) return;
        var hours = CustomHours.Trim().Length == 0 ? "0" : CustomHours.Trim();
        var minutes = CustomMinutes.Trim().Length == 0 ? "0" : CustomMinutes.Trim();
        if (!int.TryParse(hours, NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(minutes, NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || m > 59 || new TimeSpan(h, m, 0) > TaskTime.Longest)
        {
            StatusText = "Hatırlatma en çok 24 saat önce olabilir.";
            return;
        }
        if (time.Before.Count >= TaskTime.MaxReminders)
        {
            StatusText = "En çok iki hatırlatma kurulur.";
            return;
        }
        IsCustomReminderOpen = false;
        SetDetailTime(new TaskTime(time.At, time.Before.Concat(new[] { new TimeSpan(h, m, 0) })), "Hatırlatma eklendi.");
    }

    private void SetDetailTime(TaskTime? time, string message)
    {
        if (_detail is not { } entry) return;
        Mutate(_ => NoteDocument.SetTime(entry, time), message, keepPlace: true);
        NotifyDetailTime();
    }

    private void NotifyDetailTime()
    {
        Notify(nameof(ShowDetailTime));
        Notify(nameof(HasDetailTime));
        Notify(nameof(CanAddDetailTime));
        Notify(nameof(DetailTimeText));
        Notify(nameof(DetailTimeNote));
        Notify(nameof(HasDetailTimeNote));
        Notify(nameof(DetailReminders));
        Notify(nameof(ReminderChoices));
        Notify(nameof(DetailTimeTitle));
        Notify(nameof(DetailTimeSummary));
        Notify(nameof(IsDetailTimePage));
        Notify(nameof(IsDetailTaskPage));
        Notify(nameof(IsDetailMainPage));
        Notify(nameof(HasDetailBack));
    }
}
