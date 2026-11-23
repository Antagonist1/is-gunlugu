using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using IsGunlugu.Core;

namespace IsGunlugu;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>The rows of one page (Bugün, Tümü, a search…), kept while another page is on screen.</summary>
public sealed class PageList(string key) : ObservableObject
{
    private bool _isCurrent;

    public string Key { get; } = key;
    public ObservableCollection<DayGroup> Groups { get; } = new();

    public bool IsCurrent
    {
        get => _isCurrent;
        set { _isCurrent = value; Notify(); }
    }
}

/// <summary>One count under a day's date: its words, and which colour (done, part, open, dropped).</summary>
public sealed class DayChip(string text, string kind)
{
    public string Text { get; } = text;
    public string Kind { get; } = kind;
}

public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}

/// <summary>A command whose XAML CommandParameter says which of several things to do.</summary>
public sealed class ParameterCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
}

public sealed class EntryViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private bool _isEditing;
    private string _editText = "";
    private bool _isAddingSubTask;
    private string _subTaskDraft = "";
    private bool _showDropAbove;
    private bool _showDropBelow;
    private bool _isSelected;
    private bool _isJustAdded;

    public EntryViewModel(MainViewModel main, Entry entry)
    {
        _main = main;
        Entry = entry;
        _content = ContentOf(entry);
        BodyItems = BuildBody(main, entry);

        var tasks = entry.SubTasks;
        var active = entry.ActiveSubTasks; // sub-tasks that were given up are not counted
        Progress = active.Count == 0 ? "" : $"{active.Count(t => t.IsDone)}/{active.Count}";

        EditCommand = new RelayCommand(() => _main.BeginEdit(this));
        DeleteCommand = new RelayCommand(() => _main.Delete(this));
        SaveCommand = new RelayCommand(() => _main.SaveEdit(this));
        CancelCommand = new RelayCommand(() => IsEditing = false);
        BeginSubTaskCommand = new RelayCommand(() => _main.BeginSubTask(this));
        AddSubTaskCommand = new RelayCommand(() => _main.AddSubTask(this));
        EndSubTaskCommand = new RelayCommand(() => _main.EndSubTask(this));
        BringToTodayCommand = new RelayCommand(() => _main.BringToToday(this));
    }

    public Entry Entry { get; private set; }

    /// <summary>The task's time label, or null when it has none.</summary>
    public TimeChip? Chip { get; set; }

    /// <summary>The task's lines when the row was made: the entry is changed in place, so this tells what the row shows.</summary>
    private readonly string _content;

    private static string ContentOf(Entry entry) => entry.FirstLine + "\n" + string.Join("\n", entry.Body);

    /// <summary>
    /// This row, left on screen, would show exactly what the freshly made one does. A row being typed in or marked for a
    /// drop is never kept, so the list behaves as it did when every row was made again.
    /// </summary>
    public bool CanStandFor(EntryViewModel fresh) =>
        !IsBusy && !ShowDropAbove && !ShowDropBelow
        && _content == fresh._content
        && (Chip == null ? fresh.Chip == null : Chip.LooksLike(fresh.Chip))
        && Date == fresh.Date && CanReorder == fresh.CanReorder && Origin == fresh.Origin
        && CanBringToToday == fresh.CanBringToToday && CanMakeUndated == fresh.CanMakeUndated && CanMoveToFuture == fresh.CanMoveToFuture
        && IsInDetail == fresh.IsInDetail && IsSelected == fresh.IsSelected && IsJustAdded == fresh.IsJustAdded
        && IsAddingSubTask == fresh.IsAddingSubTask && IsCollapsed == fresh.IsCollapsed        && EarlierText == fresh.EarlierText && IsEarlierOpen == fresh.IsEarlierOpen
        && BodyItems.Count == fresh.BodyItems.Count
        && BodyItems.Zip(fresh.BodyItems, (a, b) => a.LooksLike(b)).All(x => x);

    /// <summary>
    /// A file read again (undo, a change from Drive) brings new objects with the same lines: the row that stays points
    /// at them, so what is done on it changes the document now open.
    /// </summary>
    public void TakeOver(EntryViewModel fresh)
    {
        if (ReferenceEquals(Entry, fresh.Entry)) return;

        Entry = fresh.Entry;
        for (var i = 0; i < BodyItems.Count; i++) BodyItems[i].Task = fresh.BodyItems[i].Task;
    }

    public string Text => Entry.Text;

    /// <summary>The task has description lines of its own (the tray panel marks it, without showing them).</summary>
    public bool HasNoteLines => Entry.Parts.Any(p => p.IsNote);

    public IList<BodyItemViewModel> BodyItems { get; }
    public bool HasBody => BodyItems.Count > 0;

    private const int TrayShown = 3;

    /// <summary>The tray panel: the first open sub-tasks only, and how many more wait behind them.</summary>
    public IList<BodyItemViewModel> NextTasks => BodyItems.Where(b => b.IsTask && !b.IsDone).Take(TrayShown).ToList();
    public int MoreTasks => Math.Max(0, BodyItems.Count(b => b.IsTask && !b.IsDone) - TrayShown);
    public bool HasMoreTasks => MoreTasks > 0;

    /// <summary>The sub-tasks and notes are folded away; the count and an arrow stay on the line.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            // Folded, a task keeps its own description in sight; only the sub-tasks go.
            foreach (var item in BodyItems) item.IsFoldHidden = value && item.IsTask;
            Notify();
            Notify(nameof(ShowBody));
            Notify(nameof(BodyToggleTip));
        }
    }

    private bool _isCollapsed;

    public bool ShowBody => HasBody && (!IsCollapsed || (!IsNoteOnly && BodyItems.Any(b => !b.IsTask)));

    /// <summary>Only a description under the title: a note sign stands where the sub-task count would.</summary>
    public bool IsNoteOnly => HasBody && BodyItems.All(b => b.Task is null);

    public string BodyToggleTip => IsNoteOnly
        ? (IsCollapsed ? "Açıklamayı göster" : "Açıklamayı gizle")
        : (IsCollapsed ? "Alt işleri göster" : "Alt işleri gizle");

    public ICommand ToggleBodyCommand { get; init; } = null!;

    /// <summary>"✓ 2 alt iş 12 Kasım'da bitti": sub-tasks finished before the day the task sits under, in one line.</summary>
    public string EarlierText { get; set; } = "";

    public bool HasEarlier => EarlierText.Length > 0;

    /// <summary>The earlier sub-tasks are shown in their places, with their days.</summary>
    public bool IsEarlierOpen
    {
        get => _isEarlierOpen;
        set
        {
            _isEarlierOpen = value;
            foreach (var (item, day) in _earlier)
            {
                item.DoneDateText = value ? day : "";
                item.IsFoldedAway = !value;
            }
            Notify();
        }
    }

    private bool _isEarlierOpen;
    private IList<(BodyItemViewModel Item, string Day)> _earlier = [];

    /// <summary>The sub-tasks finished on earlier days, each with the short day shown beside it while unfolded.</summary>
    public void SetEarlier(string text, IList<(BodyItemViewModel Item, string Day)> items, bool open)
    {
        EarlierText = text;
        _earlier = items;
        IsEarlierOpen = open;
    }

    public ICommand ToggleEarlierCommand { get; init; } = null!;

    public DateTime Date { get; init; }

    /// <summary>Open work dated any other day, or undated, which can be brought to today.</summary>
    public bool CanBringToToday { get; init; }

    /// <summary>Open work with a date, which can be moved to the undated list.</summary>
    public bool CanMakeUndated { get; init; }

    /// <summary>Open work not already ahead of today, which can be put off to tomorrow.</summary>
    public bool CanMoveToFuture { get; init; }

    /// <summary>The row sits in a single day's list, so its order can be changed.</summary>
    public bool CanReorder { get; init; }

    /// <summary>Shown when the row is listed away from its own day.</summary>
    public string Origin { get; init; } = "";
    public bool HasOrigin => Origin.Length > 0;

    public ICommand BringToTodayCommand { get; }

    /// <summary>This task is the one in the side panel.</summary>
    public bool IsInDetail { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Notify(); }
    }

    /// <summary>The task was just added from the box: a frame shows where it landed, then fades.</summary>
    public bool IsJustAdded
    {
        get => _isJustAdded;
        set { _isJustAdded = value; Notify(); }
    }

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
    public string Progress { get; }
    public bool HasProgress => Progress.Length > 0;

    public ICommand BeginSubTaskCommand { get; }
    public ICommand AddSubTaskCommand { get; }
    public ICommand EndSubTaskCommand { get; }

    public bool IsAddingSubTask
    {
        get => _isAddingSubTask;
        set { _isAddingSubTask = value; Notify(); }
    }

    public string SubTaskDraft
    {
        get => _subTaskDraft;
        set { _subTaskDraft = value; Notify(); }
    }
    public bool IsBusy => IsEditing || IsAddingSubTask || BodyItems.Any(b => b.IsEditing);

    private static List<BodyItemViewModel> BuildBody(MainViewModel main, Entry entry)
    {
        var items = new List<BodyItemViewModel>();
        var notes = new List<string>();

        void FlushNotes()
        {
            if (notes.Count == 0) return;
            items.Add(new BodyItemViewModel(main, string.Join("\n", notes)));
            notes.Clear();
        }

        foreach (var part in entry.Parts.Where(p => !p.IsDescription))
        {
            if (part.Task is null)
            {
                notes.Add(part.Line.Replace("\t", "    "));
                continue;
            }

            FlushNotes();
            items.Add(new BodyItemViewModel(main, part.Task));
        }
        FlushNotes();
        return items;
    }

    public bool IsDone => Entry.Status == EntryStatus.Done;

    /// <summary>Null draws the dash of a partly done task; clicking it finishes the rest.</summary>
    public bool? CheckState
    {
        get => Entry.IsPartlyDone ? null : IsDone;
        set
        {
            // WPF toggles a null check to false, but leaving the dash always means finishing the rest.
            var done = Entry.IsPartlyDone || value == true;
            if (Entry.IsPartlyDone || done != IsDone) _main.Toggle(this, done);
        }
    }

    public bool IsOpen => Entry.Status == EntryStatus.Open;
    public bool IsNote => Entry.Status == EntryStatus.Note;

    /// <summary>Given up: a cross in place of the box, the title struck through.</summary>
    public bool IsDropped => Entry.IsDropped;

    public bool HasNoCheck => IsNote || IsDropped;

    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; Notify(); }
    }

    public string EditText
    {
        get => _editText;
        set { _editText = value; Notify(); }
    }

    /// <summary>The finished sub-tasks, so the visual edit box shows their ticks; saving keeps them by their lines.</summary>
    public IList<string> DoneSubTasks => Entry.SubTasks.Where(t => t.IsDone).Select(t => t.Text).ToList();
}

