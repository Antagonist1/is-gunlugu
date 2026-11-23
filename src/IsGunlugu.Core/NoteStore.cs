using System.IO;
using System.Text;

namespace IsGunlugu.Core;

public sealed class NoteFileException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Reads and writes the notes file without ever changing its encoding.</summary>
public sealed class NoteStore(string path, string backupDirectory)
{
    private const int BackupsToKeep = 30;

    private Encoding _encoding = new UTF8Encoding(false);

    public string Path { get; } = path;

    public string Read()
    {
        byte[] bytes;
        try
        {
            bytes = NoteFiles.ReadAllBytes(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new NoteFileException($"Dosya okunamadı: {ex.Message}", ex);
        }

        var text = Decode(bytes, out var encoding);
        _encoding = encoding;
        return text;
    }

    public static string Decode(byte[] bytes) => Decode(bytes, out _);

    private static string Decode(byte[] bytes, out Encoding encoding)
    {
        var preamble = DetectEncoding(bytes, out encoding);
        try
        {
            var text = encoding.GetString(bytes, preamble, bytes.Length - preamble);
            if (text.IndexOf('\0') >= 0) throw new DecoderFallbackException();
            return text;
        }
        catch (DecoderFallbackException ex)
        {
            throw new NoteFileException(
                "Dosya UTF-8 veya UTF-16 değil. Not Defteri'nde Farklı Kaydet ile UTF-8 seçip yeniden dene.", ex);
        }
    }

    public void Write(string text) => WriteBytes(_encoding.GetPreamble().Concat(_encoding.GetBytes(text)).ToArray());

    /// <summary>Writes these exact bytes, with the same backup and the same safe replace as any save.</summary>
    public void WriteBytes(byte[] content)
    {
        BackupOncePerDay();

        var temp = Path + ".tmp";
        try
        {
            NoteFiles.Retry(() =>
            {
                File.WriteAllBytes(temp, content);
                if (File.Exists(Path)) File.Replace(temp, Path, null);
                else File.Move(temp, Path);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            throw new NoteFileException($"Dosya kaydedilemedi: {ex.Message}", ex);
        }
    }

    /// <summary>Keeps a version that is about to be dropped. Never replaces an earlier copy.</summary>
    public string BackupVersion(byte[] content, string label)
    {
        try
        {
            Directory.CreateDirectory(backupDirectory);
            var stem = $"{BackupName()}-{DateTime.Now:yyyyMMdd-HHmmss}-{label}";
            var target = System.IO.Path.Combine(backupDirectory, stem + ".txt");
            for (var n = 2; File.Exists(target); n++) target = System.IO.Path.Combine(backupDirectory, $"{stem}-{n}.txt");

            File.WriteAllBytes(target, content);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new NoteFileException($"Yedek alınamadı: {ex.Message}", ex);
        }
    }

    private string BackupName()
    {
        // The folder is part of the name: two groups can each hold a notlar.txt.
        var folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)));
        return $"{folder}-{System.IO.Path.GetFileNameWithoutExtension(Path)}";
    }

    private void BackupOncePerDay()
    {
        if (!File.Exists(Path)) return;

        try
        {
            Directory.CreateDirectory(backupDirectory);
            var name = BackupName();
            var target = System.IO.Path.Combine(backupDirectory, $"{name}-{DateTime.Now:yyyyMMdd}.txt");
            if (File.Exists(target)) return;

            File.Copy(Path, target);
            foreach (var old in Directory.GetFiles(backupDirectory, $"{name}-*.txt")
                         .OrderByDescending(f => f, StringComparer.Ordinal).Skip(BackupsToKeep))
            {
                File.Delete(old);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new NoteFileException($"Yedek alınamadı, kayıt yapılmadı: {ex.Message}", ex);
        }
    }

    private static int DetectEncoding(byte[] bytes, out Encoding encoding)
    {
        if (StartsWith(bytes, 0xEF, 0xBB, 0xBF))
        {
            encoding = new UTF8Encoding(true, true);
            return 3;
        }
        if (StartsWith(bytes, 0xFF, 0xFE))
        {
            encoding = new UnicodeEncoding(false, true, true);
            return 2;
        }
        if (StartsWith(bytes, 0xFE, 0xFF))
        {
            encoding = new UnicodeEncoding(true, true, true);
            return 2;
        }

        encoding = new UTF8Encoding(false, true);
        return 0;
    }

    private static bool StartsWith(byte[] bytes, params byte[] prefix) =>
        bytes.Length >= prefix.Length && prefix.Select((b, i) => bytes[i] == b).All(same => same);

    private static void TryDelete(string file)
    {
        try { File.Delete(file); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
