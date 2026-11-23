using System.IO;

namespace IsGunlugu.Core;

/// <summary>
/// Older versions sorted a day only on screen. Now the list follows the file, so a file written back then
/// is put in that order once. A copy of it is kept first, apart from the daily backups that get pruned.
/// </summary>
public static class DayOrderUpgrade
{
    public const string BackupFolderName = "siralama-oncesi";

    /// <returns>False when the file could not be handled and should be tried again another time.</returns>
    public static bool SortFile(string path, string backupDirectory, out bool changed)
    {
        changed = false;
        if (!File.Exists(path)) return true;

        try
        {
            var store = new NoteStore(path, backupDirectory);
            var text = store.Read();
            var document = NoteDocument.Parse(text);
            document.SortDaysByState();
            var sorted = document.Serialize();
            if (sorted == text) return true;

            // Lines may only change places: anything else means this file was not understood, so leave it alone.
            if (!SameLines(text, sorted)) return false;

            var folder = Path.Combine(backupDirectory, BackupFolderName);
            Directory.CreateDirectory(folder);
            var parent = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path)));
            var copy = Path.Combine(folder, $"{parent}-{Path.GetFileName(path)}");
            if (!File.Exists(copy)) File.Copy(path, copy);

            store.Write(sorted);
            changed = true;
            return true;
        }
        catch (Exception ex) when (ex is NoteFileException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool SameLines(string first, string second) => Lines(first).SequenceEqual(Lines(second));

    private static IEnumerable<string> Lines(string text) =>
        text.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).OrderBy(l => l, StringComparer.Ordinal);
}
