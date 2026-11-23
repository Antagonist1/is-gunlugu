using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class ErrorLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "isgunlugu-errorlog-" + Guid.NewGuid().ToString("N"));

    public ErrorLogTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string LogPath => Path.Combine(_folder, "hatalar.log");

    private static Exception Thrown(string message)
    {
        try { throw new InvalidOperationException(message); }
        catch (InvalidOperationException ex) { return ex; }
    }

    [Fact]
    public void Write_RecordsTimeVersionPlaceAndTheWholeError()
    {
        var log = new ErrorLog(LogPath);
        log.Write(new DateTime(2026, 11, 12, 15, 4, 5), "1.8.2", "Ekran", Thrown("bozuk satır"));

        var text = File.ReadAllText(LogPath);
        Assert.Contains("2026-11-12 15:04:05", text);
        Assert.Contains("1.8.2", text);
        Assert.Contains("Ekran", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("bozuk satır", text);
        Assert.Contains(nameof(Thrown), text); // the stack trace
    }

    [Fact]
    public void Write_AddsToTheEnd()
    {
        var log = new ErrorLog(LogPath);
        log.Write(new DateTime(2026, 11, 12, 10, 0, 0), "1.8.2", "Birinci", Thrown("a"));
        log.Write(new DateTime(2026, 11, 12, 11, 0, 0), "1.8.2", "İkinci", Thrown("b"));

        var text = File.ReadAllText(LogPath);
        Assert.True(text.IndexOf("Birinci", StringComparison.Ordinal) < text.IndexOf("İkinci", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_WithAMessageOnly_NeedsNoException()
    {
        new ErrorLog(LogPath).Write(new DateTime(2026, 11, 12, 10, 0, 0), "1.8.2", "Drive", "Bağlantı kurulamadı.");

        Assert.Contains("Bağlantı kurulamadı.", File.ReadAllText(LogPath));
    }

    [Fact]
    public void Write_TooBig_KeepsTheNewestAndStaysSmall()
    {
        var log = new ErrorLog(LogPath, maxBytes: 4000);
        for (var i = 0; i < 100; i++) log.Write(new DateTime(2026, 11, 12, 10, 0, 0).AddMinutes(i), "1.8.2", "Kayıt " + i, "ayrıntı");

        var text = File.ReadAllText(LogPath);
        Assert.True(new FileInfo(LogPath).Length <= 4000);
        Assert.Contains("Kayıt 99", text);
        Assert.DoesNotContain("Kayıt 0\r\n", text);
        Assert.StartsWith("----", text); // cut at an entry, not in the middle of one
    }

    [Fact]
    public void Write_FolderMissing_DoesNotThrow()
    {
        var log = new ErrorLog(Path.Combine(_folder, "yok", "hatalar.log"));

        log.Write(DateTime.Now, "1.8.2", "Ekran", "ayrıntı");

        Assert.True(File.Exists(Path.Combine(_folder, "yok", "hatalar.log")));
    }
}
