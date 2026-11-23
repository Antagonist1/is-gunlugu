using System.Globalization;
using System.Text.RegularExpressions;

namespace IsGunlugu.Core;

/// <summary>
/// Reads a day out of what is typed for a new task: "yarın rapor", "toplantı cuma", "haftaya salı sunum",
/// "15 kasım teslim", "teslim 15.11". The words must open or close the first line; a day named in the middle of a
/// sentence is part of the sentence. What is left becomes the task's text.
/// </summary>
public static class DateInText
{
    public sealed class Found(DateTime date, string text, string phrase)
    {
        public DateTime Date { get; } = date;

        /// <summary>The task without the date words.</summary>
        public string Text { get; } = text;

        /// <summary>The date words as they were typed.</summary>
        public string Phrase { get; } = phrase;
    }

    private const int LongestPhrase = 3;
    private static readonly CultureInfo Turkish = new("tr-TR");
    private static readonly string[] Weekdays = { "pazartesi", "salı", "çarşamba", "perşembe", "cuma", "cumartesi", "pazar" };
    private static readonly string[] Months = { "ocak", "şubat", "mart", "nisan", "mayıs", "haziran", "temmuz", "ağustos", "eylül", "ekim", "kasım", "aralık" };
    private static readonly string[] Endings = { "ya", "ye", "da", "de", "ta", "te", "a", "e" };
    private static readonly Regex Numeric = new(@"^(\d{1,2})[./](\d{1,2})(?:[./](\d{4}))?$");

    public static Found? Find(string text, DateTime today)
    {
        var lineEnd = text.IndexOf('\n');
        var first = (lineEnd < 0 ? text : text.Substring(0, lineEnd)).TrimEnd('\r');
        var rest = lineEnd < 0 ? "" : text.Substring(lineEnd);
        var words = first.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        // A line that is a date and nothing else has no task in it: "cuma günü" is not the task "günü" on Friday.
        if (words.Length <= LongestPhrase && words.Length > 0 && Take(words, 0, words.Length, today) != null) return null;

        for (var length = Math.Min(LongestPhrase, words.Length - 1); length >= 1; length--)
        {
            var opening = Take(words, 0, length, today);
            if (opening != null) return new Found(opening.Value, Join(words, length, words.Length - length) + rest, Join(words, 0, length));

            var closing = Take(words, words.Length - length, length, today);
            if (closing != null) return new Found(closing.Value, Join(words, 0, words.Length - length) + rest, Join(words, words.Length - length, length));
        }
        return null;
    }

    /// <summary>Where a task goes, in a sentence: "bugüne", "yarına", "17 Kasım Salı gününe".</summary>
    public static string DayName(DateTime date, DateTime today)
    {
        date = date.Date;
        today = today.Date;
        if (date == today) return "bugüne";
        if (date == today.AddDays(1)) return "yarına";
        return date.ToString(date.Year == today.Year ? "d MMMM dddd" : "d MMMM yyyy dddd", Turkish) + " gününe";
    }

    private static string Join(string[] words, int start, int count) => string.Join(" ", words, start, count);

    /// <summary>The day these words name when they are a date phrase as a whole, else null.</summary>
    private static DateTime? Take(string[] words, int start, int count, DateTime today)
    {
        var w = words.Skip(start).Take(count).Select(Plain).ToArray();
        today = today.Date;

        if (count == 1)
        {
            if (Is(w[0], "bugün")) return today;
            if (Is(w[0], "yarın")) return today.AddDays(1);
            if (w[0] == "haftaya") return today.AddDays(7);
            var weekday = Index(Weekdays, w[0]);
            if (weekday >= 0) return NextWeekday(today, weekday);
            return NumericDate(w[0], today);
        }

        if (count == 2)
        {
            if (w[0] == "öbür" && Is(w[1], "gün")) return today.AddDays(2);
            if (w[0] == "yarından" && Is(w[1], "sonra")) return today.AddDays(2);
            if (w[0] == "haftaya" && Index(Weekdays, w[1]) is var inWeek && inWeek >= 0) return CalendarNav.MondayOf(today).AddDays(7 + inWeek);
            if (w[1] is "günü" or "gününe" && Index(Weekdays, w[0], exact: true) is var named && named >= 0) return NextWeekday(today, named);
            return DayMonth(w[0], w[1], null, today);
        }

        return DayMonth(w[0], w[1], w[2], today);
    }

    /// <summary>Lower case the Turkish way, without what follows an apostrophe: "Kasım'da" is "kasım".</summary>
    private static string Plain(string word)
    {
        var lower = word.ToLower(Turkish);
        var apostrophe = lower.IndexOfAny(new[] { '\'', '’' });
        return apostrophe > 0 ? lower.Substring(0, apostrophe) : lower;
    }

    private static bool Is(string word, string name) => word == name || Endings.Any(e => word == name + e);

    private static int Index(string[] names, string word, bool exact = false)
    {
        for (var i = 0; i < names.Length; i++)
        {
            if (exact ? word == names[i] : Is(word, names[i])) return i;
        }
        return -1;
    }

    /// <summary>The coming day with that name; today's own name means next week, since "bugün" says today.</summary>
    private static DateTime NextWeekday(DateTime today, int mondayFirst)
    {
        var ahead = (mondayFirst - (((int)today.DayOfWeek + 6) % 7) + 7) % 7;
        return today.AddDays(ahead == 0 ? 7 : ahead);
    }

    private static DateTime? DayMonth(string day, string month, string? year, DateTime today)
    {
        var monthIndex = Index(Months, month);
        if (year == null && monthIndex < 0) return null;
        if (year != null && Index(Months, month, exact: true) < 0) return null;
        if (!int.TryParse(day, NumberStyles.None, CultureInfo.InvariantCulture, out var d)) return null;

        if (year == null) return ThisYearOrNext(d, monthIndex + 1, today);
        return year.Length == 4 && int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out var y) ? Valid(y, monthIndex + 1, d) : null;
    }

    private static DateTime? NumericDate(string word, DateTime today)
    {
        var match = Numeric.Match(word);
        if (!match.Success) return null;

        var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return match.Groups[3].Success
            ? Valid(int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), month, day)
            : ThisYearOrNext(day, month, today);
    }

    /// <summary>Without a year, a day that already passed this year is meant for next year.</summary>
    private static DateTime? ThisYearOrNext(int day, int month, DateTime today)
    {
        var date = Valid(today.Year, month, day);
        return date < today ? Valid(today.Year + 1, month, day) : date;
    }

    private static DateTime? Valid(int year, int month, int day) =>
        year >= 1 && year <= 9998 && month >= 1 && month <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateTime(year, month, day)
            : null;
}
