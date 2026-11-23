using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class TaskTimeTests
{
    private static Entry First(NoteDocument doc) => doc.Days[0].Entries[0];

    [Fact]
    public void Reads_TimeAndReminders_KeepsTitleClean()
    {
        var doc = NoteDocument.Parse("----23/11/2026\r\n🚨Müşteriyle toplantı ⏰[14:00 30dk 15dk]\r\n");
        var entry = First(doc);

        Assert.Equal("Müşteriyle toplantı", entry.Text);
        Assert.Equal(new TimeSpan(14, 0, 0), entry.Time!.At);
        Assert.Equal(new[] { TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15) }, entry.Time.Before);
    }

    [Fact]
    public void Reads_AnyOrder_EarliestFirst_DropsExtraAndTooLong()
    {
        var time = TaskTime.Read("⏰[9:05 15dk 1sa30dk 5dk]")!;
        Assert.Equal(new TimeSpan(9, 5, 0), time.At);
        Assert.Equal(new[] { TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(15) }, time.Before);

        Assert.Empty(TaskTime.Read("⏰[08:15 25sa]")!.Before);
        Assert.Equal(new[] { TimeSpan.Zero }, TaskTime.Read("⏰[08:15 0dk]")!.Before);
    }

    [Fact]
    public void BrokenMark_StaysInTitle()
    {
        var doc = NoteDocument.Parse("----23/11/2026\r\n🚨Uçak ⏰[25:00]\r\n🚨Ara ⏰[abc]\r\n");

        Assert.Equal("Uçak ⏰[25:00]", doc.Days[0].Entries[0].Text);
        Assert.Null(doc.Days[0].Entries[0].Time);
        Assert.Equal("Ara ⏰[abc]", doc.Days[0].Entries[1].Text);
    }

    [Fact]
    public void MarkInMiddle_IsLeftAsText()
    {
        var doc = NoteDocument.Parse("----23/11/2026\r\n🚨Toplantı ⏰[14:00] salonda\r\n");
        Assert.Null(First(doc).Time);
        Assert.Equal("Toplantı ⏰[14:00] salonda", First(doc).Text);
    }

    [Fact]
    public void RoundTrip_SameText()
    {
        const string text = "----23/11/2026\r\n🚨Müşteriyle toplantı ⏰[14:00 30dk 15dk]\r\n\t🚨Sunum ⏰[10:00]\r\n";
        var doc = NoteDocument.Parse(text);

        Assert.Equal(text, doc.Serialize());
        // A sub-task keeps its text as it is; times are for tasks only.
        Assert.Equal("Sunum ⏰[10:00]", First(doc).SubTasks[0].Text);
    }

    [Fact]
    public void EditKeepsAlarm()
    {
        var doc = NoteDocument.Parse("----23/11/2026\r\n🚨Toplantı ⏰[14:00 30dk]\r\n");

        NoteDocument.SetText(First(doc), "Müşteriyle toplantı");

        Assert.Equal("----23/11/2026\r\n🚨Müşteriyle toplantı ⏰[14:00 30dk]\r\n", doc.Serialize());
    }

    [Fact]
    public void TickKeepsAlarm()
    {
        var doc = NoteDocument.Parse("----23/11/2026\r\n🚨Toplantı ⏰[14:00 30dk]\r\n");

        NoteDocument.SetDone(First(doc), true, new DateTime(2026, 11, 23));

        Assert.Equal("----23/11/2026\r\n👍[Done 23/11]Toplantı ⏰[14:00 30dk]\r\n", doc.Serialize());
    }

    [Fact]
    public void SetTime_WritesEarliestFirst_NullRemoves()
    {
        var doc = NoteDocument.Parse("----23/11/2026\r\n🚨Toplantı\r\n");
        var entry = First(doc);

        NoteDocument.SetTime(entry, new TaskTime(new TimeSpan(14, 0, 0), new[] { TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(90) }));
        Assert.Equal("----23/11/2026\r\n🚨Toplantı ⏰[14:00 1sa30dk 15dk]\r\n", doc.Serialize());

        NoteDocument.SetTime(entry, null);
        Assert.Equal("----23/11/2026\r\n🚨Toplantı\r\n", doc.Serialize());
    }

    [Theory]
    [InlineData(0, "Tam saatinde")]
    [InlineData(30, "30 dk önce")]
    [InlineData(60, "1 sa önce")]
    [InlineData(90, "1 sa 30 dk önce")]
    [InlineData(1440, "1 gün önce")]
    public void SpanText(int minutes, string expected) => Assert.Equal(expected, TaskTime.SpanText(TimeSpan.FromMinutes(minutes)));
}
