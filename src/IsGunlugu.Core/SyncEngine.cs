using System.IO;

namespace IsGunlugu.Core;

/// <summary>
/// Brings the notes folder and the cloud in step, one file at a time. Compares each side with what it was at
/// the last sync, so a change on one side travels and a change on both sides waits for the person to choose.
/// </summary>
public sealed class SyncEngine(IRemoteStore remote, LocalFolder local, SyncState state, string stateFile)
{
    private const int NamesShown = 3;

    /// <summary>A few of the file names, for the question put to the person.</summary>
    public static string DescribeHeld(IList<string> paths)
    {
        var names = string.Join(", ", paths.Take(NamesShown).Select(Path.GetFileNameWithoutExtension).ToArray());
        return paths.Count > NamesShown ? $"{names} ve {paths.Count - NamesShown} dosya daha" : names;
    }

    /// <summary>The person wants these files kept: forgetting them makes the side that still has them count as new.</summary>
    public void RestoreHeld(IEnumerable<string> paths)
    {
        foreach (var path in paths) state.Records.Remove(path);
        state.Save(stateFile);
    }

    /// <param name="confirmedDeletions">Files the person agreed may go, after a previous run held them.</param>
    public SyncResult Run(ICollection<string>? confirmedDeletions = null)
    {
        var result = new SyncResult();
        var remoteFiles = remote.List();
        var localPaths = local.List();
        var held = FindHeld(remoteFiles, localPaths, confirmedDeletions);
        foreach (var path in held) result.HeldDeletions.Add(path);

        var paths = remoteFiles.Select(f => f.Path).Concat(localPaths)
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var group in paths)
        {
            var path = group.First();
            if (held.Contains(path)) continue;

            var matches = remoteFiles.Where(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1)
            {
                result.Errors.Add($"Drive'da aynı adlı birden çok dosya var: {path}");
                continue;
            }

            try
            {
                SyncOne(localPaths.FirstOrDefault(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) ?? matches[0].Path, matches.FirstOrDefault(), result);
            }
            catch (Exception ex) when (ex is NoteFileException or IOException or UnauthorizedAccessException)
            {
                result.Errors.Add($"{path}: {ex.Message}");
            }
        }

