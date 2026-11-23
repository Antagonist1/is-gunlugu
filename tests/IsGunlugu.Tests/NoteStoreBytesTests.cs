using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class NoteStoreBytesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "IsGunluguTest", Guid.NewGuid().ToString("N"));
    private string NotePath => Path.Combine(_directory, "notlar.txt");
    private string Backups => Path.Combine(_directory, "yedekler");

    public NoteStoreBytesTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void WriteBytes_KeepsExactBytes()
    {
        var content = new byte[] { 0xFF, 0xFE, 0x61, 0x00, 0x0D, 0x00, 0x0A, 0x00 };
        File.WriteAllText(NotePath, "eski", new UTF8Encoding(false));

        new NoteStore(NotePath, Backups).WriteBytes(content);

        Assert.Equal(content, File.ReadAllBytes(NotePath));
    }

    [Fact]
    public void WriteBytes_BacksUpFirst()
    {
        File.WriteAllText(NotePath, "eski", new UTF8Encoding(false));

        new NoteStore(NotePath, Backups).WriteBytes(new byte[] { 0x61 });

        Assert.Equal("eski", File.ReadAllText(Directory.GetFiles(Backups).Single()));
    }

    [Fact]
    public void BackupVersion_NeverOverwrites()
    {
        var store = new NoteStore(NotePath, Backups);

        var first = store.BackupVersion(new byte[] { 0x61 }, "secilmeyen");
        var second = store.BackupVersion(new byte[] { 0x62 }, "secilmeyen");

        Assert.NotEqual(first, second);
        Assert.Equal(new byte[] { 0x61 }, File.ReadAllBytes(first));
        Assert.Equal(new byte[] { 0x62 }, File.ReadAllBytes(second));
        Assert.Contains("notlar", Path.GetFileName(first));
        Assert.Contains("secilmeyen", Path.GetFileName(first));
    }

    [Fact]
    public void Decode_ReadsUtf16()
    {
        Assert.Equal("a", NoteStore.Decode(new byte[] { 0xFF, 0xFE, 0x61, 0x00 }));
    }
}
