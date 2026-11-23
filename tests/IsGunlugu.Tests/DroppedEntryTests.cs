using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DroppedEntryTests
{
    private const string Day = "----12/11/2026\r\n🚨Rapor\r\n🚫[İptal]Tedarikçi toplantısı\r\n\t🚨Gündem\r\n👍[Done]Fatura\r\n";

    private static Entry At(NoteDocument doc, int index) => doc.Days[0].Entries[index];

    [Fact]
    public void ReadsBack_ByteForByte()
    {
        Assert.Equal(Day, NoteDocument.Parse(Day).Serialize());
    }

    [Fact]
    public void ADroppedTask_IsNeitherOpenNorFinished()
    {
        var entry = At(NoteDocument.Parse(Day), 1);

        Assert.Equal("Tedarikçi toplantısı", entry.Text);
        Assert.Equal(EntryStatus.Dropped, entry.Status);
        Assert.True(entry.IsDropped);
        Assert.False(entry.HasOpenWork); // even with an open sub-task under it
        Assert.False(entry.IsFinished);
        Assert.False(entry.IsPartlyDone);
        Assert.False(entry.CanBeUndated);
    }

    [Theory]
    [InlineData("🚫[iptal]İş")]
    [InlineData("🚫 [İptal] İş")]
    [InlineData("🚫İş")]
    public void TheMarkIsRead_HoweverItWasTyped(string line)
    {
        var entry = At(NoteDocument.Parse("----12/11/2026\r\n" + line + "\r\n"), 0);

        Assert.True(entry.IsDropped);
        Assert.Equal("İş", entry.Text);
    }

    [Fact]
    public void Dropping_WritesTheMark_AndKeepsEverythingElse()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Rapor\r\nnot\r\n\t🚨alt\r\n");

        NoteDocument.SetDropped(At(doc, 0), true);

        Assert.Equal("----12/11/2026\r\n🚫[İptal]Rapor\r\nnot\r\n\t🚨alt\r\n", doc.Serialize());
    }

    [Fact]
    public void Reopening_GoesBackToOpen()
    {
        var doc = NoteDocument.Parse(Day);

        NoteDocument.SetDropped(At(doc, 1), false);

        Assert.Equal(EntryStatus.Open, At(doc, 1).Status);
        Assert.True(At(doc, 1).HasOpenWork);
    }

    [Fact]
    public void Reopening_WithEverySubTaskDone_IsFinished()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚫[İptal]İş\r\n\t👍[Done]alt\r\n");

        NoteDocument.SetDropped(At(doc, 0), false);

        Assert.True(At(doc, 0).IsFinished);
    }

    [Fact]
    public void TickingASubTask_DoesNotBringADroppedTaskBack()
    {
        var doc = NoteDocument.Parse(Day);

        NoteDocument.SetSubTaskDone(At(doc, 1).SubTasks[0], true);

        Assert.True(At(doc, 1).IsDropped);
    }

    [Fact]
    public void InADay_DroppedComesLast_AfterFinished()
    {
        var doc = NoteDocument.Parse(Day);

        doc.SortDaysByState();

        Assert.Equal(new[] { "Rapor", "Fatura", "Tedarikçi toplantısı" }, doc.Days[0].Entries.Select(e => e.Text).ToArray());
    }

    [Fact]
    public void TheWeekSummary_LeavesDroppedOut()
    {
        Assert.DoesNotContain("Tedarikçi", WeekSummary.Build(NoteDocument.Parse(Day), new DateTime(2026, 11, 9)));
    }
}
