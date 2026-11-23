using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DayWorkTests
{
    private static readonly DateTime Today = new(2026, 11, 14);
    private static readonly DateTime Nov12 = new(2026, 11, 12);
    private static readonly DateTime Nov13 = new(2026, 11, 13);

    private const string Moved =
        "----13/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n\t👍[Done 12/11]Grafikler\r\n\t👍[Done 13/11]Prova\r\n\t🚨Son kontrol\r\n";

    [Fact]
    public void Elsewhere_ShowsSubTasksDoneThatDay()
    {
        var work = DayWork.Elsewhere(NoteDocument.Parse(Moved), Nov12, Today);

        var one = Assert.Single(work);
        Assert.Equal("Sunum", one.Entry.Text);
        Assert.Equal(Nov13, one.Home);
        Assert.False(one.EntryDoneHere);
        Assert.Equal(new[] { "Slaytlar", "Grafikler" }, one.DoneHere.Select(t => t.Text));
    }

    [Fact]
    public void ProgressedByDay_CountsAnOpenTaskOncePerDayItMovedOn()
    {
        var doc = NoteDocument.Parse(Moved + "🚨Hiç başlanmadı\r\n\t🚨a\r\n👍[Done 13/11]Bitti\r\n\t👍[Done 13/11]b\r\n");

        var map = DayWork.ProgressedByDay(doc, Today);

        Assert.Equal(new[] { Nov12, Nov13 }, map.Keys.OrderBy(d => d));
        Assert.Equal("Sunum", Assert.Single(map[Nov12]).Text);
        Assert.Equal("Sunum", Assert.Single(map[Nov13]).Text);
    }

    [Fact]
    public void Elsewhere_EmptyOnHomeDay()
    {
        Assert.Empty(DayWork.Elsewhere(NoteDocument.Parse(Moved), Nov13, Today));
    }

    [Fact]
    public void Elsewhere_TaskFinishedLaterThanItsDay()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n👍[Done 14/11]Rapor\r\n");

        var one = Assert.Single(DayWork.Elsewhere(doc, Today, Today));
        Assert.True(one.EntryDoneHere);
        Assert.Equal(Nov12, one.Home);
    }

    [Fact]
    public void Worked_CountsTaskOnce()
    {
        var doc = NoteDocument.Parse(Moved);

        Assert.Equal(1, DayWork.Worked(doc, Nov12, Today));
        Assert.Equal(1, DayWork.Worked(doc, Nov13, Today));
        Assert.Equal(0, DayWork.Worked(doc, Today, Today));
    }

    [Fact]
    public void Worked_OldUnstampedCountsOnHomeDay()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n👍[Done]Eski\r\n🚨Yarım\r\n\t👍[Done]Bir\r\n\t🚨İki\r\n🚨Açık\r\n");

        Assert.Equal(2, DayWork.Worked(doc, Nov12, Today));
    }

    [Fact]
    public void WeekSummary_SplitsDays()
    {
        var text = WeekSummary.Build(NoteDocument.Parse(Moved), new DateTime(2026, 11, 9));

        Assert.Equal(
            "9 – 15 Kasım 2026 · biten işler\r\n\r\n" +
            "Perşembe, 12 Kasım\r\n- Sunum (2/4)\r\n  - Slaytlar\r\n  - Grafikler\r\n\r\n" +
            "Cuma, 13 Kasım\r\n- Sunum (3/4)\r\n  - Prova\r\n",
            text);
    }
}
