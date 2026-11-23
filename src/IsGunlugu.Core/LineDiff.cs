namespace IsGunlugu.Core;

public enum DiffSide { Same, Local, Remote }

public sealed class DiffBlock(DiffSide side, IList<string> lines)
{
    public DiffSide Side { get; } = side;
    public IList<string> Lines { get; } = lines;
}

/// <summary>Which lines differ between two versions. Line endings do not count as a difference.</summary>
public static class LineDiff
{
    public static IList<DiffBlock> Compare(string local, string remote)
    {
        var a = Lines(local);
        var b = Lines(remote);

        // Longest common subsequence, filled from the end so the walk below can go forwards.
        var common = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                common[i, j] = a[i] == b[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        var blocks = new List<DiffBlock>();
        int x = 0, y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y])
            {
                Add(blocks, DiffSide.Same, a[x]);
                x++;
                y++;
            }
            else if (x < a.Length && (y == b.Length || common[x + 1, y] >= common[x, y + 1]))
            {
                Add(blocks, DiffSide.Local, a[x++]);
            }
            else
            {
                Add(blocks, DiffSide.Remote, b[y++]);
            }
        }
        return blocks;
    }

    private static void Add(List<DiffBlock> blocks, DiffSide side, string line)
    {
        if (blocks.Count == 0 || blocks[blocks.Count - 1].Side != side) blocks.Add(new DiffBlock(side, new List<string>()));
        blocks[blocks.Count - 1].Lines.Add(line);
    }

    private static string[] Lines(string text)
    {
        var normal = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normal.EndsWith("\n", StringComparison.Ordinal)) normal = normal.Substring(0, normal.Length - 1);
        return normal.Length == 0 ? new string[0] : normal.Split('\n');
    }
}
