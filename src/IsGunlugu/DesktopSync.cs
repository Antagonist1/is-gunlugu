using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>Google Drive: connecting the account, syncing in the background and asking when two versions differ.</summary>
public sealed partial class MainViewModel
{
    private static readonly string SyncStateFile = Path.Combine(Settings.Directory, "sync.json");

    private readonly DispatcherTimer _syncTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _checkTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly GoogleAuth _googleAuth = new(GoogleClient.Id, GoogleClient.Secret, new DpapiTokenStore());
    private readonly List<SyncConflict> _conflicts = new();
    private List<string> _heldDeletions = new();
    private IList<string>? _confirmedDeletions;
    private Action? _afterSync;
    private bool _syncRunning;
    private bool _syncAgain;
    private bool _isConflictOpen;
    private string _driveStatus = "";

    public ICommand ConnectDriveCommand { get; private set; } = null!;
    public ICommand DisconnectDriveCommand { get; private set; } = null!;
    public ICommand SyncNowCommand { get; private set; } = null!;
    public ICommand OpenConflictCommand { get; private set; } = null!;
    public ICommand CloseConflictCommand { get; private set; } = null!;
    public ICommand KeepLocalCommand { get; private set; } = null!;
    public ICommand KeepRemoteCommand { get; private set; } = null!;
    public ICommand ConfirmHeldCommand { get; private set; } = null!;
    public ICommand RestoreHeldCommand { get; private set; } = null!;

    public bool HasHeldDeletions => _heldDeletions.Count > 0;
    public string HeldBanner => $"{_heldDeletions.Count} dosya bir anda silinmiş görünüyor: {SyncEngine.DescribeHeld(_heldDeletions)}. Silme durduruldu.";

    public bool CanSync => _notesFolder != null && !_pathsFromCommandLine;
    public bool IsDriveConnected => _googleAuth.IsConnected;
    public bool IsDriveDisconnected => !IsDriveConnected;

    public string DriveStatus
    {
        get => _driveStatus;
        private set { _driveStatus = value; Notify(); }
    }

    public bool HasConflicts => _conflicts.Count > 0;
    public string ConflictBanner => $"Farklılık bulundu: {_conflicts.Count} dosya";

    public bool IsConflictOpen
    {
        get => _isConflictOpen && HasConflicts;
        private set { _isConflictOpen = value; Notify(); }
    }

    private SyncConflict? CurrentConflict => _conflicts.FirstOrDefault();

    public string ConflictFile => CurrentConflict?.Path.Replace("/", " / ") ?? "";
    public string ConflictLocalTime => When(CurrentConflict?.LocalUtc);
    public string ConflictRemoteTime => When(CurrentConflict?.RemoteUtc);
    public bool IsLocalNewer => CurrentConflict != null && CurrentConflict.LocalUtc >= CurrentConflict.RemoteUtc;
    public bool IsRemoteNewer => CurrentConflict != null && !IsLocalNewer;

    public IList<ConflictRow> ConflictRows => CurrentConflict == null
        ? new List<ConflictRow>()
        : ConflictView.Rows(NoteStore.Decode(CurrentConflict.Local), NoteStore.Decode(CurrentConflict.Remote), "Bilgisayar", "Telefon", "Yalnızca bilgisayarda", "Yalnızca telefonda");

    private static string When(DateTime? utc) => utc?.ToLocalTime().ToString("d MMMM HH:mm", new CultureInfo("tr-TR")) ?? "";

    private void InitSync()
    {
        ConnectDriveCommand = new RelayCommand(ConnectDrive);
        DisconnectDriveCommand = new RelayCommand(DisconnectDrive);
        SyncNowCommand = new RelayCommand(StartSync);
        OpenConflictCommand = new RelayCommand(() => IsConflictOpen = true);
        CloseConflictCommand = new RelayCommand(() => IsConflictOpen = false);
        KeepLocalCommand = new RelayCommand(() => ResolveConflict(keepLocal: true));
        KeepRemoteCommand = new RelayCommand(() => ResolveConflict(keepLocal: false));
        ConfirmHeldCommand = new RelayCommand(ConfirmHeld);
        RestoreHeldCommand = new RelayCommand(RestoreHeld);

        _syncTimer.Tick += (_, _) =>
        {
            _syncTimer.Stop();
            StartSync();
        };
        _checkTimer.Tick += (_, _) => CheckDrive();
        _checkTimer.Start();

        DriveStatus = IsDriveConnected ? "Bağlı" : "";
        StartSync();
    }

