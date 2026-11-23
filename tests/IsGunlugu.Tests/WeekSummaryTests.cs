using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class WeekSummaryTests
{
    private const string Week =
        "----09/11/2026\r\n👍[Done]Rapor gönder\r\nCuma için\r\n\t👍[Done]Rakamları topla\r\n🚨Açık kalan iş\r\n" +
        "----10/11/2026\r\n🚨Sunum hazırla\r\n\t👍[Done]Slaytlar\r\n\t🚨Grafikler\r\n\t🚨Prova\r\n" +
        "----11/11/2026\r\n🚨Hiç başlanmamış\r\n" +
        "----16/11/2026\r\n👍[Done]Sonraki haftanın işi\r\n";

    private static string Summary(string text) =>
        WeekSummary.Build(NoteDocument.Parse(text), new DateTime(2026, 11, 9));

    [Fact]
    public void FinishedTasks_WithTheirFinishedSubTasks_DayByDay()
    {
        Assert.Equal(
            "9 – 15 Kasım 2026 · biten işler\r\n\r\n" +
            "Pazartesi, 9 Kasım\r\n- Rapor gönder\r\n  - Rakamları topla\r\n\r\n" +
            "Salı, 10 Kasım\r\n- Sunum hazırla (1/3)\r\n  - Slaytlar\r\n",
            Summary(Week));
    }

    [Fact]
    public void OpenWork_Descriptions_AndDaysWithNothingDone_AreLeftOut()
    {
        var text = Summary(Week);

        Assert.DoesNotContain("Açık kalan iş", text);
        Assert.DoesNotContain("Cuma için", text);
        Assert.DoesNotContain("Çarşamba", text);
        Assert.DoesNotContain("Grafikler", text);
    }

    [Fact]
    public void OtherWeeks_StayOut()
    {
        Assert.DoesNotContain("Sonraki haftanın işi", Summary(Week));
    }

    [Fact]
    public void NothingDone_IsEmpty()
    {
        Assert.Equal("", Summary("----09/11/2026\r\n🚨Açık\r\n"));
    }

    [Fact]
    public void WeekAcrossTwoMonths_NamesBoth()
    {
        var text = WeekSummary.Build(NoteDocument.Parse("----02/12/2026\r\n👍[Done]İş\r\n"), new DateTime(2026, 11, 30));

        Assert.StartsWith("30 Kasım – 6 Aralık 2026 · biten işler", text);
    }

    [Fact]
    public void Count_IsTheTasksWithSomethingDone()
    {
        Assert.Equal(2, WeekSummary.Count(NoteDocument.Parse(Week), new DateTime(2026, 11, 9)));
    }
}
