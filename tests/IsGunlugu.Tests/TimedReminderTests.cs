using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class TimedReminderTests
{
    private static readonly DateTime Day = new(2026, 11, 23);

    private static List<(DateTime, Entry)> Items(string text)
    {
        var doc = NoteDocument.Parse(text);
        return doc.Days.SelectMany(d => d.Entries.Select(e => (d.Date, e))).ToList();
    }

    private static IList<TimedReminder.Ring> Due(string text, DateTime now, ISet<string>? rung = null) =>
        TimedReminder.Due(Items(text), now, rung ?? new HashSet<string>(), "not.txt");

    private const string Meeting = "----23/11/2026\r\n🚨Toplantı ⏰[14:00 30dk 15dk]\r\n";

    [Fact]
    public void RingsAtItsTime_NotBefore()
    {
        Assert.Empty(Due(Meeting, Day.AddHours(13).AddMinutes(29)));
        var ring = Assert.Single(Due(Meeting, Day.AddHours(13).AddMinutes(30)));
        Assert.Equal(TimeSpan.FromMinutes(30), ring.Before);
        Assert.Equal(Day.AddHours(14), ring.TaskAt);
    }

    [Fact]
    public void RungKey_DoesNotRingAgain_NextOneDoes()
    {
        var first = Assert.Single(Due(Meeting, Day.AddHours(13).AddMinutes(30)));
        var rung = new HashSet<string>(first.Keys);
        Assert.Empty(Due(Meeting, Day.AddHours(13).AddMinutes(40), rung));
        Assert.Equal(TimeSpan.FromMinutes(15), Assert.Single(Due(Meeting, Day.AddHours(13).AddMinutes(45), rung)).Before);
    }

    [Fact]
    public void MissedBothRingsLastOnly()
    {
        var ring = Assert.Single(Due(Meeting, Day.AddHours(13).AddMinutes(50)));
        Assert.Equal(TimeSpan.FromMinutes(15), ring.Before);
        // Both are closed, so the earlier one does not come after it.
        Assert.Equal(2, ring.Keys.Count);
    }

    [Fact]
    public void TenMinutesAfterTheTime_Silent()
    {
        Assert.Single(Due("----23/11/2026\r\n🚨Ara ⏰[14:00 0dk]\r\n", Day.AddHours(14).AddMinutes(9)));
        Assert.Empty(Due(Meeting, Day.AddHours(14).AddMinutes(10)));
    }

    [Fact]
    public void DoneTaskDoesNotRing()
    {
        Assert.Empty(Due("----23/11/2026\r\n👍[Done 23/11]Toplantı ⏰[14:00 30dk]\r\n", Day.AddHours(13).AddMinutes(30)));
        Assert.Empty(Due("----23/11/2026\r\n🚫[İptal]Toplantı ⏰[14:00 30dk]\r\n", Day.AddHours(13).AddMinutes(30)));
    }

    [Fact]
    public void UndatedDoesNotRing()
    {
        Assert.Empty(Due("----Tarihsiz\r\n🚨Toplantı ⏰[14:00 30dk]\r\n", Day.AddHours(13).AddMinutes(30)));
    }

    [Fact]
    public void CrossesMidnight()
    {
        var ring = Assert.Single(Due("----24/11/2026\r\n🚨Uçak ⏰[00:30 1sa]\r\n", Day.AddHours(23).AddMinutes(30)));
        Assert.Equal(new DateTime(2026, 11, 24, 0, 30, 0), ring.TaskAt);
    }

    [Fact]
    public void NoReminders_NoRing()
    {
        Assert.Empty(Due("----23/11/2026\r\n🚨Uçak ⏰[14:00]\r\n", Day.AddHours(14)));
    }
}
