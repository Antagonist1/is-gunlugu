using System.Windows.Input;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>
/// One sub-task in the side panel: its box, its title to edit in place, its own page, a way to remove it, and its
/// place in the order, which a drag or Alt+arrow changes.
/// </summary>
public sealed class DetailSubTask(int index, SubTask task, ICommand toggle, ICommand remove, ICommand open, Action<string> rename) : ObservableObject
{
    private bool _showDropAbove;
    private bool _showDropBelow;

    public int Index { get; } = index;

    /// <summary>"12 Kas" when finished on another day than the one the task sits under; else empty.</summary>
    public string DoneDateText { get; init; } = "";

    public bool HasDoneDate => DoneDateText.Length > 0;

    public bool ShowDropAbove
    {
        get => _showDropAbove;
        set { _showDropAbove = value; Notify(); }
    }

    public bool ShowDropBelow
    {
        get => _showDropBelow;
        set { _showDropBelow = value; Notify(); }
    }

    public string Text
    {
        get => task.Text;
        set => rename(value ?? "");
    }

    public bool IsDone { get; } = task.IsDone;
    public bool IsDropped { get; } = task.IsDropped;

    /// <summary>Shown crossed out: given up itself, or part of a task that was given up as a whole.</summary>
    public bool IsStruck { get; } = task.IsDropped || task.Owner.IsDropped;
    public bool HasDescription { get; } = task.DescriptionLines.Count > 0;
    public ICommand ToggleCommand { get; } = toggle;
    public ICommand RemoveCommand { get; } = remove;
    public ICommand OpenCommand { get; } = open;
}

/// <summary>
/// The panel on the right that a click on a task opens: its title, description and sub-tasks to edit, and the moves
/// to another day. A click on a sub-task opens that sub-task's own title and description instead. The panel lives
/// only while its task is on screen: another page, day or file, or a click on empty space, closes it.
/// </summary>
public sealed partial class MainViewModel
{
    private Entry? _detail;
    private int _detailSub = -1;
    private DateTime _detailDate;

    public ICommand CloseDetailCommand { get; private set; } = null!;
    public ICommand DetailBackCommand { get; private set; } = null!;
    public ICommand DetailTodayCommand { get; private set; } = null!;
    public ICommand DetailTomorrowCommand { get; private set; } = null!;
    public ICommand DetailUndatedCommand { get; private set; } = null!;
    public ICommand DetailPickDayCommand { get; private set; } = null!;
    public ICommand DetailDeleteCommand { get; private set; } = null!;
    public ICommand DetailDropCommand { get; private set; } = null!;
    public ICommand DetailSubToggleCommand { get; private set; } = null!;

    /// <summary>Giving up is for a task or for one of its sub-tasks, not for a plain note.</summary>
    public bool CanDetailDrop => _detail != null && (IsDetailSubTask || _detail.Status != EntryStatus.Note);

    /// <summary>What the panel shows was given up: the sub-task when one is open, else the task.</summary>
    public bool IsDetailDropped => DetailSub is { } sub ? sub.IsDropped : _detail?.IsDropped == true;

    public string DetailDropText => IsDetailDropped ? "İptali geri al" : IsDetailSubTask ? "Alt işi iptal et" : "İşi iptal et";

    /// <summary>The strip at the top of the panel that says what was given up.</summary>
    public bool ShowDroppedBanner => IsDetailDropped;

    public string DroppedBannerText => IsDetailSubTask ? "Bu alt iş iptal edildi" : "Bu iş iptal edildi";

    public string DetailDropTip => IsDetailDropped
        ? "İptali geri alır"
        : IsDetailSubTask
            ? "Yalnızca bu alt işi kapatır: üstü çizili kalır, sayıya girmez, işin kendisi devam eder"
            : "Yapılmayacak işi silmeden kapatır: listede üstü çizili kalır, tamamlanmamışlardan çıkar";

    public bool IsDetailOpen => _detail != null;

    /// <summary>The panel shows one sub-task rather than the whole task.</summary>
    public bool IsDetailSubTask => DetailSub != null;

    /// <summary>Over a task, the day it sits under ("Bugün · 23 Kasım Pazartesi"); over a sub-task, ALT İŞ.</summary>
    public string DetailHeading
    {
        get
        {
            if (_detail == null) return "";
            if (IsDetailSubTask) return "ALT İŞ";
            var date = DetailDate;
            if (date == Day.Undated) return "Tarihsiz";
            var day = date.ToString("d MMMM dddd", Turkish);
            return date == _today ? "Bugün · " + day : date == Tomorrow ? "Yarın · " + day : date == _today.AddDays(-1) ? "Dün · " + day : day;
        }
    }

