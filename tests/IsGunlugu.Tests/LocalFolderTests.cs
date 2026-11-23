using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class LocalFolderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "IsGunluguTest", Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_directory, "kok");
    private string Backups => Path.Combine(_directory, "yedekler");

    public LocalFolderTests() => Directory.CreateDirectory(Root);

    public void Dispose() => Directory.Delete(_directory, true);

    private LocalFolder Folder() => new(Root, Backups);

    private void Put(string relative, string text)
    {
        var full = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text, new UTF8Encoding(false));
    }

    [Fact]
    public void List_FindsTxtInSubfolders_SkipsTmp()
    {
        Put("ev.txt", "a");
        Put("iş/notlar.txt", "b");
        Put("iş/notlar.txt.tmp", "c");
        Put("iş/resim.png", "d");

        Assert.Equal(new[] { "ev.txt", "iş/notlar.txt" }, Folder().List().OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void Write_CreatesFolder()
    {
        Folder().Write("yeni/not.txt", new byte[] { 0x61 });

        Assert.Equal(new byte[] { 0x61 }, File.ReadAllBytes(Path.Combine(Root, "yeni", "not.txt")));
    }

    [Fact]
    public void Delete_KeepsBackup()
    {
        Put("not.txt", "eski");

        Folder().Delete("not.txt");

        Assert.False(File.Exists(Path.Combine(Root, "not.txt")));
        Assert.Equal("eski", File.ReadAllText(Directory.GetFiles(Backups).Single()));
    }

    [Fact]
    public void Rename_StaysInItsFolder_KeepsContent()
    {
        Put("iş/eski.txt", "içerik");

        var renamed = Folder().Rename("iş/eski.txt", " yeni ");

        Assert.Equal("iş/yeni.txt", renamed);
        Assert.Equal(new[] { "iş/yeni.txt" }, Folder().List());
        Assert.Equal("içerik", Encoding.UTF8.GetString(Folder().Read(renamed)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    public void Rename_RefusesUnusableName(string name)
    {
        Put("eski.txt", "a");

        Assert.Throws<NoteFileException>(() => Folder().Rename("eski.txt", name));
        Assert.Equal(new[] { "eski.txt" }, Folder().List());
    }

    [Theory]
    [InlineData("diger")]
    [InlineData("DIGER")]
    [InlineData("baska")] // In another folder: the desktop's list shows bare names, so one name stays one file.
    public void Rename_RefusesNameTaken_IgnoringCase(string name)
    {
        Put("eski.txt", "a");
        Put("diger.txt", "b");
        Put("iş/baska.txt", "c");

        Assert.Throws<NoteFileException>(() => Folder().Rename("eski.txt", name));
        Assert.Equal("a", Encoding.UTF8.GetString(Folder().Read("eski.txt")));
    }

    [Fact]
    public void Rename_AcceptsExtensionTyped()
    {
        Put("eski.txt", "a");

        Assert.Equal("yeni.txt", Folder().Rename("eski.txt", "yeni.txt"));
    }

    [Fact]
    public void Rename_ToSameName_ChangesNothing()
    {
        Put("eski.txt", "a");

        Assert.Equal("eski.txt", Folder().Rename("eski.txt", "eski"));
        Assert.Equal("a", Encoding.UTF8.GetString(Folder().Read("eski.txt")));
    }

    [Theory]
    [InlineData("../disari.txt")]
    [InlineData("iş/../../disari.txt")]
    public void PathOutsideRoot_IsRefused(string path)
    {
        Assert.Throws<NoteFileException>(() => Folder().Write(path, new byte[] { 0x61 }));
        Assert.False(File.Exists(Path.Combine(_directory, "disari.txt")));
    }

    [Fact]
    public void State_RoundTrips()
    {
        var file = Path.Combine(_directory, "sync.json");
        var state = new SyncState();
        state.Records["iş/Not.txt"] = new SyncRecord { RemoteId = "id1", RemoteRevision = "r1", LocalHash = "h1" };

        state.Save(file);
        var loaded = SyncState.Load(file);

        var record = loaded.Records["iş/not.txt"]; // Windows treats these as one name, so the record must too.
        Assert.Equal("id1", record.RemoteId);
        Assert.Equal("r1", record.RemoteRevision);
        Assert.Equal("h1", record.LocalHash);
    }

    [Fact]
    public void State_MissingFileIsEmpty() =>
        Assert.Empty(SyncState.Load(Path.Combine(_directory, "yok.json")).Records);

    [Fact]
    public void State_CorruptFileIsEmpty()
    {
        var file = Path.Combine(_directory, "bozuk.json");
        File.WriteAllText(file, "{bu json değil");

        Assert.Empty(SyncState.Load(file).Records);
    }

    [Fact]
    public void Hash_DependsOnContent()
    {
        Assert.Equal(SyncState.Hash(new byte[] { 1, 2 }), SyncState.Hash(new byte[] { 1, 2 }));
        Assert.NotEqual(SyncState.Hash(new byte[] { 1, 2 }), SyncState.Hash(new byte[] { 2, 1 }));
    }
}
