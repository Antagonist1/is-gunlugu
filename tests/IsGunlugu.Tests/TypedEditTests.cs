using IsGunlugu.Core;

namespace IsGunlugu.Tests;

/// <summary>The pencil opens a task as it is typed in the add box: no marks, sub-tasks as "- " lines.</summary>
public sealed class TypedEditTests
{
    private static readonly DateTime Today = new(2026, 11, 13);

    private const string Sunum =
        "----12/11/2026\r\n🚨Sunum hazırla\r\nKısa açıklama\r\n\t👍[Done 12/11]Slaytlar\r\n\t🚨Prova\r\n\t\tSalı 14:00\r\n\t🚫[İptal]Afiş\r\n";

    private static Entry First(NoteDocument doc) => doc.Days[0].Entries[0];

    private static string Save(string file, string typed)
    {
        var doc = NoteDocument.Parse(file);
        NoteDocument.SetTypedText(First(doc), typed, Today);
        return doc.Serialize();
    }

    [Fact]
    public void EditableText_HasNoMarks()
    {
        var doc = NoteDocument.Parse(Sunum);

        Assert.Equal("Sunum hazırla\nKısa açıklama\n- Slaytlar\n- Prova\n  Salı 14:00\n- Afiş", NoteDocument.EditableText(First(doc)));
    }

    [Fact]
    public void SavedUnchanged_KeepsTheFile()
    {
        var doc = NoteDocument.Parse(Sunum);
        Assert.Equal(Sunum, Save(Sunum, NoteDocument.EditableText(First(doc))));
    }

    [Fact]
    public void RenamedSubTask_KeepsItsMarkAndDate()
    {
        Assert.Equal(
            "----12/11/2026\r\n🚨Sunum hazırla\r\nKısa açıklama\r\n\t👍[Done 12/11]Slaytlar (son)\r\n\t🚨Prova\r\n\t\tSalı 14:00\r\n\t🚫[İptal]Afiş\r\n",
            Save(Sunum, "Sunum hazırla\nKısa açıklama\n- Slaytlar (son)\n- Prova\n  Salı 14:00\n- Afiş"));
    }

    [Fact]
    public void RenamedSubTask_WithANewOneAdded_KeepsItsMark()
    {
        Assert.Equal(
            "----12/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]Slaytlar (son)\r\n\t🚨Prova\r\n\t🚨Kontrol\r\n",
            Save("----12/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n\t🚨Prova\r\n", "Sunum\n- Slaytlar (son)\n- Prova\n- Kontrol"));
        Assert.Equal(
            "----12/11/2026\r\n🚨Sunum\r\n\t🚨Prova\r\n\t👍[Done 12/11]Slaytlar (son)\r\n\t🚨Kontrol\r\n",
            Save("----12/11/2026\r\n🚨Sunum\r\n\t🚨Prova\r\n\t👍[Done 12/11]Slaytlar\r\n", "Sunum\n- Prova\n- Slaytlar (son)\n- Kontrol"));
    }

    [Fact]
    public void MovedSubTask_KeepsItsMark()
    {
        Assert.Equal(
            "----12/11/2026\r\n🚨Sunum hazırla\r\n\t🚨Prova\r\n\t👍[Done 12/11]Slaytlar\r\n",
            Save("----12/11/2026\r\n🚨Sunum hazırla\r\n\t👍[Done 12/11]Slaytlar\r\n\t🚨Prova\r\n", "Sunum hazırla\n- Prova\n- Slaytlar"));
    }

    [Fact]
    public void NewLine_IsAnOpenSubTask_AndReopensAFinishedTask()
    {
        Assert.Equal(
            "----12/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n\t🚨Prova\r\n",
            Save("----12/11/2026\r\n👍[Done 12/11]Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n", "Sunum\n- Slaytlar\n- Prova"));
    }

    [Fact]
    public void RemovedLine_IsDeleted_AndWhatIsLeftFinishesTheTaskToday()
    {
        Assert.Equal(
            "----12/11/2026\r\n👍[Done 13/11]Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n",
            Save("----12/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n\t🚨Prova\r\n", "Sunum\n- Slaytlar"));
    }

    [Fact]
    public void RemovedAndRenamedTogether_LeavesTheRenamedOneOpen()
    {
        // With one line gone and one renamed, which is which cannot be told: the safe guess is open, never done.
        Assert.Equal(
            "----12/11/2026\r\n🚨Sunum\r\n\t🚨Bir\r\n\t🚨Üç (yeni)\r\n",
            Save("----12/11/2026\r\n🚨Sunum\r\n\t🚨Bir\r\n\t👍[Done 12/11]İki\r\n\t🚨Üç\r\n", "Sunum\n- Bir\n- Üç (yeni)"));
    }

    [Fact]
    public void Description_AndTitle_CanChange()
    {
        Assert.Equal(
            "----12/11/2026\r\n🚨Sunumu hazırla\r\nYeni not\r\nİkinci satır\r\n\t🚨Prova\r\n\t\tB salonu\r\n",
            Save("----12/11/2026\r\n🚨Sunum\r\nEski not\r\n\t🚨Prova\r\n\t\tA salonu\r\n", "Sunumu hazırla\nYeni not\nİkinci satır\n- Prova\n  B salonu"));
    }

    [Fact]
    public void TrailingBlankLines_StayUnderTheTask()
    {
        Assert.Equal(
            "----12/11/2026\r\n🚨Sunum\r\n\t🚨Prova\r\n\r\n----13/11/2026\r\n🚨Başka\r\n",
            Save("----12/11/2026\r\n🚨Sunum\r\n\r\n----13/11/2026\r\n🚨Başka\r\n", "Sunum\n- Prova"));
    }

    [Fact]
    public void EmptyTitle_IsRefused()
    {
        var doc = NoteDocument.Parse(Sunum);
        Assert.Throws<ArgumentException>(() => NoteDocument.SetTypedText(First(doc), "\n- Prova", Today));
    }
}
