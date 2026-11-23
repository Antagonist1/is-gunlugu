using System.IO;
using System.Windows.Input;
using IsGunlugu.Core;
using Microsoft.Win32;

namespace IsGunlugu;

/// <summary>The first-run screens. Nothing is created until the last step, so going back leaves nothing behind.</summary>
public sealed partial class MainViewModel
{
    private readonly FirstRunFlow _firstRun = new();
    private string _firstFileName = "notlarım";
    private string? _firstImportPath;
    private bool _firstFileCreates = true;
    private string _firstRunError = "";
    private string _firstRunStatus = "";
    private bool _isDriveEmpty;
    private bool _isSigningIn;
    private int _signInAttempt;

    public ICommand QuickStartCommand { get; private set; } = null!;
    public ICommand ReturningUserCommand { get; private set; } = null!;
    public ICommand FirstRunBackCommand { get; private set; } = null!;
    public ICommand PickFirstImportCommand { get; private set; } = null!;
    public ICommand FirstFileNextCommand { get; private set; } = null!;
    public ICommand ConnectFirstDriveCommand { get; private set; } = null!;
    public ICommand SkipFirstDriveCommand { get; private set; } = null!;
    public ICommand FetchFromDriveCommand { get; private set; } = null!;
    public ICommand AddFromComputerCommand { get; private set; } = null!;
    public ICommand StartFreshCommand { get; private set; } = null!;

    public bool IsFirstRunWelcome => _firstRun.Step == FirstRunStep.Welcome;
    public bool IsFirstRunFile => _firstRun.Step == FirstRunStep.FirstFile;
    public bool IsFirstRunDrive => _firstRun.Step == FirstRunStep.Drive;
    public bool IsFirstRunSource => _firstRun.Step == FirstRunStep.Source;
    public bool IsFirstRunFetch => _firstRun.Step == FirstRunStep.Fetch;
    public bool IsFirstRunNewPath => IsFirstRunFile || IsFirstRunDrive;
    public bool CanFirstRunGoBack => _firstRun.CanGoBack;
    public bool CanStartSignIn => !_isSigningIn;
    public bool IsDriveEmpty => _isDriveEmpty;

    public string FirstRunHint => _firstRun.Step switch
    {
        FirstRunStep.FirstFile => "Notların Belgeler'deki İş Günlüğü klasöründe durur.",
        FirstRunStep.Drive => "Sonradan Ayarlar'dan da bağlayabilirsin.",
        FirstRunStep.Source => "Tekrar hoş geldin.",
        FirstRunStep.Fetch => "Giriş tarayıcıda açılır, sonra buraya dönersin.",
        _ => "",
    };

    public string FirstFileName
    {
        get => _firstFileName;
        set { _firstFileName = value; Notify(); }
    }

    public bool FirstFileCreates
    {
        get => _firstFileCreates;
        set
        {
            _firstFileCreates = value;
            Notify();
            Notify(nameof(FirstFileImports));
        }
    }

    public bool FirstFileImports
    {
        get => !_firstFileCreates;
        set => FirstFileCreates = !value;
    }

    public string FirstImportLabel => _firstImportPath == null ? "Kopyası alınır, aslı değişmez" : Path.GetFileName(_firstImportPath);

    public string FirstRunError
    {
        get => _firstRunError;
        private set { _firstRunError = value; Notify(); Notify(nameof(HasFirstRunError)); }
    }

    public bool HasFirstRunError => _firstRunError.Length > 0;

    public string FirstRunStatus
    {
        get => _firstRunStatus;
        private set { _firstRunStatus = value; Notify(); Notify(nameof(HasFirstRunStatus)); }
    }

    public bool HasFirstRunStatus => _firstRunStatus.Length > 0;

    private void InitFirstRun()
    {
        QuickStartCommand = new RelayCommand(() => Go(_firstRun.QuickStart));
        ReturningUserCommand = new RelayCommand(() => Go(_firstRun.Returning));
        FirstRunBackCommand = new RelayCommand(() => Go(_firstRun.Back));
        PickFirstImportCommand = new RelayCommand(PickFirstImport);
        FirstFileNextCommand = new RelayCommand(FirstFileNext);
        ConnectFirstDriveCommand = new RelayCommand(() => SignIn(() =>
        {
            FinishFirstFile();
            StartSync();
        }));
        SkipFirstDriveCommand = new RelayCommand(FinishFirstFile);
        FetchFromDriveCommand = new RelayCommand(FetchFromDrive);
        AddFromComputerCommand = new RelayCommand(AddFromComputer);
        StartFreshCommand = new RelayCommand(() => Go(_firstRun.StartFresh));
    }