    /// <summary>The panel's own pages: back to the task from a sub-task or from the time page.</summary>
    public bool HasDetailBack => IsDetailSubTask || IsDetailTimePage;

    /// <summary>The open sub-task's own box in the panel, the same tick as its box in the list.</summary>
    public bool IsDetailSubDone => DetailSub?.IsDone == true;

    /// <summary>A sub-task given up, itself or with its whole task, has no box to tick.</summary>
    public bool CanDetailSubToggle => DetailSub is { } sub && !sub.IsDropped && !sub.Owner.IsDropped;

    /// <summary>The marker of what the panel shows when it is finished, else null: given-up work has no finish day.</summary>
    private string? DetailDoneMarker =>
        DetailSub is { } sub ? (sub.IsDone && !sub.Owner.IsDropped ? sub.Marker : null)
        : _detail?.Status == EntryStatus.Done ? _detail.Marker : null;

    public bool ShowDetailDoneDate => DetailDoneMarker != null;

    /// <summary>A task gets its own BİTTİ row; a sub-task already has its box under DURUM, so the date goes there.</summary>
    public bool ShowDetailTaskDoneDate => ShowDetailDoneDate && !IsDetailSubTask;

    public bool ShowDetailSubDoneDate => ShowDetailDoneDate && IsDetailSubTask;

    /// <summary>"18 Kasım'da bitti": the date in the mark, or the day it sits under for old marks without one.</summary>
    public string DetailDoneDateText
    {
        get
        {
            if (DetailDoneMarker is not { } marker) return "";
            var on = DoneDate.Read(marker, DetailDate, _today) ?? (DetailDate == Day.Undated ? _today : DetailDate);
            return on.Year == _today.Year
                ? on.ToString("d MMMM", Turkish) + Locative(on) + " bitti"
                : on.ToString("d MMMM yyyy", Turkish) + " tarihinde bitti";
        }
    }

    public ICommand DetailChangeDoneDateCommand { get; private set; } = null!;

    /// <summary>The task a sub-task belongs to, for the way back.</summary>
    public string DetailOwnerTitle => _detail?.Text ?? "";

    private SubTask? DetailSub
    {
        get
        {
            if (_detail == null || _detailSub < 0) return null;
            var tasks = _detail.SubTasks;
            return _detailSub < tasks.Count ? tasks[_detailSub] : null;
        }
    }

    public string DetailTitle
    {
        get => DetailSub?.Text ?? _detail?.Text ?? "";
        set
        {
            var entry = _detail;
            var title = (value ?? "").Trim();
            if (entry == null || title.Length == 0 || title == DetailTitle) return;

            if (DetailSub is { } sub) RenameSubTask(sub, title);
            else Mutate(_ => NoteDocument.SetText(entry, string.Join("\n", new[] { title }.Concat(entry.VisibleBody).ToArray())), "Düzenlendi.");
        }
    }

    public string DetailDescription
    {
        get => DetailSub is { } sub ? string.Join("\n", sub.DescriptionLines.ToArray()) : _detail?.Description ?? "";
        set
        {
            var entry = _detail;
            var text = value ?? "";
            if (entry == null || text == DetailDescription) return;

            var message = text.Trim().Length > 0 ? "Açıklama kaydedildi." : "Açıklama silindi.";
            if (DetailSub is { } sub) Mutate(_ => NoteDocument.SetSubTaskText(sub, sub.Text + "\n" + text), message);
            else Mutate(_ => NoteDocument.SetDescription(entry, text), message);
        }
    }

    public IList<DetailSubTask> DetailSubTasks => _detail == null
        ? new List<DetailSubTask>()
        : _detail.SubTasks.Select((t, i) => new DetailSubTask(i, t,
            new RelayCommand(() => ToggleDetailSubTask(t)),
            new RelayCommand(() => RemoveDetailSubTask(t)),
            new RelayCommand(() => ShowDetailSubTask(i)),
            text => RenameSubTask(t, text.Trim()))
        {
            DoneDateText = t.IsDone && DoneDate.Read(t.Marker, DetailDate, _today) is { } on && on != DetailDate ? on.ToString("d MMM", Turkish) : "",
        }).ToList();

    public string DetailSubTasksTitle
    {
        get
        {
            var tasks = _detail?.ActiveSubTasks ?? new List<SubTask>();
            return tasks.Count == 0 ? "ALT İŞLER" : $"ALT İŞLER · {tasks.Count(t => t.IsDone)}/{tasks.Count}";
        }
    }

    public bool CanDetailToday => _detail != null && DetailDate != _today && _detail.HasOpenWork;
    public bool CanDetailTomorrow => _detail != null && DetailDate != Tomorrow && _detail.HasOpenWork;
    public bool CanDetailUndated => _detail != null && DetailDate != Day.Undated && _detail.CanBeUndated;

