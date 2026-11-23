using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace IsGunlugu.Core;

/// <summary>The "İş Günlüğü" folder in the person's Drive, seen as a set of relative paths.</summary>
public sealed class DriveStore(GoogleAuth auth, HttpSend? send = null) : IRemoteStore
{
    private const string RootName = "İş Günlüğü";
    private const string FolderType = "application/vnd.google-apps.folder";
    private const string Api = "https://www.googleapis.com/drive/v3/files";
    private const string UploadApi = "https://www.googleapis.com/upload/drive/v3/files";
    private const string FileFields = "id,name,mimeType,headRevisionId,modifiedTime";

    private readonly HttpSend _send = send ?? GoogleHttp.Send;
    private readonly Dictionary<string, string> _folders = new(StringComparer.OrdinalIgnoreCase);

    [DataContract]
    private sealed class DriveItem
    {
        [DataMember(Name = "id")] public string Id { get; set; } = "";
        [DataMember(Name = "name")] public string Name { get; set; } = "";
        [DataMember(Name = "mimeType")] public string MimeType { get; set; } = "";
        [DataMember(Name = "headRevisionId")] public string? Revision { get; set; }
        [DataMember(Name = "modifiedTime")] public string? ModifiedTime { get; set; }
    }

    [DataContract]
    private sealed class DriveList
    {
        [DataMember(Name = "files")] public List<DriveItem>? Files { get; set; }
        [DataMember(Name = "nextPageToken")] public string? NextPageToken { get; set; }
    }

    [DataContract]
    private sealed class Metadata
    {
        [DataMember(Name = "name")] public string Name { get; set; } = "";
        [DataMember(Name = "mimeType", EmitDefaultValue = false)] public string? MimeType { get; set; }
        [DataMember(Name = "parents", EmitDefaultValue = false)] public List<string>? Parents { get; set; }
    }

    public IList<RemoteFile> List()
    {
        var files = new List<RemoteFile>();
        _folders.Clear();

        var root = FindFolder("root", RootName);
        if (root == null) return files;

        _folders[""] = root;
        Walk(root, "", files);
        return files;
    }

    public byte[] Download(string id) => Call("GET", $"{Api}/{id}?alt=media", null, null);

    public RemoteFile Upload(string path, byte[] content, string? existingId)
    {
        var name = path.Substring(path.LastIndexOf('/') + 1);
        var metadata = existingId != null
            ? new Metadata { Name = name }
            : new Metadata { Name = name, Parents = new List<string> { EnsureFolder(path.Contains("/") ? path.Substring(0, path.LastIndexOf('/')) : "") } };

        var boundary = "isgunlugu" + Guid.NewGuid().ToString("N");
        var body = Multipart(boundary, ToJson(metadata), content);
        var url = (existingId == null ? UploadApi : UploadApi + "/" + existingId) + "?uploadType=multipart&fields=" + FileFields;
        var item = Parse<DriveItem>(Call(existingId == null ? "POST" : "PATCH", url, body, "multipart/related; boundary=" + boundary));
        return ToRemote(path, item);
    }

    public void Trash(string id) => Call("PATCH", $"{Api}/{id}", Encoding.UTF8.GetBytes("""{"trashed":true}"""), "application/json");

    public string Revision(string id)
    {
        var result = Send("GET", $"{Api}/{id}?fields=headRevisionId,trashed", null, null);
        if (result.Status == 404) return "";

        Check(result);
        return Parse<DriveItem>(result.Body).Revision ?? "";
    }

    private void Walk(string folderId, string prefix, List<RemoteFile> files)
    {
        foreach (var item in Children($"'{folderId}' in parents and trashed=false"))
        {
            var path = prefix + item.Name;
            if (item.MimeType == FolderType)
            {
                _folders[path] = item.Id;
                Walk(item.Id, path + "/", files);
            }
            else if (item.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(ToRemote(path, item));
            }
        }
    }

    private IEnumerable<DriveItem> Children(string query)
    {
        string? page = null;
        do
        {
            var url = $"{Api}?pageSize=1000&orderBy=createdTime&fields=nextPageToken,files({FileFields})&q={Uri.EscapeDataString(query)}";
            if (page != null) url += "&pageToken=" + Uri.EscapeDataString(page);

            var list = Parse<DriveList>(Call("GET", url, null, null));
            foreach (var item in list.Files ?? new List<DriveItem>()) yield return item;
            page = list.NextPageToken;
        }
        while (page != null);
    }

    private string? FindFolder(string parentId, string name)
    {
        var escaped = name.Replace("\\", "\\\\").Replace("'", "\\'");
        return Children($"name='{escaped}' and mimeType='{FolderType}' and '{parentId}' in parents and trashed=false").FirstOrDefault()?.Id;
    }

    private string EnsureFolder(string relative)
    {
        if (_folders.TryGetValue(relative, out var known)) return known;

        string id;
        if (relative.Length == 0)
        {
            id = FindFolder("root", RootName) ?? CreateFolder(RootName, "root");
        }
        else
        {
            var cut = relative.LastIndexOf('/');
            var parent = EnsureFolder(cut < 0 ? "" : relative.Substring(0, cut));
            var name = relative.Substring(cut + 1);
            id = FindFolder(parent, name) ?? CreateFolder(name, parent);
        }

        _folders[relative] = id;
        return id;
    }

    private string CreateFolder(string name, string parentId)
    {
        var body = ToJson(new Metadata { Name = name, MimeType = FolderType, Parents = new List<string> { parentId } });
        return Parse<DriveItem>(Call("POST", $"{Api}?fields={FileFields}", body, "application/json")).Id;
    }

    private static RemoteFile ToRemote(string path, DriveItem item)
    {
        var modified = item.ModifiedTime == null
            ? DateTime.MinValue
            : DateTime.Parse(item.ModifiedTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return new RemoteFile(path, item.Id, item.Revision ?? "", modified);
    }

    private static byte[] Multipart(string boundary, byte[] metadata, byte[] content)
    {
        using var body = new MemoryStream();
        void Text(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            body.Write(bytes, 0, bytes.Length);
        }

        Text($"--{boundary}\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n");
        body.Write(metadata, 0, metadata.Length);
        Text($"\r\n--{boundary}\r\nContent-Type: text/plain\r\n\r\n");
        body.Write(content, 0, content.Length);
        Text($"\r\n--{boundary}--\r\n");
        return body.ToArray();
    }

    private HttpResult Send(string method, string url, byte[]? body, string? contentType) =>
        _send(method, url, auth.AccessToken(), body, contentType);

    private byte[] Call(string method, string url, byte[]? body, string? contentType)
    {
        var result = Send(method, url, body, contentType);
        Check(result);
        return result.Body;
    }

    private static void Check(HttpResult result)
    {
        if (result.Status == 401) throw new SyncAuthException("Google erişimi sona erdi. Hesabı yeniden bağla.");
        if (result.Status < 200 || result.Status >= 300) throw new SyncNetworkException($"Google Drive isteği başarısız oldu ({result.Status}).");
    }

    private static T Parse<T>(byte[] body) where T : class
    {
        using var stream = new MemoryStream(body);
        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream)!;
    }

    private static byte[] ToJson(Metadata metadata)
    {
        using var buffer = new MemoryStream();
        new DataContractJsonSerializer(typeof(Metadata)).WriteObject(buffer, metadata);
        return buffer.ToArray();
    }
}
