using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>One line of <see cref="TaskLinesEditor"/>: what it is, what it says, and how it is drawn.</summary>
public sealed class EditorRow : ObservableObject
{
    private LineKind _kind;
    private string _text = "";
    private bool _isDone;
    private bool _isHidden;
    private double _fontSize = 14;
    private string _titleHint = "";

    public LineKind Kind
    {
        get => _kind;
        set { if (_kind == value) return; _kind = value; Notify(); Notify(nameof(Hint)); Notify(nameof(HasHint)); Notify(nameof(Label)); }
    }

    public string Text
    {
        get => _text;
        set { if (_text == value) return; _text = value ?? ""; Notify(); }
    }

    public bool IsDone { get => _isDone; set { if (_isDone == value) return; _isDone = value; Notify(); } }
    public bool IsHidden { get => _isHidden; set { if (_isHidden == value) return; _isHidden = value; Notify(); } }

    private bool _isSelected;

    /// <summary>Taken in a selection over several lines.</summary>
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; Notify(); } }
    public double FontSize { get => _fontSize; set { if (_fontSize == value) return; _fontSize = value; Notify(); } }

    public string TitleHint { get => _titleHint; set { _titleHint = value ?? ""; Notify(nameof(Hint)); Notify(nameof(HasHint)); } }

    public string Hint => _kind switch
    {
        LineKind.Title => _titleHint,
        LineKind.Note => "Açıklama",
        LineKind.SubTask => "Alt iş",
        _ => "Alt işin açıklaması",
    };

    public bool HasHint => Hint.Length > 0;

    public string Label => _kind switch
    {
        LineKind.Title => "İşin başlığı",
        LineKind.Note => "Açıklama",
        LineKind.SubTask => "Alt iş",
        _ => "Alt işin açıklaması",
    };

    public TaskLine ToLine() => new(_kind, _text);
}

