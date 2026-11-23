using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class DriveStoreTests
{
    private sealed class MemoryTokens : ITokenStore
    {
        public string? Json;
        public string? Load() => Json;
        public void Save(string json) => Json = json;
        public void Clear() => Json = null;
    }

    private const string TokenJson = """{"access_token":"a1","refresh_token":"r1","expires_in":3600}""";
    private const string Root = """{"files":[{"id":"root1","name":"İş Günlüğü","mimeType":"application/vnd.google-apps.folder","modifiedTime":"2026-11-10T10:00:00.000Z"}]}""";

    private readonly List<string> _calls = new();
    private readonly List<string> _bodies = new();
    private readonly Dictionary<string, string> _queries = new();
    private int _status = 200;
    private string _post = """{"id":"new1","name":"x","mimeType":"text/plain","headRevisionId":"rev1","modifiedTime":"2026-11-10T10:00:00.000Z"}""";

    private DriveStore Store()
    {
        HttpSend send = (method, url, _, body, _) =>
        {
            if (url.Contains("oauth2.googleapis.com")) return new HttpResult(200, Encoding.UTF8.GetBytes(TokenJson));

            var decoded = Uri.UnescapeDataString(url);
            _calls.Add(method + " " + decoded);
            _bodies.Add(body == null ? "" : Encoding.UTF8.GetString(body));
            if (_status != 200) return new HttpResult(_status, new byte[0]);

            foreach (var query in _queries.OrderBy(q => q.Key.StartsWith("pageToken") ? 0 : 1))
            {
                if (decoded.Contains(query.Key)) return new HttpResult(200, Encoding.UTF8.GetBytes(query.Value));
            }
            return new HttpResult(200, Encoding.UTF8.GetBytes(method == "GET" ? """{"files":[]}""" : _post));
        };

        var auth = new GoogleAuth("client", null, new MemoryTokens(), send);
        auth.ExchangeCode("code", "verifier", "http://127.0.0.1:1/");
        return new DriveStore(auth, send);
    }

    [Fact]
    public void List_NoRootFolderYet_IsEmpty() => Assert.Empty(Store().List());

    [Fact]
    public void List_BuildsNestedPaths()
    {
        _queries["'root' in parents"] = Root;
        _queries["'root1' in parents"] = """{"files":[{"id":"f1","name":"iş","mimeType":"application/vnd.google-apps.folder","modifiedTime":"2026-11-10T10:00:00.000Z"},{"id":"t1","name":"ev.txt","mimeType":"text/plain","headRevisionId":"r1","modifiedTime":"2026-11-10T10:00:00.000Z"}]}""";
        _queries["'f1' in parents"] = """{"files":[{"id":"t2","name":"notlar.txt","mimeType":"text/plain","headRevisionId":"r2","modifiedTime":"2026-11-10T11:30:00.000Z"}]}""";

        var files = Store().List().OrderBy(f => f.Path, StringComparer.Ordinal).ToList();

        Assert.Equal(new[] { "ev.txt", "iş/notlar.txt" }, files.Select(f => f.Path));
        Assert.Equal("t2", files[1].Id);
        Assert.Equal("r2", files[1].Revision);
        Assert.Equal(new DateTime(2026, 11, 10, 11, 30, 0, DateTimeKind.Utc), files[1].ModifiedUtc);
        Assert.Equal(DateTimeKind.Utc, files[1].ModifiedUtc.Kind);
    }

    [Fact]
    public void List_FollowsNextPageToken()
    {
        _queries["'root' in parents"] = Root;
        _queries["'root1' in parents"] = """{"nextPageToken":"p2","files":[{"id":"t1","name":"a.txt","mimeType":"text/plain","headRevisionId":"r1","modifiedTime":"2026-11-10T10:00:00.000Z"}]}""";
        _queries["pageToken=p2"] = """{"files":[{"id":"t2","name":"b.txt","mimeType":"text/plain","headRevisionId":"r2","modifiedTime":"2026-11-10T10:00:00.000Z"}]}""";

        Assert.Equal(2, Store().List().Count);
    }

    [Fact]
    public void List_SkipsNonTxt()
    {
        _queries["'root' in parents"] = Root;
        _queries["'root1' in parents"] = """{"files":[{"id":"t1","name":"resim.png","mimeType":"image/png","headRevisionId":"r1","modifiedTime":"2026-11-10T10:00:00.000Z"}]}""";

        Assert.Empty(Store().List());
    }

    [Fact]
    public void List_AsksOnlyForFilesNotInTrash()
    {
        Store().List();

        Assert.All(_calls, c => Assert.Contains("trashed=false", c));
    }

    [Fact]
    public void Upload_CreatesMissingFolders()
    {
        var store = Store();

        var file = store.Upload("iş/notlar.txt", Encoding.UTF8.GetBytes("içerik"), null);

        Assert.Equal("new1", file.Id);
        Assert.Equal("rev1", file.Revision);
        Assert.Equal("iş/notlar.txt", file.Path);
        var created = _bodies.Where(b => b.Contains("vnd.google-apps.folder") && !b.Contains("içerik")).ToList();
        Assert.Contains(created, b => b.Contains("İş Günlüğü"));
        Assert.Contains(created, b => b.Contains("\"iş\""));
        Assert.Contains(_bodies, b => b.Contains("içerik") && b.Contains("notlar.txt"));
    }

    [Fact]
    public void Upload_ExistingFile_PatchesItWithoutCreating()
    {
        Store().Upload("ev.txt", Encoding.UTF8.GetBytes("yeni"), "t1");

        var call = Assert.Single(_calls);
        Assert.StartsWith("PATCH ", call);
        Assert.Contains("/upload/drive/v3/files/t1", call);
    }

    [Fact]
    public void Trash_MarksFileTrashedInsteadOfDeleting()
    {
        Store().Trash("t1");

        Assert.StartsWith("PATCH ", _calls.Single());
        Assert.Contains("\"trashed\":true", _bodies.Single());
    }

    [Fact]
    public void Http401_ThrowsSyncAuthException()
    {
        _status = 401;

        Assert.Throws<SyncAuthException>(() => Store().List());
    }

    [Fact]
    public void Http500_ThrowsSyncNetworkException()
    {
        _status = 500;

        Assert.Throws<SyncNetworkException>(() => Store().List());
    }
}
