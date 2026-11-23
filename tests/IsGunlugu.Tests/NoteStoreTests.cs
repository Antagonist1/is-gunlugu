using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class NoteStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "isgunlugu-" + Guid.NewGuid())).FullName;

    private string NotesPath => Path.Combine(_directory, "notlar.txt");
    private string BackupDirectory => Path.Combine(_directory, "yedek");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-8-bom")]
    [InlineData("utf-16")]
    [InlineData("utf-16-be")]
    public void Keeps_the_encoding_of_the_file(string name)
    {
        var encoding = name switch
        {
            "utf-8" => new UTF8Encoding(false),
            "utf-8-bom" => new UTF8Encoding(true),
            "utf-16" => new UnicodeEncoding(false, true),
            _ => (Encoding)new UnicodeEncoding(true, true),
        };
        byte[] Encode(string s) => encoding.GetPreamble().Concat(encoding.GetBytes(s)).ToArray();
        File.WriteAllBytes(NotesPath, Encode("🚨şifre"));
        var store = new NoteStore(NotesPath, BackupDirectory);

        Assert.Equal("🚨şifre", store.Read());
        store.Write("👍[Done]şifre");

        Assert.Equal(Encode("👍[Done]şifre"), File.ReadAllBytes(NotesPath));
    }

    [Fact]
    public void Refuses_ansi_file_instead_of_corrupting_it()
    {
        var ansi = new byte[] { 0xFE, 0x69, 0x66, 0x72, 0x65 };
        File.WriteAllBytes(NotesPath, ansi);

        Assert.Throws<NoteFileException>(() => new NoteStore(NotesPath, BackupDirectory).Read());
        Assert.Equal(ansi, File.ReadAllBytes(NotesPath));
    }

    [Fact]
    public void Backs_up_the_original_once_per_day()
    {
        File.WriteAllText(NotesPath, "ilk hali");
        var store = new NoteStore(NotesPath, BackupDirectory);
        store.Read();

        store.Write("ikinci");
        store.Write("üçüncü");

        var backup = Assert.Single(Directory.GetFiles(BackupDirectory));
        Assert.Equal("ilk hali", File.ReadAllText(backup));
        Assert.Equal("üçüncü", File.ReadAllText(NotesPath));
        Assert.False(File.Exists(NotesPath + ".tmp"));
    }

    [Fact]
    public void Keeps_only_the_newest_backups()
    {
        var prefix = Path.GetFileName(_directory);
        Directory.CreateDirectory(BackupDirectory);
        for (var day = 1; day <= 31; day++) File.WriteAllText(Path.Combine(BackupDirectory, $"{prefix}-notlar-202001{day:00}.txt"), "");
        File.WriteAllText(NotesPath, "x");
        var store = new NoteStore(NotesPath, BackupDirectory);
        store.Read();

        store.Write("y");

        var names = Directory.GetFiles(BackupDirectory).Select(Path.GetFileName).ToList();
        Assert.Equal(30, names.Count);
        Assert.DoesNotContain($"{prefix}-notlar-20200102.txt", names);
        Assert.Contains($"{prefix}-notlar-20200131.txt", names);
    }

    [Fact]
    public void Finished_work_moved_to_an_earlier_empty_day_keeps_its_state_and_the_days_stay_in_order()
    {
        var doc = NoteDocument.Parse("----10/10/2026\r\n🚨eski\r\n\r\n----12/10/2026\r\n👍[Done]bitmiş\r\n🚨açık\r\n");

        doc.MoveEntry(doc.Days[1].Entries[0], new DateTime(2026, 10, 11), null);
        doc.MoveEntry(doc.Days[2].Entries[0], new DateTime(2026, 10, 1), null);

        var reread = NoteDocument.Parse(doc.Serialize());
        Assert.Equal(
            new[] { new DateTime(2026, 10, 1), new DateTime(2026, 10, 10), new DateTime(2026, 10, 11) },
            reread.Days.Select(d => d.Date));
        Assert.Equal(EntryStatus.Open, reread.Days[0].Entries[0].Status);
        Assert.Equal("açık", reread.Days[0].Entries[0].Text);
        Assert.Equal(EntryStatus.Done, reread.Days[2].Entries[0].Status);
        Assert.Equal("bitmiş", reread.Days[2].Entries[0].Text);
    }

    [Fact]
    public void Same_file_name_in_two_folders_gets_separate_backups()
    {
        foreach (var folder in new[] { "bir", "iki" })
        {
            var path = Path.Combine(Directory.CreateDirectory(Path.Combine(_directory, folder)).FullName, "notlar.txt");
            File.WriteAllText(path, folder);
            var store = new NoteStore(path, BackupDirectory);
            store.Read();
            store.Write("yeni");
        }

        var contents = Directory.GetFiles(BackupDirectory).Select(File.ReadAllText).OrderBy(s => s, StringComparer.Ordinal);
        Assert.Equal(new[] { "bir", "iki" }, contents);
    }
}
