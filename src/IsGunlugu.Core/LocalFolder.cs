using System.IO;

namespace IsGunlugu.Core;

/// <summary>The notes folder as the sync sees it: relative paths with forward slashes, bytes in and out.</summary>
public sealed class LocalFolder(string root, string backupDirectory)
{
    private readonly string _root = Path.GetFullPath(root);

    public IList<string> List()
    {
        if (!Directory.Exists(_root)) return new List<string>();

        return Directory.GetFiles(_root, "*.txt", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
    }

    public bool Exists(string path) => File.Exists(Full(path));

    public byte[] Read(string path) => NoteFiles.ReadAllBytes(Full(path));

    public DateTime ModifiedUtc(string path) => File.GetLastWriteTimeUtc(Full(path));

    public void Write(string path, byte[] content)
    {
        var full = Full(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        new NoteStore(full, backupDirectory).WriteBytes(content);
    }

    public void Delete(string path)
    {
        var full = Full(path);
        new NoteStore(full, backupDirectory).BackupVersion(NoteFiles.ReadAllBytes(full), "silinen");
        NoteFiles.Retry(() => File.Delete(full));
    }

    /// <summary>Gives a file a new name in the same folder and returns its new path. The sync sees the old file gone and a new one arrive.</summary>
    public string Rename(string path, string name)
    {
        var clean = name.Trim();
        if (clean.Length == 0 || clean.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0) throw new NoteFileException("Bu ad kullanılamaz.");

        var fileName = clean.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? clean : clean + ".txt";
        var slash = path.LastIndexOf('/');
        var target = (slash < 0 ? "" : path.Substring(0, slash + 1)) + fileName;
        if (target == path) return path;

        // Across every folder: the desktop lists bare names, so one name stays one file.
        if (List().Any(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new NoteFileException("Bu adda bir dosya zaten var.");
        }

        File.Move(Full(path), Full(target));
        return target;
    }

    public void Backup(string path, byte[] content, string label) =>
        new NoteStore(Full(path), backupDirectory).BackupVersion(content, label);

    /// <summary>A name that comes from the cloud must never reach outside the notes folder.</summary>
    private string Full(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new NoteFileException("Geçersiz dosya yolu: " + path);
        }
        return full;
    }
}
