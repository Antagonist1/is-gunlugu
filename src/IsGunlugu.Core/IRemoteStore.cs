namespace IsGunlugu.Core;

public sealed class RemoteFile(string path, string id, string revision, DateTime modifiedUtc)
{
    /// <summary>Relative to the notes root, with forward slashes.</summary>
    public string Path { get; } = path;
    public string Id { get; } = id;
    public string Revision { get; } = revision;
    public DateTime ModifiedUtc { get; } = modifiedUtc;
}

/// <summary>Where the notes live in the cloud. Drive in the apps, a list in memory in the tests.</summary>
public interface IRemoteStore
{
    IList<RemoteFile> List();
    byte[] Download(string id);
    RemoteFile Upload(string path, byte[] content, string? existingId);
    void Trash(string id);
    string Revision(string id);
}

public sealed class SyncConflict
{
    public string Path { get; set; } = "";
    public byte[] Local { get; set; } = new byte[0];
    public byte[] Remote { get; set; } = new byte[0];
    public DateTime LocalUtc { get; set; }
    public DateTime RemoteUtc { get; set; }
    public RemoteFile RemoteFile { get; set; } = null!;

    /// <summary>Both syncs found the same disagreements, so a screen showing them need not start over.</summary>
    public static bool AreSame(IList<SyncConflict> first, IList<SyncConflict> second) =>
        first.Count == second.Count && first.Zip(second, (a, b) =>
            a.Path == b.Path && a.RemoteFile.Revision == b.RemoteFile.Revision && SyncState.Hash(a.Local) == SyncState.Hash(b.Local)).All(same => same);
}

public sealed class SyncResult
{
    public IList<SyncConflict> Conflicts { get; } = new List<SyncConflict>();
    public IList<string> Errors { get; } = new List<string>();

    /// <summary>Files whose deletion is waiting for the person, because too many went at once.</summary>
    public IList<string> HeldDeletions { get; } = new List<string>();

    public int Changed { get; set; }
}
