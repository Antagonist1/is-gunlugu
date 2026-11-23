using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class TodayWorkTests
{
    private static readonly DateTime Today = new(2026, 11, 10);

    private const string File =
        "----09/11/2026\r\n" +
        "🚨dün kalan\r\n" +
        "----10/11/2026\r\n" +
        "👍[Done]bitti\r\n" +
        "🚨birinci\r\n" +
        "düz not\r\n" +
        "🚨yarım\r\n" +
        "\t👍[Done]a\r\n" +
        "\t🚨b\r\n" +
        "\t🚨c\r\n" +
        "🚨üçüncü\r\n" +
        "----11/11/2026\r\n" +
        "🚨yarın\r\n";

    private static IList<WorkLine> Lines() => TodayWork.Open(NoteDocument.Parse(File), Today);

    [Fact]
    public void Open_TodaysUnfinishedWork_InFileOrder()
    {
        Assert.Equal(new[] { "birinci", "yarım", "üçüncü" }, Lines().Select(l => l.Text));
    }

    [Fact]
    public void Open_MarksPartlyDoneWorkWithItsProgress()
    {
        var partly = Lines().Single(l => l.Text == "yarım");

        Assert.True(partly.IsPartlyDone);
        Assert.Equal("1/3", partly.Progress);
        Assert.False(Lines().First().IsPartlyDone);
        Assert.Equal("", Lines().First().Progress);
    }

    [Fact]
    public void Open_NothingToday_IsEmpty()
    {
        Assert.Empty(TodayWork.Open(NoteDocument.Parse(File), new DateTime(2026, 11, 12)));
    }

    [Fact]
    public void Find_TheLineThatWasTicked()
    {
        var doc = NoteDocument.Parse(File);
        var line = TodayWork.Open(doc, Today)[1];

        Assert.Equal("yarım", TodayWork.Find(doc, Today, line.Index, line.Text)!.Text);
    }

    [Fact]
    public void Find_AfterTheDayChangedAboveIt_GoesByText()
    {
        var doc = NoteDocument.Parse(File);
        var line = TodayWork.Open(doc, Today)[2];
        doc.AddEntry(Today, "araya giren", false);
        doc.MoveEntry(doc.Days.First(d => d.Date == Today).Entries.Last(), Today, doc.Days.First(d => d.Date == Today).Entries[0]);

        Assert.Equal("üçüncü", TodayWork.Find(doc, Today, line.Index, line.Text)!.Text);
    }

    [Fact]
    public void Find_GoneOrFinished_IsNull()
    {
        var doc = NoteDocument.Parse(File);

        Assert.Null(TodayWork.Find(doc, Today, 99, "yok böyle bir iş"));
        Assert.Null(TodayWork.Find(doc, Today, 0, "bitti"));
    }

    [Theory]
    [InlineData(250, 2, 2, 0)]
    [InlineData(250, 4, 4, 0)]
    [InlineData(250, 5, 3, 2)]
    [InlineData(180, 9, 2, 7)]
    [InlineData(250, 0, 0, 0)]
    [InlineData(2000, 20, 12, 8)]
    [InlineData(2000, 12, 12, 0)]
    public void Fit_ShowsWhatFitsAndCountsTheRest(double heightDp, int count, int shown, int more)
    {
        var fit = TodayWork.Fit(heightDp, count);

        Assert.Equal(shown, fit.Shown);
        Assert.Equal(more, fit.More);
    }
}
