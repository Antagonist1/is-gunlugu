using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class ReorderTests
{
    [Fact]
    public void Moved_Down()
    {
        Assert.Equal("a", Reorder.Moved(new[] { "a", "b", "c", "d" }, new[] { "b", "c", "a", "d" }));
    }

    [Fact]
    public void Moved_Up()
    {
        Assert.Equal("d", Reorder.Moved(new[] { "a", "b", "c", "d" }, new[] { "a", "d", "b", "c" }));
    }

    [Fact]
    public void Moved_NextDoor()
    {
        // A swap of neighbours reads either way and leaves the same order.
        Assert.Contains(Reorder.Moved(new[] { "a", "b", "c" }, new[] { "a", "c", "b" }), new[] { "b", "c" });
    }

    [Fact]
    public void Moved_NothingChanged_IsNull()
    {
        Assert.Null(Reorder.Moved(new[] { "a", "b" }, new[] { "a", "b" }));
    }

    [Fact]
    public void Before_TheNextLineShown()
    {
        Assert.Equal("a", Reorder.Before(new[] { "a", "b", "c" }, new[] { "b", "a", "c" }, "b"));
    }

    [Fact]
    public void Before_LastShown_GoesRightAfterThePreviousOne_EvenBeforeHiddenWork()
    {
        // x is finished work the list does not show; b belongs after c, so before x.
        Assert.Equal("x", Reorder.Before(new[] { "a", "b", "c", "x" }, new[] { "a", "c", "b" }, "b"));
    }

    [Fact]
    public void Before_LastShown_AndNothingAfter_IsTheEndOfTheDay()
    {
        Assert.Null(Reorder.Before(new[] { "a", "b" }, new[] { "b", "a" }.Reverse().ToArray(), "b"));
    }

    [Fact]
    public void Before_AloneInAnotherDay_IsTheEndOfThatDay()
    {
        Assert.Null(Reorder.Before(new[] { "y" }, new[] { "b" }, "b"));
    }
}