/// <summary>What sits under a task: a sub-task with its own check, or a block of plain note lines.</summary>
public sealed class BodyItemViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private bool _isEditing;
    private string _editText = "";

    public BodyItemViewModel(MainViewModel main, string note)
    {
        _main = main;
        Text = note;
        EditCommand = SaveCommand = CancelCommand = DeleteCommand = new RelayCommand(() => { });
    }

    public BodyItemViewModel(MainViewModel main, SubTask task)
    {
        _main = main;
        Task = task;
        Text = task.Text;
        Description = string.Join("\n", task.DescriptionLines);
        IsInDetail = main.IsSubTaskInDetail(task);
        EditCommand = new RelayCommand(() => _main.BeginEdit(this));
        SaveCommand = new RelayCommand(() => _main.SaveEdit(this));
        CancelCommand = new RelayCommand(() => IsEditing = false);
        DeleteCommand = new RelayCommand(() => _main.Delete(this));
    }

    public SubTask? Task { get; internal set; }

    /// <summary>Drawn the same as the other one; what they say is compared through their task's lines.</summary>
    internal bool LooksLike(BodyItemViewModel other) =>
        Text == other.Text && DoneDateText == other.DoneDateText && IsFoldedAway == other.IsFoldedAway
        && HasFold == other.HasFold && IsInDetail == other.IsInDetail && !IsEditing;

    /// <summary>Given up on its own: a cross in place of its box.</summary>
    public bool IsDropped => Task?.IsDropped == true;

    /// <summary>"12 Kas" beside a sub-task finished on an earlier day, shown while those are unfolded; else empty.</summary>
    public string DoneDateText
    {
        get => _doneDateText;
        set
        {
            if (_doneDateText == value) return;
            _doneDateText = value;
            Notify();
            Notify(nameof(HasDoneDate));
        }
    }

    private string _doneDateText = "";

    public bool HasDoneDate => DoneDateText.Length > 0;

    /// <summary>A sub-task of a folded task: hidden, while the task's description stays.</summary>
    public bool IsFoldHidden
    {
        get => _isFoldHidden;
        set
        {
            if (_isFoldHidden == value) return;
            _isFoldHidden = value;
            Notify();
        }
    }

    private bool _isFoldHidden;

    /// <summary>Finished on an earlier day and folded into the task's "✓ 2 alt iş 12 Kasım'da bitti" line.</summary>
    public bool IsFoldedAway
    {
        get => _isFoldedAway;
        set
        {
            if (_isFoldedAway == value) return;
            _isFoldedAway = value;
            Notify();
        }
    }

    private bool _isFoldedAway;

    /// <summary>On the first sub-task: the task whose earlier-days line stands above it, under the description.</summary>
    public EntryViewModel? FoldOwner { get; set; }

    public bool HasFold => FoldOwner is { HasEarlier: true };

    /// <summary>Struck through: given up itself, or part of a task that was given up as a whole.</summary>
    public bool IsStruck => IsDropped || Task?.Owner.IsDropped == true;

    /// <summary>This sub-task is the one open in the side panel. Read when the row is made, as the screen is rebuilt when that changes.</summary>
    public bool IsInDetail { get; }

    public string Text { get; }
    public string Description { get; } = "";
    public bool HasDescription => Description.Length > 0;
    public bool IsTask => Task is not null;
    public bool IsNote => Task is null;

    public bool IsDone
    {
        get => Task?.IsDone ?? false;
        set
        {
            if (value != IsDone) _main.Toggle(this);
        }
    }

    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; Notify(); }
    }

    public string EditText
    {
        get => _editText;
        set { _editText = value; Notify(); }
    }
}