    /// <summary>A look at Drive for changes made elsewhere. A slow connection just means the next look waits its turn.</summary>
    public void CheckDrive()
    {
        if (!_syncRunning && _afterSync == null) StartSync();
    }

    /// <summary>Waits a few seconds, so a burst of edits becomes one sync.</summary>
    private void RequestSync()
    {
        _syncTimer.Stop();
        _syncTimer.Start();
    }

    private void OnBackground(Action work, Action<Exception?> done)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Exception? failure = null;
            try { work(); }
            catch (Exception ex)
            {
                failure = ex; // Nothing here may take the program down.
                Errors.Write("Drive", ex);
            }
            Application.Current.Dispatcher.BeginInvoke(new Action(() => done(failure)));
        });
    }

    private void ConnectDrive()
    {
        if (!CanSync) return;
        if (!GoogleClient.HasSecret)
        {
            DriveStatus = "Bu derlemede Google anahtarı yok, Drive'a bağlanamaz. Ayrıntı: GoogleClient.Secret.example.txt";
            return;
        }

        DriveStatus = "Tarayıcıda Google hesabını seç…";
        OnBackground(() => GoogleLoopback.SignIn(_googleAuth), failure =>
        {
            NotifyDrive();
            DriveStatus = failure?.Message ?? "Bağlandı";
            if (failure == null) StartSync();
        });
    }

    private void DisconnectDrive()
    {
        _googleAuth.Disconnect();
        _conflicts.Clear();
        _heldDeletions = new List<string>();
        NotifyDrive();
        NotifyConflicts();
        NotifyHeld();
        DriveStatus = "";
    }

    private void StartSync()
    {
        if (!CanSync || !IsDriveConnected) return;
        if (_syncRunning)
        {
            _syncAgain = true;
            return;
        }

        var confirmed = _confirmedDeletions;
        _confirmedDeletions = null;
        _syncRunning = true;
        SyncResult? result = null;
        OnBackground(() => result = NewEngine().Run(confirmed), failure =>
        {
            _syncRunning = false;
            if (result != null) ApplyResult(result);
            else ReportFailure(failure);
            FinishSync();
        });
    }

    private void FinishSync()
    {
        if (_afterSync != null)
        {
            var next = _afterSync;
            _afterSync = null;
            next();
        }
        else if (_syncAgain)
        {
            _syncAgain = false;
            StartSync();
        }
    }

    /// <summary>Runs work that writes the sync record, never beside a sync: the one that finished last would undo the other's record.</summary>
    private void WhenSyncIsIdle(Action start)
    {
        if (_syncRunning) _afterSync = start;
        else start();
    }

    private void ConfirmHeld()
    {
        _confirmedDeletions = _heldDeletions.ToList();
        StartSync();
    }

    private void RestoreHeld() => WhenSyncIsIdle(() =>
    {
        var held = _heldDeletions.ToList();
        if (held.Count == 0) return;

        _syncRunning = true;
        OnBackground(() => NewEngine().RestoreHeld(held), failure =>
        {
            _syncRunning = false;
            if (failure != null)
            {
                DriveStatus = failure.Message;
            }
            else
            {
                _heldDeletions = new List<string>();
                NotifyHeld();
                _syncAgain = true;
            }
            FinishSync();
        });
    });

    private SyncEngine NewEngine() => new(new DriveStore(_googleAuth), new LocalFolder(_notesFolder!, BackupDirectory), SyncState.Load(SyncStateFile), SyncStateFile);

    private void ApplyResult(SyncResult result)
    {
        // Unchanged ones stay as they are, or an open comparison would start over at every check.
        if (!SyncConflict.AreSame(_conflicts, result.Conflicts))
        {
            _conflicts.Clear();
            _conflicts.AddRange(result.Conflicts);
            NotifyConflicts();
        }

        if (!_heldDeletions.SequenceEqual(result.HeldDeletions))
        {
            _heldDeletions = result.HeldDeletions.ToList();
            NotifyHeld();
        }

        DriveStatus = result.Errors.FirstOrDefault() ?? "Son eşitleme: " + DateTime.Now.ToString("d MMMM HH:mm", Turkish);
        if (result.Changed > 0) ReconcileFiles();
        AfterFirstRunSync(result);
    }

    private void ReportFailure(Exception? failure)
    {
        NotifyDrive();
        DriveStatus = failure is SyncAuthException ? "Hesabı yeniden bağla" : failure?.Message ?? "";
        if (IsFirstRunFetch)
        {
            FirstRunStatus = "";
            FirstRunError = DriveStatus;
        }
    }

    /// <summary>Puts files the sync brought in on the list, and takes off the ones it removed.</summary>
    private void ReconcileFiles()
    {
        foreach (var relative in new LocalFolder(_notesFolder!, BackupDirectory).List())
        {
            var full = Path.Combine(_notesFolder!, relative.Replace('/', Path.DirectorySeparatorChar));
            if (_filePaths.Any(p => IsSamePath(p, full))) continue;

            _filePaths.Add(full);
            var cut = relative.IndexOf('/');
            if (cut <= 0) continue;

            var name = relative.Substring(0, cut);
            var group = _settings.FileGroups.FirstOrDefault(g => IsSameName(g.Name, name));
            if (group == null) _settings.FileGroups.Add(group = new FileGroup { Name = name });
            group.Paths.Add(full);
        }

        foreach (var gone in _filePaths.Where(p => IsInNotesFolder(p) && !File.Exists(p)).ToList())
        {
            var wasCurrent = IsSamePath(gone, _store?.Path);
            _filePaths.RemoveAll(p => IsSamePath(p, gone));
            RemoveFromGroups(gone);
            if (wasCurrent) CloseCurrentFile();
        }

        if (_store == null)
        {
            foreach (var other in _filePaths.ToList())
            {
                if (OpenFile(other)) break;
            }
        }

        SaveFileList();
        BuildFiles();
        Rebuild();
    }

    private void ResolveConflict(bool keepLocal) => WhenSyncIsIdle(() =>
    {
        var conflict = CurrentConflict;
        if (conflict == null) return;

        _syncRunning = true;
        OnBackground(() => NewEngine().Resolve(conflict, keepLocal), failure =>
        {
            _syncRunning = false;
            if (failure != null)
            {
                DriveStatus = failure.Message;
            }
            else
            {
                _conflicts.Remove(conflict);
                NotifyConflicts();
                ReconcileFiles();
            }
            FinishSync();
        });
    });

    private string FileUpdatedMessage() => CameFromDrive() ? "Dosya Drive'dan güncellendi." : "Dosya başka yerden değişti, güncel hali yüklendi.";

    private bool CameFromDrive()
    {
        if (_store == null || !IsInNotesFolder(_store.Path)) return false;

        var relative = _store.Path.Substring(_notesFolder!.Length).TrimStart('\\', '/').Replace('\\', '/');
        try
        {
            return SyncState.Load(SyncStateFile).Matches(relative, File.ReadAllBytes(_store.Path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false; // Only the wording depends on this.
        }
    }

    private void NotifyHeld()
    {
        Notify(nameof(HasHeldDeletions));
        Notify(nameof(HeldBanner));
    }

    private void NotifyDrive()
    {
        Notify(nameof(IsDriveConnected));
        Notify(nameof(IsDriveDisconnected));
    }

    private void NotifyConflicts()
    {
        Notify(nameof(HasConflicts));
        Notify(nameof(ConflictBanner));
        Notify(nameof(IsConflictOpen));
        Notify(nameof(ConflictFile));
        Notify(nameof(ConflictLocalTime));
        Notify(nameof(ConflictRemoteTime));
        Notify(nameof(IsLocalNewer));
        Notify(nameof(IsRemoteNewer));
        Notify(nameof(ConflictRows));
    }
}