/// <summary>
/// The visual writing box: the task as the list will show it, a box before the title, notes in grey, sub-tasks with
/// their own small box. It reads and writes the same text the plain box does ("- " sub-tasks, indented notes under
/// them), so whoever saves it does not know which box was used. Shift+Enter opens a note under the line, "- " at the
/// start of a note makes it a sub-task, Backspace at the start goes back.
/// </summary>
public partial class TaskLinesEditor : UserControl
{
    /// <summary>Where the modifier keys are read; a test can answer instead of the real keyboard.</summary>
    public static Func<ModifierKeys> Modifiers { get; set; } = () => Keyboard.Modifiers;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TaskLinesEditor),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) =>
        {
            var editor = (TaskLinesEditor)d;
            editor.TextFromOutside((string?)e.NewValue ?? "");
            editor.TextEdited?.Invoke();
        })
        { DefaultUpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });

    public static readonly DependencyProperty OnlyTitleProperty = DependencyProperty.Register(nameof(OnlyTitle), typeof(bool), typeof(TaskLinesEditor),
        new PropertyMetadata(false, (d, _) => ((TaskLinesEditor)d).Refresh()));

    public static readonly DependencyProperty DoneSubTasksProperty = DependencyProperty.Register(nameof(DoneSubTasks), typeof(IEnumerable<string>), typeof(TaskLinesEditor),
        new PropertyMetadata(null, (d, _) => ((TaskLinesEditor)d).Refresh()));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(TaskLinesEditor),
        new PropertyMetadata("", (d, _) => ((TaskLinesEditor)d).Refresh()));

    public static readonly DependencyProperty TextSizeProperty = DependencyProperty.Register(nameof(TextSize), typeof(double), typeof(TaskLinesEditor),
        new PropertyMetadata(14.0, (d, _) => ((TaskLinesEditor)d).Refresh()));

    private string _emitted = "";
    private LineKind _currentKind;
    private bool _rebuilding;

    public TaskLinesEditor()
    {
        InitializeComponent();
        Rows.Add(new EditorRow { Kind = LineKind.Title });
        Refresh();
    }

    public ObservableCollection<EditorRow> Rows { get; } = new();

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool OnlyTitle { get => (bool)GetValue(OnlyTitleProperty); set => SetValue(OnlyTitleProperty, value); }
    public IEnumerable<string>? DoneSubTasks { get => (IEnumerable<string>?)GetValue(DoneSubTasksProperty); set => SetValue(DoneSubTasksProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public double TextSize { get => (double)GetValue(TextSizeProperty); set => SetValue(TextSizeProperty, value); }

    /// <summary>Enter adds or saves; true for Ctrl+Enter, done.</summary>
    public event Action<bool>? Submit;

    /// <summary>The text changed, typed or set from outside.</summary>
    public event Action? TextEdited;

    /// <summary>Esc, when someone listens; otherwise Esc goes on to the window.</summary>
    public event Action? Cancel;

    /// <summary>The line the keyboard is on changed kind, or the keyboard moved: the hint under the box follows.</summary>
    public event Action? CurrentKindChanged;

    public LineKind CurrentKind => _currentKind;
    public int LineCount => Rows.Count;

    /// <summary>The task has its own note line, so "＋ Açıklama" has nothing to open.</summary>
    public bool HasNote => Rows.Any(r => r.Kind == LineKind.Note);

    public event Action? HasNoteChanged;

    public void FocusEnd() => FocusRow(Rows.Count - 1, int.MaxValue);

    /// <summary>Typing started while the list had the keyboard: the letter goes to the end of the title.</summary>
    public void AppendToTitle(string text)
    {
        Rows[0].Text += text;
        Changed();
        FocusRow(0, int.MaxValue);
    }

    /// <summary>"＋ Alt iş": an empty sub-task line at the end, with the keyboard on it.</summary>
    public void AddSubTaskLine()
    {
        var last = Rows[Rows.Count - 1];
        if (last.Kind == LineKind.SubTask && last.Text.Length == 0)
        {
            FocusRow(Rows.Count - 1, 0);
            return;
        }
        Rows.Add(new EditorRow { Kind = LineKind.SubTask });
        Changed();
        FocusRow(Rows.Count - 1, 0);
    }

    /// <summary>"＋ Açıklama": the keyboard goes to the note right under the title; one is opened when there is none.</summary>
    public void FocusNote()
    {
        if (Rows.Count < 2 || Rows[1].Kind != LineKind.Note)
        {
            Rows.Insert(1, new EditorRow { Kind = LineKind.Note });
            Changed();
        }
        FocusRow(1, int.MaxValue);
    }

    // ---- The text, in and out ----

    private void TextFromOutside(string text)
    {
        if (text == _emitted) return;
        _rebuilding = true;
        Rows.Clear();
        foreach (var line in TaskLines.Parse(text)) Rows.Add(new EditorRow { Kind = line.Kind, Text = line.Text });
        _rebuilding = false;
        _emitted = text;
        Refresh();
        HasNoteChanged?.Invoke();
    }

    /// <summary>After every change of the lines: the text goes out, and the drawing follows the kinds.</summary>
    private void Changed()
    {
        var lines = Rows.Select(r => r.ToLine()).ToList();
        TaskLines.Normalize(lines);
        for (var i = 0; i < Rows.Count; i++) Rows[i].Kind = lines[i].Kind;
        _emitted = TaskLines.Join(lines);
        SetCurrentValue(TextProperty, _emitted);
        Refresh();
        HasNoteChanged?.Invoke();
    }

    private void Refresh()
    {
        var done = new HashSet<string>(DoneSubTasks ?? Enumerable.Empty<string>());
        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            row.IsHidden = OnlyTitle && i > 0;
            row.IsDone = row.Kind == LineKind.SubTask && done.Contains(row.Text);
            row.FontSize = row.Kind is LineKind.Title or LineKind.SubTask ? TextSize : TextSize - 1;
            row.TitleHint = Placeholder;
        }
    }

    // ---- Keys ----

    private void LineBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        var index = IndexOf(box);
        if (index < 0) return;
        var row = Rows[index];
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Modifiers();

        if (HasSpan && SpanKey(key, mods))
        {
            e.Handled = true;
            return;
        }
        // Ctrl+A once takes the line, as any box does; again (or on an empty line) every line.
        if (key == Key.A && mods == ModifierKeys.Control && Rows.Count(r => !r.IsHidden) > 1 && box.SelectionLength == box.Text.Length)
        {
            SetSpan(0, Rows.Count(r => !r.IsHidden) - 1);
            e.Handled = true;
            return;
        }
        // Shift+↑/↓ past the first or last line of a box takes the next line in as well.
        if (mods == ModifierKeys.Shift && key == Key.Up && index > 0 && box.GetLineIndexFromCharacterIndex(box.CaretIndex) <= 0)
        {
            SetSpan(_spanAnchor ?? index, (_spanAnchor is null ? index : _spanEnd) - 1);
            e.Handled = true;
            return;
        }
        if (mods == ModifierKeys.Shift && key == Key.Down && index < Rows.Count - 1 && !Rows[index + 1].IsHidden
            && box.GetLineIndexFromCharacterIndex(box.CaretIndex) >= box.LineCount - 1)
        {
            SetSpan(_spanAnchor ?? index, (_spanAnchor is null ? index : _spanEnd) + 1);
            e.Handled = true;
            return;
        }
        var atStart = box.CaretIndex == 0 && box.SelectionLength == 0;
        var atEnd = box.CaretIndex == box.Text.Length && box.SelectionLength == 0;

        if (key == Key.Enter && (mods == ModifierKeys.Shift || mods == ModifierKeys.Alt))
        {
            Split(index, box);
        }
        else if (key == Key.Enter && mods == ModifierKeys.None) Submit?.Invoke(false);
        else if (key == Key.Enter && mods == ModifierKeys.Control) Submit?.Invoke(true);
        else if (key == Key.Enter) { }
        else if (key == Key.Escape && Cancel != null) Cancel();
        else if (key == Key.Back && atStart && index > 0)
        {
            if (row.Kind == LineKind.SubTask)
            {
                var lines = Rows.Select(r => r.ToLine()).ToList();
                TaskLines.Demote(lines, index);
                row.Kind = lines[index].Kind;
                Changed();
                NoteKind(row.Kind);
            }
            else JoinUp(index);
        }
        else if (key == Key.Delete && atEnd && index < Rows.Count - 1) JoinUp(index + 1);
        else if (key == Key.Up && index > 0 && box.GetLineIndexFromCharacterIndex(box.CaretIndex) <= 0) FocusRow(index - 1, int.MaxValue);
        else if (key == Key.Down && index < Rows.Count - 1 && !Rows[index + 1].IsHidden && box.GetLineIndexFromCharacterIndex(box.CaretIndex) >= box.LineCount - 1) FocusRow(index + 1, 0);
        else return;

        e.Handled = true;
    }

    /// <summary>Shift+Enter: the line breaks at the cursor, the rest goes down as a note at the same level.</summary>
    private void Split(int index, TextBox box)
    {
        var row = Rows[index];
        var at = box.CaretIndex;
        var rest = row.Text.Substring(at);
        row.Text = row.Text.Substring(0, at);
        Rows.Insert(index + 1, new EditorRow { Kind = TaskLines.KindAfter(row.Kind), Text = rest });
        Changed();
        FocusRow(index + 1, 0);
    }

    /// <summary>The line at <paramref name="index"/> joins the end of the one above; the cursor stays at the seam.</summary>
    private void JoinUp(int index)
    {
        var above = Rows[index - 1];
        var seam = above.Text.Length;
        above.Text += Rows[index].Text;
        Rows.RemoveAt(index);
        Changed();
        FocusRow(index - 1, seam);
    }

    private void LineBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_rebuilding) return;
        var box = (TextBox)sender;
        var index = IndexOf(box);
        if (index < 0) return;
        var row = Rows[index];
        if (row.Text == box.Text && !box.Text.StartsWith("- ", StringComparison.Ordinal)) { Emit(); return; }
        row.Text = box.Text;

        var line = row.ToLine();
        if (TaskLines.TryPromote(line))
        {
            row.Kind = line.Kind;
            row.Text = line.Text;
            Changed();
            NoteKind(row.Kind);
            Dispatcher.BeginInvoke(new Action(() => { if (FindBox(index) is { } b) b.CaretIndex = 0; }), DispatcherPriority.Input);
            return;
        }
        Emit();
    }

    private void Emit()
    {
        var text = TaskLines.Join(Rows.Select(r => r.ToLine()));
        if (text == _emitted) return;
        _emitted = text;
        SetCurrentValue(TextProperty, text);
        Refresh();
    }

    /// <summary>A list pasted into a line: the line breaks there and the pasted lines take their kinds by the usual rules.</summary>
    private void LineBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string pasted || !pasted.Contains("\n") && !pasted.Contains("\r")) return;
        e.CancelCommand();

        var box = (TextBox)sender;
        var index = IndexOf(box);
        if (index < 0) return;
        var row = Rows[index];
        var start = box.SelectionStart;
        var rest = row.Text.Substring(start + box.SelectionLength);
        var parsed = TaskLines.Parse(pasted.TrimEnd('\r', '\n'));

        row.Text = row.Text.Substring(0, start) + parsed[0].Text;
        for (var i = 1; i < parsed.Count; i++)
        {
            var kind = parsed[i].Kind == LineKind.SubNote || parsed[i].Kind == LineKind.SubTask ? parsed[i].Kind : TaskLines.KindAfter(row.Kind);
            Rows.Insert(index + i, new EditorRow { Kind = kind, Text = parsed[i].Text });
        }
        var last = Rows[index + parsed.Count - 1];
        var caret = last.Text.Length;
        last.Text += rest;
        Changed();
        FocusRow(index + parsed.Count - 1, caret);
    }

    private void LineBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var index = IndexOf((TextBox)sender);
        if (index >= 0) NoteKind(Rows[index].Kind);
    }

    private void NoteKind(LineKind kind)
    {
        _currentKind = kind;
        CurrentKindChanged?.Invoke();
    }

    // ---- A selection over several lines ----
    // Each line is its own box, so a box's own selection stops at its edge. Ctrl+A twice, Shift+↑/↓ past a box's
    // edge, or a drag from one line to another takes whole lines instead: they light up, and copy, cut, delete,
    // typing and pasting work on all of them, as in any text editor.

    private int? _spanAnchor;
    private int _spanEnd;
    private int? _dragFrom;

    private bool HasSpan => _spanAnchor is not null;
    private int SpanFirst => Math.Min(_spanAnchor ?? 0, _spanEnd);
    private int SpanLast => Math.Max(_spanAnchor ?? 0, _spanEnd);

    private void SetSpan(int anchor, int end)
    {
        var last = Rows.Count(r => !r.IsHidden) - 1;
        _spanAnchor = Math.Max(0, Math.Min(anchor, last));
        _spanEnd = Math.Max(0, Math.Min(end, last));
        if (_spanAnchor == _spanEnd)
        {
            // Back to one line: that box's own selection takes over.
            var only = _spanEnd;
            ClearSpan();
            FocusRow(only, int.MaxValue);
            return;
        }
        for (var i = 0; i < Rows.Count; i++) Rows[i].IsSelected = i >= SpanFirst && i <= SpanLast;
        // The keyboard stays on a line in the span with nothing selected inside it, so only the lit lines read as chosen.
        if (FindBox(_spanEnd) is { } box)
        {
            box.Focus();
            box.Select(box.Text.Length, 0);
        }
    }

    private void ClearSpan()
    {
        _spanAnchor = null;
        foreach (var row in Rows) row.IsSelected = false;
    }

    /// <summary>The lines of the span as text, the way the plain box writes them.</summary>
    private string SpanText() => TaskLines.Join(Rows.Skip(SpanFirst).Take(SpanLast - SpanFirst + 1).Select(r => r.ToLine()));

    /// <summary>Takes the span's lines out; the title line stays, emptied. The keyboard goes where they were.</summary>
    private int RemoveSpan()
    {
        var first = SpanFirst;
        var last = SpanLast;
        ClearSpan();
        for (var i = last; i >= first; i--)
        {
            if (i == 0) Rows[0].Text = "";
            else Rows.RemoveAt(i);
        }
        Changed();
        return Math.Min(first, Rows.Count - 1);
    }

    /// <summary>A key while lines are taken: true when it was for them.</summary>
    private bool SpanKey(Key key, ModifierKeys mods)
    {
        if (mods == ModifierKeys.Control && key is Key.C or Key.Insert)
        {
            Clipboard.SetText(SpanText());
            return true;
        }
        if ((mods == ModifierKeys.Control && key == Key.X) || (mods == ModifierKeys.Shift && key == Key.Delete))
        {
            Clipboard.SetText(SpanText());
            FocusRow(RemoveSpan(), int.MaxValue);
            return true;
        }
        if (mods == ModifierKeys.None && key is Key.Delete or Key.Back)
        {
            FocusRow(RemoveSpan(), int.MaxValue);
            return true;
        }
        if (mods == ModifierKeys.Control && key == Key.V)
        {
            // The lines go, the pasted text comes in where they were (the box's own paste splits it into lines).
            var at = RemoveSpan();
            FocusRow(at, int.MaxValue);
            Dispatcher.BeginInvoke(new Action(() => (Keyboard.FocusedElement as TextBox)?.Paste()), DispatcherPriority.Input);
            return true;
        }
        if (mods == ModifierKeys.Control && key == Key.A) return true;
        if (mods == ModifierKeys.None && key == Key.Escape)
        {
            var end = _spanEnd;
            ClearSpan();
            FocusRow(end, int.MaxValue);
            return true;
        }
        if (key is Key.Up or Key.Down && mods == ModifierKeys.Shift)
        {
            SetSpan(_spanAnchor!.Value, _spanEnd + (key == Key.Up ? -1 : 1));
            return true;
        }
        // Moving the cursor or any other key lets the lines go; the key then works as usual.
        if (key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Tab or Key.Enter)
        {
            var end = _spanEnd;
            ClearSpan();
            FocusRow(end, key is Key.Left or Key.Home or Key.Up ? 0 : int.MaxValue);
            return true;
        }
        return false;
    }

    /// <summary>Typing over taken lines puts the letters in their place.</summary>
    private void Root_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!HasSpan || e.Text.Length == 0) return;
        var at = RemoveSpan();
        Rows[at].Text += e.Text;
        Changed();
        FocusRow(at, int.MaxValue);
        e.Handled = true;
    }

    private void Root_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ClearSpan();
        _dragFrom = RowAt(e.GetPosition(LineList));
    }

    /// <summary>A drag that leaves the line it started on takes whole lines, from there to the one under the mouse.</summary>
    private void Root_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragFrom is not { } from) return;
        if (RowAt(e.GetPosition(LineList)) is not { } over) return;
        if (over == from && !HasSpan) return;
        if (over == from)
        {
            ClearSpan();
            return;
        }
        // From here the editor follows the mouse, so the first box stops growing its own selection.
        if (!Root.IsMouseCaptured) Root.CaptureMouse();
        SetSpan(from, over);
    }

    private void Root_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragFrom = null;
        if (Root.IsMouseCaptured) Root.ReleaseMouseCapture();
    }

    /// <summary>The line under a point of the list, or null outside the lines.</summary>
    private int? RowAt(Point point)
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].IsHidden || LineList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement item) continue;
            var top = item.TranslatePoint(new Point(0, 0), LineList).Y;
            if (point.Y < top + item.ActualHeight) return i;
        }
        return Rows.Count == 0 ? null : Rows.Count(r => !r.IsHidden) - 1;
    }

    // ---- Finding the boxes ----

    private int IndexOf(TextBox box) => box.DataContext is EditorRow row ? Rows.IndexOf(row) : -1;

    private TextBox? FindBox(int index) =>
        LineList.ItemContainerGenerator.ContainerFromIndex(index) is DependencyObject container ? FindChild<TextBox>(container) : null;

    private void FocusRow(int index, int caret)
    {
        void Go()
        {
            if (FindBox(index) is not { } box) return;
            box.Focus();
            box.CaretIndex = Math.Min(caret, box.Text.Length);
        }

        if (FindBox(index) is { IsLoaded: true }) Go();
        else Dispatcher.BeginInvoke(new Action(Go), DispatcherPriority.Loaded);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is { } deeper) return deeper;
        }
        return null;
    }
}
