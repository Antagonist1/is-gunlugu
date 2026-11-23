using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IsGunlugu.Core;

public enum EntryStatus { Done, Open, Note, Dropped }

public sealed class Entry
{
    internal const string DoneMarker = "👍[Done]";
    internal const string OpenMarker = "🚨";

    /// <summary>Given up without being deleted: no longer open, never finished.</summary>
    internal const string DroppedMarker = "🚫[İptal]";

    public string Lead { get; internal set; } = "";
    public string Marker { get; internal set; } = "";
    public string Gap { get; internal set; } = "";
    public string Text { get; internal set; } = "";

    /// <summary>The time mark at the end of the line, " ⏰[14:00 30dk 15dk]", as written; empty when none.</summary>
    public string Alarm { get; internal set; } = "";

    /// <summary>The task's time and reminders, read from <see cref="Alarm"/>.</summary>
    public TaskTime? Time => Alarm.Length == 0 ? null : TaskTime.Read(Alarm);

    /// <summary>Lines under the entry, exactly as written. May end with blank lines.</summary>
    public List<string> Body { get; } = new();

    public EntryStatus Status =>
        Marker.Length == 0 ? EntryStatus.Note :
        Marker.Contains("👍") ? EntryStatus.Done :
        Marker.Contains("🚫") ? EntryStatus.Dropped : EntryStatus.Open;

    public bool IsDropped => Status == EntryStatus.Dropped;

    /// <summary>The day this was finished: the date in its mark, else the day it sits under. Meaningless while open.</summary>
    public DateTime DoneOn(DateTime dayDate, DateTime today) => DoneDate.On(Marker, dayDate, today);

    public IEnumerable<string> VisibleBody => Body.Take(Body.Count - TrailingBlankCount);

    /// <summary>
    /// Lines under the entry in file order. Indented lines with a marker are sub-tasks, and lines
    /// indented deeper than a sub-task are its description.
    /// </summary>
    public IList<BodyPart> Parts
    {
        get
        {
            // Read for every count and check on screen: parsing again each time made a large file slow on every click.
            if (_parts is not null && IsBodyAsParsed()) return _parts;

            _partsOf = Body.ToArray();
            return _parts = ParseParts();
        }
    }

    private IList<BodyPart>? _parts;
    private string[] _partsOf = [];

