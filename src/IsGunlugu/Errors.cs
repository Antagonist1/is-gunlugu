using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>
/// Writes what went wrong to hatalar.log beside the settings, and keeps the program alive after a slip in the
/// window: the screen is loaded again from the file, so a half-made change is never saved.
/// </summary>
public static class Errors
{
    private const int BurstLimit = 5;
    private static readonly TimeSpan Burst = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RepeatQuiet = TimeSpan.FromMinutes(10);
    private static readonly List<DateTime> RecentScreenErrors = new();
    private static string _lastText = "";
    private static DateTime _lastAt;

    public static ErrorLog Log { get; } = new(Path.Combine(Settings.Directory, "hatalar.log"));

    private static string Version => typeof(Errors).Assembly.GetName().Version?.ToString(3) ?? "";

    public static void Write(string where, Exception error) => Write(where, error.ToString(), error.GetType().Name + error.Message);

    public static void Write(string where, string details) => Write(where, details, details);

    /// <summary>The same problem again within minutes, such as Drive being out of reach every check, is written once.</summary>
    private static void Write(string where, string details, string sameness)
    {
        var key = where + "|" + sameness;
        var now = DateTime.Now;
        lock (RecentScreenErrors)
        {
            if (key == _lastText && now - _lastAt < RepeatQuiet) return;
            _lastText = key;
            _lastAt = now;
        }
        Log.Write(now, Version, where, details);
    }

    /// <summary>Hooks every place an error can escape: the window, other threads and forgotten tasks.</summary>
    public static void Watch(Application app, Action recover)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            Write("Ekran", e.Exception);
            if (IsBurst())
            {
                Write("Ekran", "Kısa sürede çok hata oldu, uygulama kapanıyor.");
                return;
            }

            e.Handled = true;
            try
            {
                recover();
            }
            catch (Exception again)
            {
                Write("Kurtarma", again);
            }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Write("Uygulama kapandı", ex);
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("Arka plan", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Opens the log in Notepad; false when nothing was ever written.</summary>
    public static bool Open()
    {
        if (!File.Exists(Log.Path)) return false;
        Process.Start("notepad.exe", "\"" + Log.Path + "\"");
        return true;
    }

    /// <summary>An error that keeps coming back is not a slip; the program then closes rather than loop.</summary>
    private static bool IsBurst()
    {
        var now = DateTime.Now;
        lock (RecentScreenErrors)
        {
            RecentScreenErrors.Add(now);
            RecentScreenErrors.RemoveAll(t => now - t > Burst);
            return RecentScreenErrors.Count > BurstLimit;
        }
    }
}