        foreach (var orphan in state.Records.Keys.Where(k => !paths.Any(g => string.Equals(g.Key, k, StringComparison.OrdinalIgnoreCase))).ToList())
        {
            Forget(orphan); // Gone from both sides.
        }
        return result;
    }

    /// <summary>
    /// A deletion travels to the other side and takes the file there too. One at a time is someone tidying up; half
    /// the notes at once is more likely a folder gone missing, so those wait until the person says so.
    /// </summary>
    private HashSet<string> FindHeld(IList<RemoteFile> remoteFiles, IList<string> localPaths, ICollection<string>? confirmed)
    {
        var deletions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in state.Records.Values)
        {
            if (WouldDelete(record, remoteFiles, localPaths)) deletions.Add(record.Path);
        }

        var massive = deletions.Count >= 2 && deletions.Count * 2 >= state.Records.Count;
        var agreed = confirmed != null && deletions.All(path => confirmed.Contains(path, StringComparer.OrdinalIgnoreCase));
        return massive && !agreed ? deletions : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private bool WouldDelete(SyncRecord record, IList<RemoteFile> remoteFiles, IList<string> localPaths)
    {
        var matches = remoteFiles.Where(f => string.Equals(f.Path, record.Path, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count > 1) return false;

        var localPath = localPaths.FirstOrDefault(p => string.Equals(p, record.Path, StringComparison.OrdinalIgnoreCase));
        if (localPath == null) return matches.Count == 1 && matches[0].Revision == record.RemoteRevision;
        if (matches.Count == 1) return false;

        try
        {
            return SyncState.Hash(local.Read(localPath)) == record.LocalHash;
        }
        catch (Exception ex) when (ex is NoteFileException or IOException or UnauthorizedAccessException)
        {
            return false; // Syncing this file reports the same problem.
        }
    }

    /// <summary>Applies the person's choice, unless either side has moved on since the question was asked.</summary>
    public void Resolve(SyncConflict conflict, bool keepLocal)
    {
        if (!local.Exists(conflict.Path) || SyncState.Hash(local.Read(conflict.Path)) != SyncState.Hash(conflict.Local)) return;
        if (remote.Revision(conflict.RemoteFile.Id) != conflict.RemoteFile.Revision) return;

        if (keepLocal)
        {
            local.Backup(conflict.Path, conflict.Remote, "secilmeyen");
            Remember(conflict.Path, remote.Upload(conflict.Path, conflict.Local, conflict.RemoteFile.Id), conflict.Local);
        }
        else
        {
            local.Backup(conflict.Path, conflict.Local, "secilmeyen");
            local.Write(conflict.Path, conflict.Remote);
            Remember(conflict.Path, conflict.RemoteFile, conflict.Remote);
        }
    }

    private void SyncOne(string path, RemoteFile? file, SyncResult result)
    {
        state.Records.TryGetValue(path, out var record);
        var localBytes = local.Exists(path) ? local.Read(path) : null;
        var localHash = localBytes == null ? null : SyncState.Hash(localBytes);

        if (record == null)
        {
            if (localBytes == null) Download(path, file!, null, result);
            else if (file == null) Upload(path, localBytes, null, result);
            else BothChanged(path, localBytes, file, result);
            return;
        }

        var localChanged = localHash != record.LocalHash;
        var remoteChanged = file == null || file.Revision != record.RemoteRevision;

        if (localBytes == null)
        {
            if (file == null) Forget(path);
            else if (remoteChanged) Download(path, file, null, result);
            else
            {
                remote.Trash(file.Id);
                Forget(path);
                result.Changed++;
            }
        }
        else if (file == null)
        {
            if (localChanged) Upload(path, localBytes, null, result);
            else if (SyncState.Hash(local.Read(path)) == record.LocalHash)
            {
                local.Delete(path);
                Forget(path);
                result.Changed++;
            }
        }
        else if (localChanged && remoteChanged) BothChanged(path, localBytes, file, result);
        else if (localChanged)
        {
            // Someone else may have saved since the listing; if so, the next sync sees both changes.
            if (remote.Revision(file.Id) == record.RemoteRevision) Upload(path, localBytes, file.Id, result);
        }
        else if (remoteChanged) Download(path, file, localHash, result);
    }

    private void BothChanged(string path, byte[] localBytes, RemoteFile file, SyncResult result)
    {
        var remoteBytes = remote.Download(file.Id);
        if (SyncState.Hash(remoteBytes) == SyncState.Hash(localBytes))
        {
            Remember(path, file, localBytes);
            return;
        }

        result.Conflicts.Add(new SyncConflict
        {
            Path = path,
            Local = localBytes,
            Remote = remoteBytes,
            LocalUtc = local.ModifiedUtc(path),
            RemoteUtc = file.ModifiedUtc,
            RemoteFile = file,
        });
    }

    private void Upload(string path, byte[] content, string? existingId, SyncResult result)
    {
        Remember(path, remote.Upload(path, content, existingId), content);
        result.Changed++;
    }

    private void Download(string path, RemoteFile file, string? expectedLocalHash, SyncResult result)
    {
        var content = remote.Download(file.Id);

        // The person may have saved while this was on its way; their words win and the next sync sorts it out.
        var current = local.Exists(path) ? SyncState.Hash(local.Read(path)) : null;
        if (current != expectedLocalHash) return;

        local.Write(path, content);
        Remember(path, file, content);
        result.Changed++;
    }

    private void Remember(string path, RemoteFile file, byte[] content)
    {
        state.Records[path] = new SyncRecord { Path = path, RemoteId = file.Id, RemoteRevision = file.Revision, LocalHash = SyncState.Hash(content) };
        state.Save(stateFile);
    }

    private void Forget(string path)
    {
        state.Records.Remove(path);
        state.Save(stateFile);
    }
}
