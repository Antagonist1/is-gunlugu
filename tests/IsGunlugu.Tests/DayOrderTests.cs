using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DayOrderTests : IDisposable
{
    private const string Mixed =
        "----03/11/2026\r\n👍[Done]bitti1\r\n🚨acik1\r\n🚨yarim1\r\n\t👍[Done]alt1\r\n\t🚨alt2\r\n\t\tnotu\r\n🚨acik2\r\n👍[Done]bitti2\r\n\r\n" +
        "----04/11/2026\r\n🚨acik3\r\n\r\n" +
        "----tarihsiz\r\n🚨son\r\n🚨yarim2\r\n\t👍[Done]alt3\r\n\t🚨alt4\r\n";

    private const string MixedSorted =
        "----03/11/2026\r\n🚨yarim1\r\n\t👍[Done]alt1\r\n\t🚨alt2\r\n\t\tnotu\r\n🚨acik1\r\n🚨acik2\r\n👍[Done]bitti1\r\n👍[Done]bitti2\r\n\r\n" +
        "----04/11/2026\r\n🚨acik3\r\n\r\n" +
        "----tarihsiz\r\n🚨son\r\n🚨yarim2\r\n\t👍[Done]alt3\r\n\t🚨alt4\r\n";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "IsGunluguTest", Guid.NewGuid().ToString("N"));

    public DayOrderTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    private string Backups => Path.Combine(_directory, "yedek");

    private string WriteFile(string text)
    {
        var path = Path.Combine(_directory, "notlar.txt");
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
        return path;
    }

    private static Entry Named(NoteDocument document, string name) =>
        document.Days.SelectMany(d => d.Entries).First(e => e.Text == name);

    [Fact]
    public void SortKeepsEachStatesOrderAndTheBlankLineBetweenDays()
    {
        var document = NoteDocument.Parse(Mixed);
        document.SortDaysByState();
        Assert.Equal(MixedSorted, document.Serialize());
    }

    [Fact]
    public void SortLeavesAnOrderedFileUntouched()
    {
        var document = NoteDocument.Parse(MixedSorted);
        document.SortDaysByState();
        Assert.Equal(MixedSorted, document.Serialize());
    }

    [Fact]
    public void FinishedTaskGoesToTheEndOfItsDay()
    {
        var document = NoteDocument.Parse(MixedSorted);
        var entry = Named(document, "acik1");
        NoteDocument.SetDone(entry, true, new DateTime(2026, 11, 3));
        document.PlaceByState(entry);
        Assert.Equal(new[] { "yarim1", "acik2", "bitti1", "bitti2", "acik1" }, document.Days[0].Entries.Select(e => e.Text));
        Assert.EndsWith("👍[Done 03/11]acik1\r\n\r\n----04/11/2026\r\n🚨acik3\r\n\r\n----tarihsiz\r\n🚨son\r\n🚨yarim2\r\n\t👍[Done]alt3\r\n\t🚨alt4\r\n", document.Serialize());
    }

    [Fact]
    public void ReopenedTaskGoesBeforeTheFinishedOnes()
    {
        var document = NoteDocument.Parse(MixedSorted);
        var entry = Named(document, "bitti2");
        NoteDocument.SetDone(entry, false);
        document.PlaceByState(entry);
        Assert.Equal(new[] { "yarim1", "acik1", "acik2", "bitti2", "bitti1" }, document.Days[0].Entries.Select(e => e.Text));
    }

    [Fact]
    public void HandMadeOrderSurvivesOtherTasksChangingState()
    {
        // A finished task placed on top by hand stays there when another task is ticked.
        var document = NoteDocument.Parse("----03/11/2026\r\n👍[Done]elle\r\n🚨acik1\r\n🚨acik2\r\n");
        var entry = Named(document, "acik1");
        NoteDocument.SetDone(entry, true, new DateTime(2026, 11, 3));
        document.PlaceByState(entry);
        Assert.Equal(new[] { "elle", "acik2", "acik1" }, document.Days[0].Entries.Select(e => e.Text));
    }

    [Fact]
    public void TaskAlreadyInPlaceDoesNotMove()
    {
        var document = NoteDocument.Parse(MixedSorted);
        document.PlaceByState(Named(document, "acik2"));
        Assert.Equal(MixedSorted, document.Serialize());
    }

    [Fact]
    public void UndatedWorkKeepsItsOrder()
    {
        var document = NoteDocument.Parse(MixedSorted);
        document.PlaceByState(Named(document, "yarim2"));
        Assert.Equal(MixedSorted, document.Serialize());
    }

    [Fact]
    public void UpgradeSortsTheFileAndKeepsACopyOfTheOldOne()
    {
        var path = WriteFile(Mixed);
        Assert.True(DayOrderUpgrade.SortFile(path, Backups, out var changed));
        Assert.True(changed);
        Assert.Equal(MixedSorted, File.ReadAllText(path));

        var copy = Assert.Single(Directory.GetFiles(Path.Combine(Backups, DayOrderUpgrade.BackupFolderName)));
        Assert.Equal(Mixed, File.ReadAllText(copy));
    }

    [Fact]
    public void UpgradeDoesNotWriteAnOrderedFile()
    {
        var path = WriteFile(MixedSorted);
        var written = File.GetLastWriteTimeUtc(path);
        Assert.True(DayOrderUpgrade.SortFile(path, Backups, out var changed));
        Assert.False(changed);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        Assert.False(Directory.Exists(Backups));
    }

    [Fact]
    public void UpgradeSkipsAMissingFile()
    {
        Assert.True(DayOrderUpgrade.SortFile(Path.Combine(_directory, "yok.txt"), Backups, out var changed));
        Assert.False(changed);
    }

    [Fact]
    public void UpgradeLeavesAFileItCannotReadAndAsksToTryAgain()
    {
        var path = Path.Combine(_directory, "bozuk.txt");
        var bytes = new byte[] { 0x2D, 0xFF, 0xC0, 0x0D, 0x0A };
        File.WriteAllBytes(path, bytes);
        Assert.False(DayOrderUpgrade.SortFile(path, Backups, out var changed));
        Assert.False(changed);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }
}
