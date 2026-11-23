using System.IO;
using System.Globalization;
using System.Text;

namespace IsGunlugu.Core;

/// <summary>
/// A plain text file of what went wrong, newest at the bottom, so a problem seen once can be found later. It never
/// grows past a size: when full, the older half goes. Writing it may never fail the program itself.
/// </summary>
public sealed class ErrorLog(string path, long maxBytes = 256 * 1024)
{
    private const string Separator = "----";
    private static readonly object Gate = new();

    public string Path { get; } = path;

    public void Write(DateTime when, string version, string where, Exception error) => Write(when, version, where, error.ToString());

    public void Write(DateTime when, string version, string where, string details)
    {
        var entry = new StringBuilder()
            .Append(Separator).Append(' ').Append(when.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Append("  sürüm ").Append(version).Append("  ").Append(where).Append("\r\n")
            .Append(details.TrimEnd()).Append("\r\n\r\n")
            .ToString();

        try
        {
            lock (Gate)
            {
                var folder = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                var old = File.Exists(Path) ? File.ReadAllText(Path, Encoding.UTF8) : "";
                File.WriteAllText(Path, Fit(old + entry), new UTF8Encoding(false));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A log that cannot be written must not become the next error.
        }
    }

    /// <summary>Drops the oldest entries, whole, until the text fits.</summary>
    private string Fit(string text)
    {
        while (Encoding.UTF8.GetByteCount(text) > maxBytes)
        {
            var next = text.IndexOf("\r\n" + Separator + " ", text.Length / 2, StringComparison.Ordinal);
            if (next < 0) return text.Substring(text.Length - (int)Math.Min(text.Length, maxBytes / 4));
            text = text.Substring(next + 2);
        }
        return text;
    }
}