    private DateTime DetailDate => _detail == null ? default : DateOf(_detail);

    private void InitDetail()
    {
        CloseDetailCommand = new RelayCommand(CloseDetail);
        DetailBackCommand = new RelayCommand(() => DetailBack());
        DetailTodayCommand = new RelayCommand(() => MoveDetail(_today, "Bugüne alındı."));
        DetailTomorrowCommand = new RelayCommand(() => MoveDetail(Tomorrow, "Yarına taşındı."));
        DetailUndatedCommand = new RelayCommand(() =>
        {
            if (CanDetailUndated) MoveDetail(Day.Undated, "Tarihsiz işlere taşındı.");
        });
        DetailPickDayCommand = new RelayCommand(() =>
        {
            if (_detail != null) StartPicking(new List<Entry> { _detail });
        });
        DetailChangeDoneDateCommand = new RelayCommand(() =>
        {
            if (!ShowDetailDoneDate) return;
            _pickingEntries = null;
            _pickingForComposer = false;
            _pickingDoneDate = true;
            NotifyPicking();
        });
        DetailDropCommand = new RelayCommand(() =>
        {
            var entry = _detail;
            if (entry == null || !CanDetailDrop) return;
            if (DetailSub is { } sub)
            {
                var dropSub = !sub.IsDropped;
                Mutate(_ => NoteDocument.SetSubTaskDropped(sub, dropSub, _today), dropSub ? "Alt iş iptal edildi." : "Alt işin iptali geri alındı.");
                return;
            }

            var drop = !entry.IsDropped;
            Mutate(_ => NoteDocument.SetDropped(entry, drop, _today), drop ? "İş iptal edildi." : "İşin iptali geri alındı.");
        });
        DetailSubToggleCommand = new RelayCommand(() =>
        {
            if (CanDetailSubToggle && DetailSub is { } sub) ToggleDetailSubTask(sub);
            else Notify(nameof(IsDetailSubDone));
        });
        DetailDeleteCommand = new RelayCommand(() =>
        {
            var entry = _detail;
            if (entry == null) return;

            if (DetailSub is { } sub)
            {
                _detailSub = -1;
                RemoveDetailSubTask(sub);
                return;
            }

            CloseDetail();
            Mutate(doc => doc.Delete(entry), "Not silindi.");
        });
    }

    /// <summary>Opens a task in the panel, or one of its sub-tasks when <paramref name="sub"/> is given.</summary>
    public void OpenDetail(Entry entry, SubTask? sub = null)
    {
        _isDetailTimePage = false;
        _detail = entry;
        _detailSub = sub == null ? -1 : entry.SubTasks.ToList().FindIndex(t => t.BodyIndex == sub.BodyIndex);
        NotifyDetail();
        Rebuild();
    }

    public void CloseDetail()
    {
        if (_detail == null) return;
        _detail = null;
        _detailSub = -1;
        _isDetailTimePage = false;
        NotifyDetail();
        Rebuild();
    }

    public bool IsInDetail(Entry entry) => entry == _detail;

    public bool IsSubTaskInDetail(SubTask task) => DetailSub is { } sub && sub.Owner == task.Owner && sub.BodyIndex == task.BodyIndex;

    public void AddDetailSubTask(string text)
    {
        var entry = _detail;
        if (entry == null || text.Trim().Length == 0) return;
        Mutate(_ => NoteDocument.AddSubTask(entry, text.Trim()), "Alt iş eklendi.");
    }

    private void ShowDetailSubTask(int index)
    {
        if (_detail == null || index == _detailSub) return;
        _isDetailTimePage = false;
        _detailSub = index;
        NotifyDetail();
        Rebuild();
    }

    /// <summary>A new title keeps the description lines under the sub-task.</summary>
    private void RenameSubTask(SubTask task, string title)
    {
        if (title.Length == 0 || title == task.Text) return;
        Mutate(_ => NoteDocument.SetSubTaskText(task, string.Join("\n", new[] { title }.Concat(task.DescriptionLines).ToArray())), "Alt iş düzenlendi.");
    }

    private void ToggleDetailSubTask(SubTask task)
    {
        var shown = DetailSub is { } sub && sub.BodyIndex == task.BodyIndex;
        var done = !task.IsDone;
        Mutate(doc =>
        {
            NoteDocument.SetSubTaskDone(task, done, _today);
            DateFinishedUndatedWork(doc, task.Owner);
        }, done ? "Alt iş yapıldı olarak işaretlendi." : "Alt iş tekrar açıldı.");

        // Ticked it goes to the bottom, unticked back above the finished ones; the panel follows it.
        if (shown && _detail is { } entry && _detailSub >= 0)
        {
            var at = entry.SubTasks.ToList().FindIndex(t => t.Text == task.Text && t.IsDone == done);
            if (at >= 0) _detailSub = at;
            NotifyDetail();
        }
    }

