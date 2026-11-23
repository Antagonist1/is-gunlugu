using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using IsGunlugu.Core;
using Microsoft.Win32;

namespace IsGunlugu;

public enum ViewMode { Today, Calendar, Open, Future, Undated, All, Day }

public sealed class DayEntries(DateTime date, List<Entry> entries)
{
    public DateTime Date { get; } = date;
    public List<Entry> Entries { get; } = entries;
}

public sealed partial class MainViewModel : ObservableObject
{
    private const int UndoLimit = 50;
    private const int CalendarCells = 42;

    private static readonly CultureInfo Turkish = new("tr-TR");

    private static readonly string BackupDirectory = Path.Combine(Settings.Directory, "yedekler");

    private static readonly string DefaultNotesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "İş Günlüğü");

    private static readonly HashSet<string> ReservedFolderNames = new(
        new[] { "CON", "PRN", "AUX", "NUL" }
            .Concat(Enumerable.Range(1, 9).SelectMany(i => new[] { $"COM{i}", $"LPT{i}" })));

    private readonly bool _pathsFromCommandLine;

    /// <summary>Where notes and their groups live on disk; null for a session on files from the command line.</summary>
    private readonly string? _notesFolder;
    private readonly Settings _settings = Settings.Load();
    private readonly List<string> _filePaths = new();
    private readonly Dictionary<string, string> _otherFileNotes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _undo = new();
    private readonly DispatcherTimer _changeTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };

    private NoteStore? _store;
    private NoteDocument _document = NoteDocument.Parse("");
    private string _loadedText = "";
    private FileSystemWatcher? _watcher;
    private Entry? _addingSubTaskTo;
    private DateTime _today = DateTime.Today;

    private ViewMode _mode = ViewMode.Today;
    private DateTime _selectedDate;
    private DateTime _visibleMonth;
    private string _searchText = "";
    private string _draftText = "";
    private string _title = "";
    private string _subtitle = "";
    private string _emptyText = "";
    private string _statusText = "";
    private string? _fileError;
    private readonly ObservableCollection<PageList> _pageLists = new();
    private PageList? _pageList;

    /// <summary>The rows of the page on screen; the other pages keep theirs, hidden, see <see cref="PageLists"/>.</summary>
    private ObservableCollection<DayGroup> _groups => _pageList?.Groups ?? EmptyGroups;
    private static readonly ObservableCollection<DayGroup> EmptyGroups = new();
    private readonly ObservableCollection<MonthCell> _monthCells = new();
    private IList<object> _files = [];
    private string _monthTitle = "";
    private string _monthSummary = "";
    private int _openCount;
    private int _futureCount;
    private int _undatedCount;
    private bool _isNamePromptOpen;
    private string _namePromptTitle = "";
    private string _namePromptText = "";
    private string _namePromptError = "";
    private Func<string, string?>? _namePromptConfirm;
    private int _allCount;
    private readonly StartupRegistration _startup = new();
    private readonly HashSet<Entry> _selected = new();
    private Entry? _selectionAnchor;
    private List<Entry>? _pickingEntries;
    private bool _isHelpOpen;
    private bool _isReleaseNotesOpen;
    private bool _isUserGuideOpen;
    private IList<ReleaseNote>? _releaseNoteList;
    private bool _isSettingsOpen;
    private bool _canGoBack;
    private bool _canGoForward;

    public MainViewModel(IList<string> paths, string? notesFolder = null)
    {
        _pathsFromCommandLine = paths.Count > 0;
        _notesFolder = notesFolder ?? (_pathsFromCommandLine ? null : DefaultNotesFolder);
        _visibleMonth = FirstOfMonth(_today);

        ShowTodayCommand = new RelayCommand(() => ShowDay(_today));
        ShowCalendarCommand = new RelayCommand(() => Navigate(ViewMode.Calendar));
        ShowOpenCommand = new RelayCommand(() => Navigate(ViewMode.Open));
        BringAllToTodayCommand = new RelayCommand(BringAllToToday);
        ShowFutureCommand = new RelayCommand(() => Navigate(ViewMode.Future));
        ShowUndatedCommand = new RelayCommand(() => Navigate(ViewMode.Undated));
        ShowAllCommand = new RelayCommand(() => Navigate(ViewMode.All));
        ToggleTaskGroupCommand = new RelayCommand(ToggleTaskGroup);
        ConfirmNamePromptCommand = new RelayCommand(ConfirmNamePrompt);
        CancelNamePromptCommand = new RelayCommand(() => IsNamePromptOpen = false);
        PreviousMonthCommand = new RelayCommand(() => ShiftMonth(-MiniStep));
        NextMonthCommand = new RelayCommand(() => ShiftMonth(MiniStep));
        BackCommand = new RelayCommand(GoBack);
        ForwardCommand = new RelayCommand(GoForward);
        AddOpenCommand = new RelayCommand(() => SubmitComposer(done: false));
        ToggleHelpCommand = new RelayCommand(() => IsHelpOpen = !IsHelpOpen);
        SelectAllCommand = new RelayCommand(SelectAll);
        PickDayForSelectedCommand = new RelayCommand(() => StartPicking(SelectedInListOrder()));
        CancelPickDayCommand = new RelayCommand(CancelPickDay);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        BringSelectedToTodayCommand = new RelayCommand(() => MoveSelectedToDay(_today));
        MoveSelectedToTomorrowCommand = new RelayCommand(() => MoveSelectedToDay(Tomorrow));
        MakeSelectedUndatedCommand = new RelayCommand(() => MoveSelectedToDay(Day.Undated));
        OpenUserGuideCommand = new RelayCommand(() =>
        {
            IsSettingsOpen = false;
            IsUserGuideOpen = true;
        });
        CloseUserGuideCommand = new RelayCommand(() => IsUserGuideOpen = false);
        ShowReleaseNotesCommand = new RelayCommand(() =>
        {
            IsSettingsOpen = false;
            IsReleaseNotesOpen = true;
        });
        CloseReleaseNotesCommand = new RelayCommand(() => IsReleaseNotesOpen = false);
        ToggleSettingsCommand = new RelayCommand(() => IsSettingsOpen = !IsSettingsOpen);
        UseSystemThemeCommand = new RelayCommand(() => SetTheme(ThemeChoice.System));
        UseLightThemeCommand = new RelayCommand(() => SetTheme(ThemeChoice.Light));
        UseDarkThemeCommand = new RelayCommand(() => SetTheme(ThemeChoice.Dark));
        UndoCommand = new RelayCommand(Undo);
        ReloadCommand = new RelayCommand(Reload);
        OpenErrorLogCommand = new RelayCommand(OpenErrorLog);
        ImportFileCommand = new RelayCommand(ImportFile);
        CreateFileCommand = new RelayCommand(BeginNewFile);
        OpenNotesFolderCommand = new RelayCommand(OpenNotesFolder);
        OpenInEditorCommand = new RelayCommand(OpenInEditor);

        _changeTimer.Tick += (_, _) =>
        {
            _changeTimer.Stop();
            PickUpExternalChange();
            RequestSync(); // Fires for this program's own saves too.
        };
        _clock.Tick += (_, _) => RollOverDay();
        _clock.Start();

        if (!_pathsFromCommandLine) RefreshStartupEntry();

        _filePaths.AddRange(_pathsFromCommandLine ? paths.Select(Path.GetFullPath) : _settings.NotesPaths);
        var sorted = !_pathsFromCommandLine && SortOlderFiles();

        // Start on the last file; if it is gone, on the first one that still opens.
        var candidates = _pathsFromCommandLine ? _filePaths : new[] { _settings.NotesPath }.OfType<string>().Concat(_filePaths);
        foreach (var candidate in candidates.ToList())
        {
            if (OpenFile(candidate)) break;
        }

        if (sorted) StatusText = $"Günler dosyada da sıralandı. Eski halleri: {Path.Combine(BackupDirectory, DayOrderUpgrade.BackupFolderName)}";
        InitFirstRun();
        InitDeleteFile();
        InitDetail();
        InitComposer();
        InitCalendarScales();
        InitSidebar();
        InitFilePicker();
        InitDayEnd();
        InitDetailTime();
        InitBackups();
        InitSync();
    }

    /// <summary>Runs until every file known at the upgrade has been handled, and never again after that.</summary>
    private bool SortOlderFiles()
    {
        if (_settings.DaysSortedInFiles) return false;

        var waiting = _settings.FilesToSort ?? _filePaths.ToList();
        var any = false;
        var left = new List<string>();
        foreach (var path in waiting)
        {
            if (!DayOrderUpgrade.SortFile(path, BackupDirectory, out var changed)) left.Add(path);
            any |= changed;
        }

        _settings.FilesToSort = left;
        _settings.DaysSortedInFiles = left.Count == 0;
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The sort is already done; next start finds the files in order and writes nothing.
        }
        return any;
    }

    public ICommand ShowTodayCommand { get; }
    public ICommand ShowCalendarCommand { get; }
    public ICommand ShowOpenCommand { get; }
    public ICommand BringAllToTodayCommand { get; }
    public bool ShowBringAll => IsOpenView && OpenCount > 0;
    public ICommand ShowFutureCommand { get; }
    public ICommand ShowUndatedCommand { get; }
    public ICommand ShowAllCommand { get; }
    public ICommand ToggleTaskGroupCommand { get; }
    public ICommand ConfirmNamePromptCommand { get; }
    public ICommand CancelNamePromptCommand { get; }
    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ForwardCommand { get; }
    public ICommand AddOpenCommand { get; }
    public ICommand ToggleHelpCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand PickDayForSelectedCommand { get; }
    public ICommand CancelPickDayCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand BringSelectedToTodayCommand { get; }
    public ICommand MoveSelectedToTomorrowCommand { get; }
    public ICommand MakeSelectedUndatedCommand { get; }



    public string AppVersion
    {
        get
        {
            var version = typeof(MainViewModel).Assembly.GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public ICommand OpenUserGuideCommand { get; }
    public ICommand CloseUserGuideCommand { get; }
    public ICommand ShowReleaseNotesCommand { get; }
    public ICommand CloseReleaseNotesCommand { get; }

    public bool IsReleaseNotesOpen
    {
        get => _isReleaseNotesOpen;
        set
        {
            _isReleaseNotesOpen = value;
            if (value) ResetReleaseNotes();
            Notify();
        }
    }

    public IList<ReleaseNote> ReleaseNoteList => _releaseNoteList ??= ReadReleaseNotes();

    public bool IsUserGuideOpen
    {
        get => _isUserGuideOpen;
        set { _isUserGuideOpen = value; Notify(); }
    }

    private static IList<ReleaseNote> ReadReleaseNotes()
    {
        using var stream = typeof(MainViewModel).Assembly.GetManifestResourceStream("CHANGELOG.md");
        if (stream is null) return new List<ReleaseNote>();

        using var reader = new StreamReader(stream);
        return ReleaseNotes.Parse(reader.ReadToEnd());
    }

    public event Action? ThemeChanged;

    public ICommand ToggleSettingsCommand { get; }
    public ICommand UseSystemThemeCommand { get; }
    public ICommand UseLightThemeCommand { get; }
    public ICommand UseDarkThemeCommand { get; }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set { _isSettingsOpen = value; Notify(); }
    }

    public ThemeChoice Theme => _settings.Theme;
    public bool IsSystemTheme => Theme == ThemeChoice.System;
    public bool IsLightTheme => Theme == ThemeChoice.Light;
    public bool IsDarkThemeChosen => Theme == ThemeChoice.Dark;

    public bool StartWithWindows
    {
        get => _startup.IsEnabled;
        set
        {
            try
            {
                if (!_pathsFromCommandLine) _startup.Set(value, StartupRegistration.ExePath);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                StatusText = $"Otomatik başlatma ayarlanamadı: {ex.Message}";
            }
            Notify();
        }
    }

    public bool CloseToTray
    {
        get => _settings.CloseToTray ?? true;
        set
        {
            _settings.CloseToTray = value;
            SaveSettings();
            Notify();
        }
    }

    /// <summary>The window's close button hides it only when the tray icon exists.</summary>
    public bool HidesOnClose => CloseToTray && !_pathsFromCommandLine;

    public bool ShouldShowGuide => !_pathsFromCommandLine && !_settings.GuideShown && !_firstRun.IsReturning;

    public void MarkGuideShown()
    {
        _settings.GuideShown = true;
        SaveSettings();
    }

    /// <summary>True once: the first time the window goes to the tray.</summary>
    public bool TakeTrayHint()
    {
        if (_settings.TrayHintShown) return false;

        _settings.TrayHintShown = true;
        SaveSettings();
        return true;
    }

    private void ToggleSection(string key)
    {
        CloseEditors();
        if (!_settings.CollapsedSections.Remove(key)) _settings.CollapsedSections.Add(key);
        SaveSettings();
        Rebuild();
    }

    public bool ShowTaskGroup => !_settings.TaskGroupCollapsed;
    public bool IsTaskGroupCollapsed => _settings.TaskGroupCollapsed;

    /// <summary>A closed group marks its heading when one of its screens is on show.</summary>
    public bool IsTaskGroupMarked => _settings.TaskGroupCollapsed && (IsOpenView || IsFutureView || IsUndatedView || IsAllView);

    private void ToggleTaskGroup()
    {
        _settings.TaskGroupCollapsed = !_settings.TaskGroupCollapsed;
        SaveSettings();
        NotifyTaskGroup();
    }

    public void ExpandTaskGroup()
    {
        if (_settings.TaskGroupCollapsed) ToggleTaskGroup();
    }

    private void NotifyTaskGroup()
    {
        Notify(nameof(ShowTaskGroup));
        Notify(nameof(IsTaskGroupCollapsed));
        Notify(nameof(IsTaskGroupMarked));
    }

    public IList<string> FileGroupNames => _settings.FileGroups.Select(g => g.Name).ToList();

    public bool IsNamePromptOpen
    {
        get => _isNamePromptOpen;
        set
        {
            _isNamePromptOpen = value;
            if (value) IsFilePickerOpen = false;
            Notify();
        }
    }

    public string NamePromptTitle
    {
        get => _namePromptTitle;
        private set { _namePromptTitle = value; Notify(); }
    }

    public string NamePromptText
    {
        get => _namePromptText;
        set { _namePromptText = value; Notify(); }
    }

    public string NamePromptError
    {
        get => _namePromptError;
        private set { _namePromptError = value; Notify(); Notify(nameof(HasNamePromptError)); }
    }

    public bool HasNamePromptError => _namePromptError.Length > 0;

    public void BeginNewGroup(string path) =>
        OpenNamePrompt("Yeni grup", "", name => PlaceFileInGroup(path, name));

    private void BeginRenameGroup(FileGroup group) =>
        OpenNamePrompt("Grubu yeniden adlandır", group.Name, name => RenameGroup(group, name));

    /// <summary>Null when it worked; a group is a folder, so its files move with the new name.</summary>
    private string? RenameGroup(FileGroup group, string name)
    {
        if (_settings.FileGroups.Any(g => g != group && IsSameName(g.Name, name))) return "Bu adda bir grup zaten var.";

        var error = Relocate(group.Paths.ToList(), p => FolderPathFor(p, name));
        if (error is not null) return error;

        group.Name = name;
        SaveSettings();
        BuildFiles();
        return null;
    }

    public void BeginNewFile() => OpenNamePrompt("Yeni dosya", "", CreateFile);

    public bool CanRenameFile(string path) => IsInNotesFolder(path);

    public void BeginRenameFile(string path) =>
        OpenNamePrompt("Dosyayı yeniden adlandır", Path.GetFileNameWithoutExtension(path), name => RenameFile(path, name));

    private void OpenNamePrompt(string title, string text, Func<string, string?> confirm)
    {
        _namePromptConfirm = confirm;
        NamePromptTitle = title;
        NamePromptText = text;
        NamePromptError = "";
        IsNamePromptOpen = true;
    }

    private void ConfirmNamePrompt()
    {
        var name = _namePromptText.Trim();
        var problem = NameProblem(name);
        if (problem is not null)
        {
            NamePromptError = problem;
            return;
        }

        var error = _namePromptConfirm?.Invoke(name);
        if (error is not null)
        {
            NamePromptError = error;
            return;
        }

        IsNamePromptOpen = false;
    }

    /// <summary>Groups are folders and files are files, so the name has to work on disk.</summary>
    private static string? NameProblem(string name)
    {
        if (name.Length == 0) return "Ad boş olamaz.";

        return name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".")
            || ReservedFolderNames.Contains(Path.GetFileNameWithoutExtension(name).ToUpperInvariant())
            ? "Bu ad kullanılamaz. \\ / : * ? \" < > | işaretlerini kullanma."
            : null;
    }

    private string? CreateFile(string name)
    {
        var fileName = WithExtension(name, ".txt");
        var folder = _notesFolder ?? DefaultNotesFolder;
        var target = Path.Combine(folder, fileName);
        if (IsFileNameTaken(target, null)) return "Bu adda bir dosya zaten var.";

        try
        {
            Directory.CreateDirectory(folder);
            using (new FileStream(target, FileMode.CreateNew)) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Dosya oluşturulamadı: {ex.Message}";
        }

        OpenFile(target);
        return null;
    }

    private string? RenameFile(string path, string name)
    {
        var target = Path.Combine(Path.GetDirectoryName(path)!, WithExtension(name, Path.GetExtension(path)));
        if (IsFileNameTaken(target, path)) return "Bu adda bir dosya zaten var.";

        var error = Relocate(new[] { path }, _ => target);
        if (error is not null) return error;

        BuildFiles();
        StatusText = "Dosyanın adı değişti.";
        return null;
    }

    private static string WithExtension(string name, string extension) =>
        name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name : name + extension;

    /// <summary>The list shows bare names, so one name stays one file even across groups.</summary>
    private bool IsFileNameTaken(string target, string? except)
    {
        var fileName = Path.GetFileName(target);
        return (File.Exists(target) && !IsSamePath(target, except))
            || _filePaths.Any(p => !IsSamePath(p, except) && string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase));
    }

    public void MoveFileToGroup(string path, string? groupName)
    {
        var error = PlaceFileInGroup(path, groupName);
        if (error is not null) StatusText = error;
    }

    /// <summary>Null when it worked; otherwise why the file stayed where it was.</summary>
    private string? PlaceFileInGroup(string path, string? groupName)
    {
        var group = groupName is null ? null : _settings.FileGroups.FirstOrDefault(g => IsSameName(g.Name, groupName));
        var folderName = group?.Name ?? groupName;

        var error = Relocate(new[] { path }, p => FolderPathFor(p, folderName));
        if (error is not null) return error;

        var placed = IsInNotesFolder(path) ? FolderPathFor(path, folderName) : path;
        RemoveFromGroups(placed);
        if (groupName is not null)
        {
            // Looked up again: taking the file out may have emptied and removed this very group.
            group = _settings.FileGroups.FirstOrDefault(g => IsSameName(g.Name, groupName));
            if (group is null)
            {
                group = new FileGroup { Name = groupName };
                _settings.FileGroups.Add(group);
            }
            group.Paths.Add(placed);
        }

        SaveSettings();
        BuildFiles();
        return null;
    }

    /// <summary>Files directly in the notes folder or in a group's folder; anything else stays where the user keeps it.</summary>
    private bool IsInNotesFolder(string path)
    {
        if (_notesFolder is null) return false;

        var folder = Path.GetDirectoryName(path);
        return IsSamePath(_notesFolder, folder) || IsSamePath(_notesFolder, Path.GetDirectoryName(folder!));
    }

    private string FolderPathFor(string path, string? groupName)
    {
        var folder = groupName is null ? _notesFolder! : Path.Combine(_notesFolder!, groupName);
        return Path.Combine(folder, Path.GetFileName(path));
    }

    /// <summary>Moves the files on disk, all or none. Null when it worked; otherwise why nothing moved.</summary>
    private string? Relocate(IEnumerable<string> paths, Func<string, string> target)
    {
        var moves = paths
            .Where(p => IsInNotesFolder(p) && File.Exists(p))
            .Select(p => new FileMove(p, target(p)))
            .Where(m => m.From != m.To)
            .ToList();
        if (moves.Count == 0) return null;

        var clash = moves.FirstOrDefault(m => File.Exists(m.To) && !IsSamePath(m.From, m.To));
        if (clash is not null) return $"{clash.To} zaten var, dosya taşınmadı.";

        var movingCurrent = moves.FirstOrDefault(m => IsSamePath(m.From, _store?.Path));
        if (movingCurrent is not null)
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        var done = new List<FileMove>();
        try
        {
            foreach (var move in moves)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(move.To)!);
                File.Move(move.From, move.To);
                done.Add(move);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            foreach (var move in Enumerable.Reverse(done)) File.Move(move.To, move.From);
            if (movingCurrent is not null) Watch(movingCurrent.From);
            return $"Dosya taşınamadı: {ex.Message}";
        }

        foreach (var move in moves)
        {
            var index = _filePaths.FindIndex(p => IsSamePath(p, move.From));
            if (index >= 0) _filePaths[index] = move.To;
            foreach (var group in _settings.FileGroups)
            {
                var inGroup = group.Paths.FindIndex(p => IsSamePath(p, move.From));
                if (inGroup >= 0) group.Paths[inGroup] = move.To;
            }
            RemoveFolderIfEmpty(Path.GetDirectoryName(move.From)!);
        }

        if (movingCurrent is not null)
        {
            OpenFile(movingCurrent.To);
        }
        else
        {
            ReadOtherFileNotes();
            SaveFileList();
        }
        return null;
    }

    private void RemoveFolderIfEmpty(string folder)
    {
        if (IsSamePath(folder, _notesFolder)) return;

        try
        {
            if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An empty folder left behind does no harm.
        }
    }

    private void RemoveFromGroups(string path)
    {
        // A group stays when its last file leaves: it was made on purpose, and only its ✕ takes it away.
        foreach (var group in _settings.FileGroups) group.Paths.RemoveAll(p => IsSamePath(p, path));
    }

    private void ToggleFileGroup(FileGroup group)
    {
        group.IsCollapsed = !group.IsCollapsed;
        SaveSettings();
        BuildFiles();
    }

    private void DeleteFileGroup(FileGroup group)
    {
        var error = Relocate(group.Paths.ToList(), p => FolderPathFor(p, null));
        if (error is not null)
        {
            StatusText = error;
            return;
        }

        _settings.FileGroups.Remove(group);
        SaveSettings();
        BuildFiles();
        StatusText = $"{group.Name} grubu kaldırıldı, dosyalar listede duruyor.";
    }

    private static bool IsSameName(string a, string b) => string.Equals(a, b, StringComparison.CurrentCultureIgnoreCase);
    private static bool IsSamePath(string a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void SetTheme(ThemeChoice theme)
    {
        _settings.Theme = theme;
        SaveSettings();
        Notify(nameof(IsSystemTheme));
        Notify(nameof(IsLightTheme));
        Notify(nameof(IsDarkThemeChosen));
        ThemeChanged?.Invoke();
    }

    private void SaveSettings()
    {
        if (!_pathsFromCommandLine) _settings.Save();
        RequestSync(); // Files may have been added, renamed or moved.
    }
    public ICommand UndoCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand OpenErrorLogCommand { get; }
    public ICommand ImportFileCommand { get; }
    public ICommand CreateFileCommand { get; }
    public ICommand OpenNotesFolderCommand { get; }
    public ICommand OpenInEditorCommand { get; }

    public bool HasFile => _store is not null;
    public string FileName => _store is null ? "" : Path.GetFileName(_store.Path);
    public string FilePath => _store?.Path ?? "";
    public bool IsTodayView => _mode == ViewMode.Today && !IsSearching;
    public bool IsOpenView => _mode == ViewMode.Open && !IsSearching;
    public bool IsFutureView => _mode == ViewMode.Future && !IsSearching;
    public bool IsUndatedView => _mode == ViewMode.Undated && !IsSearching;
    public bool IsAllView => _mode == ViewMode.All && !IsSearching;
    public bool IsCalendarView => _mode == ViewMode.Calendar && !IsSearching;
    public bool IsListView => !IsCalendarView;
    public bool IsSearching => _searchText.Trim().Length > 0;
    public bool HasPager => IsCalendarView || (!IsSearching && _mode is ViewMode.Today or ViewMode.Day);

    /// <summary>Another day is open in the list: a link beside the arrows leads back to today.</summary>
    public bool ShowBackToToday => !IsSearching && _mode == ViewMode.Day;
    public bool CanUndo => _undo.Count > 0;
    public bool HasStatus => _statusText.Length > 0;
    public bool IsEmpty => _groups.Count == 0 && _echoRows.Count == 0;

    private IList<EchoRow> _echoRows = [];

    /// <summary>On a day's page: tasks that sit under other days but had something finished on this one.</summary>
    public IList<EchoRow> EchoRows
    {
        get => _echoRows;
        private set
        {
            _echoRows = value;
            Notify();
            Notify(nameof(HasEchoRows));
            Notify(nameof(IsEmpty));
        }
    }

    public bool HasEchoRows => _echoRows.Count > 0;
    public bool HasFileError => _fileError is not null;
    public string? FileError => _fileError;

    public string Title { get => _title; private set { _title = value; Notify(); } }
    public string Subtitle { get => _subtitle; private set { _subtitle = value; Notify(); } }
    public string EmptyText { get => _emptyText; private set { _emptyText = value; Notify(); } }
    public int OpenCount { get => _openCount; private set { _openCount = value; Notify(); Notify(nameof(HasOverdueWork)); } }
    public int FutureCount { get => _futureCount; private set { _futureCount = value; Notify(); } }
    public int UndatedCount { get => _undatedCount; private set { _undatedCount = value; Notify(); } }
    public int AllCount { get => _allCount; private set { _allCount = value; Notify(); } }
    public string MonthTitle { get => _monthTitle; private set { _monthTitle = value; Notify(); } }
    public string MonthSummary { get => _monthSummary; private set { _monthSummary = value; Notify(); } }
    public bool CanGoBack { get => _canGoBack; private set { _canGoBack = value; Notify(); } }
    public bool CanGoForward { get => _canGoForward; private set { _canGoForward = value; Notify(); } }

    public IList<object> Files
    {
        get => _files;
        private set { _files = value; Notify(); }
    }

    public IList<MonthCell> MonthCells
    {
        get => _monthCells;
        private set => ListPatch.Apply(_monthCells, value, (old, fresh) => old.CanStandFor(fresh));
    }

    public DateTime AddTarget => _mode switch
    {
        ViewMode.Day => _selectedDate,
        ViewMode.Future => Tomorrow,
        ViewMode.Undated => Day.Undated,
        _ => _today,
    };

    public DateTime Tomorrow => _today.AddDays(1);

    public string AddHint =>
        (AddTarget == _today ? "Bugüne iş ekle" :
        AddTarget == Tomorrow ? "Yarına iş ekle" :
        AddTarget == Day.Undated ? "Tarihsiz iş ekle" :
        $"{AddTarget.ToString("d MMMM yyyy", Turkish)} gününe iş ekle") + "… (örn. “yarın rapor gönder”)";

    /// <summary>Patched, not replaced: a new list would make WPF draw every row again, which froze the window on each click.</summary>
    public IList<DayGroup> Groups
    {
        get => _groups;
        private set
        {
            ShowPageList(IsSearching ? "search" : _mode.ToString());
            ListPatch.Apply(_groups, value, (old, fresh) => old.CanStandFor(fresh), (old, fresh) => old.TakeOver(fresh));
            Notify(nameof(IsEmpty));
        }
    }

    /// <summary>
    /// One list of rows per page, each kept after it was first shown. Building rows is what makes a page switch slow
    /// (about 150 ms on a real file), while showing rows already built is about 2 ms; coming back to a page then only
    /// patches what changed in it.
    /// </summary>
    public IList<PageList> PageLists => _pageLists;

    private void ShowPageList(string key)
    {
        if (_pageList?.Key == key) return;
        var list = _pageLists.FirstOrDefault(l => l.Key == key);
        if (list == null) _pageLists.Add(list = new PageList(key));
        if (_pageList != null) _pageList.IsCurrent = false;
        _pageList = list;
        list.IsCurrent = true;
        Notify(nameof(Groups));
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            Notify();
            Rebuild();
        }
    }

    public string DraftText
    {
        get => _draftText;
        set
        {
            // An emptied title starts over: the next one reads its day from its words again.
            if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(_draftText)) _composerChosenDay = null;
            _draftText = value ?? "";
            Notify();
            NotifyComposer();
        }
    }

    /// <summary>The day words found in a text for the Win+Shift+N box.</summary>
    public DateInText.Found? ReadDate(string text) => DateInText.Find(text, _today);

    public string DayName(DateTime date) => DateInText.DayName(date, _today);

    public static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Turkish) + text.Substring(1);

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            Notify();
            Notify(nameof(HasStatus));
            if (_addedElsewhere is not null) SetAddedElsewhere(null);
        }
    }

    public void Toggle(EntryViewModel row, bool done)
    {
        Mutate(doc =>
        {
            NoteDocument.SetDone(row.Entry, done, _today);
            DateFinishedUndatedWork(doc, row.Entry);
        }, done ? "Yapıldı olarak işaretlendi." : "Tekrar tamamlanmamış yapıldı.");
    }

    /// <summary>Finished work needs a day in the record, so an undated task lands on the day it was done.</summary>
    private void DateFinishedUndatedWork(NoteDocument doc, Entry entry)
    {
        if (entry.IsFinished && doc.Days.First(d => d.Entries.Contains(entry)).IsUndated) doc.MoveEntry(entry, _today, null);
    }

    public void Toggle(BodyItemViewModel row)
    {
        var done = !row.IsDone;
        Mutate(doc =>
        {
            NoteDocument.SetSubTaskDone(row.Task!, done, _today);
            DateFinishedUndatedWork(doc, row.Task!.Owner);
        }, done ? "Alt iş yapıldı olarak işaretlendi." : "Alt iş tekrar açıldı.");
    }

    public void BeginEdit(BodyItemViewModel row)
    {
        CloseEditors();
        row.EditText = row.HasDescription ? row.Text + "\n" + row.Description : row.Text;
        row.IsEditing = true;
    }

    public void SaveEdit(BodyItemViewModel row, bool done = false)
    {
        if (string.IsNullOrWhiteSpace(row.EditText))
        {
            StatusText = "Alt iş boş olamaz. Silmek için işe tıkla, sağdaki panelden sil.";
            return;
        }

        var text = row.EditText;
        var owner = row.Task!.Owner;
        var index = row.Task.BodyIndex;
        var saved = Mutate(doc =>
        {
            NoteDocument.SetSubTaskText(row.Task, text);
            if (!done) return;

            // Saving rewrote the line, so the sub-task is looked up again.
            NoteDocument.SetSubTaskDone(owner.SubTasks.First(t => t.BodyIndex == index), true, _today);
            DateFinishedUndatedWork(doc, owner);
        }, done ? "Alt iş kaydedildi ve yapıldı olarak işaretlendi." : "Alt iş kaydedildi.");
        if (!saved) CopyToClipboard(text);
    }

    public void Delete(BodyItemViewModel row) => Mutate(_ => NoteDocument.DeleteSubTask(row.Task!, _today), "Alt iş silindi.");

    public void BeginSubTask(EntryViewModel row)
    {
        CloseEditors();
        _addingSubTaskTo = row.Entry;
        // A box put away with text in it opens where it was left.
        if (_heldSubTaskDrafts.TryGetValue(row.Entry.Text, out var held)) row.SubTaskDraft = held;
        _heldSubTaskDrafts.Remove(row.Entry.Text);
        row.IsAddingSubTask = true;
    }

    /// <summary>Sub-task text typed and left by a press elsewhere, by task title, so it survives the rows being made again.</summary>
    private readonly Dictionary<string, string> _heldSubTaskDrafts = new();

    public void AddSubTask(EntryViewModel row, bool done = false)
    {
        if (string.IsNullOrWhiteSpace(row.SubTaskDraft)) return;

        var text = row.SubTaskDraft;
        var added = Mutate(doc =>
        {
            NoteDocument.AddSubTask(row.Entry, text);
            if (!done) return;

            NoteDocument.SetSubTaskDone(row.Entry.SubTasks.Last(), true, _today);
            DateFinishedUndatedWork(doc, row.Entry);
        }, done ? "Alt iş yapıldı olarak eklendi." : "Alt iş eklendi.");
        if (!added) CopyToClipboard(text);
    }

    public void EndSubTask(EntryViewModel row)
    {
        _addingSubTaskTo = null;
        row.IsAddingSubTask = false;
        row.SubTaskDraft = ""; // rows stay on screen now; text worth keeping is held before this
    }

    /// <summary>A press outside the sub-task box closes it; typed text is kept out of sight and comes back when the box opens again.</summary>
    public void PutAwaySubTaskEntry()
    {
        foreach (var row in _groups.SelectMany(g => g.Entries).Where(r => r.IsAddingSubTask).ToList())
        {
            HoldSubTaskDraft(row);
            EndSubTask(row);
        }
    }

    private void HoldSubTaskDraft(EntryViewModel row)
    {
        if (!row.IsAddingSubTask || string.IsNullOrWhiteSpace(row.SubTaskDraft)) return;

        _heldSubTaskDrafts[row.Entry.Text] = row.SubTaskDraft;
    }

    private void CloseEditors()
    {
        _addingSubTaskTo = null;
        foreach (var other in _groups.SelectMany(g => g.Entries))
        {
            other.IsEditing = false;
            HoldSubTaskDraft(other);
            other.IsAddingSubTask = false;
            other.SubTaskDraft = "";
            foreach (var item in other.BodyItems) item.IsEditing = false;
        }
    }

    private bool IsAnyEditorOpen => _groups.SelectMany(g => g.Entries).Any(e => e.IsBusy);

    private void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            StatusText += " Yazdığın metin panoya kopyalandı.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // The clipboard can be held by another program; the status message still stands.
        }
    }

    public void BeginEdit(EntryViewModel row)
    {
        CloseEditors();
        // As typed in the add box: sub-tasks as "- " lines, no marks. The file itself is for Notepad.
        row.EditText = NoteDocument.EditableText(row.Entry);
        row.IsEditing = true;
    }

    public void SaveEdit(EntryViewModel row, bool done = false)
    {
        if (string.IsNullOrWhiteSpace(row.EditText))
        {
            StatusText = "Not boş olamaz. Silmek için işe tıkla, sağdaki panelden Sil'e bas.";
            return;
        }

        var text = IsVisualWriting ? TaskLines.TrimEmpty(row.EditText) : row.EditText;
        if (NoteDocument.IsTypedSubTask(text.TrimStart('\r', '\n').Split('\n')[0]))
        {
            StatusText = "İlk satır işin başlığı olmalı; alt işleri altına - ile yaz.";
            return;
        }
        if (!done && text.Replace("\r\n", "\n").Trim() == NoteDocument.EditableText(row.Entry).Trim())
        {
            // Nothing changed: the file is left alone, as it was written.
            row.IsEditing = false;
            return;
        }

        var saved = Mutate(doc =>
        {
            NoteDocument.SetTypedText(row.Entry, text, _today);
            if (!done) return;

            NoteDocument.SetDone(row.Entry, true, _today);
            DateFinishedUndatedWork(doc, row.Entry);
        }, done ? "Kaydedildi ve yapıldı olarak işaretlendi." : "Değişiklik kaydedildi.");
        if (!saved) CopyToClipboard(text);
    }

    public void Delete(EntryViewModel row) => Mutate(doc => doc.Delete(row.Entry), "Not silindi.");

    public void BringToToday(EntryViewModel row) =>
        Mutate(doc => doc.MoveEntry(row.Entry, _today, null), "Bugüne alındı.");

    /// <summary>One change, so a single undo puts everything back. Oldest work ends up first.</summary>
    private void BringAllToToday()
    {
        var count = OpenCount;
        Mutate(doc =>
        {
            var carried = doc.Days
                .Where(d => d.Date < _today)
                .OrderBy(d => d.Date)
                .SelectMany(d => d.Entries.Where(e => e.HasOpenWork))
                .ToList();
            foreach (var entry in carried) doc.MoveEntry(entry, _today, null);
        }, $"{count} iş bugüne alındı.");
    }

    public void MoveToTomorrow(EntryViewModel row) => MoveToDay(row, Tomorrow);

    /// <summary>Every list of work except search results and the month grid.</summary>
    public bool CanSelectRows => !IsSearching && _mode != ViewMode.Calendar;

    public int SelectedCount => _selected.Count;
    public bool HasSelection => _selected.Count > 0;
    public string SelectionText => $"{_selected.Count} iş seçildi";
    public bool ShowSelectAll => CanSelectRows && !HasSelection && ListedRows.Any();

    private IEnumerable<EntryViewModel> ListedRows => _groups.Where(g => g.ShowEntries).SelectMany(g => g.Entries);

    public void ToggleSelection(EntryViewModel row)
    {
        if (!CanSelectRows) return;

        if (!_selected.Remove(row.Entry)) _selected.Add(row.Entry);
        row.IsSelected = _selected.Contains(row.Entry);
        _selectionAnchor = row.Entry;
        NotifySelection();
    }

    /// <summary>Everything between the last row picked and this one, in the order the list shows them.</summary>
    public void SelectRange(EntryViewModel row)
    {
        if (!CanSelectRows) return;

        var rows = ListedRows.ToList();
        var from = rows.FindIndex(r => r.Entry == _selectionAnchor);
        var to = rows.IndexOf(row);
        if (from < 0)
        {
            ToggleSelection(row);
            return;
        }

        foreach (var picked in rows.Skip(Math.Min(from, to)).Take(Math.Abs(to - from) + 1))
        {
            _selected.Add(picked.Entry);
            picked.IsSelected = true;
        }
        NotifySelection();
    }

    private void SelectAll()
    {
        foreach (var row in ListedRows)
        {
            _selected.Add(row.Entry);
            row.IsSelected = true;
        }
        NotifySelection();
    }

    public void ClearSelection()
    {
        _selected.Clear();
        _selectionAnchor = null;
        foreach (var row in _groups.SelectMany(g => g.Entries)) row.IsSelected = false;
        NotifySelection();
    }

    private void NotifySelection()
    {
        Notify(nameof(SelectedCount));
        Notify(nameof(HasSelection));
        Notify(nameof(SelectionText));
        Notify(nameof(ShowSelectAll));
        Notify(nameof(ShowSelectionBar));
    }

    /// <summary>A row that is no longer on the list, because it was finished or its heading closed, is no longer selected.</summary>
    private void PruneSelection()
    {
        if (CanSelectRows) _selected.IntersectWith(ListedRows.Select(r => r.Entry));
        else _selected.Clear();
        NotifySelection();
    }

    /// <summary>What the tray panel lists: today's half-done work first, then what has not been started.</summary>
    private List<Entry> TodayOpenEntries => EntriesOf(_today).Where(e => e.HasOpenWork).ToList();

    public IList<EntryViewModel> TodayOpenRows => TodayOpenEntries.Select(e => ToRow(e, _today)).ToList();
    public bool IsTodayListEmpty => TodayOpenEntries.Count == 0;

    /// <summary>The number on the tray icon.</summary>
    public int TodayOpenCount => _store is null ? 0 : TodayOpenEntries.Count;

    public string TraySummary
    {
        get
        {
            var entries = TodayOpenEntries;
            var halfDone = entries.Count(e => e.IsPartlyDone);
            var counts = halfDone == 0 ? $"{entries.Count} iş" : $"{entries.Count} iş, {halfDone} tanesi yarım";
            return _store is null ? "Henüz not dosyası yok" : $"{FileName}: {counts}";
        }
    }

    /// <summary>The panel can open long after the last change, even on a new day.</summary>
    public void RefreshToday()
    {
        RollOverDay();
        NotifyToday();
    }

    private void NotifyToday()
    {
        Notify(nameof(TodayOpenRows));
        Notify(nameof(IsTodayListEmpty));
        Notify(nameof(TraySummary));
        Notify(nameof(TodayOpenCount));
    }

    public bool MovesAsGroup(EntryViewModel row) => row.IsSelected && _selected.Count > 1;

    private bool _pickingDoneDate;

    public bool IsPickingDay => _pickingEntries is not null || _pickingForComposer || _pickingDoneDate;
    public bool ShowSelectionBar => HasSelection && !IsPickingDay;
    public string PickingText => _pickingDoneDate
        ? "İşin bittiği günü takvimden seç"
        : _pickingForComposer
        ? "Yeni işin gününü takvimden seç"
        : $"{_pickingEntries?.Count ?? 0} iş taşınacak: soldaki takvimden bir gün seç";

    /// <summary>The next day clicked in a calendar is where the work goes.</summary>
    public void BeginPickDay(EntryViewModel row) =>
        StartPicking(MovesAsGroup(row) ? SelectedInListOrder() : new List<Entry> { row.Entry });

    private void StartPicking(List<Entry> entries)
    {
        _pickingEntries = entries.Count == 0 ? null : entries;
        _pickingForComposer = false;
        _pickingDoneDate = false;
        NotifyPicking();
    }

    // ---- picking a day with the keyboard: the arrows walk the small calendar, Enter takes the day ----

    private DateTime? _pickCursor;

    public bool HasPickCursor => _pickCursor is not null;

    /// <summary>M, then G: the work's day is picked from the small calendar; the cursor starts on the day shown.</summary>
    public void BeginPickDayByKeys(EntryViewModel row)
    {
        BeginPickDay(row);
        _pickCursor = row.Date == Day.Undated ? _today : row.Date;
        _visibleMonth = FirstOfMonth(_pickCursor.Value);
        Rebuild();
    }

    public void MovePickCursor(int days)
    {
        if (_pickCursor is not { } at) return;
        _pickCursor = at.AddDays(days);
        _visibleMonth = FirstOfMonth(_pickCursor.Value);
        Rebuild();
    }

    public void TakePickCursor()
    {
        if (_pickCursor is not { } day) return;
        _pickCursor = null;
        OpenDay(day);
    }

    /// <summary>Ctrl+Shift+Left/Right: the task (or the selection) a day earlier or later.</summary>
    public void MoveByDays(EntryViewModel row, int step)
    {
        if (row.Date == Day.Undated)
        {
            StatusText = "Tarihsiz işin günü yok; M, sonra G ile bir gün seç.";
            return;
        }
        MoveEntriesToDate(MovesAsGroup(row) ? SelectedInListOrder() : new List<Entry> { row.Entry }, row.Date.AddDays(step));
    }

    public void CancelPickDay()
    {
        _pickCursor = null;
        _pickingEntries = null;
        _pickingForComposer = false;
        _pickingDoneDate = false;
        NotifyPicking();
    }

    private void NotifyPicking()
    {
        Notify(nameof(IsPickingDay));
        Notify(nameof(PickingText));
        Notify(nameof(ShowSelectionBar));
        ShowSectionForPicking();
    }

    private void OpenDay(DateTime date)
    {
        if (TakeComposerDay(date) || TakeDoneDate(date)) return;
        if (_pickingEntries is null)
        {
            NoteDayFromCalendar();
            ShowDay(date);
            return;
        }

        var entries = _pickingEntries;
        CancelPickDay();
        MoveEntriesToDate(entries, date);
    }

    /// <summary>A drop on a calendar day. Finished or not, past or ahead: the work goes to that day.</summary>
    public void MoveDraggedToDate(EntryViewModel row, DateTime date) =>
        MoveEntriesToDate(MovesAsGroup(row) ? SelectedInListOrder() : new List<Entry> { row.Entry }, date);

    private void MoveEntriesToDate(List<Entry> entries, DateTime date)
    {
        var moving = entries.Where(e => _document.Days.Any(d => d.Entries.Contains(e)) && DateOf(e) != date).ToList();
        if (moving.Count == 0)
        {
            StatusText = "Seçilen iş zaten o günde.";
            return;
        }

        var place = date == _today ? "bugüne" : $"{date.ToString("d MMMM yyyy", Turkish)} gününe";
        var moved = Mutate(doc =>
        {
            foreach (var entry in moving) doc.MoveEntry(entry, date, null);
        }, $"{moving.Count} iş {place} taşındı.");
        if (moved) ClearSelection();
    }

    /// <summary>A drop next to a row: dragging a selected row takes the whole selection along.</summary>
    public void MoveDragged(EntryViewModel row, DateTime date, Entry? before)
    {
        if (MovesAsGroup(row)) MoveSelected(date, before);
        else Move(row, date, before);
    }

    /// <summary>A drop on the sidebar: work already there stays where it is in its list.</summary>
    public void MoveDraggedToDay(EntryViewModel row, DateTime date)
    {
        if (MovesAsGroup(row))
        {
            MoveSelectedToDay(date);
        }
        else if (CanGoToDay(row.Entry, date))
        {
            MoveToDay(row, date);
        }
        else if (date == Day.Undated && !row.Entry.CanBeUndated)
        {
            StatusText = "Bitmiş iş tarihsize alınamaz, yapıldığı günde kalır.";
        }
    }

    private bool CanGoToDay(Entry entry, DateTime date)
    {
        var current = DateOf(entry);
        if (current == date) return false;
        if (date == Day.Undated && !entry.CanBeUndated) return false;

        // Tomorrow means putting work off, and work already dated ahead has its own day.
        return date != Tomorrow || current <= _today || current == Day.Undated;
    }

    private DateTime DateOf(Entry entry) => _document.Days.First(d => d.Entries.Contains(entry)).Date;

    private List<Entry> SelectedInListOrder() =>
        _groups.SelectMany(g => g.Entries).Select(r => r.Entry).Where(_selected.Contains).ToList();

    private void MoveSelected(DateTime date, Entry? before)
    {
        var entries = SelectedInListOrder();
        var siblings = EntriesOf(date);
        var anchor = before;
        while (anchor is not null && _selected.Contains(anchor))
        {
            var next = siblings.IndexOf(anchor) + 1;
            anchor = next < siblings.Count ? siblings[next] : null;
        }

        var moved = Mutate(doc =>
        {
            foreach (var entry in entries) doc.MoveEntry(entry, date, anchor);
        }, $"{entries.Count} iş taşındı.", keepPlace: true);
        if (moved) ClearSelection();
    }

    private void MoveSelectedToDay(DateTime date)
    {
        // Finished work stays on the day it was done, as it does with the single-row commands.
        var entries = SelectedInListOrder().Where(e => e.HasOpenWork && CanGoToDay(e, date)).ToList();
        if (entries.Count == 0)
        {
            StatusText = "Taşınacak iş yok: seçilenler zaten orada ya da bitmiş.";
            return;
        }

        var place =
            date == _today ? "bugüne alındı" :
            date == Tomorrow ? "yarına taşındı" :
            "tarihsiz işlere taşındı";
        var moved = Mutate(doc =>
        {
            foreach (var entry in entries) doc.MoveEntry(entry, date, null);
        }, $"{entries.Count} iş {place}.");
        if (moved) ClearSelection();
    }

    public void MakeUndated(EntryViewModel row) =>
        Mutate(doc => doc.MoveEntry(row.Entry, Day.Undated, null), "Tarihsiz işlere taşındı.");

    /// <summary>Drops a task before another one, or at the end of the day when there is none. It stays where it was put.</summary>
    public void Move(EntryViewModel row, DateTime date, Entry? before)
    {
        if (date == Day.Undated && !row.Entry.CanBeUndated)
        {
            StatusText = "Bitmiş iş tarihsize alınamaz, yapıldığı günde kalır.";
            return;
        }
        Mutate(doc => doc.MoveEntry(row.Entry, date, before), MoveMessage(row, date), keepPlace: true);
    }

    /// <summary>Sends a task to a day with no particular place in mind, so it joins the others in its state.</summary>
    private void MoveToDay(EntryViewModel row, DateTime date) =>
        Mutate(doc => doc.MoveEntry(row.Entry, date, null), MoveMessage(row, date));

    private string MoveMessage(EntryViewModel row, DateTime date) =>
        row.Date == date ? "Sıra değişti." :
        date == _today ? "Bugüne alındı." :
        date == Day.Undated ? "Tarihsiz işlere taşındı." :
        date == Tomorrow ? "Yarına taşındı." :
        "Taşındı.";

    public void MoveUp(EntryViewModel row)
    {
        var siblings = ListedSiblings(row);
        var index = siblings.IndexOf(row.Entry);
        if (index > 0) Move(row, row.Date, siblings[index - 1]);
    }

    public void MoveDown(EntryViewModel row)
    {
        var siblings = ListedSiblings(row);
        var index = siblings.IndexOf(row.Entry);
        if (index < 0 || index == siblings.Count - 1) return;

        var all = EntriesOf(row.Date);
        var next = all.IndexOf(siblings[index + 1]);
        Move(row, row.Date, next + 1 < all.Count ? all[next + 1] : null);
    }

    /// <summary>A step has to pass what the list shows: some lists leave finished work out.</summary>
    private List<Entry> ListedSiblings(EntryViewModel row)
    {
        var entries = EntriesOf(row.Date);
        return _mode is ViewMode.All or ViewMode.Open or ViewMode.Future
            ? entries.Where(e => e.HasOpenWork).ToList()
            : entries;
    }

    /// <summary>The task right after this one in its day, or null at the end.</summary>
    public Entry? EntryAfter(EntryViewModel row)
    {
        var siblings = EntriesOf(row.Date);
        var index = siblings.IndexOf(row.Entry);
        return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
    }

    /// <summary>The day a drop on the empty part of the list lands in; null where that means nothing.</summary>
    public DateTime? ShownDay =>
        IsSearching ? null :
        _mode == ViewMode.Undated ? Day.Undated :
        _mode is ViewMode.Today or ViewMode.Day ? ShownDate :
        null;

    public DateTime TodayDate => _today;

    private List<Entry> EntriesOf(DateTime date) =>
        EntriesByDate().FirstOrDefault(d => d.Date == date)?.Entries ?? new List<Entry>();

    private void RefreshStartupEntry()
    {
        try
        {
            _startup.Refresh(StartupRegistration.ExePath);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Without write access the old entry stays; the settings panel still shows its state.
        }
    }

    public void GoBack()
    {
        if (!HasPager || !CanGoBack) return;

        if (IsCalendarView)
        {
            ShiftCalendar(-1);
            return;
        }

        var current = ShownDate;
        ShowDay(EntriesByDate().Select(d => d.Date).First(d => d < current));
    }

    /// <summary>
    /// Ctrl+Left/Right: the day before or after, every day and not only the days with notes; the calendar steps by
    /// the week, month or year it shows. Ctrl+Shift jumps to days with work (<see cref="GoBack"/>, <see cref="GoForward"/>).
    /// </summary>
    public void StepDay(int step)
    {
        if (IsCalendarView)
        {
            ShiftCalendar(step);
            return;
        }
        if (_mode is ViewMode.Today or ViewMode.Day) ShowDay(ShownDate.AddDays(step));
    }

    public void GoForward()
    {
        if (!HasPager || !CanGoForward) return;

        if (IsCalendarView)
        {
            ShiftCalendar(1);
            return;
        }

        var current = ShownDate;
        var later = EntriesByDate().Select(d => d.Date).Where(d => d > current && d < _today).ToList();
        ShowDay(later.Count > 0 ? later[later.Count - 1] : _today);
    }

    private DateTime ShownDate => _mode == ViewMode.Today ? _today : _selectedDate;

    /// <summary>
    /// The Win+Shift+N box: today (or the day read from the text), whichever page the window is on, into
    /// <paramref name="path"/>, the open file when left out.
    /// </summary>
    public bool QuickAdd(string text, bool done, bool readDate = true, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(text) || !HasFile) return false;
        var target = _today;
        if (readDate && ReadDate(text) is { } found)
        {
            target = found.Date;
            text = found.Text;
        }
        text = WithTime(text, target);
        if (path is not null && !IsSamePath(path, _store?.Path)) return AddToOtherFile(path, target, text, done);
        return Mutate(doc => doc.AddEntry(target, text, done), AddedMessage(target, done));
    }

    /// <summary>The note files as the picker lists them: group by group, then the ones outside every group.</summary>
    public IList<QuickAddFile> QuickAddFiles
    {
        get
        {
            var list = new List<QuickAddFile>();
            foreach (var group in _settings.FileGroups)
                list.AddRange(_filePaths.Where(p => group.Paths.Any(g => IsSamePath(g, p))).Select(p => ToQuickAddFile(p, group.Name)));
            var grouped = _settings.FileGroups.SelectMany(g => g.Paths).ToList();
            list.AddRange(_filePaths.Where(p => !grouped.Any(g => IsSamePath(g, p))).Select(p => ToQuickAddFile(p, null)));
            return list;
        }
    }

    private QuickAddFile ToQuickAddFile(string path, string? group) =>
        new(path, Path.GetFileNameWithoutExtension(path), group, IsSamePath(path, _store?.Path));

    /// <summary>The right panel's width, kept once it is dragged.</summary>
    public double? DetailPanelWidth
    {
        get => _settings.DetailWidth;
        set
        {
            _settings.DetailWidth = value;
            _settings.Save();
        }
    }

    /// <summary>Where the Win+Shift+N box adds: the file picked there last while it is still in the list, else the open one.</summary>
    public string? QuickAddPath
    {
        get => _settings.QuickAddPath is { } picked && _filePaths.Any(p => IsSamePath(p, picked)) ? picked : _store?.Path;
        set
        {
            _settings.QuickAddPath = value;
            _settings.Save();
        }
    }

    /// <summary>A task for a file that is not open: read, add, write back; the open file and its undo are left alone.</summary>
    private bool AddToOtherFile(string path, DateTime target, string text, bool done)
    {
        var store = new NoteStore(path, BackupDirectory);
        try
        {
            var doc = NoteDocument.Parse(store.Read());
            var before = new HashSet<Entry>(doc.Days.SelectMany(d => d.Entries));
            doc.AddEntry(target, text, done);
            foreach (var entry in doc.Days.SelectMany(d => d.Entries).Where(e => !before.Contains(e)).ToList()) doc.PlaceByState(entry);
            store.Write(doc.Serialize());
            _otherFileNotes[path] = OpenNote(doc);
        }
        catch (Exception ex) when (ex is NoteFileException or IOException or UnauthorizedAccessException)
        {
            Errors.Write("Kaydetme", ex);
            StatusText = ex.Message;
            return false;
        }

        BuildFiles();
        StatusText = (done ? "Yapıldı olarak " : "İş ") + Path.GetFileNameWithoutExtension(path) + " dosyasına eklendi.";
        return true;
    }

    /// <summary>Says the day only when the task went somewhere other than where the window is looking.</summary>
    private string AddedMessage(DateTime target, bool done, bool dated = true) =>
        dated && target != ShownDate
            ? (done ? "Yapıldı olarak " : "İş ") + (target == Day.Undated ? "tarihsiz işlere" : DateInText.DayName(target, _today)) + " eklendi."
            : done ? "Yapıldı olarak eklendi." : "İş eklendi.";

    /// <summary>Another program holds Win+Shift+N, so the quick box cannot open; said once at the bottom.</summary>
    public void QuickAddKeyTaken() =>
        StatusText = GlobalHotKey.Keys + " başka bir program tarafından kullanılıyor, hızlı ekleme kutusu açılamıyor.";

    private void Undo()
    {
        if (_undo.Count == 0 || _store is null || !IsFileFresh()) return;

        var previous = _undo.Peek();
        try
        {
            _store.Write(previous);
        }
        catch (NoteFileException ex)
        {
            StatusText = ex.Message;
            return;
        }

        _undo.Pop();
        _loadedText = previous;
        _document = NoteDocument.Parse(previous);
        StatusText = "Son işlem geri alındı.";
        Notify(nameof(CanUndo));
        Rebuild();
    }

    /// <summary>
    /// Every change starts from what is on disk right now: if the file was edited elsewhere the
    /// change is dropped rather than written over those edits.
    /// </summary>
    /// <param name="keepPlace">The user chose the place, as with a drop. Otherwise work that is new, changed state or changed day joins its state.</param>
    private bool Mutate(Action<NoteDocument> change, string message, bool keepPlace = false)
    {
        if (_store is null || !IsFileFresh()) return false;

        var previous = _loadedText;
        try
        {
            var was = _document.Days.SelectMany(d => d.Entries.Select(e => new { Entry = e, d.Date, e.StateRank })).ToDictionary(x => x.Entry);
            change(_document);
            if (!keepPlace)
            {
                var arrived = _document.Days
                    .SelectMany(d => d.Entries.Where(e => !was.TryGetValue(e, out var old) || old.Date != d.Date || old.StateRank != e.StateRank))
                    .ToList();
                foreach (var entry in arrived) _document.PlaceByState(entry);
            }
            var text = _document.Serialize();
            if (text == previous)
            {
                Rebuild();
                return true;
            }

            _store.Write(text);
            _loadedText = text;
        }
        catch (NoteFileException ex)
        {
            Errors.Write("Kaydetme", ex);
            _document = NoteDocument.Parse(previous);
            StatusText = ex.Message;
            Rebuild();
            return false;
        }

        _undo.Push(previous);
        if (_undo.Count > UndoLimit) TrimUndo();
        StatusText = message;
        Notify(nameof(CanUndo));
        Rebuild();
        return true;
    }

    private void TrimUndo()
    {
        var kept = _undo.Take(UndoLimit).Reverse().ToList();
        _undo.Clear();
        foreach (var text in kept) _undo.Push(text);
    }

    private bool IsFileFresh()
    {
        try
        {
            if (_store!.Read() == _loadedText) return true;
        }
        catch (NoteFileException ex)
        {
            StatusText = ex.Message;
            return false;
        }

        LoadFromStore();
        StatusText = FileUpdatedMessage() + " İşlemi tekrar dene.";
        return false;
    }

    private void PickUpExternalChange()
    {
        if (_store is null || IsAnyEditorOpen) return;

        try
        {
            if (_store.Read() == _loadedText) return;
        }
        catch (NoteFileException)
        {
            return; // Usually another program mid-save; the next change event tries again.
        }

        LoadFromStore();
        StatusText = FileUpdatedMessage();
    }

    private void Reload()
    {
        if (_store is null) return;

        LoadFromStore();
        StatusText = _fileError is null ? "Dosya yeniden yüklendi." : "";
    }

    /// <summary>After an unexpected error the screen is read again from the file, so a half-made change is dropped.</summary>
    public void RecoverAfterError()
    {
        CloseEditors();
        if (_store is not null) LoadFromStore();
        StatusText = "Beklenmeyen bir hata oldu, ayrıntısı hata günlüğüne yazıldı. Ekran dosyadan yeniden yüklendi.";
    }

    /// <summary>Settings › Hata günlüğü.</summary>
    private void OpenErrorLog()
    {
        if (!Errors.Open()) StatusText = "Hata günlüğü boş: şimdiye kadar bir hata kaydedilmedi.";
    }

    private void LoadFromStore()
    {
        CancelPickDay();
        try
        {
            _loadedText = _store!.Read();
            _document = NoteDocument.Parse(_loadedText);
            SetFileError(null);
        }
        catch (NoteFileException ex)
        {
            SetFileError(ex.Message);
        }

        _undo.Clear();
        Notify(nameof(CanUndo));
        ReadOtherFileNotes();
        Rebuild();
    }

    /// <summary>A file that cannot be opened leaves the current one on screen.</summary>
    private bool OpenFile(string path)
    {
        var full = Path.GetFullPath(path);
        var store = new NoteStore(full, BackupDirectory);
        var error = ReadError(store);
        if (error is not null)
        {
            if (_store is null) SetFileError(error);
            else StatusText = error;
            return false;
        }

        _watcher?.Dispose();
        _store = store;
        _addingSubTaskTo = null;
        SetFileError(null);
        StatusText = "";
        if (!_filePaths.Contains(full, StringComparer.OrdinalIgnoreCase)) _filePaths.Add(full);
        NotifyFile();
        LoadFromStore();
        Watch(full);
        SaveFileList();
        return true;
    }

    private void RemoveFile(string path)
    {
        var removingCurrent = string.Equals(path, _store?.Path, StringComparison.OrdinalIgnoreCase);
        _filePaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RemoveFromGroups(path);

        if (removingCurrent)
        {
            CloseCurrentFile();
            foreach (var other in _filePaths.ToList())
            {
                if (OpenFile(other)) break;
            }
        }

        SaveFileList();
        StatusText = _store is null ? "" : $"{Path.GetFileName(path)} listeden kaldırıldı, dosya yerinde duruyor.";
        Rebuild();
    }

    private void CloseCurrentFile()
    {
        _watcher?.Dispose();
        _watcher = null;
        _store = null;
        _document = NoteDocument.Parse("");
        _loadedText = "";
        _addingSubTaskTo = null;
        _undo.Clear();
        Notify(nameof(CanUndo));
        NotifyFile();
    }

    private void SaveFileList()
    {
        if (_pathsFromCommandLine) return;

        _settings.NotesPath = _store?.Path;
        _settings.NotesPaths = _filePaths.ToList();
        _settings.Save();
    }

    private void ReadOtherFileNotes()
    {
        _otherFileNotes.Clear();
        foreach (var path in _filePaths.Where(p => !string.Equals(p, _store?.Path, StringComparison.OrdinalIgnoreCase)))
        {
            var store = new NoteStore(path, BackupDirectory);
            try
            {
                _otherFileNotes[path] = File.Exists(path) ? OpenNote(NoteDocument.Parse(store.Read())) : "bulunamadı";
            }
            catch (NoteFileException)
            {
                _otherFileNotes[path] = "açılamadı";
            }
        }
    }

    private static string? ReadError(NoteStore store)
    {
        if (!File.Exists(store.Path)) return $"Dosya bulunamadı: {store.Path}";

        try
        {
            store.Read();
            return null;
        }
        catch (NoteFileException ex)
        {
            return ex.Message;
        }
    }

    private static string OpenNote(NoteDocument document)
    {
        var open = document.Days.Sum(d => d.Entries.Count(e => e.HasOpenWork));
        return open == 0 ? "" : open.ToString(CultureInfo.InvariantCulture);
    }

    private void BuildFiles()
    {
        var items = new List<object>();

        foreach (var group in _settings.FileGroups)
        {
            var rows = _filePaths.Where(p => group.Paths.Any(g => IsSamePath(g, p))).Select(p => ToFileRow(p, group.Name)).ToList();

            var current = group;
            items.Add(new FileGroupRow(group.Name,
                rows.Sum(r => int.TryParse(r.Note, out var open) ? open : 0),
                group.IsCollapsed,
                group.IsCollapsed && rows.Any(r => r.IsCurrent),
                new RelayCommand(() => ToggleFileGroup(current)),
                new RelayCommand(() => BeginRenameGroup(current)),
                new RelayCommand(() => DeleteFileGroup(current))) { IsEmpty = rows.Count == 0 });
            if (!group.IsCollapsed) items.AddRange(rows);
        }

        var grouped = _settings.FileGroups.SelectMany(g => g.Paths).ToList();
        items.AddRange(_filePaths.Where(p => !grouped.Any(g => IsSamePath(g, p))).Select(p => ToFileRow(p, null)));
        Files = items;
    }

    private FileRow ToFileRow(string path, string? groupName)
    {
        var isCurrent = IsSamePath(path, _store?.Path);
        var note = isCurrent ? OpenNote(_document) : _otherFileNotes.TryGetValue(path, out var known) ? known : "";
        return new FileRow(path, isCurrent, note,
            new RelayCommand(() =>
            {
                IsFilePickerOpen = false;
                if (!isCurrent) OpenFile(path);
            }),
            new RelayCommand(() => AskToDeleteFile(path)))
        {
            GroupName = groupName,
            RemoveTip = RemoveTipFor(path),
        };
    }

    private void Watch(string path)
    {
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        FileSystemEventHandler restart = (_, _) => Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _changeTimer.Stop();
            _changeTimer.Start();
        }));
        _watcher.Changed += restart;
        _watcher.Created += restart;
        _watcher.Renamed += (s, e) => restart(s, e);
        _watcher.EnableRaisingEvents = true;
    }

    private const string TextFileFilter = "Metin belgeleri (*.txt)|*.txt|Tüm dosyalar (*.*)|*.*";

    private void ImportFile()
    {
        IsSettingsOpen = false;
        var dialog = new OpenFileDialog
        {
            Title = "Eklenecek metin belgesini seç",
            Filter = TextFileFilter,
        };
        if (dialog.ShowDialog() == true) AddExistingFile(dialog.FileName);
    }

    /// <summary>Notes live in the notes folder: a file from elsewhere comes in as a copy and the original is left alone.</summary>
    public void AddExistingFile(string path)
    {
        var source = Path.GetFullPath(path);
        if (_notesFolder is null || IsInNotesFolder(source))
        {
            if (!OpenFile(source) || _notesFolder is null) return;

            // Back on the list after being removed: its folder says which group it belongs to.
            var folder = Path.GetDirectoryName(source)!;
            if (!IsSamePath(folder, _notesFolder)) MoveFileToGroup(source, Path.GetFileName(folder));
            return;
        }

        var error = ReadError(new NoteStore(source, BackupDirectory));
        var target = Path.Combine(_notesFolder, Path.GetFileName(source));
        if (error is null && IsFileNameTaken(target, null)) error = $"{Path.GetFileName(source)} zaten var, dosya eklenmedi.";
        if (error is null)
        {
            try
            {
                Directory.CreateDirectory(_notesFolder);
                File.Copy(source, target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = $"Dosya kopyalanamadı: {ex.Message}";
            }
        }

        if (error is not null)
        {
            if (_store is null) SetFileError(error);
            else StatusText = error;
            return;
        }

        if (OpenFile(target)) StatusText = $"{Path.GetFileName(target)} İş Günlüğü klasörüne kopyalandı, orijinali yerinde duruyor.";
    }

    private void OpenNotesFolder()
    {
        var folder = _notesFolder ?? Path.GetDirectoryName(_store?.Path) ?? DefaultNotesFolder;
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            StatusText = $"Klasör açılamadı: {ex.Message}";
        }
    }

    private void OpenInEditor()
    {
        if (_store is not null) Process.Start(new ProcessStartInfo(_store.Path) { UseShellExecute = true });
    }

    private void Navigate(ViewMode mode)
    {
        _mode = mode;
        _calendarReturn = false;
        // The calendar is the overview, every other page is a list of work.
        if (mode == ViewMode.Calendar) Section = SidebarSection.Calendar;
        else if (!IsPickingDay) Section = SidebarSection.Tasks;
        _selected.Clear();
        ClearSearch();
        Rebuild();
    }

    private void ShowDay(DateTime date)
    {
        _mode = date == _today ? ViewMode.Today : ViewMode.Day;
        if (!IsPickingDay) Section = SidebarSection.Tasks;
        _selectedDate = date;
        _visibleMonth = FirstOfMonth(date);
        ClearSearch();
        Rebuild();
    }

    private void ShiftMonth(int months)
    {
        _visibleMonth = _visibleMonth.AddMonths(months);
        Rebuild();
    }

    private void ClearSearch()
    {
        if (_searchText.Length == 0) return;

        _searchText = "";
        Notify(nameof(SearchText));
    }

    private void RollOverDay()
    {
        var now = DateTime.Today;
        if (now == _today) return;

        _today = now;
        if (!IsAnyEditorOpen) Rebuild();
    }

    private void Rebuild()
    {
        _progressedByDay = null;
        RefreshDetail();
        var byDate = EntriesByDate();

        BuildFiles();
        BuildMonth(byDate);
        BuildSidebarCalendars(byDate);
        EchoRows = [];
        DayChips = Array.Empty<DayChip>();

        if (IsSearching) BuildSearch(byDate);
        else if (_mode == ViewMode.Open) BuildOpen(byDate);
        else if (_mode == ViewMode.Future) BuildFuture(byDate);
        else if (_mode == ViewMode.Undated) BuildUndated(byDate);
        else if (_mode == ViewMode.All) BuildAll(byDate);
        else if (_mode == ViewMode.Calendar) BuildCalendar(byDate);
        else BuildDay(byDate);

        CanGoBack = IsCalendarView || byDate.Any(d => d.Date < ShownDate);
        CanGoForward = IsCalendarView || ShownDate < _today;

        Notify(nameof(IsTodayView));
        Notify(nameof(IsOpenView));
        PruneSelection();
        NotifyToday();
        Notify(nameof(IsFutureView));
        Notify(nameof(IsUndatedView));
        Notify(nameof(IsTaskGroupMarked));
        Notify(nameof(IsAllView));
        Notify(nameof(ShowBringAll));
        Notify(nameof(IsCalendarView));
        Notify(nameof(IsListView));
        Notify(nameof(IsSearching));
        Notify(nameof(HasPager));
        Notify(nameof(ShowBackToToday));
        Notify(nameof(AddHint));
        NotifyComposer();
        NotifyCalendarScale();
        NotifyDayProgress();
        CloseDetailIfHidden();
    }

    private List<DayEntries> EntriesByDate() =>
        _document.Days
            .GroupBy(d => d.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new DayEntries(g.Key, g.SelectMany(d => d.Entries).ToList()))
            .ToList();

    private void BuildMonth(List<DayEntries> byDate)
    {
        var lookup = byDate.ToDictionary(d => d.Date, d => d.Entries);

        // Always six Monday-first weeks, so the layout does not jump between months.
        var offset = ((int)_visibleMonth.DayOfWeek + 6) % 7;
        var first = _visibleMonth.AddDays(-offset);
        DateTime? highlighted = !IsSearching && _mode is ViewMode.Today or ViewMode.Day ? ShownDate : null;

        var worked = DayWork.WorkedByDay(_document, _today);
        MonthCells = Enumerable.Range(0, CalendarCells).Select(i =>
        {
            var date = first.AddDays(i);
            var entries = lookup.TryGetValue(date, out var found) ? found : new List<Entry>();
            return new MonthCell(
                date,
                date.Month == _visibleMonth.Month,
                date == _today,
                date == highlighted,
                entries,
                date.ToString("d MMMM dddd", Turkish) + ", " + Counts(entries, ProgressedOn(date)),
                new RelayCommand(() => OpenDay(date)),
                worked.TryGetValue(date, out var count) ? count : 0) { IsPickCursor = date == _pickCursor };
        }).ToList();

        MonthTitle = _visibleMonth.ToString("MMMM yyyy", Turkish);

        var inMonth = byDate.Where(d => FirstOfMonth(d.Date) == _visibleMonth).SelectMany(d => d.Entries).ToList();
        MonthSummary = $"{_visibleMonth.ToString("MMMM", Turkish)} ayında {inMonth.Count(e => e.IsFinished)} iş yapıldı";
        OpenCount = byDate.Where(d => d.Date < _today).Sum(d => d.Entries.Count(e => e.HasOpenWork));
        FutureCount = byDate.Where(d => d.Date > _today && d.Date != Day.Undated).Sum(d => d.Entries.Count(e => e.HasOpenWork));
        UndatedCount = byDate.Where(d => d.Date == Day.Undated).Sum(d => d.Entries.Count(e => e.HasOpenWork));
        AllCount = byDate.Sum(d => d.Entries.Count(e => e.HasOpenWork));
    }

    private void BuildDay(List<DayEntries> byDate)
    {
        var date = ShownDate;
        var entries = byDate.FirstOrDefault(d => d.Date == date)?.Entries ?? new List<Entry>();
        if (_mode == ViewMode.Today)
        {
            Title = "Bugün";
            Subtitle = date.ToString("d MMMM yyyy dddd", Turkish);
        }
        else
        {
            Title = date.ToString("d MMMM yyyy", Turkish);
            Subtitle = date.ToString("dddd", Turkish);
        }
        DayChips = ChipsOf(entries, ProgressedOn(date), withDone: _mode != ViewMode.Today);

        EmptyText = "Bu gün için henüz not yok. Aşağıdan ilk notunu yaz.";
        // The list follows the file, so what is arranged here is what the text says.
        var rows = entries.Select(e => ToRow(e, date, canReorder: true)).ToList();

        // What was finished (or given up) that day waits folded at the bottom: "✓ 4 iş bitti".
        var closed = rows.Where(r => r.Entry.IsFinished || r.Entry.IsDropped).ToList();
        var open = rows.Except(closed).ToList();
        var groups = new List<DayGroup>();
        if (open.Count > 0) groups.Add(new DayGroup(null, null, open));
        // A day with nothing left open has only its finished work to show: it is listed as it is, not folded.
        if (open.Count == 0 && closed.Count > 0) groups.Add(new DayGroup(null, null, closed));
        else if (closed.Count > 0)
        {
            var finished = closed.Count(r => r.Entry.IsFinished);
            var dropped = closed.Count - finished;
            var title = "✓ " + (finished > 0 ? $"{finished} iş bitti" : "") + (finished > 0 && dropped > 0 ? ", " : "") + (dropped > 0 ? $"{dropped} iptal" : "");
            var shown = _openDoneDays.Contains(date);
            groups.Add(new DayGroup(title, new RelayCommand(() => ToggleDoneFold(date)), closed)
            {
                Key = "bitenler",
                IsDoneFold = true,
                IsCollapsed = !shown,
                TitleToolTip = shown ? "Bitenleri gizle" : "Bitenleri göster",
            });
        }
        Groups = groups;
        if (!IsSearching) EchoRows = DayWork.Elsewhere(_document, date, _today).Select(ToEchoRow).ToList();
    }

    private void BuildOpen(List<DayEntries> byDate)
    {
        var groups = ToGroups(byDate.Where(d => d.Date < _today).ToList(), e => e.HasOpenWork, canReorder: true);
        Title = "Tamamlanmamış";
        Subtitle = groups.Count == 0
            ? "Önceki günlerden kalan iş yok"
            : $"Önceki günlerden kalan {groups.Sum(g => g.Entries.Count)} iş";
        EmptyText = "Bütün işler tamam. Bugünün işleri Bugün ekranında.";
        Groups = groups;
    }

    private void BuildFuture(List<DayEntries> byDate)
    {
        // Nearest day first: what comes next is what this list is read for.
        var groups = ToGroups(byDate.Where(d => d.Date > _today && d.Date != Day.Undated).OrderBy(d => d.Date).ToList(), e => e.HasOpenWork, canReorder: true);

        Title = "Gelecek";
        Subtitle = groups.Count == 0 ? "İleri tarihli iş yok" : $"İleri tarihli {groups.Sum(g => g.Entries.Count)} iş";
        EmptyText = "Aşağıdan yazdığın iş yarına eklenir. Başka bir gün için takvimden o güne tıkla.";
        Groups = groups;
    }

    private void BuildUndated(List<DayEntries> byDate)
    {
        var rows = byDate.Where(d => d.Date == Day.Undated)
            .SelectMany(d => d.Entries.Where(e => e.HasOpenWork).Select(e => ToRow(e, d.Date, canReorder: true)))
            .ToList();

        Title = "Tarihsiz";
        Subtitle = rows.Count == 0 ? "Tarihsiz iş yok" : $"Tarihsiz {rows.Count} iş";
        EmptyText = "Aşağıdan yazdığın işler tarihsiz olarak buraya eklenir. Tiklediğinde o günün altına yazılır.";
        Groups = rows.Count == 0 ? [] : [new DayGroup(null, null, rows)];
    }

    private void BuildAll(List<DayEntries> byDate)
    {
        var open = byDate
            .SelectMany(d => d.Entries.Where(e => e.HasOpenWork).Select(e => new { d.Date, Entry = e }))
            .ToList();

        var today = open.Where(o => o.Date == _today).Select(o => ToRow(o.Entry, o.Date, canReorder: true)).ToList();
        var carried = open.Where(o => o.Date < _today).Select(o => ToRow(o.Entry, o.Date, canReorder: true, origin: OriginOf(o.Date))).ToList();
        var later = open.Where(o => o.Date > _today && o.Date != Day.Undated).OrderBy(o => o.Date).Select(o => ToRow(o.Entry, o.Date, canReorder: true, origin: OriginOf(o.Date))).ToList();
        var undated = open.Where(o => o.Date == Day.Undated).Select(o => ToRow(o.Entry, o.Date, canReorder: true)).ToList();

        var groups = new List<DayGroup>();

        void AddSection(string key, string title, List<EntryViewModel> rows)
        {
            if (rows.Count == 0) return;

            var collapsed = _settings.CollapsedSections.Contains(key);
            groups.Add(new DayGroup($"{title} ({rows.Count})", new RelayCommand(() => ToggleSection(key)), rows)
            {
                Key = key,
                IsCollapsible = true,
                IsCollapsed = collapsed,
                TitleToolTip = collapsed ? "Listeyi aç" : "Listeyi kapat",
            });
        }

        AddSection("bugun", "Bugün", today);
        AddSection(Settings.CarriedSection, "Önceki günlerden kalan", carried);
        AddSection("gelecek", "Gelecek", later);
        AddSection("tarihsiz", "Tarihsiz", undated);

        Title = "Tümü";
        Subtitle = open.Count == 0 ? "Bitmemiş iş yok" : $"Bitmemiş {open.Count} iş";
        EmptyText = "Bütün işler tamam.";
        Groups = groups;
    }

    private static string OriginOf(DateTime date) => date.ToString("d MMMM", Turkish);

    private void BuildSearch(List<DayEntries> byDate)
    {
        var query = TextSearch.Fold(_searchText.Trim());
        var groups = ToGroups(byDate, e => TextSearch.Matches(e, query));
        Title = $"“{_searchText.Trim()}”";
        Subtitle = $"{groups.Sum(g => g.Entries.Count)} sonuç, {groups.Count} günde";
        EmptyText = "Bu aramayla eşleşen not yok.";
        Groups = groups;
    }

    private List<DayGroup> ToGroups(List<DayEntries> byDate, Func<Entry, bool> filter, bool canReorder = false) =>
        byDate
            .Select(day => new { day.Date, Entries = day.Entries.Where(filter).Select(e => ToRow(e, day.Date, canReorder)).ToList() })
            .Where(day => day.Entries.Count > 0)
            .Select(day => new DayGroup(
                day.Date == Day.Undated ? "Tarihsiz" : day.Date.ToString("d MMMM yyyy dddd", Turkish),
                day.Date == Day.Undated ? ShowUndatedCommand : new RelayCommand(() => ShowDay(day.Date)),
                day.Entries))
            .ToList();

    // Rows are rebuilt after every change; the sub-task box stays open so several can be typed in a row.
    private EntryViewModel ToRow(Entry entry, DateTime date, bool canReorder = false, string origin = "")
    {
        var row = new EntryViewModel(this, entry)
        {
            IsAddingSubTask = entry == _addingSubTaskTo,
            Date = date,
            CanReorder = canReorder,
            Origin = origin,
            CanBringToToday = date != _today && entry.HasOpenWork,
            CanMakeUndated = date != Day.Undated && entry.CanBeUndated,
            CanMoveToFuture = entry.HasOpenWork && (date <= _today || date == Day.Undated),
            IsSelected = _selected.Contains(entry),
            IsJustAdded = entry == _justAdded,
            IsInDetail = entry == _detail,
            IsCollapsed = IsBodyCollapsed(entry),
            Chip = TimeChip.Of(entry, date, DateTime.Now),
            ToggleBodyCommand = new RelayCommand(() => ToggleBody(entry)),
            ToggleEarlierCommand = new RelayCommand(() => ToggleEarlier(entry)),
        };

        // An undated task has no day of its own: each finished sub-task just says when.
        if (date == Day.Undated)
        {
            foreach (var item in row.BodyItems.Where(b => b.Task is { IsDone: true }))
            {
                if (DoneDate.Read(item.Task!.Marker, date, _today) is { } on) item.DoneDateText = on.ToString("d MMM", Turkish);
            }
            return row;
        }

        // Sub-tasks finished on earlier days fold into one line, so the day shows what is left and what it did.
        var earlier = row.BodyItems
            .Where(b => b.Task is { IsDone: true } && DoneDate.Read(b.Task.Marker, date, _today) is { } on && on < date)
            .ToList();
        if (earlier.Count > 0)
        {
            var days = earlier.Select(b => b.Task!.DoneOn(date, _today)).Distinct().ToList();
            var when = days.Count == 1 ? days[0].ToString("d MMMM", Turkish) + Locative(days[0]) : "önceki günlerde";
            row.SetEarlier(
                $"{earlier.Count} alt iş {when} bitti",
                earlier.Select(b => (b, b.Task!.DoneOn(date, _today).ToString("d MMM", Turkish))).ToList(),
                _earlierOpen.Contains(entry.Text));

            // The line sits where the sub-tasks begin, under the description.
            row.BodyItems.First(b => b.Task is not null).FoldOwner = row;
        }
        return row;
    }

    /// <summary>Tasks whose earlier sub-tasks are unfolded, for as long as the program runs.</summary>
    private readonly HashSet<string> _earlierOpen = new();

    // Folding changes no note, so only the rows of that task change; rebuilding the whole screen made it stutter.
    private void ToggleEarlier(Entry entry)
    {
        var open = !_earlierOpen.Remove(entry.Text);
        if (open) _earlierOpen.Add(entry.Text);
        foreach (var row in RowsOf(entry)) row.IsEarlierOpen = open;
    }

    /// <summary>The rows on screen for a task: folds are kept by title, so every row with that title follows.</summary>
    private IEnumerable<EntryViewModel> RowsOf(Entry entry) =>
        Groups.SelectMany(g => g.Entries).Where(r => r.Text == entry.Text);

    /// <summary>The Turkish "in" ending after a date's month name: 13 Kasım'da, 1 Mart'ta, 5 Eylül'de.</summary>
    private static string Locative(DateTime date) => date.Month switch
    {
        2 or 3 or 5 or 8 or 12 => "'ta",
        9 or 10 => "'de",
        _ => "'da",
    };

    /// <summary>A task under another day, as it stood at the end of the day shown, with only what was finished on it.</summary>
    private EchoRow ToEchoRow(WorkElsewhere work)
    {
        var date = ShownDate;
        var home = work.Home;
        var entry = work.Entry;
        var total = entry.ActiveSubTasks.Count;
        var count = total == 0 ? "" : $"{(work.EntryDoneHere ? total : DayWork.DoneBy(entry, home, date, _today))}/{total}";

        string note;
        var noteDone = false;
        if (home == Day.Undated) note = "tarihsiz iş";
        else if (!work.EntryDoneHere && entry.IsFinished && entry.DoneOn(home, _today) is var finished && finished > date)
        {
            note = $"✓ {finished.ToString("d MMMM", Turkish)}{Locative(finished)} bitti";
            noteDone = true;
        }
        else if (home > date && !work.EntryDoneHere) note = $"devamı {home.ToString("d MMMM", Turkish)}{Locative(home)}";
        else note = $"{home.ToString("d MMMM", Turkish)}{Locative(home)}ki iş";

        return new EchoRow(entry.Text, count, note, noteDone, work.DoneHere.Select(t => t.Text).ToList(), work.EntryDoneHere,
            new RelayCommand(() =>
            {
                // Finished there, it waits in that day's folded list: open it, so the task can be shown.
                if (entry.IsFinished || entry.IsDropped) _openDoneDays.Add(home);
                if (home == Day.Undated) Navigate(ViewMode.Undated);
                else ShowDay(home);
                RowWanted?.Invoke(entry);
            }))
        {
            IsCollapsed = IsBodyCollapsed(entry),
            ToggleCommand = new RelayCommand(() => ToggleBody(entry)),
        };
    }

    /// <summary>
    /// A task with sub-tasks starts folded (its description still shows) until opened by hand, and stays as left;
    /// a task with only a description stays open until folded by hand.
    /// </summary>
    /// <summary>Days whose finished work was opened by hand in this run; every day starts with it folded.</summary>
    private readonly HashSet<DateTime> _openDoneDays = new();

    private void ToggleDoneFold(DateTime date)
    {
        if (!_openDoneDays.Remove(date)) _openDoneDays.Add(date);
        Rebuild();
    }

    /// <summary>A task to go to on the page just opened: the window puts the keyboard on its row and scrolls to it.</summary>
    public event Action<Entry>? RowWanted;

    private bool IsBodyCollapsed(Entry entry) => entry.SubTasks.Count > 0
        ? _settings.ExpandedTasks?.Contains(entry.Text) != true
        : _settings.CollapsedTasks?.Contains(entry.Text) == true;

    /// <summary>Folds or opens a task's sub-tasks. Titles no longer in the file are dropped, so the list stays short.</summary>
    private void ToggleBody(Entry entry)
    {
        var titles = new HashSet<string>(_document.Days.SelectMany(d => d.Entries).Select(e => e.Text));
        var hasTasks = entry.SubTasks.Count > 0;
        var list = ((hasTasks ? _settings.ExpandedTasks : _settings.CollapsedTasks) ?? new List<string>()).Where(titles.Contains).ToList();
        if (!list.Remove(entry.Text)) list.Add(entry.Text);
        if (hasTasks) _settings.ExpandedTasks = list;
        else _settings.CollapsedTasks = list;
        if (!_pathsFromCommandLine) _settings.Save();

        var collapsed = IsBodyCollapsed(entry);
        foreach (var row in RowsOf(entry)) row.IsCollapsed = collapsed;
        foreach (var echo in EchoRows.Where(e => e.Title == entry.Text)) echo.IsCollapsed = collapsed;
    }

    /// <summary>
    /// "2 yapıldı, 1 ilerledi, 6 tamamlanmamış": <paramref name="progressed"/> are the open tasks that had a sub-task
    /// finished in the period; they are counted there and not again as unfinished.
    /// </summary>
    private IList<DayChip> _dayChips = Array.Empty<DayChip>();

    /// <summary>Under a day's date, the counts as small coloured labels, so they never push the date onto two lines.</summary>
    public IList<DayChip> DayChips
    {
        get => _dayChips;
        private set { _dayChips = value; Notify(); Notify(nameof(HasDayChips)); }
    }

    public bool HasDayChips => _dayChips.Count > 0;

    /// <summary>
    /// "1 ilerledi", "2 tamamlanmamış", "1 iptal". The finished count is left to today's bar (2/5); another day, with
    /// no bar, shows it first as "✓ 2 yapıldı".
    /// </summary>
    private static IList<DayChip> ChipsOf(ICollection<Entry> entries, ICollection<Entry> progressed, bool withDone)
    {
        var chips = new List<DayChip>();
        var done = entries.Count(e => e.IsFinished);
        var open = entries.Count(e => e.HasOpenWork && !progressed.Contains(e));
        var dropped = entries.Count(e => e.IsDropped);
        if (withDone && done > 0) chips.Add(new DayChip($"✓ {done} yapıldı", "done"));
        if (progressed.Count > 0) chips.Add(new DayChip($"{progressed.Count} ilerledi", "part"));
        if (open > 0) chips.Add(new DayChip($"{open} tamamlanmamış", "open"));
        if (dropped > 0) chips.Add(new DayChip($"{dropped} iptal", "dropped"));
        return chips;
    }

    private static string Counts(ICollection<Entry> entries, ICollection<Entry>? progressed = null)
    {
        progressed ??= Array.Empty<Entry>();
        var dropped = entries.Count(e => e.IsDropped);
        return $"{entries.Count(e => e.IsFinished)} yapıldı"
            + (progressed.Count > 0 ? $", {progressed.Count} ilerledi" : "")
            + $", {entries.Count(e => e.HasOpenWork && !progressed.Contains(e))} tamamlanmamış"
            + (dropped > 0 ? $", {dropped} iptal edildi" : "");
    }

    private Dictionary<DateTime, List<Entry>>? _progressedByDay;

    /// <summary>Worked out once per redraw; the document changes in place, so <see cref="Rebuild"/> drops it.</summary>
    private Dictionary<DateTime, List<Entry>> ProgressedByDay => _progressedByDay ??= DayWork.ProgressedByDay(_document, _today);

    private List<Entry> ProgressedOn(DateTime date) => ProgressedByDay.TryGetValue(date, out var list) ? list : new List<Entry>();

    private List<Entry> ProgressedIn(DateTime from, DateTime to) =>
        ProgressedByDay.Where(p => p.Key >= from && p.Key <= to).SelectMany(p => p.Value).Distinct().ToList();

    private static DateTime FirstOfMonth(DateTime date) => new(date.Year, date.Month, 1);

    private void SetFileError(string? message)
    {
        _fileError = message;
        Notify(nameof(FileError));
        Notify(nameof(HasFileError));
    }

    private void NotifyFile()
    {
        Notify(nameof(HasFile));
        Notify(nameof(FileName));
        Notify(nameof(CurrentFileTitle));
        Notify(nameof(FilePath));
    }
}
