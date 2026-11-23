using System.IO;
using System.Threading;

namespace IsGunlugu.Core;

/// <summary>
/// Every read and write of a notes file goes through here. Inside the app they take turns, so the sync never reads a
/// file halfway through a save and a save never trips over the sync. Another program holding the file for a moment,
/// such as OneDrive uploading it, is waited out with short retries instead of failing the save.
/// </summary>
public static class NoteFiles
{
    private const int Attempts = 20;
    private const int PauseMs = 50;

    private static readonly object Gate = new();

    public static T Retry<T>(Func<T> work)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                lock (Gate) return work();
            }
            catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < Attempts)
            {
                Thread.Sleep(PauseMs); // Outside the lock, so whoever holds the file can finish.
            }
        }
    }

    public static void Retry(Action work) => Retry(() =>
    {
        work();
        return true;
    });

    public static byte[] ReadAllBytes(string path) => Retry(() => File.ReadAllBytes(path));
}
