using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>Where the keyboard is inside the add box; Enter and Esc mean something different in each.</summary>
public enum ComposerField { Title, Description, SubTask, Frame }

/// <summary>
/// The add box at the bottom: one line while idle; opened, it collects the day, a description and sub-tasks before the
/// task is written. Closing it keeps what was typed, so it comes back as it was.
/// </summary>
public sealed partial class MainViewModel
{
    private bool _isComposerOpen;
    private string _composerDescription = "";
    private bool _isDescriptionOpen;
    private bool _isSubTaskEntryOpen;
    private string _subTaskEntryText = "";
    private DateTime? _composerChosenDay;
    private ComposerField _composerField;
    private bool _pickingForComposer;
    private Entry? _justAdded;
    private DateTime? _addedElsewhere;
    private readonly DispatcherTimer _justAddedTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };

    /// <summary>The window moves the keyboard there; null means out of the box.</summary>
    public event Action<ComposerField?>? ComposerFocusRequested;

    public ObservableCollection<string> ComposerSubTasks { get; } = new();

    public ICommand OpenComposerCommand { get; private set; } = null!;
    public ICommand CloseComposerCommand { get; private set; } = null!;
    public ICommand DiscardComposerCommand { get; private set; } = null!;
    public ICommand ChooseComposerDayCommand { get; private set; } = null!;
    public ICommand OpenDescriptionCommand { get; private set; } = null!;
    public ICommand RemoveDescriptionCommand { get; private set; } = null!;
    public ICommand OpenSubTaskEntryCommand { get; private set; } = null!;
    public ICommand RemoveComposerSubTaskCommand { get; private set; } = null!;
    public ICommand SubmitComposerCommand { get; private set; } = null!;
    public ICommand ShowAddedDayCommand { get; private set; } = null!;

    private void InitComposer()
    {
        OpenComposerCommand = new RelayCommand(OpenComposer);
        CloseComposerCommand = new RelayCommand(CloseComposer);
        DiscardComposerCommand = new RelayCommand(DiscardComposer);
        ChooseComposerDayCommand = new ParameterCommand(p => ChooseComposerDay(p as string));
        OpenDescriptionCommand = new RelayCommand(OpenDescription);
        RemoveDescriptionCommand = new RelayCommand(RemoveDescription);
        OpenSubTaskEntryCommand = new RelayCommand(OpenSubTaskEntry);
        RemoveComposerSubTaskCommand = new ParameterCommand(p =>
        {
            if (p is string text) ComposerSubTasks.Remove(text);
            NotifyComposer();
        });
        SubmitComposerCommand = new RelayCommand(() => SubmitComposer(done: false));
        ShowAddedDayCommand = new RelayCommand(ShowAddedDay);

        _justAddedTimer.Tick += (_, _) => EndJustAdded();
    }

    public bool IsComposerOpen
    {
        get => _isComposerOpen;
        private set
        {
            _isComposerOpen = value;
            Notify();
            Notify(nameof(IsComposerClosed));
            Notify(nameof(DraftEditorHint));
            Notify(nameof(ComposerSummary));
            Notify(nameof(HasComposerSummary));
        }
    }

    public bool IsComposerClosed => !_isComposerOpen;

    // In the visual box the buttons are shortcuts into the same lines, so they teach the keys.
    public string? ComposerNoteChipTip => IsVisualWriting ? "Kısayol: Shift+Enter" : null;
    public string? ComposerSubTaskChipTip => IsVisualWriting ? "Kısayol: Shift+Enter, sonra - " : null;

    /// <summary>The empty title's hint in the visual box: where it goes while closed, what to type once open.</summary>
    public string DraftEditorHint => _isComposerOpen ? "İşin adını yaz…" : AddHint;

    public string ComposerDescription
    {
        get => _composerDescription;
        set
        {
            _composerDescription = value ?? "";
            Notify();
            NotifyComposer();
        }
    }

    public bool IsDescriptionOpen
    {
        get => _isDescriptionOpen;
        private set { _isDescriptionOpen = value; Notify(); Notify(nameof(ComposerHasNote)); }
    }

    public bool IsSubTaskEntryOpen
    {
        get => _isSubTaskEntryOpen;
        private set { _isSubTaskEntryOpen = value; Notify(); }
    }

    public string SubTaskEntryText
    {
        get => _subTaskEntryText;
        set { _subTaskEntryText = value ?? ""; Notify(); }
    }

    public ComposerField ActiveComposerField
    {
        get => _composerField;
        set
        {
            _composerField = value;
            Notify();
            Notify(nameof(ComposerKeyHint));
            Notify(nameof(IsAddHighlighted));
        }
    }

    /// <summary>What Enter and Esc do right now, in small print at the bottom right of the box.</summary>
    public string ComposerKeyHint => IsVisualWriting
        ? _visualKind switch
        {
            LineKind.Note or LineKind.SubNote => "- ile başlarsan alt iş olur · Enter: ekle",
            LineKind.SubTask => "Shift+Enter: açıklama · Enter: ekle",
            _ => "Enter: ekle · Shift+Enter: alt satır · Esc: kapat",
        }
        : _composerField switch
    {
        ComposerField.Description => "Enter: tamam",
        ComposerField.SubTask => "Enter: alt iş ekle · Esc: alt işler bitti",
        ComposerField.Frame => "Enter: ekle · Esc: kapat",
        _ => "Enter: ekle · Alt+Enter: alt satır · Esc: kapat",
    };

    private LineKind _visualKind;

    /// <summary>Set by the window: the kind of line the keyboard is on in the visual box, for the hint.</summary>
    public LineKind VisualKind
    {
        get => _visualKind;
        set
        {
            _visualKind = value;
            Notify(nameof(ComposerKeyHint));
        }
    }

    /// <summary>The keyboard left a description or the sub-tasks: the next Enter adds, so Ekle is lit.</summary>
    public bool IsAddHighlighted => _composerField == ComposerField.Frame;

    private NewTaskDraft.Resolution ResolveDraft(bool done = false)
    {
        var draft = new NewTaskDraft { Title = _draftText, ChosenDay = _composerChosenDay };

        // Finished work belongs to the day it was finished, even when typed on the Gelecek or Tarihsiz page.
        var defaultDay = done && _mode is ViewMode.Future or ViewMode.Undated ? _today : AddTarget;
        return draft.Resolve(_today, defaultDay);
    }

    public bool CanAddComposer => HasFile && ResolveDraft().Title.Length > 0;

    /// <summary>The day button: the page's own day, a day read from the title, or one picked by hand.</summary>
    public string ComposerDayText
    {
        get
        {
            var date = ResolveDraft().Date;
            return date == _today ? "Bugün" :
                date == Tomorrow ? "Yarın" :
                date == Day.Undated ? "Tarihsiz" :
                Capitalize(date.ToString(date.Year == _today.Year ? "d MMMM dddd" : "d MMMM yyyy dddd", Turkish));
        }
    }

    /// <summary>Which item of the day menu gets the tick: "today", "tomorrow", "undated", or empty for any other day.</summary>
    public string ComposerDayKey
    {
        get
        {
            var date = ResolveDraft().Date;
            return date == _today ? "today" : date == Tomorrow ? "tomorrow" : date == Day.Undated ? "undated" : "";
        }
    }

    /// <summary>Shown on the closed box when more than the title is waiting: "+ açıklama, 2 alt iş".</summary>
    public string ComposerSummary
    {
        get
        {
            var parts = new List<string>();
            if (IsVisualWriting)
            {
                // The visual box keeps everything in the text; closed, it shows the title only.
                var lines = TaskLines.Parse(_draftText).Skip(1).ToList();
                if (lines.Any(l => l.Kind == LineKind.Note && l.Text.Trim().Length > 0)) parts.Add("açıklama");
                var subTasks = lines.Count(l => l.Kind == LineKind.SubTask && l.Text.Trim().Length > 0);
                if (subTasks > 0) parts.Add($"{subTasks} alt iş");
                return parts.Count == 0 ? "" : "+ " + string.Join(", ", parts);
            }
            if (_composerDescription.Trim().Length > 0) parts.Add("açıklama");
            if (ComposerSubTasks.Count > 0) parts.Add($"{ComposerSubTasks.Count} alt iş");
            return parts.Count == 0 ? "" : "+ " + string.Join(", ", parts);
        }
    }

    public bool HasComposerSummary => !_isComposerOpen && ComposerSummary.Length > 0;

    public bool HasAddedElsewhere => _addedElsewhere is not null;

    /// <summary>The link after "İş yarına eklendi.": it opens the day the task went to.</summary>
    public string ShowAddedDayText =>
        _addedElsewhere is not { } date ? "" :
        date == _today ? "Bugüne git" :
        date == Tomorrow ? "Yarına git" :
        date == Day.Undated ? "Tarihsize git" :
        "O güne git";

    /// <summary>"＋ Açıklama" has nothing left to open: the description is already there.</summary>
    public bool ComposerHasNote => IsVisualWriting
        ? TaskLines.Parse(_draftText).Any(l => l.Kind == LineKind.Note)
        : IsDescriptionOpen;

    private void NotifyComposer()
    {
        Notify(nameof(DraftEditorHint));
        Notify(nameof(ComposerHasNote));
        Notify(nameof(CanAddComposer));
        Notify(nameof(ComposerDayText));
        Notify(nameof(ComposerDayKey));
        Notify(nameof(ComposerSummary));
        Notify(nameof(HasComposerSummary));
    }

    private void SetAddedElsewhere(DateTime? date)
    {
        _addedElsewhere = date;
        Notify(nameof(HasAddedElsewhere));
        Notify(nameof(ShowAddedDayText));
    }

    private void RequestComposerFocus(ComposerField? field)
    {
        if (field is { } f) ActiveComposerField = f;
        ComposerFocusRequested?.Invoke(field);
    }

    private void OpenComposer()
    {
        IsComposerOpen = true;
        RequestComposerFocus(ComposerField.Title);
    }

    /// <summary>The tour shows the open box without taking the keyboard from its own card.</summary>
    public void ShowComposer(bool open) => IsComposerOpen = open;

    /// <summary>A press anywhere outside the open box shrinks it like Esc; while it picks its day on the calendar, the press is that pick.</summary>
    public void ShrinkComposer()
    {
        if (_isComposerOpen && !_pickingForComposer) CloseComposer();
    }

    /// <summary>Vazgeç: the task is given up, nothing typed is kept.</summary>
    private void DiscardComposer()
    {
        if (_pickingForComposer) CancelPickDay();
        ClearComposer();
    }

    /// <summary>Esc or a press elsewhere: the box closes, what was typed stays for next time.</summary>
    private void CloseComposer()
    {
        if (_isSubTaskEntryOpen) KeepPendingSubTask();
        IsSubTaskEntryOpen = false;
        if (_composerDescription.Trim().Length == 0) IsDescriptionOpen = false;
        if (_pickingForComposer) CancelPickDay();
        IsComposerOpen = false;
        RequestComposerFocus(null);
        ActiveComposerField = ComposerField.Title;
    }

    private void ChooseComposerDay(string? which)
    {
        switch (which)
        {
            case "today": _composerChosenDay = _today; break;
            case "tomorrow": _composerChosenDay = Tomorrow; break;
            case "undated": _composerChosenDay = Day.Undated; break;
            case "calendar":
                _pickingEntries = null;
                _pickingDoneDate = false;
                _pickingForComposer = true;
                NotifyPicking();
                return;
            default: return;
        }
        NotifyComposer();
        RequestComposerFocus(ComposerField.Title);
    }

    private void OpenDescription()
    {
        IsDescriptionOpen = true;
        RequestComposerFocus(ComposerField.Description);
    }

    private void RemoveDescription()
    {
        ComposerDescription = "";
        IsDescriptionOpen = false;
        RequestComposerFocus(ComposerField.Title);
    }

    /// <summary>Enter in the description: it is done, the keyboard rests on the box and the next Enter adds the task.</summary>
    public void ConfirmDescription()
    {
        if (_composerDescription.Trim().Length == 0)
        {
            ComposerDescription = "";
            IsDescriptionOpen = false;
        }
        RequestComposerFocus(ComposerField.Frame);
    }

    private void OpenSubTaskEntry()
    {
        IsSubTaskEntryOpen = true;
        RequestComposerFocus(ComposerField.SubTask);
    }

    /// <summary>Enter in the sub-task line: it joins the list and the line empties for the next one.</summary>
    public void AddComposerSubTask()
    {
        var text = _subTaskEntryText.Trim();
        if (text.Length == 0) return;

        ComposerSubTasks.Add(text);
        SubTaskEntryText = "";
        NotifyComposer();
    }

    /// <summary>Esc in the sub-task line: the empty line closes, the sub-tasks stay, the next Enter adds the task.</summary>
    public void EndSubTaskEntry()
    {
        KeepPendingSubTask();
        IsSubTaskEntryOpen = false;
        RequestComposerFocus(ComposerField.Frame);
    }

    // A sub-task typed without its Enter is not thrown away.
    private void KeepPendingSubTask() => AddComposerSubTask();

    /// <summary>Writes the task with its description and sub-tasks; Ctrl+Enter passes done.</summary>
    public bool SubmitComposer(bool done)
    {
        if (!HasFile) return false;

        // Lines the visual box left empty ("- " with nothing after it) are not sub-tasks.
        if (IsVisualWriting) DraftText = TaskLines.TrimEmpty(_draftText);
        var resolved = ResolveDraft(done);
        if (resolved.Title.Length == 0) return false;

        KeepPendingSubTask();
        var description = _composerDescription.Trim().Length == 0 ? null : _composerDescription.Trim();
        var subTasks = ComposerSubTasks.ToList();
        var date = resolved.Date;

        EndJustAdded();
        var title = WithTime(resolved.Title, date);
        if (!Mutate(doc => _justAdded = doc.AddEntry(date, title, description, subTasks, done), ""))
        {
            _justAdded = null;
            return false;
        }

        var added = _justAdded;
        var onScreen = _groups.SelectMany(g => g.Entries).Any(r => r.Entry == added);
        StatusText = onScreen ? AddedMessage(date, done, dated: false) : AddedMessage(date, done);
        if (onScreen) _justAddedTimer.Start();
        else
        {
            _justAdded = null;
            SetAddedElsewhere(date);
        }

        ClearComposer();
        return true;
    }

    private void ClearComposer()
    {
        _composerChosenDay = null;
        DraftText = "";
        ComposerDescription = "";
        IsDescriptionOpen = false;
        ComposerSubTasks.Clear();
        SubTaskEntryText = "";
        IsSubTaskEntryOpen = false;
        IsComposerOpen = false;
        NotifyComposer();
        RequestComposerFocus(null);
        ActiveComposerField = ComposerField.Title;
    }

    /// <summary>The frame round a just-added row goes out.</summary>
    private void EndJustAdded()
    {
        _justAddedTimer.Stop();
        _justAdded = null;
        foreach (var row in _groups.SelectMany(g => g.Entries).Where(r => r.IsJustAdded)) row.IsJustAdded = false;
    }

    private void ShowAddedDay()
    {
        if (_addedElsewhere is not { } date) return;

        if (date == Day.Undated) Navigate(ViewMode.Undated);
        else ShowDay(date);
    }

    /// <summary>A day clicked in a calendar while choosing the new task's day: it becomes the day, no page opens.</summary>
    private bool TakeComposerDay(DateTime date)
    {
        if (!_pickingForComposer) return false;

        _pickingForComposer = false;
        _composerChosenDay = date;
        NotifyPicking();
        NotifyComposer();
        RequestComposerFocus(ComposerField.Title);
        return true;
    }
}