public sealed class DayGroup(string? title, ICommand? open, IList<EntryViewModel> entries) : ObservableObject
{
    private string? _title = title;

    public string? Title
    {
        get => _title;
        private set { _title = value; Notify(); }
    }

    public bool HasTitle => Title is not null;
    public ICommand? OpenCommand { get; } = open;
    public bool HasCommand => OpenCommand is not null;

    /// <summary>Changed in place after every edit, so only the rows that changed are drawn again.</summary>
    public ObservableCollection<EntryViewModel> Entries { get; } = new(entries);

    /// <summary>What makes two headings the same one; a Tümü heading keeps its key while the count in its title changes.</summary>
    public string? Key { get; init; } = title;

    public bool CanStandFor(DayGroup fresh) =>
        Key == fresh.Key && HasTitle == fresh.HasTitle && HasCommand == fresh.HasCommand
        && TitleToolTip == fresh.TitleToolTip && IsCollapsible == fresh.IsCollapsible && IsCollapsed == fresh.IsCollapsed && IsDoneFold == fresh.IsDoneFold
        && EmptyText == fresh.EmptyText;

    /// <summary>Takes over the fresh heading's title and rows, keeping the rows that did not change.</summary>
    public void TakeOver(DayGroup fresh)
    {
        if (Title != fresh.Title) Title = fresh.Title;
        ListPatch.Apply(Entries, fresh.Entries, (old, row) => old.CanStandFor(row), (old, row) => old.TakeOver(row));
        Notify(nameof(ShowEmptyText));
    }

