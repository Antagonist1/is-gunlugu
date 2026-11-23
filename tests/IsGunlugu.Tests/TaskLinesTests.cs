using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class TaskLinesTests
{
    private static string Kinds(IEnumerable<TaskLine> lines) =>
        string.Join("|", lines.Select(l => l.Kind + ":" + l.Text));

    [Fact]
    public void Parse_Kinds()
    {
        var lines = TaskLines.Parse("Sunum\nCuma önce\n- Grafik\n  Mart eksik\n- Demo");

        Assert.Equal("Title:Sunum|Note:Cuma önce|SubTask:Grafik|SubNote:Mart eksik|SubTask:Demo", Kinds(lines));
    }

    [Theory]
    [InlineData("Sunum\nCuma önce\n- Grafik\n  Mart eksik\n- Demo")]
    [InlineData("Sunum")]
    [InlineData("Sunum\n- A\nsonra not")]
    [InlineData("Sunum\n\tgirintili not")]
    [InlineData("Sunum\n\n- A")]
    public void RoundTrip_EditableText(string typed)
    {
        Assert.Equal(typed, TaskLines.Join(TaskLines.Parse(typed)));
    }

    [Fact]
    public void RoundTrip_RealEntry()
    {
        var doc = NoteDocument.Parse("----12/11/2026\r\n🚨Sunum\r\nCuma önce\r\n\t👍[Done 12/11]Grafik\r\n\t\tMart eksik\r\n\t🚨Demo\r\n");
        var typed = NoteDocument.EditableText(doc.Days[0].Entries[0]);

        Assert.Equal(typed, TaskLines.Join(TaskLines.Parse(typed)));
    }

    [Fact]
    public void Parse_EmptySubTaskLine_StaysSubTask()
    {
        var lines = TaskLines.Parse("T\n- \n-");

        Assert.Equal("Title:T|SubTask:|Note:-", Kinds(lines));
        Assert.Equal("T\n- \n-", TaskLines.Join(lines));
    }

    [Fact]
    public void Parse_Empty_IsOneEmptyTitle()
    {
        Assert.Equal("Title:", Kinds(TaskLines.Parse("")));
    }

    [Fact]
    public void Dash_Alone_IsNote()
    {
        var lines = TaskLines.Parse("Sunum\n-5 derece");

        Assert.Equal("Title:Sunum|Note:-5 derece", Kinds(lines));
        Assert.False(TaskLines.TryPromote(lines[1]));
    }

    [Fact]
    public void TryPromote_DashSpace()
    {
        var note = new TaskLine(LineKind.Note, "- Grafik");
        var subNote = new TaskLine(LineKind.SubNote, "- X");
        var title = new TaskLine(LineKind.Title, "- X");

        Assert.True(TaskLines.TryPromote(note));
        Assert.True(TaskLines.TryPromote(subNote));
        Assert.False(TaskLines.TryPromote(title));
        Assert.Equal("SubTask:Grafik|SubTask:X", Kinds(new[] { note, subNote }));
    }

    [Fact]
    public void KindAfter_OpensNoteAtSameLevel()
    {
        Assert.Equal(LineKind.Note, TaskLines.KindAfter(LineKind.Title));
        Assert.Equal(LineKind.Note, TaskLines.KindAfter(LineKind.Note));
        Assert.Equal(LineKind.SubNote, TaskLines.KindAfter(LineKind.SubTask));
        Assert.Equal(LineKind.SubNote, TaskLines.KindAfter(LineKind.SubNote));
    }

    [Fact]
    public void Demote_SubTaskBecomesNote()
    {
        var afterSub = TaskLines.Parse("T\n- A\n- B");
        TaskLines.Demote(afterSub, 2);
        Assert.Equal("Title:T|SubTask:A|SubNote:B", Kinds(afterSub));

        var first = TaskLines.Parse("T\nnot\n- A");
        TaskLines.Demote(first, 2);
        Assert.Equal("Title:T|Note:not|Note:A", Kinds(first));
    }

    [Fact]
    public void Normalize_FixesKinds()
    {
        var lines = new List<TaskLine> { new(LineKind.Note, "x"), new(LineKind.SubNote, "y") };

        TaskLines.Normalize(lines);

        Assert.Equal("Title:x|Note:y", Kinds(lines));
    }

    [Theory]
    [InlineData("Sunum\n- \n- A\n  \n", "Sunum\n- A")]
    [InlineData("Sunum", "Sunum")]
    [InlineData("Sunum\nnot\n\n", "Sunum\nnot")]
    public void TrimEmpty_DropsEmptyLines(string typed, string expected)
    {
        Assert.Equal(expected, TaskLines.TrimEmpty(typed));
    }
}
