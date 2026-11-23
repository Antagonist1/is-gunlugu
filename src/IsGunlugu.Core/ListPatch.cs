using System.Collections.ObjectModel;

namespace IsGunlugu.Core;

/// <summary>
/// Brings a list on screen to a freshly built one by moving, adding and removing, and keeps the items that did not
/// change. A list handed over whole is drawn again from scratch; patched, only the rows that changed are drawn.
/// </summary>
public static class ListPatch
{
    /// <param name="same">The old item shows exactly what the fresh one would, so the old one can stay.</param>
    /// <param name="keep">Called for each old item that stays, with the fresh one it stands for.</param>
    public static void Apply<T>(ObservableCollection<T> target, IList<T> fresh, Func<T, T, bool> same, Action<T, T>? keep = null)
    {
        for (var i = 0; i < fresh.Count; i++)
        {
            var want = fresh[i];
            var found = -1;
            for (var j = i; j < target.Count; j++)
            {
                if (!same(target[j], want)) continue;
                found = j;
                break;
            }

            if (found < 0)
            {
                target.Insert(i, want);
                continue;
            }

            if (found != i) target.Move(found, i);
            keep?.Invoke(target[i], want);
        }

        while (target.Count > fresh.Count) target.RemoveAt(target.Count - 1);
    }
}
