namespace IsGunlugu;

/// <summary>Where the window was left, so it opens there again: size, place and whether it filled the screen.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Where the window was left last time, or null the first time.</summary>
    public double[]? SavedWindowPlace => _settings.WindowPlace is { Length: 4 } place && place[2] > 0 && place[3] > 0 ? place : null;

    public bool SavedWindowMaximized => _settings.WindowMaximized;

    /// <summary>Called as the window closes or goes to the tray: the place it comes back to next time.</summary>
    public void RememberWindow(double left, double top, double width, double height, bool maximized)
    {
        _settings.WindowPlace = new[] { left, top, width, height };
        _settings.WindowMaximized = maximized;
        if (!_pathsFromCommandLine) _settings.Save();
    }
}
