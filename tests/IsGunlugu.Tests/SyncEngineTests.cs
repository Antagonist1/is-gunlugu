using System.Text;
using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class SyncEngineTests : IDisposable
{
    private sealed class FakeRemote : IRemoteStore
    {
        public sealed class Item { public string Path = ""; public string Id = ""; public int Revision; public byte[] Content = new byte[0]; }

        private int _nextId;
        public readonly List<Item> Items = new();
        public int Uploads;
        public int Trashes;
        public int FailUploadsAfter = int.MaxValue;
        public Action<string>? OnDownload;
        public Action<string>? OnRevision;

        public Item? Find(string path) => Items.SingleOrDefault(i => i.Path == path);

        public void Put(string path, string text)
        {
            var item = Find(path);
            if (item == null) Items.Add(item = new Item { Path = path, Id = "id" + ++_nextId });
            item.Content = Encoding.UTF8.GetBytes(text);
            item.Revision++;
        }

        public void AddDuplicate(string path, string text) =>
            Items.Add(new Item { Path = path, Id = "id" + ++_nextId, Revision = 1, Content = Encoding.UTF8.GetBytes(text) });

        public string Text(string path) => Encoding.UTF8.GetString(Find(path)!.Content);

        private static RemoteFile Info(Item i) => new(i.Path, i.Id, "r" + i.Revision, new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc));

        public IList<RemoteFile> List() => Items.Select(Info).ToList();

        public byte[] Download(string id)
        {
            var item = Items.Single(i => i.Id == id);
            OnDownload?.Invoke(item.Path);
            return item.Content;
        }

        public RemoteFile Upload(string path, byte[] content, string? existingId)
        {
            if (Uploads >= FailUploadsAfter) throw new SyncNetworkException("bağlantı gitti");
            Uploads++;
            var item = existingId == null ? null : Items.Single(i => i.Id == existingId);
            if (item == null) Items.Add(item = new Item { Path = path, Id = "id" + ++_nextId });
            item.Content = content;
            item.Revision++;
            return Info(item);
        }

        public void Trash(string id)
        {
            Trashes++;
            Items.RemoveAll(i => i.Id == id);
        }

        public string Revision(string id)
        {
            var item = Items.Single(i => i.Id == id);
            OnRevision?.Invoke(item.Path);
            return "r" + item.Revision;
        }
    }

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "IsGunluguTest", Guid.NewGuid().ToString("N"));
    private readonly FakeRemote _remote = new();

    private string Root => Path.Combine(_directory, "kok");
    private string Backups => Path.Combine(_directory, "yedekler");
    private string StateFile => Path.Combine(_directory, "sync.json");

    public SyncEngineTests() => Directory.CreateDirectory(Root);

    public void Dispose() => Directory.Delete(_directory, true);

    private SyncEngine Engine() => new(_remote, new LocalFolder(Root, Backups), SyncState.Load(StateFile), StateFile);

    private SyncResult Sync() => Engine().Run();

    private string Full(string path) => Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));

    private void Local(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Full(path))!);
        File.WriteAllText(Full(path), text, new UTF8Encoding(false));
    }

    private string LocalText(string path) => File.ReadAllText(Full(path));

    private bool LocalExists(string path) => File.Exists(Full(path));

    private string[] BackupTexts() => Directory.Exists(Backups) ? Directory.GetFiles(Backups).Select(File.ReadAllText).ToArray() : new string[0];

    /// <summary>A file both sides have, already in step.</summary>
    private void Synced(string path, string text)
    {
        Local(path, text);
        Assert.Empty(Sync().Conflicts);
        _remote.Uploads = 0;
    }

    [Fact]
    public void NothingChanged_NoCalls()
    {
        Synced("a.txt", "bir");

        var result = Sync();

        Assert.Equal(0, result.Changed);
        Assert.Equal(0, _remote.Uploads);
    }

    [Fact]
    public void NewLocal_Uploads()
    {
        Local("iş/a.txt", "bir");

        Sync();

        Assert.Equal("bir", _remote.Text("iş/a.txt"));
    }

    [Fact]
    public void NewRemote_Downloads()
    {
        _remote.Put("iş/a.txt", "uzak");

        Sync();

        Assert.Equal("uzak", LocalText("iş/a.txt"));
    }

    [Fact]
    public void LocalChanged_Uploads()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "iki");

        Sync();

        Assert.Equal("iki", _remote.Text("a.txt"));
    }

    [Fact]
    public void RemoteChanged_Downloads()
    {
        Synced("a.txt", "bir");
        _remote.Put("a.txt", "uzak");

        Sync();

        Assert.Equal("uzak", LocalText("a.txt"));
        Assert.Contains("bir", BackupTexts());
    }

    [Fact]
    public void BothChangedSameContent_OnlyRecords()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "ayni");
        _remote.Put("a.txt", "ayni");

        var result = Sync();

        Assert.Empty(result.Conflicts);
        Assert.Equal(0, _remote.Uploads);
        Assert.Equal(0, Sync().Changed);
    }

    [Fact]
    public void BothChangedDifferent_IsConflictAndTouchesNothing()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "yerel");
        _remote.Put("a.txt", "uzak");

        var conflict = Sync().Conflicts.Single();

        Assert.Equal("a.txt", conflict.Path);
        Assert.Equal("yerel", Encoding.UTF8.GetString(conflict.Local));
        Assert.Equal("uzak", Encoding.UTF8.GetString(conflict.Remote));
        Assert.Equal("yerel", LocalText("a.txt"));
        Assert.Equal("uzak", _remote.Text("a.txt"));
    }

    [Fact]
    public void LocalDeleted_TrashesRemote()
    {
        Synced("a.txt", "bir");
        File.Delete(Full("a.txt"));

        Sync();

        Assert.Null(_remote.Find("a.txt"));
    }

    [Fact]
    public void RemoteDeleted_DeletesLocalWithBackup()
    {
        Synced("a.txt", "bir");
        _remote.Items.Clear();

        Sync();

        Assert.False(LocalExists("a.txt"));
        Assert.Contains("bir", BackupTexts());
    }

    private void FourSynced()
    {
        Local("a.txt", "bir");
        Local("b.txt", "iki");
        Local("iş/c.txt", "üç");
        Local("iş/d.txt", "dört");
        Assert.Empty(Sync().Conflicts);
    }

    private void DeleteLocally(params string[] paths)
    {
        foreach (var path in paths) File.Delete(Full(path));
    }

    [Fact]
    public void MostFilesGoneLocally_NothingTrashedAndAsked()
    {
        FourSynced();
        DeleteLocally("a.txt", "b.txt", "iş/c.txt");

        var result = Sync();

        Assert.Equal(0, _remote.Trashes);
        Assert.Equal(new[] { "a.txt", "b.txt", "iş/c.txt" }, result.HeldDeletions.OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void MostFilesGoneFromDrive_LocalFilesStayAndAsked()
    {
        FourSynced();
        _remote.Items.Clear();

        var result = Sync();

        Assert.True(LocalExists("a.txt"));
        Assert.True(LocalExists("iş/d.txt"));
        Assert.Equal(4, result.HeldDeletions.Count);
    }

    [Fact]
    public void HalfOfTheFilesGone_IsHeld()
    {
        FourSynced();
        DeleteLocally("a.txt", "b.txt");

        Assert.Equal(2, Sync().HeldDeletions.Count);
        Assert.Equal(0, _remote.Trashes);
    }

    [Fact]
    public void OneFileOfSeveralDeleted_StillTravels()
    {
        FourSynced();
        DeleteLocally("a.txt");

        var result = Sync();

        Assert.Empty(result.HeldDeletions);
        Assert.Null(_remote.Find("a.txt"));
    }

    [Fact]
    public void WhileHeld_OtherFilesKeepSyncing()
    {
        FourSynced();
        DeleteLocally("a.txt", "b.txt", "iş/c.txt");
        Local("iş/d.txt", "dört değişti");

        Sync();

        Assert.Equal("dört değişti", _remote.Text("iş/d.txt"));
    }

    [Fact]
    public void HeldDeletions_AreAskedAgainUntilAnswered()
    {
        FourSynced();
        DeleteLocally("a.txt", "b.txt", "iş/c.txt");
        Sync();

        Assert.Equal(3, Sync().HeldDeletions.Count);
    }

    [Fact]
    public void ConfirmedDeletions_Travel()
    {
        FourSynced();
        DeleteLocally("a.txt", "b.txt", "iş/c.txt");
        var held = Sync().HeldDeletions;

        var result = Engine().Run(held);

        Assert.Empty(result.HeldDeletions);
        Assert.Equal(3, _remote.Trashes);
        Assert.NotNull(_remote.Find("iş/d.txt"));
    }

    [Fact]
    public void Confirmation_DoesNotCoverFilesThatVanishedAfterIt()
    {
        FourSynced();
        DeleteLocally("a.txt", "b.txt");
        var held = Sync().HeldDeletions;
        DeleteLocally("iş/c.txt");

        var result = Engine().Run(held);

        Assert.Equal(0, _remote.Trashes);
        Assert.Equal(3, result.HeldDeletions.Count);
    }

    [Fact]
    public void RestoreHeld_BringsBackFilesMissingLocally()
    {
        FourSynced();
        DeleteLocally("a.txt", "b.txt", "iş/c.txt");
        var held = Sync().HeldDeletions;

        Engine().RestoreHeld(held);
        var result = Sync();

        Assert.Empty(result.HeldDeletions);
        Assert.Equal("bir", LocalText("a.txt"));
        Assert.Equal("üç", LocalText("iş/c.txt"));
        Assert.Equal(0, _remote.Trashes);
    }

    [Fact]
    public void RestoreHeld_PutsFilesMissingFromDriveBack()
    {
        FourSynced();
        _remote.Items.Clear();
        var held = Sync().HeldDeletions;

        Engine().RestoreHeld(held);
        var result = Sync();

        Assert.Empty(result.HeldDeletions);
        Assert.Equal("bir", _remote.Text("a.txt"));
        Assert.Equal("dört", _remote.Text("iş/d.txt"));
    }

    [Fact]
    public void DescribeHeld_NamesAFewFiles()
    {
        Assert.Equal("a, c", SyncEngine.DescribeHeld(new[] { "a.txt", "iş/c.txt" }));
        Assert.Equal("a, b, c ve 2 dosya daha", SyncEngine.DescribeHeld(new[] { "a.txt", "b.txt", "iş/c.txt", "d.txt", "e.txt" }));
    }

    [Fact]
    public void SameConflict_TwoSyncsInARow_CountsAsUnchanged()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "yerel");
        _remote.Put("a.txt", "uzak");

        var first = Sync().Conflicts;
        var second = Sync().Conflicts;

        Assert.True(SyncConflict.AreSame(first, second));
    }

    [Fact]
    public void ConflictThatMovedOn_CountsAsChanged()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "yerel");
        _remote.Put("a.txt", "uzak");
        var first = Sync().Conflicts;

        _remote.Put("a.txt", "uzak yine");

        Assert.False(SyncConflict.AreSame(first, Sync().Conflicts));
    }

    [Fact]
    public void Matches_FileAsDriveBroughtIt()
    {
        _remote.Put("iş/a.txt", "uzak");
        Sync();

        Assert.True(SyncState.Load(StateFile).Matches("iş/a.txt", File.ReadAllBytes(Full("iş/a.txt"))));
    }

    [Fact]
    public void Matches_NotAfterAnEditElsewhere()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "not defterinde değişti");

        Assert.False(SyncState.Load(StateFile).Matches("a.txt", File.ReadAllBytes(Full("a.txt"))));
    }

    [Fact]
    public void Matches_NotForAFileNeverSynced()
    {
        Local("a.txt", "bir");

        Assert.False(SyncState.Load(StateFile).Matches("a.txt", File.ReadAllBytes(Full("a.txt"))));
    }

    [Fact]
    public void LocalDeletedRemoteChanged_Downloads()
    {
        Synced("a.txt", "bir");
        File.Delete(Full("a.txt"));
        _remote.Put("a.txt", "uzak");

        Sync();

        Assert.Equal("uzak", LocalText("a.txt"));
        Assert.Equal(0, _remote.Trashes);
    }

    [Fact]
    public void LocalChangedRemoteDeleted_Uploads()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "yerel");
        _remote.Items.Clear();

        Sync();

        Assert.Equal("yerel", _remote.Text("a.txt"));
        Assert.Equal("yerel", LocalText("a.txt"));
    }

    [Fact]
    public void BothDeleted_ClearsRecord()
    {
        Synced("a.txt", "bir");
        File.Delete(Full("a.txt"));
        _remote.Items.Clear();

        Sync();
        _remote.Put("a.txt", "yeniden");
        var result = Sync();

        Assert.Empty(result.Conflicts);
        Assert.Equal("yeniden", LocalText("a.txt"));
    }

    [Fact]
    public void BothNewDifferent_IsConflict()
    {
        Local("a.txt", "yerel");
        _remote.Put("a.txt", "uzak");

        Assert.Single(Sync().Conflicts);
        Assert.Equal("yerel", LocalText("a.txt"));
    }

    [Fact]
    public void BothNewSame_NeedsNoUpload()
    {
        Local("a.txt", "ayni");
        _remote.Put("a.txt", "ayni");

        Assert.Empty(Sync().Conflicts);
        Assert.Equal(0, _remote.Uploads);
    }

    private SyncConflict MakeConflict()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "yerel");
        _remote.Put("a.txt", "uzak");
        return Sync().Conflicts.Single();
    }

    [Fact]
    public void Resolve_KeepLocal_UploadsAndBacksUpRemote()
    {
        var conflict = MakeConflict();

        Engine().Resolve(conflict, keepLocal: true);

        Assert.Equal("yerel", _remote.Text("a.txt"));
        Assert.Contains("uzak", BackupTexts());
        Assert.Empty(Sync().Conflicts);
    }

    [Fact]
    public void Resolve_KeepRemote_WritesAndBacksUpLocal()
    {
        var conflict = MakeConflict();

        Engine().Resolve(conflict, keepLocal: false);

        Assert.Equal("uzak", LocalText("a.txt"));
        Assert.Contains("yerel", BackupTexts());
        Assert.Empty(Sync().Conflicts);
        Assert.Equal(0, Sync().Changed);
    }

    [Fact]
    public void Resolve_DoesNothingIfLocalChangedAfterTheQuestion()
    {
        var conflict = MakeConflict();
        Local("a.txt", "daha yeni");

        Engine().Resolve(conflict, keepLocal: false);

        Assert.Equal("daha yeni", LocalText("a.txt"));
    }

    [Fact]
    public void Unresolved_IsAskedAgainNextRun()
    {
        MakeConflict();

        Assert.Single(Sync().Conflicts);
    }

    [Fact]
    public void OneConflictDoesNotStopOthers()
    {
        MakeConflict();
        Local("b.txt", "yeni");

        var result = Sync();

        Assert.Single(result.Conflicts);
        Assert.Equal("yeni", _remote.Text("b.txt"));
    }

    [Fact]
    public void NetworkFailsMidway_DoneFilesKept_NextRunFinishes()
    {
        Local("a.txt", "bir");
        Local("b.txt", "iki");
        Local("c.txt", "uc");
        _remote.FailUploadsAfter = 1;

        Assert.Throws<SyncNetworkException>(() => Sync());
        var uploadedBefore = _remote.Items.Count;
        _remote.FailUploadsAfter = int.MaxValue;
        Sync();

        Assert.Equal(1, uploadedBefore);
        Assert.Equal(3, _remote.Items.Count);
        Assert.Equal(1 + 2, _remote.Uploads);
    }

    [Fact]
    public void LocalChangedDuringRun_NotOverwritten()
    {
        Synced("a.txt", "bir");
        _remote.Put("a.txt", "uzak");
        _remote.OnDownload = _ => Local("a.txt", "tam o sirada yazildi");

        Sync();

        Assert.Equal("tam o sirada yazildi", LocalText("a.txt"));
    }

    [Fact]
    public void RemoteChangedBeforeUpload_NotOverwritten()
    {
        Synced("a.txt", "bir");
        Local("a.txt", "yerel");
        _remote.OnRevision = _ =>
        {
            _remote.OnRevision = null;
            _remote.Put("a.txt", "uzak o sirada degisti");
        };

        Sync();

        Assert.Equal("uzak o sirada degisti", _remote.Text("a.txt"));
        Assert.Equal("yerel", LocalText("a.txt"));
    }

    [Fact]
    public void DuplicateRemoteNames_ReportedAndUntouched()
    {
        _remote.Put("a.txt", "bir");
        _remote.AddDuplicate("a.txt", "iki");
        Local("a.txt", "yerel");

        var result = Sync();

        Assert.Single(result.Errors);
        Assert.Equal("yerel", LocalText("a.txt"));
        Assert.Equal(2, _remote.Items.Count);
        Assert.Equal(0, _remote.Uploads);
    }

    [Fact]
    public void CaseOnlyDifferentNames_ReportedAndUntouched()
    {
        _remote.Put("iş/Not.txt", "bir");
        _remote.Put("iş/not.txt", "iki");

        var result = Sync();

        Assert.Single(result.Errors);
        Assert.False(LocalExists("iş/Not.txt"));
        Assert.False(LocalExists("iş/not.txt"));
    }

    [Fact]
    public void RemotePathOutsideRoot_ReportedOthersContinue()
    {
        _remote.Put("../disari.txt", "kotu");
        _remote.Put("a.txt", "iyi");

        var result = Sync();

        Assert.Single(result.Errors);
        Assert.Equal("iyi", LocalText("a.txt"));
        Assert.False(File.Exists(Path.Combine(_directory, "disari.txt")));
    }
}
