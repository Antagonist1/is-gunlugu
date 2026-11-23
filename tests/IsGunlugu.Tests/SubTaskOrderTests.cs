using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class SubTaskOrderTests
{
    private const string Three = "----01/10/2026\r\n🚨ana\r\n\t🚨bir\r\n\t👍[Done]iki\r\n\t🚨üç\r\n";

    private static string Moved(string text, int from, int to)
    {
        var doc = NoteDocument.Parse(text);
        NoteDocument.MoveSubTask(doc.Days[0].Entries[0].SubTasks[from], to);
        return doc.Serialize();
    }

    [Fact]
    public void FirstToLast()
    {
        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t👍[Done]iki\r\n\t🚨üç\r\n\t🚨bir\r\n", Moved(Three, 0, 2));
    }

    [Fact]
    public void LastToFirst()
    {
        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨üç\r\n\t🚨bir\r\n\t👍[Done]iki\r\n", Moved(Three, 2, 0));
    }

    [Fact]
    public void OneDown()
    {
        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t👍[Done]iki\r\n\t🚨bir\r\n\t🚨üç\r\n", Moved(Three, 0, 1));
    }

    [Fact]
    public void SamePlace_ChangesNothing()
    {
        Assert.Equal(Three, Moved(Three, 1, 1));
    }

    [Fact]
    public void TheDescriptionTravelsWithItsSubTask()
    {
        const string text = "----01/10/2026\r\n🚨ana\r\n\t🚨bir\r\n\t\tbirin açıklaması\r\n\t🚨iki\r\n";

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨iki\r\n\t🚨bir\r\n\t\tbirin açıklaması\r\n", Moved(text, 0, 1));
    }

    [Fact]
    public void TheTasksOwnNoteStaysOnTop()
    {
        const string text = "----01/10/2026\r\n🚨ana\r\nnot satırı\r\n\t🚨bir\r\n\t🚨iki\r\n";

        Assert.Equal("----01/10/2026\r\n🚨ana\r\nnot satırı\r\n\t🚨iki\r\n\t🚨bir\r\n", Moved(text, 1, 0));
    }

    [Fact]
    public void BlankLinesBeforeTheNextDayStayAtTheEnd()
    {
        const string text = "----01/10/2026\r\n🚨ana\r\n\t🚨bir\r\n\t🚨iki\r\n\r\n----02/10/2026\r\n🚨öbür\r\n";

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨iki\r\n\t🚨bir\r\n\r\n----02/10/2026\r\n🚨öbür\r\n", Moved(text, 0, 1));
    }
}