    public string TitleToolTip { get; init; } = "Bu günü aç";

    /// <summary>The day's finished work under a thin line: "✓ 4 iş bitti", folded until opened.</summary>
    public bool IsDoneFold { get; init; }

    /// <summary>A heading of the Tümü screen, which opens and closes its list.</summary>
    public bool IsCollapsible { get; init; }

    public bool IsCollapsed { get; init; }
    public bool ShowEntries => !IsCollapsed;

    /// <summary>What an empty list says instead of being blank.</summary>
    public string EmptyText { get; init; } = "";
    public bool ShowEmptyText => Entries.Count == 0 && EmptyText.Length > 0;
}

public sealed class FileRow(string path, bool isCurrent, string note, ICommand select, ICommand remove) : PickerRow
{
    public string Path { get; } = path;
    public override string Name { get; } = System.IO.Path.GetFileNameWithoutExtension(path);
    public bool IsCurrent { get; } = isCurrent;

    /// <summary>Open work count, or why the file cannot be opened.</summary>
    public string Note { get; } = note;

    public ICommand SelectCommand { get; } = select;
    public ICommand RemoveCommand { get; } = remove;

    public string? GroupName { get; init; }
    public bool IsGrouped => GroupName is not null;

    /// <summary>What the cross does here: delete a file of the notes folder, or only take one from elsewhere off the list.</summary>
    public string RemoveTip { get; init; } = "";
}

