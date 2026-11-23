using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace IsGunlugu;

/// <summary>
/// The file picker's handling in the window: lines dragged by their dots into order or under another group, names
/// edited in place, and the menu of a file. The view model does the moving and renaming.
/// </summary>
public partial class MainWindow
{
    private const string PickerDragFormat = "IsGunlugu.PickerRow";
    private PickerRow? _pickerPressed;
    private Point _pickerPressedAt;

    private void PickerGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pickerPressed = ((FrameworkElement)sender).DataContext as PickerRow;
        _pickerPressedAt = e.GetPosition(this);
    }

    private void PickerGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pickerPressed is null || e.LeftButton != MouseButtonState.Pressed) return;

        var moved = e.GetPosition(this) - _pickerPressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var row = _pickerPressed;
        _pickerPressed = null;
        _viewModel.CancelInlineRename();
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(PickerDragFormat, row), DragDropEffects.Move);
        ClearPickerDrop();
    }

    /// <summary>Where the dragged line would land, with the line to mark; false when the place makes no sense.</summary>
    private bool PickerDropPlace(DragEventArgs e, out PickerRow? mark, out bool below, out Action? drop)
    {
        mark = null;
        below = false;
        drop = null;
        if (e.Data.GetData(PickerDragFormat) is not PickerRow dragged) return false;

        var rows = _viewModel.Files.OfType<PickerRow>().ToList();
        var over = RowUnder(e.OriginalSource as DependencyObject);
        if (over is not null)
        {
            var element = ContainerOf(over);
            below = element is not null && e.GetPosition(element).Y > element.ActualHeight / 2;
        }

        if (dragged is FileRow file)
        {
            switch (over)
            {
                case FileRow target when target == file:
                    return false;
                case FileRow target:
                {
                    mark = target;
                    string? before;
                    if (!below) before = target.Path;
                    else
                    {
                        var next = rows.Skip(rows.IndexOf(target) + 1).FirstOrDefault();
                        before = next is FileRow sibling && sibling.GroupName == target.GroupName ? sibling.Path : null;
                    }
                    var group = target.GroupName;
                    drop = () => _viewModel.MoveFileTo(file.Path, before, group);
                    return true;
                }
                case FileGroupRow heading:
                {
                    // On a heading: first in that group.
                    mark = heading;
                    below = true;
                    var first = rows.Skip(rows.IndexOf(heading) + 1).FirstOrDefault() as FileRow;
                    var before = first is not null && first.GroupName == heading.Name ? first.Path : null;
                    drop = () => _viewModel.MoveFileTo(file.Path, before, heading.Name);
                    return true;
                }
                default:
                {
                    // Under every line: last, outside the groups.
                    mark = rows.LastOrDefault();
                    below = true;
                    drop = () => _viewModel.MoveFileTo(file.Path, null, null);
                    return mark is not null;
                }
            }
        }

        if (dragged is FileGroupRow moving)
        {
            var groups = rows.OfType<FileGroupRow>().ToList();
            var heading = over as FileGroupRow ?? (over is FileRow { GroupName: { } name } ? groups.FirstOrDefault(g => g.Name == name) : null);
            if (over is FileRow { GroupName: not null }) below = true;
            if (heading is null || heading == moving) return false;

            mark = heading;
            var before = below ? groups.Skip(groups.IndexOf(heading) + 1).FirstOrDefault()?.Name : heading.Name;
            drop = () => _viewModel.MoveGroupTo(moving.Name, before);
            return true;
        }

        return false;
    }

    private static PickerRow? RowUnder(DependencyObject? source)
    {
        for (var current = source; current != null; current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: PickerRow row }) return row;
        }
        return null;
    }

    private FrameworkElement? ContainerOf(PickerRow row) => PickerList.ItemContainerGenerator.ContainerFromItem(row) as FrameworkElement;

    private void Picker_DragOver(object sender, DragEventArgs e)
    {
        ClearPickerDrop();
        e.Handled = true;
        if (!PickerDropPlace(e, out var mark, out var below, out _))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Move;
        if (mark is null) return;
        if (below) mark.ShowDropBelow = true;
        else mark.ShowDropAbove = true;
    }

    private void Picker_DragLeave(object sender, DragEventArgs e) => ClearPickerDrop();

    private void Picker_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var ok = PickerDropPlace(e, out _, out _, out var drop);
        ClearPickerDrop();
        // After the drag is over: the move rebuilds the list the drag started from.
        if (ok && drop is not null) Dispatcher.BeginInvoke(drop);
    }

    private void ClearPickerDrop()
    {
        foreach (var row in _viewModel.Files.OfType<PickerRow>())
        {
            if (row.ShowDropAbove) row.ShowDropAbove = false;
            if (row.ShowDropBelow) row.ShowDropBelow = false;
        }
    }

    private void PickerRename_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PickerRow row) _viewModel.BeginInlineRename(row);
    }

    private void PickerRenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) return;

        var box = (TextBox)sender;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            box.Focus();
            box.SelectAll();
        }));
    }

    private void PickerRenameBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.CommitInlineRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CancelInlineRename();
            e.Handled = true;
        }
    }

    private void PickerRenameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is PickerRow { IsRenaming: true }) _viewModel.CommitInlineRename();
    }

    /// <summary>Esc closes the picker (or the name being edited first); F2 renames the line that has the keyboard.</summary>
    private bool PickerKey(KeyEventArgs e)
    {
        if (!_viewModel.IsFilePickerOpen) return false;

        if (e.Key == Key.Escape)
        {
            // The window hears Esc before the name box: a name being edited is given up first, the picker stays.
            if (Keyboard.FocusedElement is TextBox { DataContext: PickerRow { IsRenaming: true } })
            {
                _viewModel.CancelInlineRename();
                return true;
            }
            _viewModel.IsFilePickerOpen = false;
            FilePickerButton.Focus();
            return true;
        }

        if (e.Key == Key.F2 && PickerRowForRename() is { } row)
        {
            _viewModel.BeginInlineRename(row);
            return true;
        }
        return false;
    }

    /// <summary>
    /// The line F2 renames: the one with the keyboard, else the one under the pointer, else the open file. The picker is
    /// opened with the mouse, so the keyboard is rarely on a line of it.
    /// </summary>
    private PickerRow? PickerRowForRename()
    {
        if (Keyboard.FocusedElement is FrameworkElement { DataContext: PickerRow focused } && IsWithin(Keyboard.FocusedElement as DependencyObject, FilesPanel))
        {
            return focused;
        }
        if (Mouse.DirectlyOver is DependencyObject pointed && IsWithin(pointed, FilesPanel) && RowUnder(pointed) is { } hovered) return hovered;
        return _viewModel.Files.OfType<FileRow>().FirstOrDefault(f => f.IsCurrent);
    }
}
