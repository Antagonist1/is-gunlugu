namespace IsGunlugu.Core;

/// <summary>What a line of a task is while it is being written: its title, a note under it, a sub-task, or a note under a sub-task.</summary>
public enum LineKind { Title, Note, SubTask, SubNote }

public sealed class TaskLine(LineKind kind, string text)
{
    public LineKind Kind { get; set; } = kind;
    public string Text { get; set; } = text;
}

/// <summary>
/// The text typed for a task (as <see cref="NoteDocument.EditableText"/> writes it) as lines with a kind each, and back.
/// The visual writing box shows these; the text it gives back is the same the plain box would have, so saving is unchanged.
/// </summary>
public static class TaskLines
{
    public static List<TaskLine> Parse(string typed)
    {
        var lines = new List<TaskLine>();
        var raw = (typed ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        lines.Add(new TaskLine(LineKind.Title, raw[0]));

        var inSubTask = false;
        foreach (var line in raw.Skip(1))
        {
            // "- " with nothing after it is a sub-task line still being written; a bare "-" is just a dash.
            if (NoteDocument.IsTypedSubTask(line) || line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                lines.Add(new TaskLine(LineKind.SubTask, line.TrimStart().Substring(2).Trim()));
                inSubTask = true;
            }
            else if (inSubTask && NoteDocument.IsIndentedDeeper(line, ""))
            {
                lines.Add(new TaskLine(LineKind.SubNote, line.Trim()));
            }
            else
            {
                lines.Add(new TaskLine(LineKind.Note, line));
                inSubTask = false;
            }
        }
        return lines;
    }

    public static string Join(IEnumerable<TaskLine> lines) => string.Join("\n", lines.Select(l => l.Kind switch
    {
        LineKind.SubTask => "- " + l.Text,
        LineKind.SubNote => "  " + l.Text,
        _ => l.Text,
    }).ToArray());

    /// <summary>
    /// Before saving: sub-task lines left empty ("-") go, and so do blank lines at the end. Blank lines inside stay, as
    /// they may already be in the note.
    /// </summary>
    public static string TrimEmpty(string typed)
    {
        var raw = (typed ?? "").Replace("\r\n", "\n").Split('\n').ToList();
        var kept = new List<string> { raw[0] };
        kept.AddRange(raw.Skip(1).Where(l => l.Trim() != "-"));
        while (kept.Count > 1 && kept[kept.Count - 1].Trim().Length == 0) kept.RemoveAt(kept.Count - 1);
        return string.Join("\n", kept.ToArray());
    }

    /// <summary>The line Shift+Enter opens: a note at the same level, under the task or under the sub-task.</summary>
    public static LineKind KindAfter(LineKind current) =>
        current is LineKind.SubTask or LineKind.SubNote ? LineKind.SubNote : LineKind.Note;

    /// <summary>"- " typed at the start of a note makes it a sub-task; the dash and space go.</summary>
    public static bool TryPromote(TaskLine line)
    {
        if (line.Kind is not (LineKind.Note or LineKind.SubNote) || !line.Text.StartsWith("- ", StringComparison.Ordinal)) return false;
        line.Kind = LineKind.SubTask;
        line.Text = line.Text.Substring(2);
        return true;
    }

    /// <summary>Backspace at the start of a sub-task: it becomes a note, under the sub-task before it if there is one.</summary>
    public static void Demote(IList<TaskLine> lines, int index)
    {
        if (lines[index].Kind != LineKind.SubTask) return;
        var underSubTask = index > 0 && lines[index - 1].Kind is LineKind.SubTask or LineKind.SubNote;
        lines[index].Kind = underSubTask ? LineKind.SubNote : LineKind.Note;
    }

    /// <summary>The first line is the title; a sub-task note with no sub-task above it is the task's note.</summary>
    public static void Normalize(IList<TaskLine> lines)
    {
        if (lines.Count == 0) return;
        lines[0].Kind = LineKind.Title;
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].Kind == LineKind.SubNote && lines[i - 1].Kind is not (LineKind.SubTask or LineKind.SubNote)) lines[i].Kind = LineKind.Note;
        }
    }
}
