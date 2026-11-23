using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DoneDateTests
{
    private static readonly DateTime Today = new(2026, 11, 13);

    private static Entry First(NoteDocument doc) => doc.Days[0].Entries[0];

    [Fact]
    public void Tick_WritesToday()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\n\t🚨Slaytlar\r\n\t🚨Prova\r\n");

        NoteDocument.SetSubTaskDone(First(doc).SubTasks[0], true, Today);

        // Finished, it goes under what is left.
        Assert.Equal("----12/11/2026\r\n🚨Sunum\r\n\t🚨Prova\r\n\t👍[Done 13/11]Slaytlar\r\n", doc.Serialize());
    }

    [Fact]
    public void Untick_RemovesDate()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n\t🚨Prova\r\n");

        NoteDocument.SetSubTaskDone(First(doc).SubTasks[0], false, Today);

        // Open again, it goes under the open ones.
        Assert.Equal("----12/11/2026\r\n🚨Sunum\r\n\t🚨Prova\r\n\t🚨Slaytlar\r\n", doc.Serialize());
    }

    [Fact]
    public void LastSubTask_FinishesTaskWithDate()
    {
        var doc = NoteDocument.Parse("----13/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n\t🚨Prova\r\n");
        NoteDocument.SetSubTaskDone(First(doc).SubTasks[1], true, new DateTime(2026, 11, 14));
        Assert.Equal("----13/11/2026\r\n👍[Done 14/11]Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n\t👍[Done 14/11]Prova\r\n", doc.Serialize());

        var dropped = NoteDocument.Parse("----13/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]Slaytlar\r\n\t🚨Prova\r\n");
        NoteDocument.SetSubTaskDropped(First(dropped).SubTasks[1], true, Today);
        Assert.StartsWith("----13/11/2026\r\n👍[Done 13/11]Sunum\r\n", dropped.Serialize());
    }

    [Fact]
    public void TickTask_StampsOnlyOpenSubTasks()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\n\t👍[Done 12/11]A\r\n\t🚨B\r\n");

        NoteDocument.SetDone(First(doc), true, Today);

        Assert.Equal("----12/11/2026\r\n👍[Done 13/11]Sunum\r\n\t👍[Done 12/11]A\r\n\t👍[Done 13/11]B\r\n", doc.Serialize());
    }

    [Fact]
    public void Read_YearBoundary()
    {
        var late = NoteDocument.Parse("----30/12/2026\r\n🚨Yıl sonu\r\n\t👍[Done 02/01]Sayım\r\n");
        Assert.Equal(new DateTime(2027, 1, 2), First(late).SubTasks[0].DoneOn(new DateTime(2026, 12, 30), Today));

        var early = NoteDocument.Parse("----05/01/2027\r\n🚨Yıl başı\r\n\t👍[Done 30/12]Hazırlık\r\n");
        Assert.Equal(new DateTime(2026, 12, 30), First(early).SubTasks[0].DoneOn(new DateTime(2027, 1, 5), Today));
    }

    [Fact]
    public void Read_LongForm()
    {
        var doc = NoteDocument.Parse("----13/11/2026\r\n👍[Done 12/11/2025]Eski iş\r\n");

        Assert.Equal(new DateTime(2025, 11, 12), First(doc).DoneOn(new DateTime(2026, 11, 13), Today));
        Assert.True(First(doc).IsFinished);
    }

    [Fact]
    public void OldFile_RoundTrips()
    {
        const string text = "----12/11/2026\r\n👍[Done]Sunum\r\n\t👍[Done]Slaytlar\r\n\t🚨Prova\r\n";
        var doc = NoteDocument.Parse(text);

        Assert.Equal(text, doc.Serialize());
        Assert.Equal(new DateTime(2026, 11, 12), First(doc).SubTasks[0].DoneOn(new DateTime(2026, 11, 12), Today));
    }

    [Fact]
    public void AddDone_StampsTargetDay()
    {
        var doc = NoteDocument.Parse("");

        doc.AddEntry(new DateTime(2026, 11, 10), "Rapor", null, Array.Empty<string>(), true);

        Assert.Contains("👍[Done 10/11]Rapor", doc.Serialize());
    }

    [Fact]
    public void UndatedEntry_DoneOnToday()
    {
        var doc = NoteDocument.Parse("----tarihsiz\r\n👍[Done]Bir gün\r\n");

        Assert.Equal(Today, First(doc).DoneOn(Day.Undated, Today));
    }

    [Fact]
    public void ChangeDate_TaskOnly()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n👍[Done 13/11]Sunum\r\n\t👍[Done 13/11]A\r\n");

        NoteDocument.SetDoneDate(First(doc), new DateTime(2026, 11, 12), new DateTime(2026, 11, 12), Today);

        Assert.Equal("----12/11/2026\r\n👍[Done 12/11]Sunum\r\n\t👍[Done 13/11]A\r\n", doc.Serialize());
    }

    [Fact]
    public void ChangeDate_SubTask()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n👍[Done 13/11]Sunum\r\n\t👍[Done 13/11]A\r\n");

        NoteDocument.SetSubTaskDoneDate(First(doc).SubTasks[0], new DateTime(2026, 11, 11), new DateTime(2026, 11, 12), Today);

        Assert.Equal("----12/11/2026\r\n👍[Done 13/11]Sunum\r\n\t👍[Done 11/11]A\r\n", doc.Serialize());
    }

    [Fact]
    public void ChangeDate_OldOrPrefixedMarkGetsDate()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n👍[Done]Sunum\r\n🛠️👍[Done]Prova\r\n");

        NoteDocument.SetDoneDate(doc.Days[0].Entries[0], new DateTime(2026, 11, 10), new DateTime(2026, 11, 12), Today);
        NoteDocument.SetDoneDate(doc.Days[0].Entries[1], new DateTime(2026, 11, 10), new DateTime(2026, 11, 12), Today);

        Assert.Equal("----12/11/2026\r\n👍[Done 10/11]Sunum\r\n👍[Done 10/11]Prova\r\n", doc.Serialize());
    }

    [Fact]
    public void ChangeDate_FarDateWritesYear()
    {
        var day = new DateTime(2026, 12, 20);
        var doc = NoteDocument.Parse("----20/12/2026\r\n👍[Done 20/12]Sunum\r\n👍[Done 20/12]Prova\r\n");

        NoteDocument.SetDoneDate(doc.Days[0].Entries[0], new DateTime(2026, 5, 5), day, Today);
        NoteDocument.SetDoneDate(doc.Days[0].Entries[1], new DateTime(2027, 1, 5), day, Today);

        Assert.Equal("----20/12/2026\r\n👍[Done 05/05/2026]Sunum\r\n👍[Done 05/01]Prova\r\n", doc.Serialize());
        Assert.Equal(new DateTime(2026, 5, 5), doc.Days[0].Entries[0].DoneOn(day, Today));
        Assert.Equal(new DateTime(2027, 1, 5), doc.Days[0].Entries[1].DoneOn(day, Today));
    }

    [Fact]
    public void ChangeDate_OpenOrDroppedUntouched()
    {
        const string text = "----12/11/2026\r\n🚨Sunum\r\n\t🚨A\r\n🚫[İptal]Prova\r\n";
        var doc = NoteDocument.Parse(text);
        var on = new DateTime(2026, 11, 10);
        var day = new DateTime(2026, 11, 12);

        NoteDocument.SetDoneDate(doc.Days[0].Entries[0], on, day, Today);
        NoteDocument.SetSubTaskDoneDate(doc.Days[0].Entries[0].SubTasks[0], on, day, Today);
        NoteDocument.SetDoneDate(doc.Days[0].Entries[1], on, day, Today);

        Assert.Equal(text, doc.Serialize());
    }
}
