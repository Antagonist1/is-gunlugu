using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class NewTaskDraftTests
{
    // A Thursday.
    private static readonly DateTime Today = new(2026, 11, 12);

    [Fact]
    public void Resolve_ReadsDayFromTitle()
    {
        var r = new NewTaskDraft { Title = "yarın rapor gönder" }.Resolve(Today, Today);
        Assert.Equal(new DateTime(2026, 11, 13), r.Date);
        Assert.Equal("rapor gönder", r.Title);
        Assert.Equal("yarın", r.Phrase);
    }

    [Fact]
    public void Resolve_ChosenDayWins_AndKeepsWords()
    {
        var r = new NewTaskDraft { Title = "yarın rapor", ChosenDay = Day.Undated }.Resolve(Today, Today);
        Assert.Equal(Day.Undated, r.Date);
        Assert.Equal("yarın rapor", r.Title);
        Assert.Null(r.Phrase);
    }

    [Fact]
    public void Resolve_NoDay_UsesDefault()
    {
        var r = new NewTaskDraft { Title = "rapor" }.Resolve(Today, Today.AddDays(1));
        Assert.Equal(Today.AddDays(1), r.Date);
        Assert.Equal("rapor", r.Title);
    }

    [Fact]
    public void Add_WithDescriptionAndSubTasks()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Eski\r\n");
        doc.AddEntry(Today, "Rapor", "Özet sayfa.", new[] { "Topla", " ", "Grafik" }, false);
        Assert.Equal("----12/11/2026\r\n🚨Eski\r\n🚨Rapor\r\nÖzet sayfa.\r\n\t🚨Topla\r\n\t🚨Grafik\r\n", doc.Serialize());
    }

    [Fact]
    public void Add_DescriptionStartingWithDash_StaysDescription()
    {
        var doc = NoteDocument.Parse("");
        var e = doc.AddEntry(Today, "Rapor", "- sadece özet", Array.Empty<string>(), false);
        Assert.Empty(e.SubTasks);
        Assert.Contains("- sadece özet", doc.Serialize());
    }

    [Fact]
    public void Add_TypedLinesInTitle_FollowOldRules_BetweenDescriptionAndSubTasks()
    {
        var doc = NoteDocument.Parse("");
        var e = doc.AddEntry(Today, "Rapor\n- topla\n\tönce kasa\nnot satırı\n- grafik", "Özet", new[] { "Son kontrol" }, false);
        Assert.Equal("Rapor", e.Text);
        Assert.Equal(
            "----12/11/2026\r\n🚨Rapor\r\nÖzet\r\n\t🚨topla\r\n\t\tönce kasa\r\nnot satırı\r\n\t🚨grafik\r\n\t🚨Son kontrol\r\n",
            doc.Serialize());
    }

    [Fact]
    public void Add_TypedLinesInTitle_Done_MarksTypedSubTasksDone()
    {
        var doc = NoteDocument.Parse("");
        doc.AddEntry(Today, "Rapor\r\n- topla", null, new[] { "Grafik" }, true);
        Assert.Contains("👍[Done 12/11]Rapor\r\n\t👍[Done 12/11]topla\r\n\t👍[Done 12/11]Grafik", doc.Serialize());
    }

    [Fact]
    public void Resolve_MultiLineTitle_ReadsDayFromFirstLineOnly()
    {
        var r = new NewTaskDraft { Title = "yarın rapor\n- cuma topla" }.Resolve(Today, Today);
        Assert.Equal(new DateTime(2026, 11, 13), r.Date);
        Assert.Equal("rapor\n- cuma topla", r.Title.Replace("\r", ""));

        var none = new NewTaskDraft { Title = "rapor\nyarın" }.Resolve(Today, Today);
        Assert.Equal(Today, none.Date);
    }

    [Fact]
    public void Add_Done_MarksSubTasksDone()
    {
        var doc = NoteDocument.Parse("");
        doc.AddEntry(Today, "Rapor", null, new[] { "Topla" }, true);
        Assert.Contains("👍[Done 12/11]Rapor\r\n\t👍[Done 12/11]Topla", doc.Serialize());
    }
}
