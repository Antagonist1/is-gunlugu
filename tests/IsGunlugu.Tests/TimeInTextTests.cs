using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class TimeInTextTests
{
    [Theory]
    [InlineData("toplantı 14:00", 14, 0, "toplantı")]
    [InlineData("9:30 Ahmet'i ara", 9, 30, "Ahmet'i ara")]
    [InlineData("müşteriyle 14:00 toplantı", 14, 0, "müşteriyle toplantı")]
    public void Finds(string text, int h, int m, string rest)
    {
        var found = TimeInText.Find(text)!;
        Assert.Equal(new TimeSpan(h, m, 0), found.At);
        Assert.Equal(rest, found.Text);
    }

    [Theory]
    [InlineData("rapor")]
    [InlineData("toplantı 24:00")]
    [InlineData("toplantı 14:60")]
    [InlineData("oran 1:2")]
    [InlineData("a14:00b")]
    [InlineData("14:00")]
    public void Ignores(string text) => Assert.Null(TimeInText.Find(text));

    [Fact]
    public void OnlyFirstLine()
    {
        var found = TimeInText.Find("toplantı 14:00\n  salon 3:15 değil")!;
        Assert.Equal("toplantı\n  salon 3:15 değil", found.Text);
    }
}
