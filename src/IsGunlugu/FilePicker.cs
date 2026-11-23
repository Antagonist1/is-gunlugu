using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace IsGunlugu;

/// <summary>
/// The file picker at the top of the sidebar list: one press shows the files and groups, one press on a file
/// switches to it and the page in the middle stays as it was. Lines are renamed in place and dragged into order;
/// a file dropped under another group moves into that group's folder.
/// </summary>
public sealed partial class MainViewModel
{
    private bool _isFilePickerOpen;
    private PickerRow? _renaming;

    public bool IsFilePickerOpen
    {
        get => _isFilePickerOpen;
        set
        {
            if (_isFilePickerOpen == value) return;
            _isFilePickerOpen = value;
            if (!value) CancelInlineRename();
            Notify();
        }
    }

    /// <summary>The open file's name on the picker button.</summary>
    public string CurrentFileTitle => _store is null ? "" : Path.GetFileNameWithoutExtension(_store.Path);

    public ICommand ToggleFilePickerCommand { get; private set; } = null!;

    /// <summary>＋ Grup: an empty group; files are dragged into it afterwards.</summary>
    public ICommand NewEmptyGroupCommand { get; private set; } = null!;

    private void InitFilePicker()
    {
        ToggleFilePickerCommand = new RelayCommand(() => IsFilePickerOpen = !IsFilePickerOpen);
        NewEmptyGroupCommand = new RelayCommand(() =>
        {
            IsFilePickerOpen = false;
            OpenNamePrompt("Yeni grup", "", CreateEmptyGroup);
        });
    }

    private string? CreateEmptyGroup(string name)
    {
        if (_settings.FileGroups.Any(g => IsSameName(g.Name, name))) return "Bu adda bir grup zaten var.";

        _settings.FileGroups.Add(new FileGroup { Name = name });
        SaveSettings();
        BuildFiles();
        StatusText = $"{name} grubu açıldı. Dosyaları listede tutup bu grubun altına sürükle.";
        return null;
    }

    public void OpenFileInEditor(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public void BeginInlineRename(PickerRow row)
    {
        CancelInlineRename();
        if (row is FileRow file && !CanRenameFile(file.Path)) return;

        _renaming = row;
        row.RenameText = row.Name;
        row.IsRenaming = true;
    }

    public void CancelInlineRename()
    {
        if (_renaming is null) return;
        _renaming.IsRenaming = false;
        _renaming = null;
    }

    /// <summary>Enter in the name box. A name the disk cannot take keeps the box open and says why.</summary>
    public void CommitInlineRename()
    {
        var row = _renaming;
        if (row is null) return;

        var name = row.RenameText.Trim();
        if (name == row.Name)
        {
            CancelInlineRename();
            return;
        }

        var error = NameProblem(name) ?? row switch
        {
            FileRow file => RenameFile(file.Path, name),
            FileGroupRow group => _settings.FileGroups.FirstOrDefault(g => IsSameName(g.Name, group.Name)) is { } found ? RenameGroup(found, name) : null,
            _ => null,
        };
        if (error is not null)
        {
            StatusText = error;
            return;
        }

        _renaming = null;
        BuildFiles();
    }

    /// <summary>
    /// A file dropped in the picker: it lands before <paramref name="beforePath"/> (or last), inside
    /// <paramref name="groupName"/> (null: outside every group). A new group moves the file into its folder.
    /// </summary>
    public void MoveFileTo(string path, string? beforePath, string? groupName)
    {
        var current = _settings.FileGroups.FirstOrDefault(g => g.Paths.Any(p => IsSamePath(p, path)))?.Name;
        if (!string.Equals(current, groupName, StringComparison.CurrentCultureIgnoreCase))
        {
            var before = _filePaths.FindIndex(p => IsSamePath(p, path));
            var error = PlaceFileInGroup(path, groupName);
            if (error is not null)
            {
                StatusText = error;
                return;
            }

            // The move gave the file a new path at the same place in the list.
            path = before >= 0 && before < _filePaths.Count ? _filePaths[before] : path;
        }

        var moving = _filePaths.FirstOrDefault(p => IsSamePath(p, path));
        if (moving is null) return;
        _filePaths.Remove(moving);

        var at = beforePath is null ? -1 : _filePaths.FindIndex(p => IsSamePath(p, beforePath));
        if (at < 0)
        {
            // Last of its group: after the group's last file, or at the very end.
            var members = groupName is null ? null : _settings.FileGroups.FirstOrDefault(g => IsSameName(g.Name, groupName))?.Paths;
            var last = members is null ? -1 : _filePaths.FindLastIndex(p => members.Any(m => IsSamePath(m, p)));
            at = last >= 0 ? last + 1 : _filePaths.Count;
        }
        _filePaths.Insert(at, moving);

        SaveFileList();
        BuildFiles();
    }

    /// <summary>A group heading dropped before another heading, or at the end when <paramref name="beforeName"/> is null.</summary>
    public void MoveGroupTo(string name, string? beforeName)
    {
        var group = _settings.FileGroups.FirstOrDefault(g => IsSameName(g.Name, name));
        if (group is null || (beforeName is not null && IsSameName(name, beforeName))) return;

        _settings.FileGroups.Remove(group);
        var at = beforeName is null ? -1 : _settings.FileGroups.FindIndex(g => IsSameName(g.Name, beforeName));
        _settings.FileGroups.Insert(at < 0 ? _settings.FileGroups.Count : at, group);

        SaveSettings();
        BuildFiles();
    }
}