    private bool IsBodyAsParsed()
    {
        if (_partsOf.Length != Body.Count) return false;
        for (var i = 0; i < Body.Count; i++)
        {
            if (!string.Equals(_partsOf[i], Body[i], StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private IList<BodyPart> ParseParts()
    {
        {
            var parts = new List<BodyPart>();
            SubTask? current = null;
            foreach (var line in VisibleBody)
            {
                var task = NoteDocument.ParseSubTask(this, parts.Count, line);
                if (task is not null)
                {
                    current = task;
                    parts.Add(new BodyPart(line, task));
                }
                else if (current is not null && NoteDocument.IsIndentedDeeper(line, current.Lead))
                {
                    current.DescriptionLines.Add(line.Trim());
                    parts.Add(new BodyPart(line, null, isDescription: true));
                }
                else
                {
                    current = null;
                    parts.Add(new BodyPart(line, null));
                }
            }
            return parts;
        }
    }

    public IList<SubTask> SubTasks => Parts.Where(p => p.Task is not null).Select(p => p.Task!).ToList();

    /// <summary>The entry's own note lines: what is under it and belongs to no sub-task.</summary>
    public string Description => string.Join("\n", Parts.Where(p => p.IsNote).Select(p => p.Line.Trim()).ToArray());

    /// <summary>The sub-tasks still in play: one that was given up is neither open nor done, and is not counted.</summary>
    public IList<SubTask> ActiveSubTasks => SubTasks.Where(t => !t.IsDropped).ToList();

    public bool HasOpenWork => !IsDropped && (Status == EntryStatus.Open || ActiveSubTasks.Any(t => !t.IsDone));

    /// <summary>The undated block lists work still to do: finished work and plain notes stay on their day.</summary>
    public bool CanBeUndated => HasOpenWork;

    public bool IsFinished => Status == EntryStatus.Done && !HasOpenWork;

    /// <summary>
    /// The order a day falls into by itself: half-done work, then what has not been started, then what is finished,
    /// and last what was given up.
    /// </summary>
    public int StateRank => IsDropped ? 3 : IsPartlyDone ? 0 : IsFinished ? 2 : 1;

    /// <summary>Some sub-tasks are done and some are not.</summary>
    public bool IsPartlyDone
    {
        get
        {
            if (IsDropped) return false;
            var tasks = ActiveSubTasks;
            return tasks.Any(t => t.IsDone) && tasks.Any(t => !t.IsDone);
        }
    }

    internal int TrailingBlankCount
    {
        get
        {
            var count = 0;
            for (var i = Body.Count - 1; i >= 0 && string.IsNullOrWhiteSpace(Body[i]); i--) count++;
            return count;
        }
    }

    internal string FirstLine => Lead + Marker + Gap + Text + Alarm;
}

public sealed class BodyPart(string line, SubTask? task, bool isDescription = false)
{
    public string Line { get; } = line;
    public SubTask? Task { get; } = task;
    public bool IsDescription { get; } = isDescription;
    public bool IsNote => Task is null && !IsDescription && !string.IsNullOrWhiteSpace(Line);
}

public sealed class SubTask(Entry owner, int bodyIndex, string lead, string marker, string gap, string text)
{
    public Entry Owner { get; } = owner;
    public int BodyIndex { get; } = bodyIndex;
    public string Lead { get; } = lead;
    public string Marker { get; } = marker;
    public string Gap { get; } = gap;
    public string Text { get; } = text;

    public List<string> DescriptionLines { get; } = new();

    public bool IsDone => Marker.Contains("👍");

    /// <summary>The day this was finished: the date in its mark, else the day its task sits under. Meaningless while open.</summary>
    public DateTime DoneOn(DateTime dayDate, DateTime today) => DoneDate.On(Marker, dayDate, today);

    /// <summary>Given up on its own, while the task it belongs to goes on.</summary>
    public bool IsDropped => Marker.Contains("🚫");

    internal int LineCount => 1 + DescriptionLines.Count;

    internal string LineWith(string newMarker, string newText) => Lead + newMarker + Gap + newText;
}

public sealed class Day
{
    /// <summary>The date of the "tarihsiz" block. Later than any real date, so that block stays last in the file.</summary>
    public static readonly DateTime Undated = DateTime.MaxValue.Date;

    public bool IsUndated => Date == Undated;

    public string Header { get; internal set; } = "";
    public DateTime Date { get; internal set; }

    /// <summary>Blank lines between the header and the first entry.</summary>
    internal List<string> Lead { get; } = new();

    public List<Entry> Entries { get; } = new();
}

/// <summary>
/// The notes file as a tree. Everything keeps its original text, so a file that is only
/// read serializes back byte for byte and an edit changes nothing but the edited lines.
/// </summary>
public sealed class NoteDocument
{
    private static readonly Regex HeaderPattern = new(@"^\s*-{3,}\s*(\d{1,2}/\d{1,2}/\d{4})\s*$");

    private static readonly Regex UndatedHeaderPattern = new(@"^\s*-{3,}\s*(tarihsiz|TARİHSİZ)\s*$", RegexOptions.IgnoreCase);

    private const string UndatedHeader = "----tarihsiz";

    private static readonly Regex EntryPattern = new(
        @"^(?<lead>[ \t]*)(?<marker>(?:🛠️?\s*)?👍(?:\s*\[done(?:\s+\d{1,2}/\d{1,2}(?:/\d{4})?)?\])?|🚫(?:\s*\[[İIiı]ptal\])?|🚨)(?<gap>\s*)(?<text>.*)$",
        RegexOptions.IgnoreCase);

    private static readonly string[] DateFormats = { "dd/MM/yyyy", "d/M/yyyy" };

    private readonly List<string> _preamble = new();
    private string _newLine = "\r\n";
    private bool _endsWithNewLine = true;

    public List<Day> Days { get; } = new();

    public static NoteDocument Parse(string text)
    {
        var doc = new NoteDocument();
        var crlf = Regex.Matches(text, "\r\n").Count;
        var lf = Regex.Matches(text, "(?<!\r)\n").Count;
        doc._newLine = lf > crlf ? "\n" : "\r\n";

        var lines = Regex.Split(text, "\r\n|\n|\r").ToList();
        var lastIsEmpty = lines[lines.Count - 1].Length == 0;
        doc._endsWithNewLine = text.Length == 0 || lastIsEmpty;
        if (lastIsEmpty) lines.RemoveAt(lines.Count - 1);

        Day? day = null;
        Entry? entry = null;
        foreach (var line in lines)
        {
            if (TryParseHeader(line, out var date))
            {
                day = new Day { Header = line, Date = date };
                entry = null;
                doc.Days.Add(day);
            }
            else if (day is null)
            {
                doc._preamble.Add(line);
            }
            else if (TryParseEntry(line, out var parsed) && (entry is null || parsed.Lead.Length == 0))
            {
                entry = parsed;
                TaskTime.Split(entry);
                day.Entries.Add(entry);
            }
            else if (entry is not null)
            {
                entry.Body.Add(line);
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                day.Lead.Add(line);
            }
            else
            {
                entry = new Entry { Text = line };
                day.Entries.Add(entry);
            }
        }

        return doc;
    }

    public string Serialize()
    {
        var lines = new List<string>(_preamble);
        foreach (var day in Days)
        {
            lines.Add(day.Header);
            lines.AddRange(day.Lead);
            foreach (var entry in day.Entries)
            {
                lines.Add(entry.FirstLine);
                lines.AddRange(entry.Body);
            }
        }

        var builder = new StringBuilder(string.Join(_newLine, lines));
        if (_endsWithNewLine && lines.Count > 0) builder.Append(_newLine);
        return builder.ToString();
    }

    /// <summary>Ticks a task, and its open sub-tasks with it, on <paramref name="on"/> (today when left out).</summary>
    public static void SetDone(Entry entry, bool done, DateTime? on = null)
    {
        if (entry.Status == EntryStatus.Note) return;
        var mark = DoneDate.Marker(on ?? DateTime.Today);
        entry.Marker = done ? mark : Entry.OpenMarker;
        if (!done) return;

        // Sub-tasks finished before keep their own day.
        foreach (var task in entry.ActiveSubTasks.Where(t => !t.IsDone))
        {
            entry.Body[task.BodyIndex] = task.LineWith(mark, task.Text);
        }
    }

    /// <summary>
    /// Gives a task up without deleting it, or takes it back. Taken back, it is open again, or finished when all
    /// its sub-tasks already are.
    /// </summary>
    public static void SetDropped(Entry entry, bool dropped, DateTime? on = null)
    {
        if (entry.Status == EntryStatus.Note) return;
        entry.Marker = dropped ? Entry.DroppedMarker : Entry.OpenMarker;
        if (!dropped) SyncWithSubTasks(entry, on);
    }

    /// <summary>
    /// Ticks one sub-task on <paramref name="on"/> (today when left out) and moves it under the others, so what is left
    /// stays on top; the untick takes the date away and puts it back under the last open one.
    /// </summary>
    public static void SetSubTaskDone(SubTask task, bool done, DateTime? on = null)
    {
        task.Owner.Body[task.BodyIndex] = task.LineWith(done ? DoneDate.Marker(on ?? DateTime.Today) : Entry.OpenMarker, task.Text);
        PlaceByState(task, done);
        SyncWithSubTasks(task.Owner, on);
    }

    /// <summary>Finished or given up: to the bottom. Open again: right under the open ones, above the finished.</summary>
    private static void PlaceByState(SubTask task, bool closed)
    {
        if (closed)
        {
            MoveSubTask(task, int.MaxValue);
            return;
        }
        var others = task.Owner.SubTasks.Where(t => t.BodyIndex != task.BodyIndex).ToList();
        MoveSubTask(task, others.FindLastIndex(t => !t.IsDone && !t.IsDropped) + 1);
    }

    /// <summary>
    /// Puts every task's finished and given-up sub-tasks under its open ones, each group in its old order. For files
    /// written before sub-tasks moved on their own. True when anything moved.
    /// </summary>
    public static bool SinkClosedSubTasks(NoteDocument document)
    {
        var moved = false;
        foreach (var entry in document.Days.SelectMany(d => d.Entries))
        {
            // Only where an open one sits under a closed one; otherwise nothing moves, notes between them included.
            if (!entry.SubTasks.SkipWhile(t => !t.IsDone && !t.IsDropped).Any(t => !t.IsDone && !t.IsDropped)) continue;
            var before = entry.Body.ToList();
            var count = entry.SubTasks.Count;
            var closedCount = entry.SubTasks.Count(t => t.IsDone || t.IsDropped);
            // The ones moved already sit at the end; the next in the old order is the first closed one before them.
            for (var placed = 0; placed < closedCount; placed++)
                MoveSubTask(entry.SubTasks.Take(count - placed).First(t => t.IsDone || t.IsDropped), int.MaxValue);
            moved |= !before.SequenceEqual(entry.Body);
        }
        return moved;
    }

    /// <summary>Changes the day a finished task was finished; its sub-tasks keep theirs. Open or given-up tasks stay as they are.</summary>
    public static void SetDoneDate(Entry entry, DateTime on, DateTime dayDate, DateTime today)
    {
        if (entry.Status != EntryStatus.Done) return;
        entry.Marker = DoneDate.Marker(on, dayDate, today);
    }

    /// <summary>Changes the day a finished sub-task was finished; nothing else moves.</summary>
    public static void SetSubTaskDoneDate(SubTask task, DateTime on, DateTime dayDate, DateTime today)
    {
        if (!task.IsDone) return;
        task.Owner.Body[task.BodyIndex] = task.LineWith(DoneDate.Marker(on, dayDate, today), task.Text);
    }

    /// <summary>Gives one sub-task up, or takes it back as open; the task then follows the sub-tasks still in play.</summary>
    public static void SetSubTaskDropped(SubTask task, bool dropped, DateTime? on = null)
    {
        task.Owner.Body[task.BodyIndex] = task.LineWith(dropped ? Entry.DroppedMarker : Entry.OpenMarker, task.Text);
        PlaceByState(task, dropped);
        SyncWithSubTasks(task.Owner, on);
    }

    public static void SetSubTaskText(SubTask task, string text)
    {
        var lines = SplitInput(text);
        if (lines.Count == 0) throw new ArgumentException("Boş alt iş kaydedilemez.", nameof(text));

        var body = task.Owner.Body;
        body.RemoveRange(task.BodyIndex, task.LineCount);
        body.InsertRange(task.BodyIndex, SubTaskLines(lines, task.Lead, task.Marker, task.Gap));
    }

    /// <summary>The first line is the sub-task, the lines after it are its description.</summary>
    public static void AddSubTask(Entry entry, string text)
    {
        var lines = SplitInput(text);
        if (lines.Count == 0) throw new ArgumentException("Boş alt iş eklenemez.", nameof(text));

        var index = entry.Body.Count - entry.TrailingBlankCount;
        entry.Body.InsertRange(index, SubTaskLines(lines, "\t", Entry.OpenMarker, ""));
        SyncWithSubTasks(entry);
    }

    /// <summary>Replaces the entry's note lines, in the place of the first one or above the sub-tasks. Empty text removes them.</summary>
    public static void SetDescription(Entry entry, string text)
    {
        var old = entry.Parts.Select((part, index) => new { part, index }).Where(x => x.part.IsNote).Select(x => x.index).ToList();
        var at = old.Count > 0 ? old[0] : 0;
        for (var i = old.Count - 1; i >= 0; i--) entry.Body.RemoveAt(old[i]);

        entry.Body.InsertRange(at, SplitInput(text).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));
    }

    public static void DeleteSubTask(SubTask task, DateTime? on = null)
    {
        task.Owner.Body.RemoveRange(task.BodyIndex, task.LineCount);
        SyncWithSubTasks(task.Owner, on);
    }

    /// <summary>
    /// Puts a sub-task, with its description lines, at another place among its siblings: <paramref name="toIndex"/> is
    /// its place in the new order. The task's own note lines stay where they are.
    /// </summary>
    public static void MoveSubTask(SubTask task, int toIndex)
    {
        var entry = task.Owner;
        var tasks = entry.SubTasks;
        var from = tasks.ToList().FindIndex(t => t.BodyIndex == task.BodyIndex);
        var others = tasks.Where(t => t.BodyIndex != task.BodyIndex).ToList();
        toIndex = Math.Max(0, Math.Min(toIndex, others.Count));
        if (from < 0 || others.Count == 0 || toIndex == from) return;

        var moving = tasks[from];
        var block = entry.Body.GetRange(moving.BodyIndex, moving.LineCount);
        entry.Body.RemoveRange(moving.BodyIndex, moving.LineCount);

        int StartAfterRemoval(SubTask t) => t.BodyIndex > moving.BodyIndex ? t.BodyIndex - moving.LineCount : t.BodyIndex;
        var last = others[others.Count - 1];
        var at = toIndex < others.Count ? StartAfterRemoval(others[toIndex]) : StartAfterRemoval(last) + last.LineCount;
        entry.Body.InsertRange(at, block);
    }

    /// <summary>
    /// The task as it is typed in the add box: the title, the description, every sub-task as a "- " line with its
    /// description indented under it. No marks: whether a sub-task is done is kept when the text is saved back.
    /// </summary>
    public static string EditableText(Entry entry)
    {
        var lines = new List<string> { entry.Text };
        foreach (var part in entry.Parts)
        {
            if (part.Task is { } task) lines.Add("- " + task.Text);
            else if (part.IsDescription) lines.Add("  " + part.Line.Trim());
            else lines.Add(part.Line);
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Saves text written as <see cref="EditableText"/>. A sub-task keeps its mark and finish date when its line is
    /// kept, even moved; when only renames are left over, they pair up in order. Any other new line is an open
    /// sub-task, and a line taken out is deleted. The task's own mark then follows its sub-tasks.
    /// </summary>
    public static void SetTypedText(Entry entry, string text, DateTime today)
    {
        var lines = SplitInput(text);
        if (lines.Count == 0) throw new ArgumentException("Boş not kaydedilemez.", nameof(text));
        if (IsTypedSubTask(lines[0])) throw new ArgumentException("İlk satır işin başlığı olmalı, alt iş değil.", nameof(text));

        // What was typed: plain lines, and sub-tasks with the indented lines under them.
        var items = new List<(string? Plain, List<string>? Task)>();
        List<string>? current = null;
        foreach (var line in lines.Skip(1))
        {
            if (IsTypedSubTask(line))
            {
                current = new List<string> { line.TrimStart().Substring(2).Trim() };
                items.Add((null, current));
            }
            else if (current is not null && IsIndentedDeeper(line, ""))
            {
                current.Add(line.Trim());
            }
            else
            {
                current = null;
                items.Add((line, null));
            }
        }

        var typed = items.Where(i => i.Task is not null).Select(i => i.Task!).ToList();
        var old = entry.SubTasks;
        var match = new SubTask?[typed.Count];
        var used = new HashSet<SubTask>();
        for (var i = 0; i < typed.Count; i++)
        {
            var same = old.FirstOrDefault(t => !used.Contains(t) && t.Text == typed[i][0]);
            if (same is null) continue;
            match[i] = same;
            used.Add(same);
        }

        // Renames: between two kept lines, the changed lines take the old ones there in order. Where lines were taken
        // out of that stretch, which is which cannot be told, so they stay new and open rather than wrongly done.
        var oldIndex = old.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);
        var start = 0;
        var from = -1;
        for (var i = 0; i <= typed.Count; i++)
        {
            if (i < typed.Count && match[i] is null) continue;
            var to = i < typed.Count ? oldIndex[match[i]!] : old.Count;
            var newGap = Enumerable.Range(start, i - start).ToList();
            var oldGap = to > from ? old.Skip(from + 1).Take(to - from - 1).Where(t => !used.Contains(t)).ToList() : new List<SubTask>();
            if (oldGap.Count <= newGap.Count)
            {
                for (var k = 0; k < oldGap.Count; k++)
                {
                    match[newGap[k]] = oldGap[k];
                    used.Add(oldGap[k]);
                }
            }
            if (i < typed.Count) from = to;
            start = i + 1;
        }

        var body = new List<string>();
        var n = 0;
        foreach (var item in items)
        {
            if (item.Task is null)
            {
                body.Add(item.Plain!);
                continue;
            }
            var was = match[n++];
            body.AddRange(was is null
                ? SubTaskLines(item.Task, "\t", Entry.OpenMarker, "")
                : SubTaskLines(item.Task, was.Lead, was.Marker, was.Gap));
        }

        var trailing = entry.Body.Skip(entry.Body.Count - entry.TrailingBlankCount).ToList();
        entry.Text = lines[0].Trim();
        entry.Body.Clear();
        entry.Body.AddRange(body);
        entry.Body.AddRange(trailing);
        SyncWithSubTasks(entry, today);
    }

    /// <summary>A typed line that starts a sub-task: "- " and some text after it.</summary>
    public static bool IsTypedSubTask(string line) =>
        line.TrimStart().StartsWith("- ", StringComparison.Ordinal) && line.TrimStart().Substring(2).Trim().Length > 0;

    private static IEnumerable<string> SubTaskLines(IList<string> lines, string lead, string marker, string gap)
    {
        yield return lead + marker + gap + lines[0].Trim();
        foreach (var line in lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            yield return lead + "\t" + line.Trim();
        }
    }

    internal static bool IsIndentedDeeper(string line, string lead) =>
        !string.IsNullOrWhiteSpace(line) && IndentWidth(line) > IndentWidth(lead);

    private static int IndentWidth(string text)
    {
        var width = 0;
        foreach (var c in text)
        {
            if (c == '\t') width += 4;
            else if (c == ' ') width++;
            else break;
        }
        return width;
    }

    /// <summary>A task with sub-tasks is done exactly when all of them are.</summary>
    /// <summary>The task follows its sub-tasks; finished by the last of them, it takes that day.</summary>
    private static void SyncWithSubTasks(Entry entry, DateTime? on = null)
    {
        // Sub-tasks that were given up have no say; with none left in play the task keeps its own mark.
        var tasks = entry.ActiveSubTasks;
        if (tasks.Count == 0 || entry.Status == EntryStatus.Note || entry.IsDropped) return;

        var allDone = tasks.All(t => t.IsDone);
        if (allDone && entry.Status == EntryStatus.Open) entry.Marker = DoneDate.Marker(on ?? DateTime.Today);
        else if (!allDone && entry.Status == EntryStatus.Done) entry.Marker = Entry.OpenMarker;
    }

    internal static SubTask? ParseSubTask(Entry owner, int index, string line)
    {
        if (!TryParseEntry(line, out var parsed) || parsed.Lead.Length == 0) return null;

        return new SubTask(owner, index, parsed.Lead, parsed.Marker, parsed.Gap, parsed.Text);
    }

    /// <summary>Gives the task a time and reminders, or takes them away with null. The mark goes at the line's end.</summary>
    public static void SetTime(Entry entry, TaskTime? time) => entry.Alarm = time?.Write() ?? "";

    public static void SetText(Entry entry, string text)
    {
        var lines = SplitInput(text);
        if (lines.Count == 0) throw new ArgumentException("Boş not kaydedilemez.", nameof(text));

        var trailing = entry.Body.Skip(entry.Body.Count - entry.TrailingBlankCount).ToList();
        entry.Text = lines[0];
        entry.Body.Clear();
        entry.Body.AddRange(lines.Skip(1));
        entry.Body.AddRange(trailing);
    }

    public Entry AddEntry(DateTime date, string text, bool done)
    {
        var lines = SplitInput(text);
        if (lines.Count == 0) throw new ArgumentException("Boş not eklenemez.", nameof(text));

        var marker = done ? DoneDate.Marker(date == Day.Undated ? DateTime.Today : date) : Entry.OpenMarker;
        var entry = new Entry { Marker = marker, Text = lines[0] }; TaskTime.Split(entry);
        entry.Body.AddRange(TypedBodyLines(lines.Skip(1), marker));

        return PlaceEntry(date, entry);
    }

    /// <summary>
    /// Adds a task written in separate fields: the description is plain text under the title (even if it starts
    /// with "- "), every sub-task gets its own marker line.
    /// </summary>
    public Entry AddEntry(DateTime date, string title, string? description, IReadOnlyList<string> subTasks, bool done)
    {
        // Lines typed under the title with Alt+Enter follow the old typed-input rules and sit between the two.
        var typed = SplitInput(title ?? "");
        if (typed.Count == 0 || typed[0].Trim().Length == 0) throw new ArgumentException("Boş not eklenemez.", nameof(title));

        var marker = done ? DoneDate.Marker(date == Day.Undated ? DateTime.Today : date) : Entry.OpenMarker;
        var entry = new Entry { Marker = marker, Text = typed[0].Trim() }; TaskTime.Split(entry);
        foreach (var line in SplitInput(description ?? ""))
            entry.Body.Add(line);
        entry.Body.AddRange(TypedBodyLines(typed.Skip(1), marker));
        foreach (var subTask in subTasks)
        {
            var text = (subTask ?? "").Trim();
            if (text.Length > 0) entry.Body.Add("	" + marker + text);
        }
        return PlaceEntry(date, entry);
    }

    private Entry PlaceEntry(DateTime date, Entry entry)
    {
        var existing = Days.LastOrDefault(d => d.Date == date);
        var day = existing ?? InsertDay(date);
        if (day.Entries.Count > 0)
        {
            // The blank lines that separate this day from the next one stay at the end of the day.
            var previous = day.Entries[day.Entries.Count - 1];
            var trailing = previous.TrailingBlankCount;
            entry.Body.AddRange(previous.Body.Skip(previous.Body.Count - trailing));
            previous.Body.RemoveRange(previous.Body.Count - trailing, trailing);
        }
        day.Entries.Add(entry);
        if (existing is null && day != Days[Days.Count - 1]) EnsureTrailingBlank(day);
        return entry;
    }

    /// <summary>
    /// While typing a new task: "- x" becomes a sub-task and an indented line under it its description.
    /// Other lines stay as written.
    /// </summary>
    private static IEnumerable<string> TypedBodyLines(IEnumerable<string> lines, string marker)
    {
        var afterSubTask = false;
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                afterSubTask = true;
                yield return "\t" + marker + line.TrimStart().Substring(2).Trim();
            }
            else if (afterSubTask && IsIndentedDeeper(line, ""))
            {
                yield return "\t\t" + line.Trim();
            }
            else
            {
                afterSubTask = false;
                yield return line;
            }
        }
    }

    /// <summary>
    /// Moves a task with everything under it. It lands before <paramref name="before"/>, or at the end of
    /// <paramref name="date"/> when that is null. A day left with no tasks disappears from the file.
    /// </summary>
    public void MoveEntry(Entry entry, DateTime date, Entry? before)
    {
        if (before == entry) return;

        var source = Days.First(d => d.Entries.Contains(entry));
        var target = before is not null
            ? Days.First(d => d.Entries.Contains(before))
            : Days.LastOrDefault(d => d.Date == date);

        var sourceIndex = source.Entries.IndexOf(entry);
        var wasLast = sourceIndex == source.Entries.Count - 1;
        var trailing = new List<string>();
        if (wasLast)
        {
            trailing = entry.Body.Skip(entry.Body.Count - entry.TrailingBlankCount).ToList();
            entry.Body.RemoveRange(entry.Body.Count - trailing.Count, trailing.Count);
        }

        source.Entries.RemoveAt(sourceIndex);
        if (wasLast && source.Entries.Count > 0) source.Entries[source.Entries.Count - 1].Body.AddRange(trailing);
        if (source.Entries.Count == 0 && source != target) Days.Remove(source);

        var isNewDay = target is null;
        var day = target ?? InsertDay(date);
        var index = before is not null ? day.Entries.IndexOf(before) : day.Entries.Count;
        if (index == day.Entries.Count && index > 0)
        {
            // The blank lines that separate the day from the next one stay at the end of the day.
            var previous = day.Entries[index - 1];
            var blanks = previous.TrailingBlankCount;
            entry.Body.AddRange(previous.Body.Skip(previous.Body.Count - blanks));
            previous.Body.RemoveRange(previous.Body.Count - blanks, blanks);
        }
        else if (day.Entries.Count == 0)
        {
            entry.Body.AddRange(trailing);
        }

        day.Entries.Insert(index, entry);
        if (isNewDay && day != Days[Days.Count - 1]) EnsureTrailingBlank(day);
    }

    /// <summary>
    /// Puts a task whose state just changed, or that just arrived, with the others in that state: at the end of
    /// them, before the first task of a later state. Nothing else moves, so an order set by hand survives.
    /// Undated work has no such order: it is a list the user keeps as they like.
    /// </summary>
    public void PlaceByState(Entry entry)
    {
        var day = Days.First(d => d.Entries.Contains(entry));
        if (day.IsUndated) return;

        var sameDate = Days.Where(d => d.Date == day.Date).SelectMany(d => d.Entries).ToList();
        var next = sameDate.Where(e => e != entry).FirstOrDefault(e => e.StateRank > entry.StateRank);
        var index = sameDate.IndexOf(entry);
        var after = index + 1 < sameDate.Count ? sameDate[index + 1] : null;
        if (next != after) MoveEntry(entry, day.Date, next);
    }

    /// <summary>Orders every dated day by state, keeping each state's own order. Undated work stays as it is.</summary>
    public void SortDaysByState()
    {
        foreach (var day in Days.Where(d => !d.IsUndated && d.Entries.Count > 1))
        {
            var sorted = day.Entries.OrderBy(e => e.StateRank).ToList();
            if (sorted.SequenceEqual(day.Entries)) continue;

            // The blank lines that separate the day from the next one stay at the end of the day.
            var last = day.Entries[day.Entries.Count - 1];
            var blanks = last.Body.Skip(last.Body.Count - last.TrailingBlankCount).ToList();
            last.Body.RemoveRange(last.Body.Count - blanks.Count, blanks.Count);
            sorted[sorted.Count - 1].Body.AddRange(blanks);

            day.Entries.Clear();
            day.Entries.AddRange(sorted);
        }
    }

    public void Delete(Entry entry)
    {
        var day = Days.First(d => d.Entries.Contains(entry));
        var index = day.Entries.IndexOf(entry);
        var trailing = entry.Body.Skip(entry.Body.Count - entry.TrailingBlankCount).ToList();
        day.Entries.RemoveAt(index);

        if (index == day.Entries.Count && index > 0) day.Entries[index - 1].Body.AddRange(trailing);
        if (day.Entries.Count == 0) Days.Remove(day);
    }

    private Day InsertDay(DateTime date)
    {
        var header = date == Day.Undated ? UndatedHeader : "----" + date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var day = new Day { Header = header, Date = date };
        var index = Days.FindLastIndex(d => d.Date <= date) + 1;

        if (index > 0) EnsureTrailingBlank(Days[index - 1]);

        Days.Insert(index, day);
        return day;
    }

    private static void EnsureTrailingBlank(Day day)
    {
        if (day.Entries.Count == 0)
        {
            if (day.Lead.Count == 0) day.Lead.Add("");
            return;
        }

        var last = day.Entries[day.Entries.Count - 1];
        if (last.TrailingBlankCount == 0) last.Body.Add("");
    }

    private static bool TryParseHeader(string line, out DateTime date)
    {
        if (UndatedHeaderPattern.IsMatch(line))
        {
            date = Day.Undated;
            return true;
        }

        date = default;
        var match = HeaderPattern.Match(line);
        return match.Success && DateTime.TryParseExact(
            match.Groups[1].Value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseEntry(string line, out Entry entry)
    {
        entry = null!;
        var match = EntryPattern.Match(line);
        if (!match.Success) return false;

        entry = new Entry
        {
            Lead = match.Groups["lead"].Value,
            Marker = match.Groups["marker"].Value,
            Gap = match.Groups["gap"].Value,
            Text = match.Groups["text"].Value,
        };
        return true;
    }

    private static List<string> SplitInput(string text)
    {
        var lines = Regex.Split(text, "\r\n|\n|\r").ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1])) lines.RemoveAt(lines.Count - 1);
        return lines;
    }
}
