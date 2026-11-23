using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public class EntryPartsCacheTests
{
    [Fact]
    public void PartsFollowTheBodyAfterItChanges()
    {
        var entry = NoteDocument.Parse("----18/11/2026\n🚨iş\n\t🚨bir\n").Days[0].Entries[0];
        Assert.Single(entry.SubTasks);
        Assert.Same(entry.Parts, entry.Parts);

        entry.Body.Add("\t👍[Done]iki");
        Assert.Equal(2, entry.SubTasks.Count);
        Assert.True(entry.SubTasks[1].IsDone);

        entry.Body[0] = "\t👍[Done]bir";
        Assert.True(entry.SubTasks[0].IsDone);
        Assert.All(entry.SubTasks, t => Assert.True(t.IsDone));
    }
}