    private void RemoveDetailSubTask(SubTask task) => Mutate(_ => NoteDocument.DeleteSubTask(task, _today), "Alt iş silindi.");

    /// <summary>A sub-task dragged or moved with Alt+arrow in the panel; <paramref name="to"/> is its place in the new order.</summary>
    public void MoveDetailSubTask(int from, int to)
    {
        var tasks = _detail?.SubTasks;
        if (tasks is null || from < 0 || from >= tasks.Count || to < 0 || to >= tasks.Count || from == to) return;
        Mutate(_ => NoteDocument.MoveSubTask(tasks[from], to), "Alt işin sırası değişti.");
    }

    /// <summary>A day clicked in a calendar while choosing the finish day: the mark gets that date, the task stays put.</summary>
    private bool TakeDoneDate(DateTime date)
    {
        if (!_pickingDoneDate) return false;

        _pickingDoneDate = false;
        NotifyPicking();
        var entry = _detail;
        if (entry == null || !ShowDetailDoneDate || date == Day.Undated) return true;
        if (date > _today)
        {
            StatusText = "Bitiş tarihi bugünden sonra olamaz.";
            return true;
        }

        var day = DetailDate;
        var message = $"Bitiş tarihi {date.ToString("d MMMM", Turkish)} oldu.";
        if (DetailSub is { } sub) Mutate(_ => NoteDocument.SetSubTaskDoneDate(sub, date, day, _today), message);
        else Mutate(_ => NoteDocument.SetDoneDate(entry, date, day, _today), message);
        return true;
    }

    private void MoveDetail(DateTime date, string message)
    {
        var entry = _detail;
        if (entry == null) return;
        Mutate(doc => doc.MoveEntry(entry, date, null), message);
    }

    /// <summary>
    /// Before every redraw. Undo, Drive and edits outside the app load the file again, which makes new tasks: the
    /// panel then finds its task again by title, on the same day first, and closes only when it is really gone.
    /// </summary>
    private void RefreshDetail()
    {
        if (_detail != null && !_document.Days.Any(d => d.Entries.Contains(_detail)))
        {
            var title = _detail.Text;
            _detail = _document.Days.Where(d => d.Date == _detailDate).SelectMany(d => d.Entries).FirstOrDefault(e => e.Text == title)
                ?? _document.Days.SelectMany(d => d.Entries).FirstOrDefault(e => e.Text == title);
        }
        if (_detail != null) _detailDate = DateOf(_detail);
        if (_detail == null || DetailSub == null) _detailSub = -1;
        NotifyDetail();
    }

    /// <summary>After every redraw: the panel stays only while its task is on screen, in the list or on the week page.</summary>
    private void CloseDetailIfHidden()
    {
        // Closing redraws once more, so the row loses its panel colour.
        if (_detail != null && !ListedRows.Any(r => r.Entry == _detail) && !IsOnWeekPage(_detail)) CloseDetail();
    }

    private bool IsOnWeekPage(Entry entry) =>
        !IsListView && _calendarScale == CalendarScale.Week && WeekCards.Any(c => c.Lines.Any(l => l.Entry == entry));

    private void NotifyDetail()
    {
        Notify(nameof(IsDetailOpen));
        Notify(nameof(IsDetailSubTask));
        Notify(nameof(DetailHeading));
        Notify(nameof(HasDetailBack));
        Notify(nameof(DetailOwnerTitle));
        Notify(nameof(DetailTitle));
        Notify(nameof(DetailDescription));
        Notify(nameof(DetailSubTasks));
        Notify(nameof(DetailSubTasksTitle));
        Notify(nameof(CanDetailToday));
        Notify(nameof(CanDetailTomorrow));
        Notify(nameof(CanDetailUndated));
        Notify(nameof(CanDetailDrop));
        Notify(nameof(IsDetailDropped));
        Notify(nameof(DetailDropText));
        Notify(nameof(DetailDropTip));
        Notify(nameof(ShowDroppedBanner));
        Notify(nameof(DroppedBannerText));
        Notify(nameof(IsDetailSubDone));
        Notify(nameof(CanDetailSubToggle));
        Notify(nameof(ShowDetailDoneDate));
        Notify(nameof(ShowDetailTaskDoneDate));
        Notify(nameof(ShowDetailSubDoneDate));
        Notify(nameof(DetailDoneDateText));
        NotifyDetailTime();
    }
}
