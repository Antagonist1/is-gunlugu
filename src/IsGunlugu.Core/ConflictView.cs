namespace IsGunlugu.Core;

public sealed class ConflictRow(DiffSide side, string tag, string text)
{
    public string Tag { get; } = tag;
    public string Text { get; } = text;
    public bool IsSame => side == DiffSide.Same;
    public bool IsLocal => side == DiffSide.Local;
    public bool IsRemote => side == DiffSide.Remote;
}

/// <summary>The lines a person needs to see to choose between two versions: only the ones that differ.</summary>
public static class ConflictView
{
    public static IList<ConflictRow> Rows(string local, string remote, string localName, string remoteName, string onlyLocal, string onlyRemote)
    {
        var blocks = LineDiff.Compare(local, remote);
        var rows = new List<ConflictRow>();

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.Side == DiffSide.Same)
            {
                rows.Add(new ConflictRow(DiffSide.Same, "", $"{block.Lines.Count} satır aynı"));
                continue;
            }

            var paired = block.Side == DiffSide.Local
                ? i + 1 < blocks.Count && blocks[i + 1].Side == DiffSide.Remote
                : i > 0 && blocks[i - 1].Side == DiffSide.Local;
            var name = block.Side == DiffSide.Local ? localName : remoteName;
            var only = block.Side == DiffSide.Local ? onlyLocal : onlyRemote;
            foreach (var line in block.Lines) rows.Add(new ConflictRow(block.Side, paired ? name : only, line));
        }
        return rows;
    }
}
