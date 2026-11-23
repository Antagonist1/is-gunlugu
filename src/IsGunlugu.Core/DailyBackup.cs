using System.Globalization;
using System.IO;

namespace IsGunlugu.Core;

/// <summary>
/// Once a day, a copy of every note file with its folders, in a folder named for the day; thirty days are kept.
/// The copies live on this computer only, so a mistake that Drive spreads cannot reach them.
/// </summary>
public static class DailyBackup
{
    public const int KeepDays = 30;
    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>True when today's copy was made now; false when it already existed or there is nothing to copy.</summary>
    public static bool Run(string notesFolder, string backupRoot, DateTime today, int keepDays = KeepDays)
    {
        if (!Directory.Exists(notesFolder)) return false;

        var made = false;
        var target = Path.Combine(backupRoot, today.ToString(DayFormat, CultureInfo.InvariantCulture));
        if (!Directory.Exists(target))
        {
            // Copied beside the target first, so a half-done copy never passes for a day's backup.
            var partial = target + ".yarim";
            if (Directory.Exists(partial)) Directory.Delete(partial, true);

            var source = Path.GetFullPath(notesFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var file in Directory.GetFiles(source, "*.txt", SearchOption.AllDirectories))
            {
                var copy = Path.Combine(partial, Path.GetFullPath(file).Substring(source.Length));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(file, copy);
                made = true;
            }

            if (made) Directory.Move(partial, target);
        }

        Prune(backupRoot, today, keepDays);
        return made;
    }

    /// <summary>Removes the days older than the kept ones; folders with other names were made by hand and stay.</summary>
    private static void Prune(string backupRoot, DateTime today, int keepDays)
    {
        if (!Directory.Exists(backupRoot)) return;

        var oldest = today.AddDays(-(keepDays - 1));
        foreach (var folder in Directory.GetDirectories(backupRoot))
        {
            if (DateTime.TryParseExact(Path.GetFileName(folder), DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                && day < oldest)
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
