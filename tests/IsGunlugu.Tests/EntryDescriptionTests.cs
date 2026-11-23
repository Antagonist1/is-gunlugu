using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class EntryDescriptionTests
{
    private const string Header = "----10/11/2026\r\n";

    private static (NoteDocument Doc, Entry Entry) Open(string body)
    {
        var doc = NoteDocument.Parse(Header + body);
        return (doc, doc.Days[0].Entries[0]);
    }

    private static string BodyOf(NoteDocument doc) => doc.Serialize().Substring(Header.Length);

    [Fact]
    public void NoDescription_IsEmpty()
    {
        var (_, entry) = Open("🚨iş\r\n\t🚨alt\r\n");

        Assert.Equal("", entry.Description);
    }

    [Fact]
    public void Description_IsTheNoteLinesUnderTheEntry()
    {
        var (_, entry) = Open("🚨iş\r\nbir\r\nsatır daha\r\n\t🚨alt\r\n");

        Assert.Equal("bir\nsatır daha", entry.Description);
    }

    [Fact]
    public void Description_LeavesOutWhatBelongsToASubTask()
    {
        var (_, entry) = Open("🚨iş\r\n\t🚨alt\r\n\t\talt işin açıklaması\r\n");

        Assert.Equal("", entry.Description);
        Assert.Equal("alt işin açıklaması", entry.SubTasks[0].DescriptionLines.Single());
    }

    [Fact]
    public void SetDescription_GoesAboveTheSubTasks()
    {
        var (doc, entry) = Open("🚨iş\r\n\t🚨alt\r\n\t\tayrıntı\r\n");

        NoteDocument.SetDescription(entry, "bir\nsatır daha");

        Assert.Equal("🚨iş\r\nbir\r\nsatır daha\r\n\t🚨alt\r\n\t\tayrıntı\r\n", BodyOf(doc));
    }

    [Fact]
    public void SetDescription_ReplacesTheOldOne()
    {
        var (doc, entry) = Open("🚨iş\r\neski\r\n\t🚨alt\r\n");

        NoteDocument.SetDescription(entry, "yeni");

        Assert.Equal("🚨iş\r\nyeni\r\n\t🚨alt\r\n", BodyOf(doc));
    }

    [Fact]
    public void SetDescription_Empty_RemovesItAndKeepsSubTasks()
    {
        var (doc, entry) = Open("🚨iş\r\neski\r\n\t🚨alt\r\n");

        NoteDocument.SetDescription(entry, "   ");

        Assert.Equal("🚨iş\r\n\t🚨alt\r\n", BodyOf(doc));
    }

    [Fact]
    public void SetDescription_KeepsTheBlankLineBeforeTheNextDay()
    {
        var (doc, entry) = Open("🚨iş\r\n\r\n----11/11/2026\r\n🚨yarın\r\n");

        NoteDocument.SetDescription(entry, "bir");

        Assert.Equal("🚨iş\r\nbir\r\n\r\n----11/11/2026\r\n🚨yarın\r\n", BodyOf(doc));
    }

    [Fact]
    public void SetDescription_DropsBlankLinesTyped()
    {
        var (doc, entry) = Open("🚨iş\r\n");

        NoteDocument.SetDescription(entry, "bir\n\n  iki  ");

        Assert.Equal("🚨iş\r\nbir\r\niki\r\n", BodyOf(doc));
    }

    [Fact]
    public void SetDescription_ScatteredNotes_BecomeOneBlockAtTheFirst()
    {
        var (doc, entry) = Open("🚨iş\r\n\t🚨alt\r\nort\r\n\t🚨öbür\r\nson\r\n");

        NoteDocument.SetDescription(entry, "tek");

        Assert.Equal("🚨iş\r\n\t🚨alt\r\ntek\r\n\t🚨öbür\r\n", BodyOf(doc));
    }

    [Fact]
    public void SetDescription_ReadsBackAfterSaving()
    {
        var (doc, entry) = Open("🚨iş\r\n\t🚨alt\r\n");

        NoteDocument.SetDescription(entry, "bir\nçift");
        var again = NoteDocument.Parse(doc.Serialize()).Days[0].Entries[0];

        Assert.Equal("bir\nçift", again.Description);
        Assert.Single(again.SubTasks);
    }
}

public sealed class CanBeUndatedTests
{
    private static Entry First(string body) => NoteDocument.Parse("----10/11/2026\r\n" + body).Days[0].Entries[0];

    [Fact]
    public void OpenWork_CanBeUndated() => Assert.True(First("🚨iş\r\n").CanBeUndated);

    [Fact]
    public void PartlyDoneWork_CanBeUndated() => Assert.True(First("🚨iş\r\n\t👍[Done]a\r\n\t🚨b\r\n").CanBeUndated);

    [Fact]
    public void FinishedWork_StaysOnItsDay() => Assert.False(First("👍[Done]iş\r\n").CanBeUndated);
}
