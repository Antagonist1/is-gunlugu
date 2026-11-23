using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public class NoteDocumentTests
{
    private const string Sample =
        "----01/10/2026\r\n" +
        "👍[Done]birinci iş\r\n" +
        "\t->alt satır 73,85\r\n" +
        "👍[Done] boşluklu iş\r\n" +
        "denenecek.\r\n" +
        "\r\n" +
        "----07/10/2026\r\n" +
        "🛠👍[Done] araçlı iş\r\n" +
        "\r\n" +
        "👍[Done]boşluktan sonraki iş\r\n" +
        "2601P0001002\r\n" +
        "\r\n" +
        "----03/11/2026\r\n" +
        "👍[Done]bitti\r\n" +
        "🚨açık iş\r\n";

    private static readonly DateTime First = new(2026, 10, 1);
    private static readonly DateTime Last = new(2026, 11, 3);

    [Theory]
    [InlineData(Sample)]
    [InlineData("")]
    [InlineData("başlıksız satır\r\n----01/10/2026\r\n🚨iş")]
    [InlineData("----01/10/2026\n🚨iş\n\n----02/10/2026\n👍[Done]iş\n")]
    [InlineData("----01/10/2026\r\n\r\nserbest not\r\n\talt\r\n🚨iş\r\n")]
    [InlineData("----99/99/2026\r\n🚨tarih geçersiz\r\n")]
    public void Unchanged_file_serializes_byte_for_byte(string text)
    {
        Assert.Equal(text, NoteDocument.Parse(text).Serialize());
    }

    [Fact]
    public void Parses_days_statuses_and_body_lines()
    {
        var doc = NoteDocument.Parse(Sample);

        Assert.Equal(new[] { First, new DateTime(2026, 10, 7), Last }, doc.Days.Select(d => d.Date));

        var first = doc.Days[0].Entries;
        Assert.Equal(2, first.Count);
        Assert.Equal("birinci iş", first[0].Text);
        Assert.Equal(new[] { "\t->alt satır 73,85" }, first[0].VisibleBody);
        Assert.Equal("boşluklu iş", first[1].Text);
        Assert.Equal(new[] { "denenecek." }, first[1].VisibleBody);

        Assert.Equal(EntryStatus.Done, doc.Days[1].Entries[0].Status);
        Assert.Equal("araçlı iş", doc.Days[1].Entries[0].Text);
        Assert.Equal(EntryStatus.Open, doc.Days[2].Entries[1].Status);
    }

    [Fact]
    public void Line_without_marker_at_start_of_day_is_a_note()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\nserbest not\r\n");

        Assert.Equal(EntryStatus.Note, doc.Days[0].Entries[0].Status);
        NoteDocument.SetDone(doc.Days[0].Entries[0], true);
        Assert.Equal("----01/10/2026\r\nserbest not\r\n", doc.Serialize());
    }

    [Fact]
    public void Marking_done_changes_only_that_line()
    {
        var doc = NoteDocument.Parse(Sample);

        NoteDocument.SetDone(doc.Days[2].Entries[1], true, new DateTime(2026, 10, 7));

        Assert.Equal(Sample.Replace("🚨açık iş", "👍[Done 07/10]açık iş"), doc.Serialize());
    }

    [Fact]
    public void Reopening_keeps_the_gap_and_body()
    {
        var doc = NoteDocument.Parse(Sample);

        NoteDocument.SetDone(doc.Days[0].Entries[1], false);

        Assert.Equal(Sample.Replace("👍[Done] boşluklu iş", "🚨 boşluklu iş"), doc.Serialize());
    }

    [Fact]
    public void Adding_to_existing_day_goes_to_its_end_before_the_blank_line()
    {
        var doc = NoteDocument.Parse(Sample);

        doc.AddEntry(First, "yeni iş", done: false);

        Assert.Equal(Sample.Replace("denenecek.\r\n", "denenecek.\r\n🚨yeni iş\r\n"), doc.Serialize());
    }

    [Fact]
    public void Adding_with_new_date_appends_day_in_file_format()
    {
        var doc = NoteDocument.Parse(Sample);

        doc.AddEntry(new DateTime(2026, 11, 4), "yarınki iş\n\t->detay", done: true);

        Assert.Equal(Sample + "\r\n----04/11/2026\r\n👍[Done 04/11]yarınki iş\r\n\t->detay\r\n", doc.Serialize());
    }

    [Fact]
    public void Adding_past_date_inserts_day_in_date_order()
    {
        var doc = NoteDocument.Parse(Sample);

        doc.AddEntry(new DateTime(2026, 10, 5), "araya giren", done: true);

        Assert.Equal(
            Sample.Replace("----07/10/2026", "----05/10/2026\r\n👍[Done 05/10]araya giren\r\n\r\n----07/10/2026"),
            doc.Serialize());
    }

    [Fact]
    public void Editing_replaces_text_and_body_but_keeps_marker_and_separator()
    {
        var doc = NoteDocument.Parse(Sample);

        NoteDocument.SetText(doc.Days[0].Entries[1], "yeni metin\nyeni alt");

        Assert.Equal(
            Sample.Replace("👍[Done] boşluklu iş\r\ndenenecek.", "👍[Done] yeni metin\r\nyeni alt"),
            doc.Serialize());
    }

    [Fact]
    public void Deleting_last_entry_keeps_the_separator_before_next_day()
    {
        var doc = NoteDocument.Parse(Sample);

        doc.Delete(doc.Days[0].Entries[1]);

        Assert.Equal(Sample.Replace("👍[Done] boşluklu iş\r\ndenenecek.\r\n", ""), doc.Serialize());
    }

    [Fact]
    public void Deleting_only_entry_removes_the_day()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨bir\r\n\r\n----02/10/2026\r\n🚨iki\r\n");

        doc.Delete(doc.Days[1].Entries[0]);

        Assert.Equal(new[] { new DateTime(2026, 10, 1) }, doc.Days.Select(d => d.Date));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n\t")]
    public void Empty_text_is_rejected(string text)
    {
        var doc = NoteDocument.Parse(Sample);

        Assert.Throws<ArgumentException>(() => doc.AddEntry(Last, text, false));
        Assert.Throws<ArgumentException>(() => NoteDocument.SetText(doc.Days[0].Entries[0], text));
    }

    private const string WithSubTasks =
        "----01/10/2026\r\n" +
        "🚨ana iş\r\n" +
        "\t🚨birinci alt\r\n" +
        "\t->düz not\r\n" +
        "\t👍[Done] ikinci alt\r\n" +
        "\r\n" +
        "----02/10/2026\r\n" +
        "🚨sonraki\r\n";

    [Fact]
    public void Indented_marker_lines_are_sub_tasks_and_round_trip()
    {
        var doc = NoteDocument.Parse(WithSubTasks);
        var entry = doc.Days[0].Entries[0];

        Assert.Single(doc.Days[0].Entries);
        Assert.Equal(new[] { "birinci alt", "ikinci alt" }, entry.SubTasks.Select(t => t.Text));
        Assert.Equal(new[] { false, true }, entry.SubTasks.Select(t => t.IsDone));
        Assert.Equal(3, entry.Parts.Count);
        Assert.Null(entry.Parts[1].Task);
        Assert.Equal(WithSubTasks, doc.Serialize());
    }

    [Fact]
    public void Indented_marker_before_any_entry_stays_a_top_level_entry()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n\t🚨girintili\r\n");

        Assert.Equal(EntryStatus.Open, doc.Days[0].Entries[0].Status);
        Assert.Empty(doc.Days[0].Entries[0].SubTasks);
    }

    [Fact]
    public void Finishing_the_last_sub_task_finishes_the_parent()
    {
        var doc = NoteDocument.Parse(WithSubTasks);
        var entry = doc.Days[0].Entries[0];

        NoteDocument.SetSubTaskDone(entry.SubTasks[0], true, new DateTime(2026, 10, 1));

        Assert.Equal(EntryStatus.Done, entry.Status);
        Assert.True(entry.IsFinished);
        Assert.Equal(
            WithSubTasks.Replace("🚨ana iş", "👍[Done 01/10]ana iş").Replace("\t🚨birinci alt\r\n", "")
                .Replace("\t👍[Done] ikinci alt\r\n", "\t👍[Done] ikinci alt\r\n\t👍[Done 01/10]birinci alt\r\n"),
            doc.Serialize());
    }

    [Fact]
    public void A_finished_sub_task_goes_under_the_others_with_its_description()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨ana\r\n\t🚨bir\r\n\t\tbirin notu\r\n\t🚨iki\r\n\t👍[Done 30/09]eski\r\n");
        var entry = doc.Days[0].Entries[0];

        NoteDocument.SetSubTaskDone(entry.SubTasks[0], true, new DateTime(2026, 10, 1));

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨iki\r\n\t👍[Done 30/09]eski\r\n\t👍[Done 01/10]bir\r\n\t\tbirin notu\r\n", doc.Serialize());
    }

    [Fact]
    public void An_unticked_sub_task_goes_back_under_the_open_ones()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨ana\r\n\t🚨bir\r\n\t👍[Done 30/09]iki\r\n\t👍[Done 30/09]üç\r\n");

        NoteDocument.SetSubTaskDone(doc.Days[0].Entries[0].SubTasks[2], false);

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨bir\r\n\t🚨üç\r\n\t👍[Done 30/09]iki\r\n", doc.Serialize());
    }

    [Fact]
    public void Old_files_get_closed_sub_tasks_under_open_ones_in_their_old_order()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨ana\r\n\t👍[Done]a\r\n\t🚨b\r\n\t\tb notu\r\n\t🚫c\r\n\t🚨d\r\n\r\n----02/10/2026\r\n🚨düz\r\n\t🚨x\r\n\t👍[Done]y\r\n");

        Assert.True(NoteDocument.SinkClosedSubTasks(doc));

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨b\r\n\t\tb notu\r\n\t🚨d\r\n\t👍[Done]a\r\n\t🚫c\r\n\r\n----02/10/2026\r\n🚨düz\r\n\t🚨x\r\n\t👍[Done]y\r\n", doc.Serialize());
        Assert.False(NoteDocument.SinkClosedSubTasks(doc));
    }

    [Fact]
    public void A_task_with_every_sub_task_closed_is_left_as_written()
    {
        var text = "----01/10/2026\r\n👍[Done]ana\r\n\t👍[Done]a\r\nara not\r\n\t👍[Done]b\r\n";
        var doc = NoteDocument.Parse(text);

        Assert.False(NoteDocument.SinkClosedSubTasks(doc));
        Assert.Equal(text, doc.Serialize());
    }

    [Fact]
    public void Reopening_a_sub_task_reopens_the_parent()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n👍[Done]ana\r\n\t👍[Done]alt\r\n");
        var entry = doc.Days[0].Entries[0];

        NoteDocument.SetSubTaskDone(entry.SubTasks[0], false);

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨alt\r\n", doc.Serialize());
    }

    [Fact]
    public void Marking_parent_done_finishes_open_sub_tasks()
    {
        var doc = NoteDocument.Parse(WithSubTasks);

        NoteDocument.SetDone(doc.Days[0].Entries[0], true);

        Assert.All(doc.Days[0].Entries[0].SubTasks, t => Assert.True(t.IsDone));
        Assert.Contains("\t->düz not", doc.Serialize());
    }

    [Fact]
    public void Reopening_parent_leaves_sub_tasks_alone()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n👍[Done]ana\r\n\t👍[Done]alt\r\n");

        NoteDocument.SetDone(doc.Days[0].Entries[0], false);

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t👍[Done]alt\r\n", doc.Serialize());
    }

    [Fact]
    public void Adding_a_sub_task_goes_before_the_blank_line_and_reopens_a_done_parent()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n👍[Done]ana\r\n\r\n----02/10/2026\r\n🚨x\r\n");

        NoteDocument.AddSubTask(doc.Days[0].Entries[0], "yeni alt");

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨yeni alt\r\n\r\n----02/10/2026\r\n🚨x\r\n", doc.Serialize());
    }

    [Fact]
    public void Adding_first_sub_task_to_an_open_parent_keeps_it_open()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨ana\r\n");

        NoteDocument.AddSubTask(doc.Days[0].Entries[0], "alt bir");

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨alt bir\r\n", doc.Serialize());
    }

    private const string WithDescription =
        "----01/10/2026\r\n" +
        "🚨ana\r\n" +
        "\t🚨birinci\r\n" +
        "\t\tilk açıklama\r\n" +
        "\t\tikinci açıklama\r\n" +
        "\t👍[Done] ikinci\r\n" +
        "\t->düz not\r\n";

    [Fact]
    public void Lines_indented_deeper_than_a_sub_task_are_its_description()
    {
        var doc = NoteDocument.Parse(WithDescription);
        var tasks = doc.Days[0].Entries[0].SubTasks;

        Assert.Equal(new[] { "ilk açıklama", "ikinci açıklama" }, tasks[0].DescriptionLines);
        Assert.Empty(tasks[1].DescriptionLines);
        Assert.False(doc.Days[0].Entries[0].Parts[4].IsDescription);
        Assert.True(doc.Days[0].Entries[0].Parts[2].IsDescription);
        Assert.Equal(WithDescription, doc.Serialize());
    }

    [Fact]
    public void Adding_a_sub_task_with_more_lines_makes_them_its_description()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨ana\r\n\r\n----02/10/2026\r\n🚨x\r\n");

        NoteDocument.AddSubTask(doc.Days[0].Entries[0], "alt\nbirinci satır\n\n  ikinci satır  ");

        Assert.Equal(
            "----01/10/2026\r\n🚨ana\r\n\t🚨alt\r\n\t\tbirinci satır\r\n\t\tikinci satır\r\n\r\n----02/10/2026\r\n🚨x\r\n",
            doc.Serialize());
        Assert.Single(doc.Days[0].Entries[0].SubTasks);
    }

    [Fact]
    public void Editing_a_sub_task_rewrites_its_description_and_keeps_marker_and_neighbours()
    {
        var doc = NoteDocument.Parse(WithDescription);

        NoteDocument.SetSubTaskText(doc.Days[0].Entries[0].SubTasks[0], "yeni\nyeni açıklama");

        Assert.Equal(
            "----01/10/2026\r\n🚨ana\r\n\t🚨yeni\r\n\t\tyeni açıklama\r\n\t👍[Done] ikinci\r\n\t->düz not\r\n",
            doc.Serialize());
    }

    [Fact]
    public void Editing_can_remove_the_description()
    {
        var doc = NoteDocument.Parse(WithDescription);

        NoteDocument.SetSubTaskText(doc.Days[0].Entries[0].SubTasks[0], "sade");

        Assert.Equal("----01/10/2026\r\n🚨ana\r\n\t🚨sade\r\n\t👍[Done] ikinci\r\n\t->düz not\r\n", doc.Serialize());
    }

    [Fact]
    public void Deleting_a_sub_task_removes_its_description()
    {
        var doc = NoteDocument.Parse(WithDescription);

        NoteDocument.DeleteSubTask(doc.Days[0].Entries[0].SubTasks[0], new DateTime(2026, 10, 1));

        Assert.Equal("----01/10/2026\r\n👍[Done 01/10]ana\r\n\t👍[Done] ikinci\r\n\t->düz not\r\n", doc.Serialize());
    }

    [Fact]
    public void Toggling_a_sub_task_leaves_its_description_alone()
    {
        var doc = NoteDocument.Parse(WithDescription);

        NoteDocument.SetSubTaskDone(doc.Days[0].Entries[0].SubTasks[0], true, new DateTime(2026, 10, 1));

        Assert.Contains("\t👍[Done 01/10]birinci\r\n\t\tilk açıklama\r\n\t\tikinci açıklama\r\n", doc.Serialize());
    }

    [Fact]
    public void Search_finds_text_in_a_description()
    {
        var entry = NoteDocument.Parse(WithDescription).Days[0].Entries[0];

        Assert.True(TextSearch.Matches(entry, TextSearch.Fold("ikinci aciklama")));
    }

    private const string ForMoving =
        "----01/10/2026\r\n" +
        "🚨eski bir\r\n" +
        "\t🚨alt\r\n" +
        "\t\taçıklama\r\n" +
        "🚨eski iki\r\n" +
        "\r\n" +
        "----03/11/2026\r\n" +
        "👍[Done]bugün bir\r\n" +
        "🚨bugün iki\r\n" +
        "🚨bugün üç\r\n";

    [Fact]
    public void Moving_a_task_up_within_its_day_changes_only_the_order()
    {
        var doc = NoteDocument.Parse(ForMoving);
        var today = doc.Days[1].Entries;

        doc.MoveEntry(today[2], Last, today[0]);

        Assert.Equal(
            ForMoving.Replace("👍[Done]bugün bir\r\n🚨bugün iki\r\n🚨bugün üç", "🚨bugün üç\r\n👍[Done]bugün bir\r\n🚨bugün iki"),
            doc.Serialize());
    }

    [Fact]
    public void Moving_a_task_to_the_end_of_its_day()
    {
        var doc = NoteDocument.Parse(ForMoving);

        doc.MoveEntry(doc.Days[1].Entries[0], Last, null);

        Assert.Equal(
            ForMoving.Replace("👍[Done]bugün bir\r\n🚨bugün iki\r\n🚨bugün üç", "🚨bugün iki\r\n🚨bugün üç\r\n👍[Done]bugün bir"),
            doc.Serialize());
    }

    [Fact]
    public void Moving_the_last_task_of_a_day_keeps_the_blank_line_before_the_next_day()
    {
        var doc = NoteDocument.Parse(ForMoving);

        doc.MoveEntry(doc.Days[0].Entries[1], Last, null);

        Assert.Equal(
            ForMoving.Replace("🚨eski iki\r\n", "").Replace("🚨bugün üç\r\n", "🚨bugün üç\r\n🚨eski iki\r\n"),
            doc.Serialize());
    }

    [Fact]
    public void Moving_to_another_day_takes_sub_tasks_and_descriptions_along()
    {
        var doc = NoteDocument.Parse(ForMoving);
        var today = doc.Days[1].Entries;

        doc.MoveEntry(doc.Days[0].Entries[0], Last, today[1]);

        Assert.Equal(
            "----01/10/2026\r\n🚨eski iki\r\n\r\n----03/11/2026\r\n👍[Done]bugün bir\r\n🚨eski bir\r\n\t🚨alt\r\n\t\taçıklama\r\n🚨bugün iki\r\n🚨bugün üç\r\n",
            doc.Serialize());
    }

    [Fact]
    public void Moving_to_a_date_without_a_header_creates_the_day()
    {
        var doc = NoteDocument.Parse(ForMoving);

        doc.MoveEntry(doc.Days[1].Entries[2], new DateTime(2026, 11, 4), null);

        Assert.Equal(
            ForMoving.Replace("🚨bugün üç\r\n", "") + "\r\n----04/11/2026\r\n🚨bugün üç\r\n",
            doc.Serialize());
    }

    [Fact]
    public void Moving_the_only_task_of_a_day_removes_that_day()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨tek\r\n\r\n----03/11/2026\r\n🚨bugün\r\n");

        doc.MoveEntry(doc.Days[0].Entries[0], Last, null);

        Assert.Equal("----03/11/2026\r\n🚨bugün\r\n🚨tek\r\n", doc.Serialize());
    }

    [Fact]
    public void Moving_the_only_task_of_a_day_within_that_day_changes_nothing()
    {
        var text = "----01/10/2026\r\n🚨tek\r\n\r\n----03/11/2026\r\n🚨bugün\r\n";
        var doc = NoteDocument.Parse(text);

        doc.MoveEntry(doc.Days[0].Entries[0], First, null);

        Assert.Equal(text, doc.Serialize());
    }

    [Fact]
    public void Moving_a_task_before_itself_changes_nothing()
    {
        var doc = NoteDocument.Parse(ForMoving);
        var entry = doc.Days[1].Entries[1];

        doc.MoveEntry(entry, Last, entry);

        Assert.Equal(ForMoving, doc.Serialize());
    }

    [Theory]
    [InlineData("----tarihsiz\r\n🚨bir gün\r\n")]
    [InlineData("---- Tarihsiz \r\n🚨bir gün\r\n")]
    [InlineData("----TARİHSİZ\r\n🚨bir gün\r\n")]
    public void Undated_header_is_recognized_and_kept_as_written(string text)
    {
        var doc = NoteDocument.Parse(text);

        Assert.True(doc.Days[0].IsUndated);
        Assert.Equal(text, doc.Serialize());
    }

    [Fact]
    public void Adding_an_undated_task_creates_the_block_at_the_end_of_the_file()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨tarihli\r\n");

        doc.AddEntry(Day.Undated, "bir gün", done: false);

        Assert.Equal("----01/10/2026\r\n🚨tarihli\r\n\r\n----tarihsiz\r\n🚨bir gün\r\n", doc.Serialize());
    }

    [Fact]
    public void A_new_dated_day_goes_before_the_undated_block()
    {
        var doc = NoteDocument.Parse("----01/10/2026\r\n🚨tarihli\r\n\r\n----tarihsiz\r\n🚨bir gün\r\n");

        doc.AddEntry(new DateTime(2026, 10, 2), "yeni", done: false);

        Assert.Equal(
            "----01/10/2026\r\n🚨tarihli\r\n\r\n----02/10/2026\r\n🚨yeni\r\n\r\n----tarihsiz\r\n🚨bir gün\r\n",
            doc.Serialize());
    }

    [Fact]
    public void Moving_between_a_day_and_undated_works_both_ways()
    {
        var text = "----01/10/2026\r\n🚨bir\r\n🚨iki\r\n";
        var doc = NoteDocument.Parse(text);

        doc.MoveEntry(doc.Days[0].Entries[1], Day.Undated, null);
        Assert.Equal("----01/10/2026\r\n🚨bir\r\n\r\n----tarihsiz\r\n🚨iki\r\n", doc.Serialize());

        doc.MoveEntry(doc.Days[1].Entries[0], First, null);
        Assert.Equal("----01/10/2026\r\n🚨bir\r\n🚨iki\r\n", doc.Serialize().TrimEnd('\r', '\n') + "\r\n");
        Assert.Single(doc.Days);
    }

    [Fact]
    public void Dash_lines_in_a_new_task_become_sub_tasks_with_descriptions()
    {
        var doc = NoteDocument.Parse("");

        doc.AddEntry(First, "ana iş\n- bir\n  açıklaması\n- iki\nnormal not", done: false);

        Assert.Equal(
            "----01/10/2026\r\n🚨ana iş\r\n\t🚨bir\r\n\t\taçıklaması\r\n\t🚨iki\r\nnormal not\r\n",
            doc.Serialize());
        Assert.Equal(2, doc.Days[0].Entries[0].SubTasks.Count);
    }

    [Fact]
    public void Dash_lines_of_a_task_added_as_done_are_done_too()
    {
        var doc = NoteDocument.Parse("");

        doc.AddEntry(First, "ana\n- bir", done: true);

        Assert.Equal("----01/10/2026\r\n👍[Done 01/10]ana\r\n\t👍[Done 01/10]bir\r\n", doc.Serialize());
    }

    [Fact]
    public void Arrow_lines_are_not_mistaken_for_dash_lines()
    {
        var doc = NoteDocument.Parse("");

        doc.AddEntry(First, "ana\n\t->estia 73,85", done: false);

        Assert.Empty(doc.Days[0].Entries[0].SubTasks);
        Assert.Contains("\t->estia 73,85", doc.Serialize());
    }

    [Fact]
    public void Deleting_the_last_open_sub_task_finishes_the_parent()
    {
        var doc = NoteDocument.Parse(WithSubTasks);
        var entry = doc.Days[0].Entries[0];

        NoteDocument.DeleteSubTask(entry.SubTasks[0]);

        Assert.Equal(EntryStatus.Done, entry.Status);
        Assert.DoesNotContain("birinci alt", doc.Serialize());
    }

    [Fact]
    public void Editing_a_sub_task_keeps_its_marker_and_gap()
    {
        var doc = NoteDocument.Parse(WithSubTasks);

        NoteDocument.SetSubTaskText(doc.Days[0].Entries[0].SubTasks[1], "değişti");

        Assert.Equal(WithSubTasks.Replace("ikinci alt", "değişti"), doc.Serialize());
    }

    [Fact]
    public void Done_parent_with_an_open_sub_task_counts_as_open_work()
    {
        var entry = NoteDocument.Parse("----01/10/2026\r\n👍[Done]ana\r\n\t🚨alt\r\n").Days[0].Entries[0];

        Assert.True(entry.HasOpenWork);
        Assert.False(entry.IsFinished);
    }

    [Theory]
    [InlineData("🚨ana\r\n", false)]
    [InlineData("🚨ana\r\n\t🚨bir\r\n\t🚨iki\r\n", false)]
    [InlineData("🚨ana\r\n\t👍[Done]bir\r\n\t🚨iki\r\n", true)]
    [InlineData("👍[Done]ana\r\n\t👍[Done]bir\r\n\t🚨iki\r\n", true)]
    [InlineData("👍[Done]ana\r\n\t👍[Done]bir\r\n\t👍[Done]iki\r\n", false)]
    public void Partly_done_means_some_sub_tasks_are_finished_and_some_are_not(string entryText, bool expected)
    {
        var entry = NoteDocument.Parse("----01/10/2026\r\n" + entryText).Days[0].Entries[0];

        Assert.Equal(expected, entry.IsPartlyDone);
    }

    [Fact]
    public void Search_does_not_match_the_done_marker_of_a_sub_task()
    {
        var entry = NoteDocument.Parse(WithSubTasks).Days[0].Entries[0];

        Assert.False(TextSearch.Matches(entry, TextSearch.Fold("done")));
        Assert.True(TextSearch.Matches(entry, TextSearch.Fold("ikinci")));
    }

    [Theory]
    [InlineData("şifre", "SIFRE")]
    [InlineData("LİSANS güncelleme", "lisans")]
    [InlineData("IŞIK", "isik")]
    public void Search_ignores_case_and_turkish_letters(string text, string query)
    {
        var entry = NoteDocument.Parse($"----01/10/2026\r\n🚨{text}\r\n").Days[0].Entries[0];

        Assert.True(TextSearch.Matches(entry, TextSearch.Fold(query)));
    }

    [Fact]
    public void Search_looks_in_body_lines()
    {
        var entry = NoteDocument.Parse(Sample).Days[1].Entries[1];

        Assert.True(TextSearch.Matches(entry, TextSearch.Fold("2601p")));
        Assert.False(TextSearch.Matches(entry, TextSearch.Fold("yok böyle")));
    }
}
