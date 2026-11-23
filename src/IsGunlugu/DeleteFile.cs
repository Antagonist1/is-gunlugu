using System.IO;
using System.Windows.Input;
using IsGunlugu.Core;

namespace IsGunlugu;

/// <summary>
/// The cross on a file row. A file in the notes folder is really deleted, after asking, with its last version kept
/// among the backups; the sync then takes it off Drive too. A file from elsewhere only leaves the list.
/// </summary>
public sealed partial class MainViewModel
{
    private string? _pendingDelete;
    private Action? _pendingDeleteTask;
    private string _deleteTitle = "";
    private string _deleteText = "";
    private bool _isDeleteConfirmOpen;

    public ICommand ConfirmDeleteCommand { get; private set; } = null!;
    public ICommand CancelDeleteCommand { get; private set; } = null!;

    public bool IsDeleteConfirmOpen
    {
        get => _isDeleteConfirmOpen;
        private set
        {
            _isDeleteConfirmOpen = value;
            if (value) IsFilePickerOpen = false;
            Notify();
        }
    }

    public string DeleteConfirmTitle => _deleteTitle;

    public string DeleteConfirmText => _deleteText;

    private void InitDeleteFile()
    {
        ConfirmDeleteCommand = new RelayCommand(ConfirmDelete);
        CancelDeleteCommand = new RelayCommand(() =>
        {
            _pendingDelete = null;
            _pendingDeleteTask = null;
            IsDeleteConfirmOpen = false;
        });
    }

    /// <summary>Del on a task or sub-task in the list: asked first, with the same window as a file.</summary>
    public void AskToDelete(EntryViewModel row) =>
        AskToDeleteTask($"“{Shorten(row.Text)}” silinsin mi?", row.Entry.SubTasks.Count > 0 ? "Alt işleriyle birlikte silinir. Ctrl+Z geri alır." : "Ctrl+Z geri alır.", () => Delete(row));

    public void AskToDelete(BodyItemViewModel row) =>
        AskToDeleteTask($"“{Shorten(row.Text)}” alt işi silinsin mi?", "Ctrl+Z geri alır.", () => Delete(row));

    private void AskToDeleteTask(string title, string text, Action delete)
    {
        _pendingDelete = null;
        _pendingDeleteTask = delete;
        ShowDeleteConfirm(title, text);
    }

    private static string Shorten(string text) => text.Length <= 60 ? text : text.Substring(0, 57).TrimEnd() + "…";

    private void ShowDeleteConfirm(string title, string text)
    {
        _deleteTitle = title;
        _deleteText = text;
        Notify(nameof(DeleteConfirmTitle));
        Notify(nameof(DeleteConfirmText));
        IsDeleteConfirmOpen = true;
    }

    private void AskToDeleteFile(string path)
    {
        if (!IsInNotesFolder(path))
        {
            RemoveFile(path);
            return;
        }

        _pendingDelete = path;
        _pendingDeleteTask = null;
        ShowDeleteConfirm($"{Path.GetFileNameWithoutExtension(path)} silinsin mi?",
            "Dosya içindeki bütün notlarla silinir. Son hali yedeklerde kalır." + (IsDriveConnected ? " Drive'dan ve telefondan da kalkar." : ""));
    }

    private void ConfirmDelete()
    {
        var path = _pendingDelete;
        var task = _pendingDeleteTask;
        _pendingDelete = null;
        _pendingDeleteTask = null;
        IsDeleteConfirmOpen = false;
        task?.Invoke();
        if (path == null || _notesFolder == null) return;

        // Off the list first, so the file is no longer open or watched while it goes.
        RemoveFile(path);
        var relative = path.Substring(_notesFolder.Length).TrimStart('\\', '/').Replace('\\', '/');
        try
        {
            new LocalFolder(_notesFolder, BackupDirectory).Delete(relative);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NoteFileException)
        {
            AddExistingFile(path);
            StatusText = $"Dosya silinemedi: {ex.Message}";
            return;
        }

        StatusText = $"{Path.GetFileNameWithoutExtension(path)} silindi. Son hali yedeklerde duruyor.";
        RequestSync();
    }

    private string RemoveTipFor(string path) => IsInNotesFolder(path) ? "Sil (son hali yedeklerde kalır)" : "Listeden kaldır (dosya silinmez)";
}
