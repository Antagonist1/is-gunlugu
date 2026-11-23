using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu.Tests;

/// <summary>
/// Saving while something else reads the same file: the sync every three seconds, OneDrive, the phone's background job.
/// Neither side may fail, and every read must see one whole version.
/// </summary>
public sealed class ConcurrentFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "IsGunluguTest", Guid.NewGuid().ToString("N"));

    public ConcurrentFileTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    private string Notes => Path.Combine(_directory, "notlar.txt");

    private static string Version(int n) => $"----11/11/2026\r\n🚨iş {n}\r\n" + new string('x', 4000) + "\r\n";

    [Fact]
    public void SavingWhileTheSyncReads_NeitherSideFails()
    {
        var store = new NoteStore(Notes, Path.Combine(_directory, "yedek"));
        store.Write(Version(0));
        var folder = new LocalFolder(_directory, Path.Combine(_directory, "yedek"));
        var failures = new List<Exception>();
        var torn = 0;
        var writing = true;

        var reader = new Thread(() =>
        {
            while (Volatile.Read(ref writing))
            {
                try
                {
                    var text = Encoding.UTF8.GetString(folder.Read("notlar.txt"));
                    if (!text.Contains(new string('x', 4000))) Interlocked.Increment(ref torn);
                }
                catch (Exception ex)
                {
                    lock (failures) failures.Add(ex);
                }
            }
        });
        reader.Start();

        for (var i = 1; i <= 300; i++)
        {
            try
            {
                store.Write(Version(i));
            }
            catch (Exception ex)
            {
                lock (failures) failures.Add(ex);
            }
        }

        Volatile.Write(ref writing, false);
        reader.Join();

        Assert.Empty(failures.Select(f => f.GetType().Name + ": " + f.Message).Distinct());
        Assert.Equal(0, torn);
        Assert.Contains("iş 300", store.Read());
    }

    [Fact]
    public void SavingWhileAnotherProgramHoldsTheFileBriefly_StillSaves()
    {
        var store = new NoteStore(Notes, Path.Combine(_directory, "yedek"));
        store.Write(Version(0));

        // Like OneDrive reading the file for upload: open, without letting it be replaced, for a moment.
        var holding = new ManualResetEventSlim();
        var holder = new Thread(() =>
        {
            using var stream = new FileStream(Notes, FileMode.Open, FileAccess.Read, FileShare.Read);
            holding.Set();
            Thread.Sleep(200);
        });
        holder.Start();
        holding.Wait();

        store.Write(Version(1));
        holder.Join();

        Assert.Contains("iş 1", store.Read());
    }
}
