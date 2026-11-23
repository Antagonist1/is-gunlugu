using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class WindowFitTests
{
    [Fact]
    public void BigScreen_KeepsTheWishedSize_Centred()
    {
        var fit = WindowFit.Place(1180, 900, 820, 600, 0, 0, 1920, 1040);

        Assert.Equal(1180, fit.Width);
        Assert.Equal(900, fit.Height);
        Assert.Equal(370, fit.Left);
        Assert.Equal(70, fit.Top);
        Assert.Equal(600, fit.MinHeight);
    }

    [Fact]
    public void Screen768At125Percent_FillsTheWorkArea_TopVisible()
    {
        // 1366x768 at 125% is 1093x614, the taskbar takes about 32 of it.
        var fit = WindowFit.Place(1180, 900, 820, 600, 0, 0, 1093, 582);

        Assert.Equal(1093, fit.Width);
        Assert.Equal(582, fit.Height);
        Assert.Equal(0, fit.Left);
        Assert.Equal(0, fit.Top);
        Assert.Equal(582, fit.MinHeight);
    }

    [Fact]
    public void WorkAreaNotAtZero_StaysInsideIt()
    {
        // Taskbar on top or left: the work area starts further in.
        var fit = WindowFit.Place(1180, 900, 820, 600, 60, 40, 1300, 700);

        Assert.Equal(700, fit.Height);
        Assert.Equal(40, fit.Top);
        Assert.Equal(120, fit.Left);
    }

    [Fact]
    public void SecondMonitorOnTheRight_At125Percent_InDips()
    {
        // Pixels from Windows: a 1366x768 laptop right of a 1920 wide monitor, 40 px taskbar, 125% scale.
        var area = WindowFit.AreaInDips(1920, 0, 1366, 728, 1.25);

        Assert.Equal(1536, area[0]);
        Assert.Equal(0, area[1]);
        Assert.Equal(1092.8, area[2], 3);
        Assert.Equal(582.4, area[3], 3);
    }

    [Fact]
    public void SecondMonitor_WindowOpensInsideIt()
    {
        var area = WindowFit.AreaInDips(1920, 0, 1366, 728, 1.25);
        var fit = WindowFit.Place(1180, 900, 820, 600, area[0], area[1], area[2], area[3]);

        Assert.True(fit.Left >= 1536);
        Assert.True(fit.Left + fit.Width <= 1536 + 1092.8 + 0.001);
        Assert.Equal(0, fit.Top);
        Assert.Equal(582.4, fit.Height, 3);
    }

    [Fact]
    public void MonitorLeftOfPrimary_NegativeCoordinatesStay()
    {
        var area = WindowFit.AreaInDips(-1920, 0, 1920, 1040, 1);
        var fit = WindowFit.Place(1180, 900, 820, 600, area[0], area[1], area[2], area[3]);

        Assert.Equal(-1920 + 370, fit.Left);
    }

    [Fact]
    public void Kept_SavedPlaceInsideTheScreen_ComesBackAsItWas()
    {
        var fit = WindowFit.Keep(1000, 50, 900, 950, 820, 600, 0, 0, 1920, 1040);

        Assert.Equal(1000, fit.Left);
        Assert.Equal(50, fit.Top);
        Assert.Equal(900, fit.Width);
        Assert.Equal(950, fit.Height);
    }

    [Fact]
    public void Kept_HangingOverTheEdge_IsPulledBackIn()
    {
        var fit = WindowFit.Keep(1500, 700, 900, 600, 820, 600, 0, 0, 1920, 1040);

        Assert.Equal(1020, fit.Left);
        Assert.Equal(440, fit.Top);
    }

    [Fact]
    public void Kept_FromABiggerScreen_ShrinksToThisOne()
    {
        // Saved on a 1920x1040 monitor, opened on a 1366x728 laptop after the monitor was unplugged.
        var fit = WindowFit.Keep(2400, 100, 1600, 1000, 820, 600, 0, 0, 1366, 728);

        Assert.Equal(1366, fit.Width);
        Assert.Equal(728, fit.Height);
        Assert.Equal(0, fit.Left);
        Assert.Equal(0, fit.Top);
    }

    [Fact]
    public void Kept_TooSmall_GrowsToTheMinimum()
    {
        var fit = WindowFit.Keep(100, 100, 300, 200, 820, 600, 0, 0, 1920, 1040);

        Assert.Equal(820, fit.Width);
        Assert.Equal(600, fit.Height);
    }

    [Fact]
    public void TinyScreen_MinimumsShrinkToTheArea()
    {
        var fit = WindowFit.Place(1180, 900, 820, 600, 0, 0, 760, 500);

        Assert.Equal(760, fit.MinWidth);
        Assert.Equal(500, fit.MinHeight);
        Assert.Equal(760, fit.Width);
        Assert.Equal(500, fit.Height);
    }
}
