using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class LineDiffTests
{
    private static string Show(IList<DiffBlock> blocks) =>
        string.Join(" ", blocks.Select(b => b.Side.ToString()[0] + "[" + string.Join(",", b.Lines) + "]"));

    [Fact]
    public void Identical_IsOneSameBlock() =>
        Assert.Equal("S[a,b]", Show(LineDiff.Compare("a\nb", "a\nb")));

    [Fact]
    public void ChangedLine_GivesLocalThenRemote() =>
        Assert.Equal("S[a] L[b] R[x] S[c]", Show(LineDiff.Compare("a\nb\nc", "a\nx\nc")));

    [Fact]
    public void OnlyLocal_HasNoRemoteBlock() =>
        Assert.Equal("S[a] L[b]", Show(LineDiff.Compare("a\nb", "a")));

    [Fact]
    public void OnlyRemote_HasNoLocalBlock() =>
        Assert.Equal("R[z] S[a]", Show(LineDiff.Compare("a", "z\na")));

    [Fact]
    public void LineEndingsAreNotADifference() =>
        Assert.Equal("S[a,b]", Show(LineDiff.Compare("a\r\nb", "a\nb")));

    [Fact]
    public void TrailingNewlineIsNotADifference() =>
        Assert.Equal("S[a]", Show(LineDiff.Compare("a\n", "a")));

    [Fact]
    public void EmptyAgainstText_IsOnlyRemote() =>
        Assert.Equal("R[a]", Show(LineDiff.Compare("", "a")));
}
