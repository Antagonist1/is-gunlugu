using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DayEndReminderTests
{
    private static readonly TimeSpan At = new(17, 30, 0);

    [Fact]
    public void BeforeTheTime_NotDue()
    {
        Assert.False(DayEndReminder.IsDue(new DateTime(2026, 11, 12, 17, 29, 0), At, null, 3));
    }

    [Fact]
    public void AtTheTime_WithOpenWork_Due()
    {
        Assert.True(DayEndReminder.IsDue(new DateTime(2026, 11, 12, 17, 30, 0), At, null, 3));
    }

    [Fact]
    public void LaterTheSameEvening_StillDue_WhenNotShownYet()
    {
        Assert.True(DayEndReminder.IsDue(new DateTime(2026, 11, 12, 22, 5, 0), At, new DateTime(2026, 11, 11), 1));
    }

    [Fact]
    public void AlreadyShownToday_NotDueAgain()
    {
        Assert.False(DayEndReminder.IsDue(new DateTime(2026, 11, 12, 19, 0, 0), At, new DateTime(2026, 11, 12), 3));
    }

    [Fact]
    public void NothingOpen_NotDue()
    {
        Assert.False(DayEndReminder.IsDue(new DateTime(2026, 11, 12, 18, 0, 0), At, null, 0));
    }

    [Theory]
    [InlineData(17, 30, 1, 17, 45)]
    [InlineData(17, 30, -1, 17, 15)]
    [InlineData(23, 45, 1, 23, 45)]
    [InlineData(12, 0, -1, 12, 0)]
    public void Step_QuarterHours_BetweenNoonAndMidnight(int h, int m, int steps, int eh, int em)
    {
        Assert.Equal(new TimeSpan(eh, em, 0), DayEndReminder.Step(new TimeSpan(h, m, 0), steps));
    }

    [Fact]
    public void Message_NamesUpToTwo_AndCountsTheRest()
    {
        Assert.Equal("Rapor, Toplantı ve 1 iş daha", DayEndReminder.Message(new[] { "Rapor", "Toplantı", "Sunum" }));
        Assert.Equal("Rapor ve Toplantı", DayEndReminder.Message(new[] { "Rapor", "Toplantı" }));
        Assert.Equal("Rapor", DayEndReminder.Message(new[] { "Rapor" }));
    }

    [Fact]
    public void Message_CutsLongTitles()
    {
        var text = DayEndReminder.Message(new[] { new string('a', 80) });
        Assert.True(text.Length <= 41);
        Assert.EndsWith("…", text);
    }
}
