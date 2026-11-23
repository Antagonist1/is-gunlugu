namespace IsGunlugu.Core;

/// <summary>Date sums behind moving around the calendar, and how the year page is laid out.</summary>
public static class CalendarNav
{
    private const int YearColumns = 3;
    private const int YearRows = 4;

    public static DateTime MondayOf(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>The week to show when a month is picked: this week in the current month, otherwise the month's first.</summary>
    public static DateTime WeekFor(DateTime month, DateTime today) =>
        month.Year == today.Year && month.Month == today.Month ? MondayOf(today) : MondayOf(new DateTime(month.Year, month.Month, 1));

    /// <summary>Which month, 1 to 12, a point on the year page falls on.</summary>
    public static int MonthAt(float x, float y, float width, float height)
    {
        var column = Math.Min(YearColumns - 1, Math.Max(0, (int)(x / (width / YearColumns))));
        var row = Math.Min(YearRows - 1, Math.Max(0, (int)(y / (height / YearRows))));
        return row * YearColumns + column + 1;
    }

    /// <summary>
    /// The week of the year as counted in Turkey (ISO 8601): weeks start on Monday and the first week is the one
    /// holding the year's first Thursday.
    /// </summary>
    public static int WeekNumber(DateTime date)
    {
        var thursday = MondayOf(date).AddDays(3);
        return (thursday.DayOfYear - 1) / 7 + 1;
    }

    /// <summary>The Monday of every week that has a day in the month, first to last.</summary>
    public static IList<DateTime> WeeksOfMonth(DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var weeks = new List<DateTime>();
        for (var monday = MondayOf(first); monday <= last; monday = monday.AddDays(7)) weeks.Add(monday);
        return weeks;
    }

    /// <summary>The nine years of the sidebar's three-by-three grid, the given one in the middle.</summary>
    public static IList<int> YearsAround(int year) => Enumerable.Range(year - 4, 9).ToList();

    /// <summary>Empty places before the 1st in a week that starts on Monday.</summary>
    public static int LeadingBlanks(int year, int month) => ((int)new DateTime(year, month, 1).DayOfWeek + 6) % 7;
}
