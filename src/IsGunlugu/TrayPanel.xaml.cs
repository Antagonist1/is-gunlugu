using System.Windows;
using System.Windows.Input;

namespace IsGunlugu;

/// <summary>Today's unfinished work, opened from the tray without bringing up the main window.</summary>
public partial class TrayPanel : Window
{
    private readonly Action _openApp;
    private bool _isClosing;

    public TrayPanel(MainViewModel viewModel, Action openApp)
    {
        _openApp = openApp;
        DataContext = viewModel;
        InitializeComponent();
        Closing += (_, _) => _isClosing = true;
    }

    public void ShowNearTray()
    {
        // Off screen until its height is known, so it does not jump into place.
        Left = -32000;
        Show();
        UpdateLayout();
        Place();
        SizeChanged += (_, _) => Place();
        Activate();
    }

    /// <summary>The corner of the work area next to the taskbar, wherever the taskbar is.</summary>
    public static Point CornerFor(Rect workArea, Size screen, Size panel)
    {
        var left = workArea.Left > 0 ? workArea.Left : workArea.Right - panel.Width;
        var top = workArea.Top > 0 ? workArea.Top : workArea.Bottom - panel.Height;
        return new Point(left, Math.Max(workArea.Top, top));
    }

    private void Place()
    {
        var corner = CornerFor(
            SystemParameters.WorkArea,
            new Size(SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
            new Size(ActualWidth, ActualHeight));
        Left = corner.X;
        Top = corner.Y;
    }

    private void Window_Deactivated(object sender, EventArgs e) => CloseOnce();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        CloseOnce();
        e.Handled = true;
    }

    private void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        CloseOnce();
        _openApp();
    }

    /// <summary>"+ 18 alt iş daha": the app opens on today with that task in the panel on the right.</summary>
    private void MoreTasks_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EntryViewModel row || DataContext is not MainViewModel viewModel) return;
        CloseOnce();
        _openApp();
        viewModel.ShowTodayCommand.Execute(null);
        viewModel.OpenDetail(row.Entry);
    }

    private void CloseOnce()
    {
        if (!_isClosing) Close();
    }
}
