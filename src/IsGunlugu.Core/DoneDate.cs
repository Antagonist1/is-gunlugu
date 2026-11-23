using System.Globalization;
using System.Text.RegularExpressions;

namespace IsGunlugu.Core;

/// <summary>
/// The day a task or a sub-task was finished, kept in its mark: "👍[Done 12/11]". A task worked on over several days
/// then shows on each of them, wherever it sits now. The year is left out and read from the day the task sits under.
/// </summary>
public static class DoneDate
{
    private static readonly Regex InMarker = new(@"(\d{1,2})/(\d{1,2})(?:/(\d{4}))?");

    public static string Marker(DateTime on) => "👍[Done " + on.ToString("dd/MM", CultureInfo.InvariantCulture) + "]";

    /// <summary>A chosen finish day: the year is written only when, left out, it would be read as another year.</summary>
    public static string Marker(DateTime on, DateTime dayDate, DateTime today)
    {
        var mark = Marker(on);
        return Read(mark, dayDate, today) == on.Date
            ? mark
            : "👍[Done " + on.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + "]";
    }

    /// <summary>The finish day written in a mark, or null for a mark without one (as in notes written before).</summary>
    public static DateTime? Read(string marker, DateTime dayDate, DateTime today)
    {
        if (!marker.Contains("👍")) return null;

        var match = InMarker.Match(marker);
        if (!match.Success) return null;

        var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (month < 1 || month > 12) return null;

        if (match.Groups[3].Success) return Valid(int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), month, day);

        // Without a year: the one that puts the day nearest to where the task sits, so December and January meet.
        var around = dayDate == Day.Undated ? today.Date : dayDate.Date;
        return new[] { around.Year - 1, around.Year, around.Year + 1 }
            .Select(year => Valid(year, month, day))
            .Where(date => date.HasValue)
            .OrderBy(date => Math.Abs((date!.Value - around).TotalDays))
            .FirstOrDefault();
    }

    /// <summary>When something finished was finished: its own date, else the day it sits under (today for the undated).</summary>
    internal static DateTime On(string marker, DateTime dayDate, DateTime today) =>
        Read(marker, dayDate, today) ?? (dayDate == Day.Undated ? today.Date : dayDate.Date);

    private static DateTime? Valid(int year, int month, int day) =>
        year >= 1 && year <= 9998 && day >= 1 && day <= DateTime.DaysInMonth(year, month) ? new DateTime(year, month, day) : null;
}