/// <summary>A line of the file picker: renamed in place, and marked where a dragged line would land.</summary>
public abstract class PickerRow : ObservableObject
{
    private bool _isRenaming;
    private string _renameText = "";
    private bool _showDropAbove;
    private bool _showDropBelow;

    public abstract string Name { get; }

    public bool IsRenaming
    {
        get => _isRenaming;
        set { _isRenaming = value; Notify(); }
    }

    public string RenameText
    {
        get => _renameText;
        set { _renameText = value; Notify(); }
    }

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
}

public sealed class FileMove(string from, string to)
{
    public string From { get; } = from;
    public string To { get; } = to;
}

public sealed class FileGroupRow(string name, int openCount, bool isCollapsed, bool hidesCurrent, ICommand toggle, ICommand rename, ICommand delete) : PickerRow
{
    public override string Name { get; } = name;
    public string Note { get; } = openCount == 0 ? "" : openCount.ToString();
    public bool IsCollapsed { get; } = isCollapsed;

    /// <summary>The open file is inside this closed group, so the heading marks it instead.</summary>
    public bool HidesCurrent { get; } = hidesCurrent;

    /// <summary>A group made from the picker before any file was dragged into it.</summary>
    public bool IsEmpty { get; init; }

    public ICommand ToggleCommand { get; } = toggle;
    public ICommand RenameCommand { get; } = rename;
    public ICommand DeleteCommand { get; } = delete;
}

public sealed class CellLine(string text, bool isOpen)
{
    public string Text { get; } = text;
    public bool IsOpen { get; } = isOpen;
}

/// <summary>One day in both the small and the full month calendar.</summary>
public sealed class MonthCell : ObservableObject
{
    private const int MaxLines = 3;

    private bool _isDropTarget;

