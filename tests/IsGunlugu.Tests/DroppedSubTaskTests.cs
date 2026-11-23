using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DroppedSubTaskTests
{
    private const string Text = "----12/11/2026\r\n🚨Sunum\r\n\t👍[Done]Slaytlar\r\n\t🚨Grafikler\r\n\t🚨Prova\r\n";

    private static Entry First(NoteDocument doc) => doc.Days[0].Entries[0];

    [Fact]
    public void Dropping_ChangesOnlyThatSubTask()
    {
        var doc = NoteDocument.Parse(Text);

        NoteDocument.SetSubTaskDropped(First(doc).SubTasks[2], true);

        Assert.Equal("----12/11/2026\r\n🚨Sunum\r\n\t👍[Done]Slaytlar\r\n\t🚨Grafikler\r\n\t🚫[İptal]Prova\r\n", doc.Serialize());
        Assert.True(First(doc).SubTasks[2].IsDropped);
        Assert.False(First(doc).SubTasks[2].IsDone);
        Assert.False(First(doc).IsDropped);
    }

    [Fact]
    public void ADroppedSubTask_IsNotOpenWork_AndNotCounted()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\n\t👍[Done]Slaytlar\r\n\t🚫[İptal]Prova\r\n");

        Assert.Single(First(doc).ActiveSubTasks);
        Assert.False(First(doc).IsPartlyDone);
    }

    [Fact]
    public void DroppingTheLastOpenSubTask_FinishesTheTask()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\n\t👍[Done]Slaytlar\r\n\t🚨Prova\r\n");

        NoteDocument.SetSubTaskDropped(First(doc).SubTasks[1], true);

        Assert.True(First(doc).IsFinished);
    }

    [Fact]
    public void WithEverySubTaskDropped_TheTaskStaysAsItWas()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\n\t🚨Prova\r\n");

        NoteDocument.SetSubTaskDropped(First(doc).SubTasks[0], true);

        Assert.Equal(EntryStatus.Open, First(doc).Status);
        Assert.True(First(doc).HasOpenWork);
    }

    [Fact]
    public void TakingItBack_OpensTheSubTask_AndTheTask()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n👍[Done]Sunum\r\n\t👍[Done]Slaytlar\r\n\t🚫[İptal]Prova\r\n");

        NoteDocument.SetSubTaskDropped(First(doc).SubTasks[1], false);

        Assert.False(First(doc).SubTasks[1].IsDropped);
        Assert.Equal(EntryStatus.Open, First(doc).Status);
    }

    [Fact]
    public void FinishingTheTask_LeavesDroppedSubTasksDropped()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\n\t🚨Grafikler\r\n\t🚫[İptal]Prova\r\n");

        NoteDocument.SetDone(First(doc), true, new DateTime(2026, 11, 12));

        Assert.Equal("----12/11/2026\r\n👍[Done 12/11]Sunum\r\n\t👍[Done 12/11]Grafikler\r\n\t🚫[İptal]Prova\r\n", doc.Serialize());
    }

    [Fact]
    public void TheWeekSummary_CountsAndListsWithoutTheDropped()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\n\t👍[Done]Slaytlar\r\n\t🚨Grafikler\r\n\t🚫[İptal]Prova\r\n");

        var text = WeekSummary.Build(doc, new DateTime(2026, 11, 9));

        Assert.Contains("- Sunum (1/2)", text);
        Assert.DoesNotContain("Prova", text);
    }
}
