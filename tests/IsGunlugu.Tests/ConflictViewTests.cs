using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class ConflictViewTests
{
    private static string Show(string local, string remote) => string.Join(" | ",
        ConflictView.Rows(local, remote, "Telefon", "Bilgisayar", "Yalnızca telefonda", "Yalnızca bilgisayarda")
            .Select(r => (r.Tag.Length == 0 ? "" : r.Tag + ": ") + r.Text));

    [Fact]
    public void ChangedLine_NamesBothSources_AndCountsTheSameLines() =>
        Assert.Equal("1 satır aynı | Telefon: b | Bilgisayar: x | 2 satır aynı", Show("a\nb\nc\nd", "a\nx\nc\nd"));

    [Fact]
    public void LineOnOneSide_SaysSo() =>
        Assert.Equal("1 satır aynı | Yalnızca telefonda: b", Show("a\nb", "a"));

    [Fact]
    public void LineOnTheOtherSide_SaysSo() =>
        Assert.Equal("Yalnızca bilgisayarda: z | 1 satır aynı", Show("a", "z\na"));

    [Fact]
    public void EveryChangedLineGetsItsOwnRow() =>
        Assert.Equal("Telefon: a | Telefon: b | Bilgisayar: x", Show("a\nb", "x"));

    [Fact]
    public void RowsSayWhichSideTheyAreFrom()
    {
        var rows = ConflictView.Rows("a\nb", "a\nx", "T", "B", "YT", "YB");

        Assert.True(rows[0].IsSame);
        Assert.True(rows[1].IsLocal);
        Assert.True(rows[2].IsRemote);
    }
}