    /// <param name="worked">Tasks with something finished that day, wherever they sit now; the green follows it.</param>
    public MonthCell(DateTime date, bool inMonth, bool isToday, bool isSelected,
        IList<Entry> entries, string name, ICommand open, int? worked = null)
    {
        Date = date;
        Day = date.Day;
        InMonth = inMonth;
        IsToday = isToday;
        IsSelected = isSelected;
        Name = name;
        OpenCommand = open;

        var done = worked ?? entries.Count(e => e.IsFinished);
        HasOpen = entries.Any(e => e.HasOpenWork);
        HeatLevel = done switch { 0 => 0, <= 2 => 1, <= 4 => 2, _ => 3 };

        // Open work is what a month view is read for, so it comes first when the cell is full.
        var ordered = entries.OrderBy(e => e.HasOpenWork ? 0 : 1).ToList();
        var shown = ordered.Count > MaxLines ? MaxLines - 1 : ordered.Count;
        Lines = ordered.Take(shown).Select(e => new CellLine(e.Text, e.HasOpenWork)).ToList();
        More = ordered.Count > shown ? $"{ordered.Count - shown} iş daha" : "";
    }

    public DateTime Date { get; }

    /// <summary>The day the arrow keys are on while a day is being picked for moving work.</summary>
    public bool IsPickCursor { get; init; }

    /// <summary>The cell would look the same as the fresh one, so it can stay on screen.</summary>
    public bool CanStandFor(MonthCell fresh) =>
        Date == fresh.Date && !IsDropTarget && InMonth == fresh.InMonth && IsToday == fresh.IsToday && IsSelected == fresh.IsSelected
        && IsPickCursor == fresh.IsPickCursor
        && Name == fresh.Name && HasOpen == fresh.HasOpen && HeatLevel == fresh.HeatLevel && More == fresh.More
        && Lines.Count == fresh.Lines.Count
        && Lines.Zip(fresh.Lines, (a, b) => a.Text == b.Text && a.IsOpen == b.IsOpen).All(x => x);

    /// <summary>A dragged task is over this day.</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set { _isDropTarget = value; Notify(); }
    }

    public int Day { get; }
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public bool InMonth { get; }
    public bool IsToday { get; }
    public bool IsSelected { get; }
    public string Name { get; }
    public ICommand OpenCommand { get; }
    public bool HasOpen { get; }
    public int HeatLevel { get; }
    public IList<CellLine> Lines { get; }
    public string More { get; }
    public bool HasMore => More.Length > 0;
}

/// <summary>
/// Under a day's list: a task that sits under another day but had something finished on this one, faded, with only
/// what was finished here. A press opens the day it sits under.
/// </summary>
public sealed class EchoRow(string title, string count, string note, bool noteDone, IList<string> doneHere, bool taskDoneHere, ICommand open)
    : ObservableObject
{
    public string Title { get; } = title;

    /// <summary>How far along the task was at the end of this day, as "2/4"; empty without sub-tasks.</summary>
    public string Count { get; } = count;

    /// <summary>"devamı 14 Kasım'da", "✓ 14 Kasım'da bitti", or "12 Kasım'daki iş" for a task under an earlier day.</summary>
    public string Note { get; } = note;

    /// <summary>The task was finished later: the note is in the finished colour.</summary>
    public bool NoteDone { get; } = noteDone;

    public IList<string> DoneHere { get; } = doneHere;

    /// <summary>The task itself was finished on this day: a ticked box; otherwise the half box of work under way.</summary>
    public bool TaskDoneHere { get; } = taskDoneHere;

    public ICommand OpenCommand { get; } = open;

    /// <summary>Folded like the task's own row: one fold, wherever the task is seen.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            Notify();
            Notify(nameof(ShowDoneHere));
            Notify(nameof(FoldTip));
        }
    }

    private bool _isCollapsed;

    public bool ShowDoneHere => DoneHere.Count > 0 && !IsCollapsed;
    public bool CanFold => DoneHere.Count > 0;
    public string FoldTip => IsCollapsed ? "Alt işleri göster" : "Alt işleri gizle";
    public ICommand ToggleCommand { get; init; } = null!;
}

/// <summary>A note file in the Win+Shift+N box's list.</summary>
public sealed class QuickAddFile(string path, string name, string? group, bool isOpen)
{
    public string Path { get; } = path;
    public string Name { get; } = name;
    public string? Group { get; } = group;
    /// <summary>Open in the window.</summary>
    public bool IsOpen { get; } = isOpen;
}
