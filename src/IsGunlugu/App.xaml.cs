using System.Threading;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Threading;

namespace IsGunlugu;

public partial class App : Application
{
    private const string InstanceName = "IsGunlugu-tek-kopya";
    private const string ActivateName = "IsGunlugu-one-getir";

    private Mutex? _instance;
    private TrayIcon? _tray;
    private MainWindow? _window;
    private TrayPanel? _panel;
    private GlobalHotKey? _hotKey;
    private DispatcherTimer? _dayEnd;
    private QuickAdd? _quickAdd;
    private DateTime _panelClosedAt;
    private MainViewModel? _viewModel;
    private bool? _themeOverride;

    public static bool IsDarkTheme { get; private set; }

    /// <summary>Set when the app really quits, so closing the window no longer means hiding it.</summary>
    public static bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Errors.Watch(this, () => _viewModel?.RecoverAfterError());

        var themeArg = e.Args.FirstOrDefault(a => a.StartsWith("--theme=", StringComparison.Ordinal));
        if (themeArg is not null) _themeOverride = themeArg == "--theme=dark";

        // Paths on the command line replace the file list for this run only; the saved list stays.
        var paths = e.Args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var isSessionOnly = paths.Count > 0;
        var startHidden = e.Args.Contains("--tray");

        if (!isSessionOnly && !BecomeOnlyInstance(startHidden))
        {
            Shutdown();
            return;
        }

        _viewModel = new MainViewModel(paths);
        _viewModel.ThemeChanged += ApplyTheme;
        ApplyTheme();
        SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyTheme));

        _window = new MainWindow(_viewModel);
        if (isSessionOnly)
        {
            _window.Show();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _tray = new TrayIcon(ShowTodayPanel, ShowWindow, Quit);
        _tray.SetOpenCount(_viewModel.TodayOpenCount);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.TodayOpenCount)) _tray.SetOpenCount(_viewModel.TodayOpenCount);
        };
        _window.HiddenToTray += () =>
        {
            if (_viewModel.TakeTrayHint()) _tray.ShowHint();
        };
        _window.Closed += (_, _) => Shutdown();

        _hotKey = new GlobalHotKey(ShowQuickAdd);
        if (!_hotKey.IsWorking) _viewModel.QuickAddKeyTaken();

        // The evening reminder: checked every minute, and once soon after start for a PC switched on late.
        _dayEnd = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _dayEnd.Tick += (_, _) =>
        {
            _dayEnd.Interval = TimeSpan.FromMinutes(1);
            _viewModel.BackUpToday(DateTime.Now);
            _tray.SetOpenCount(_viewModel.TodayOpenCount); // a new day starts with a new count
            var reminder = _viewModel.TakeDayEndReminder(DateTime.Now);
            if (reminder is not null) _tray.ShowReminder(reminder[0], reminder[1]);
            // A task's own reminder comes after the evening one when both fall in the same minute; the next tick shows it.
            if (reminder is null && _viewModel.TakeTimedReminder(DateTime.Now) is { } timed)
                _tray.ShowReminder(timed.Title, timed.Text, () =>
                {
                    ShowWindow();
                    timed.Open();
                });
        };
        _dayEnd.Start();

        if (!startHidden) _window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dayEnd?.Stop();
        _hotKey?.Dispose();
        _tray?.Dispose();
        if (_instance is not null)
        {
            _instance.ReleaseMutex();
            _instance.Dispose();
        }
        base.OnExit(e);
    }

    private bool BecomeOnlyInstance(bool startHidden)
    {
        _instance = new Mutex(true, InstanceName, out var isFirst);
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateName);
        if (!isFirst)
        {
            _instance.Dispose();
            _instance = null;
            if (!startHidden) activate.Set();
            activate.Dispose();
            return false;
        }

        ThreadPool.RegisterWaitForSingleObject(
            activate, (_, _) => Dispatcher.BeginInvoke(new Action(ShowWindow)), null, Timeout.Infinite, false);
        return true;
    }

    private void ShowTodayPanel()
    {
        if (_viewModel is null) return;

        if (_panel is not null)
        {
            _panel.Close();
            return;
        }

        // Clicking the icon while the panel is open closes it by taking the focus away; the same click must not reopen it.
        if (DateTime.UtcNow - _panelClosedAt < TimeSpan.FromMilliseconds(300)) return;

        _viewModel.RefreshToday();
        _panel = new TrayPanel(_viewModel, ShowWindow);
        _panel.Closed += (_, _) =>
        {
            _panel = null;
            _panelClosedAt = DateTime.UtcNow;
        };
        _panel.ShowNearTray();
    }

    /// <summary>Win+Shift+N: the small box for today's work. Before a notes file exists, the window itself instead.</summary>
    private void ShowQuickAdd()
    {
        if (_viewModel is null) return;
        if (!_viewModel.HasFile)
        {
            ShowWindow();
            return;
        }

        _panel?.Close();
        _quickAdd ??= new QuickAdd(_viewModel);
        _quickAdd.ShowNearPointer();
    }

    private void ShowWindow()
    {
        if (_window is null) return;

        _panel?.Close();
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void Quit()
    {
        IsExiting = true;
        _window?.Close();
        Shutdown();
    }

    private void ApplyTheme()
    {
        var dark = _themeOverride ?? _viewModel?.Theme switch
        {
            ThemeChoice.Dark => true,
            ThemeChoice.Light => false,
            _ => SystemUsesDarkTheme(),
        };

        var themeChanged = dark != IsDarkTheme || Resources.MergedDictionaries.Count < 2;
        if (!themeChanged) return;

        IsDarkTheme = dark;
        var theme = new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative),
        };
        if (Resources.MergedDictionaries.Count >= 2) Resources.MergedDictionaries[0] = theme;
        else Resources.MergedDictionaries.Insert(0, theme);

        _window?.UpdateTitleBar();
    }

    private static bool SystemUsesDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is 0;
    }
}
