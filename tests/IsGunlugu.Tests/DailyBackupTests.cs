using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DailyBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "isgunlugu-backup-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTime Today = new(2026, 11, 12);

    public DailyBackupTests()
    {
        Directory.CreateDirectory(Path.Combine(Notes, "iş"));
        File.WriteAllText(Path.Combine(Notes, "iş", "İş Günlüğü.txt"), "----12/11/2026\r\n🚨bir\r\n");
        File.WriteAllText(Path.Combine(Notes, "valheim.txt"), "----12/11/2026\r\n🚨iki\r\n");
        File.WriteAllText(Path.Combine(Notes, "resim.png"), "değil");
    }

    public void Dispose() => Directory.Delete(_root, true);

    private string Notes => Path.Combine(_root, "notlar");
    private string Backups => Path.Combine(_root, "yedekler");

    [Fact]
    public void CopiesEveryNoteWithItsFolders_IntoTodaysFolder()
    {
        Assert.True(DailyBackup.Run(Notes, Backups, Today));

        var day = Path.Combine(Backups, "2026-11-12");
        Assert.Equal("----12/11/2026\r\n🚨bir\r\n", File.ReadAllText(Path.Combine(day, "iş", "İş Günlüğü.txt")));
        Assert.True(File.Exists(Path.Combine(day, "valheim.txt")));
        Assert.False(File.Exists(Path.Combine(day, "resim.png")));
    }

    [Fact]
    public void SecondTimeTheSameDay_DoesNothing()
    {
        DailyBackup.Run(Notes, Backups, Today);
        File.WriteAllText(Path.Combine(Notes, "valheim.txt"), "değişti");

        Assert.False(DailyBackup.Run(Notes, Backups, Today));
        Assert.NotEqual("değişti", File.ReadAllText(Path.Combine(Backups, "2026-11-12", "valheim.txt")));
    }

    [Fact]
    public void KeepsThirtyDays_AndLeavesOtherFoldersAlone()
    {
        for (var i = 1; i <= 40; i++) Directory.CreateDirectory(Path.Combine(Backups, Today.AddDays(-i).ToString("yyyy-MM-dd")));
        Directory.CreateDirectory(Path.Combine(Backups, "elle-alinan"));

        DailyBackup.Run(Notes, Backups, Today);

        var kept = Directory.GetDirectories(Backups).Select(Path.GetFileName).ToList();
        Assert.Contains("2026-11-12", kept);
        Assert.Contains(Today.AddDays(-29).ToString("yyyy-MM-dd"), kept);
        Assert.DoesNotContain(Today.AddDays(-30).ToString("yyyy-MM-dd"), kept);
        Assert.Contains("elle-alinan", kept);
        Assert.Equal(31, kept.Count); // 30 days and the folder kept by hand
    }

    [Fact]
    public void NoNotesFolder_NothingHappens()
    {
        Assert.False(DailyBackup.Run(Path.Combine(_root, "yok"), Backups, Today));
        Assert.False(Directory.Exists(Path.Combine(Backups, "2026-11-12")));
    }
}
