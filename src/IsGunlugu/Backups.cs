using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>
/// The daily copy of the notes, kept on this computer for thirty days. The app's minute timer asks for it, so it
/// is made soon after start and again after midnight; a copy that fails is tried again a minute later.
/// </summary>
public sealed partial class MainViewModel
{
    private DateTime _backedUpOn;

    /// <summary>All backups: the app's own copies of deleted and sorted files, and the daily ones in a folder of their own.</summary>
    public static string BackupFolder => Path.Combine(Settings.Directory, "yedekler");

    public static string DailyBackupFolder => Path.Combine(BackupFolder, "gunluk");

    public ICommand OpenBackupsCommand { get; private set; } = null!;

    private void InitBackups() => OpenBackupsCommand = new RelayCommand(OpenBackups);

    public void BackUpToday(DateTime now)
    {
        if (_pathsFromCommandLine || _backedUpOn == now.Date) return;

        try
        {
            DailyBackup.Run(_notesFolder ?? DefaultNotesFolder, DailyBackupFolder, now.Date);
            _backedUpOn = now.Date;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Errors.Write("Yedek", ex);
        }
    }

    private void OpenBackups()
    {
        if (!Directory.Exists(BackupFolder))
        {
            StatusText = "Henüz yedek yok; uygulama açıkken ilk yedek bir dakika içinde alınır.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(BackupFolder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            StatusText = $"Klasör açılamadı: {ex.Message}";
        }
    }
}