    /// <summary>Moves to another step. A sign-in still open in the browser no longer decides what happens here.</summary>
    private void Go(Action move)
    {
        move();
        _signInAttempt++;
        _isSigningIn = false;
        _isDriveEmpty = false;
        FirstRunError = "";
        FirstRunStatus = "";
        NotifyFirstRun();
    }

    private void NotifyFirstRun()
    {
        Notify(nameof(IsFirstRunWelcome));
        Notify(nameof(IsFirstRunFile));
        Notify(nameof(IsFirstRunDrive));
        Notify(nameof(IsFirstRunSource));
        Notify(nameof(IsFirstRunFetch));
        Notify(nameof(IsFirstRunNewPath));
        Notify(nameof(CanFirstRunGoBack));
        Notify(nameof(CanStartSignIn));
        Notify(nameof(IsDriveEmpty));
        Notify(nameof(FirstRunHint));
    }

    private void PickFirstImport()
    {
        var dialog = new OpenFileDialog { Title = "Eklenecek metin belgesini seç", Filter = TextFileFilter };
        if (dialog.ShowDialog() != true) return;

        _firstImportPath = dialog.FileName;
        FirstRunError = "";
        Notify(nameof(FirstImportLabel));
    }

    private void FirstFileNext()
    {
        var problem = _firstFileCreates ? FirstNameProblem() : _firstImportPath == null ? "Eklenecek metin belgesini seç." : null;
        if (problem != null)
        {
            FirstRunError = problem;
            return;
        }

        if (IsDriveConnected) FinishFirstFile();
        else Go(_firstRun.FileChosen);
    }

    private string? FirstNameProblem()
    {
        var name = _firstFileName.Trim();
        var problem = NameProblem(name);
        if (problem != null) return problem;

        var target = Path.Combine(_notesFolder ?? DefaultNotesFolder, WithExtension(name, ".txt"));
        return IsFileNameTaken(target, null) || File.Exists(target) ? "Bu adda bir dosya zaten var." : null;
    }

    /// <summary>The last step of a new start: the file comes into being only now.</summary>
    private void FinishFirstFile()
    {
        string? error = null;
        if (_firstFileCreates) error = CreateFile(_firstFileName.Trim());
        else AddExistingFile(_firstImportPath!);

        if (_store != null) return;

        if (IsFirstRunDrive) Go(_firstRun.Back);
        FirstRunError = error ?? "";
    }

    private void FetchFromDrive()
    {
        Go(_firstRun.FetchFromDrive);
        if (IsDriveConnected)
        {
            FirstRunStatus = "Drive'daki notların getiriliyor…";
            StartSync();
            return;
        }

        SignIn(() =>
        {
            FirstRunStatus = "Drive'daki notların getiriliyor…";
            StartSync();
        });
    }

    private void SignIn(Action signedIn)
    {
        if (_isSigningIn) return;

        var attempt = ++_signInAttempt;
        _isSigningIn = true;
        Notify(nameof(CanStartSignIn));
        FirstRunError = "";
        FirstRunStatus = "Tarayıcıda Google hesabını seç…";

        OnBackground(() => GoogleLoopback.SignIn(_googleAuth), failure =>
        {
            NotifyDrive();
            if (attempt != _signInAttempt) return;

            _isSigningIn = false;
            Notify(nameof(CanStartSignIn));
            FirstRunStatus = "";
            if (failure != null) FirstRunError = failure.Message;
            else signedIn();
        });
    }

    private void AddFromComputer()
    {
        var dialog = new OpenFileDialog { Title = "Eklenecek metin belgelerini seç", Filter = TextFileFilter, Multiselect = true };
        if (dialog.ShowDialog() != true) return;

        foreach (var file in dialog.FileNames) AddExistingFile(file);
        if (_store != null) MarkGuideShown();
    }

    /// <summary>Tells the fetch screen how the sync went: the app opens by itself once a file has arrived.</summary>
    private void AfterFirstRunSync(SyncResult result)
    {
        if (!IsFirstRunFetch) return;

        if (_store != null)
        {
            MarkGuideShown();
            return;
        }

        if (result.Errors.Count > 0)
        {
            FirstRunStatus = "";
            FirstRunError = result.Errors[0];
        }
        else if (result.Changed == 0 && !_isDriveEmpty)
        {
            _isDriveEmpty = true;
            Notify(nameof(IsDriveEmpty));
            FirstRunStatus = "Drive'da not bulunamadı. Yeni bir başlangıç yapabilirsin.";
        }
    }
}
