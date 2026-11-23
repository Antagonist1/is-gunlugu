using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace IsGunlugu.Core;

[DataContract]
public sealed class SyncRecord
{
    [DataMember] public string Path { get; set; } = "";
    [DataMember] public string RemoteId { get; set; } = "";
    [DataMember] public string RemoteRevision { get; set; } = "";
    [DataMember] public string LocalHash { get; set; } = "";
}

/// <summary>What each file looked like at the last sync: the third point that tells who changed what.</summary>
public sealed class SyncState
{
    public IDictionary<string, SyncRecord> Records { get; } = new Dictionary<string, SyncRecord>(StringComparer.OrdinalIgnoreCase);

    public static SyncState Load(string file)
    {
        var state = new SyncState();
        try
        {
            using var stream = File.OpenRead(file);
            var records = (List<SyncRecord>?)new DataContractJsonSerializer(typeof(List<SyncRecord>)).ReadObject(stream);
            foreach (var record in records ?? new List<SyncRecord>()) state.Records[record.Path] = record;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SerializationException)
        {
            // No usable record means every file is compared by content, which cannot lose anything.
            state.Records.Clear();
        }
        return state;
    }

    public void Save(string file)
    {
        foreach (var pair in Records) pair.Value.Path = pair.Key;

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + ".tmp";
        using (var stream = File.Create(temp))
        {
            new DataContractJsonSerializer(typeof(List<SyncRecord>)).WriteObject(stream, Records.Values.ToList());
        }
        if (File.Exists(file)) File.Replace(temp, file, null);
        else File.Move(temp, file);
    }

    /// <summary>The file is exactly what the last sync left, so a change that just appeared came through Drive.</summary>
    public bool Matches(string path, byte[] content) =>
        Records.TryGetValue(path, out var record) && record.LocalHash == Hash(content);

    public static string Hash(byte[] content)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(content)).Replace("-", "");
    }
}
