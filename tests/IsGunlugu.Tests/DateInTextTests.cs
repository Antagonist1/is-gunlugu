using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DateInTextTests
{
    // A Thursday.
    private static readonly DateTime Today = new(2026, 11, 12);

    private static void Finds(string text, int year, int month, int day, string rest)
    {
        var found = DateInText.Find(text, Today);
        Assert.NotNull(found);
        Assert.Equal(new DateTime(year, month, day), found!.Date);
        Assert.Equal(rest, found.Text);
    }

    [Theory]
    [InlineData("yarın rapor", 13, "rapor")]
    [InlineData("rapor yarın", 13, "rapor")]
    [InlineData("Yarına Rapor gönder", 13, "Rapor gönder")]
    [InlineData("bugün rapor", 12, "rapor")]
    [InlineData("öbür gün rapor", 14, "rapor")]
    [InlineData("rapor yarından sonra", 14, "rapor")]
    public void RelativeDays(string text, int day, string rest) => Finds(text, 2026, 11, day, rest);

    [Theory]
    [InlineData("cuma toplantı", 13, "toplantı")]
    [InlineData("toplantı cuma", 13, "toplantı")]
    [InlineData("cuma günü sunum", 13, "sunum")]
    [InlineData("sunum cuma günü", 13, "sunum")]
    [InlineData("cumaya sunum", 13, "sunum")]
    [InlineData("toplantı pazartesiye", 16, "toplantı")]
    [InlineData("CUMARTESİ alışveriş", 14, "alışveriş")]
    [InlineData("Salı diş", 17, "diş")]
    [InlineData("çarşambaya teslim", 18, "teslim")]
    public void Weekdays_TheNextOne(string text, int day, string rest) => Finds(text, 2026, 11, day, rest);

    [Fact]
    public void TodaysOwnWeekday_MeansNextWeek() => Finds("perşembe toplantı", 2026, 11, 19, "toplantı");

    [Theory]
    [InlineData("haftaya rapor", 19, "rapor")]
    [InlineData("haftaya salı rapor", 17, "rapor")]
    [InlineData("haftaya cuma rapor", 20, "rapor")]
    [InlineData("rapor haftaya pazartesi", 16, "rapor")]
    public void NextWeek(string text, int day, string rest) => Finds(text, 2026, 11, day, rest);

    [Theory]
    [InlineData("15 kasım sunum", 2026, 11, 15, "sunum")]
    [InlineData("sunum 15 kasım'da", 2026, 11, 15, "sunum")]
    [InlineData("sunum 15 Kasımda", 2026, 11, 15, "sunum")]
    [InlineData("3 aralık teslim", 2026, 12, 3, "teslim")]
    [InlineData("15 kasım 2027 sunum", 2027, 11, 15, "sunum")]
    [InlineData("1 şubata rapor", 2027, 2, 1, "rapor")]
    public void DayAndMonth(string text, int year, int month, int day, string rest) => Finds(text, year, month, day, rest);

    [Fact]
    public void ADayAlreadyPastThisYear_MeansNextYear() => Finds("5 kasım sunum", 2027, 11, 5, "sunum");

    [Theory]
    [InlineData("15.11 sunum", 2026, 11, 15, "sunum")]
    [InlineData("sunum 15/11", 2026, 11, 15, "sunum")]
    [InlineData("15.11.2027 sunum", 2027, 11, 15, "sunum")]
    [InlineData("sunum 1.12.2026", 2026, 12, 1, "sunum")]
    [InlineData("10.11.2026 geçmişe not", 2026, 11, 10, "geçmişe not")]
    public void Numbers(string text, int year, int month, int day, string rest) => Finds(text, year, month, day, rest);

    [Theory]
    [InlineData("rapor gönder")]
    [InlineData("yarın")]
    [InlineData("cuma günü")]
    [InlineData("raporu yarın sabaha kadar bitir")]
    [InlineData("31 şubat sunum")]
    [InlineData("32.11 sunum")]
    [InlineData("3 rapor yaz")]
    [InlineData("cumartesiler boş")]
    [InlineData("")]
    public void NoDate_OrNothingLeft_FindsNothing(string text) => Assert.Null(DateInText.Find(text, Today));

    [Fact]
    public void OnlyTheFirstLineIsRead_TheRestStays()
    {
        Finds("yarın rapor\n- ilk alt iş\n- cuma ikinci", 2026, 11, 13, "rapor\n- ilk alt iş\n- cuma ikinci");
    }

    [Fact]
    public void TheWordsFoundAreGivenBack_ForTheHint()
    {
        Assert.Equal("haftaya salı", DateInText.Find("haftaya salı rapor", Today)!.Phrase);
    }

    [Theory]
    [InlineData(2026, 11, 12, "bugüne")]
    [InlineData(2026, 11, 13, "yarına")]
    [InlineData(2026, 11, 17, "17 Kasım Salı gününe")]
    [InlineData(2027, 1, 4, "4 Ocak 2027 Pazartesi gününe")]
    public void TheDayIsNamed_ForTheHintAndTheMessage(int year, int month, int day, string name) =>
        Assert.Equal(name, DateInText.DayName(new DateTime(year, month, day), Today));
}
