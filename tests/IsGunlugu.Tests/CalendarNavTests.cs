using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class CalendarNavTests
{
    [Theory]
    [InlineData(2026, 11, 11, 2026, 11, 9)]  // Wednesday
    [InlineData(2026, 11, 9, 2026, 11, 9)]   // Monday stays
    [InlineData(2026, 11, 8, 2026, 11, 2)]   // Sunday belongs to the week before
    public void MondayOf(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(new DateTime(ey, em, ed), CalendarNav.MondayOf(new DateTime(y, m, d)));
    }

    [Fact]
    public void WeekFor_ThisMonth_IsThisWeek()
    {
        Assert.Equal(new DateTime(2026, 11, 9), CalendarNav.WeekFor(new DateTime(2026, 11, 1), new DateTime(2026, 11, 11)));
    }

    [Fact]
    public void WeekFor_AnotherMonth_IsTheWeekOfItsFirstDay()
    {
        Assert.Equal(new DateTime(2025, 5, 26), CalendarNav.WeekFor(new DateTime(2025, 6, 1), new DateTime(2026, 11, 11)));
    }

    [Theory]
    [InlineData(10, 10, 1)]
    [InlineData(150, 10, 2)]
    [InlineData(290, 10, 3)]
    [InlineData(10, 110, 4)]
    [InlineData(299, 399, 12)]
    public void MonthAt_ThreeColumnsFourRows(float x, float y, int month)
    {
        Assert.Equal(month, CalendarNav.MonthAt(x, y, 300, 400));
    }

    [Theory]
    [InlineData(-5, -5, 1)]
    [InlineData(900, 900, 12)]
    public void MonthAt_OutsideTheArea_StaysInTheYear(float x, float y, int month)
    {
        Assert.Equal(month, CalendarNav.MonthAt(x, y, 300, 400));
    }

    [Theory]
    [InlineData(2026, 11, 6)] // starts on a Sunday
    [InlineData(2025, 9, 0)]  // starts on a Monday
    public void LeadingBlanks_BeforeTheFirstDay(int year, int month, int blanks)
    {
        Assert.Equal(blanks, CalendarNav.LeadingBlanks(year, month));
    }

    [Theory]
    [InlineData(2026, 11, 13, 46)]
    [InlineData(2026, 10, 26, 44)]
    [InlineData(2025, 12, 29, 1)]  // Monday of the week holding 1 January 2026, a Thursday
    [InlineData(2027, 1, 1, 53)]   // a Friday: still the last week of 2026
    [InlineData(2021, 1, 3, 53)]   // a Sunday: the last week of 2020
    [InlineData(2024, 12, 30, 1)]
    public void WeekNumber_IsTheIsoWeekUsedInTurkey(int y, int m, int d, int expected)
    {
        Assert.Equal(expected, CalendarNav.WeekNumber(new DateTime(y, m, d)));
    }

    [Fact]
    public void WeeksOfMonth_AreTheMondaysOfEveryWeekTouchingIt()
    {
        var weeks = CalendarNav.WeeksOfMonth(new DateTime(2026, 11, 1));
        Assert.Equal(new[] { 26, 2, 9, 16, 23, 30 }, weeks.Select(w => w.Day));
        Assert.Equal(new DateTime(2026, 10, 26), weeks[0]);
    }

    [Fact]
    public void WeeksOfMonth_AMonthStartingOnMonday_HasNoWeekBefore()
    {
        var weeks = CalendarNav.WeeksOfMonth(new DateTime(2027, 2, 1));
        Assert.Equal(new[] { 1, 8, 15, 22 }, weeks.Select(w => w.Day));
    }

    [Fact]
    public void YearsAround_AreNineWithTheGivenYearInTheMiddle()
    {
        Assert.Equal(Enumerable.Range(2022, 9), CalendarNav.YearsAround(2026));
    }
}
