namespace IsGunlugu.Core;

/// <summary>Turns a drag in a list into a move in the file, where the list may be hiding some of the day's work.</summary>
public static class Reorder
{
    /// <summary>
    /// The item one drag moved, from the order before and after. Two neighbours swapping could be either of them;
    /// both readings give the same order, so either answer is right.
    /// </summary>
    public static T? Moved<T>(IList<T> before, IList<T> after) where T : class
    {
        var first = 0;
        while (first < before.Count && first < after.Count && ReferenceEquals(before[first], after[first])) first++;
        if (first == before.Count) return null;

        var last = before.Count - 1;
        while (last > first && ReferenceEquals(before[last], after[last])) last--;

        return ReferenceEquals(before[first], after[last]) ? before[first] : after[first];
    }

    /// <summary>
    /// The entry the moved one goes in front of: the next line shown, or else whatever follows the line it now sits under,
    /// hidden work included. Null means the end of the day.
    /// </summary>
    public static T? Before<T>(IList<T> dayOrder, IList<T> shownAfter, T moved) where T : class
    {
        var at = shownAfter.IndexOf(moved);
        if (at < 0) return null;
        if (at + 1 < shownAfter.Count) return shownAfter[at + 1];
        if (at == 0) return null;

        var rest = dayOrder.Where(x => !ReferenceEquals(x, moved)).ToList();
        var previous = rest.IndexOf(shownAfter[at - 1]);
        return previous >= 0 && previous + 1 < rest.Count ? rest[previous + 1] : null;
    }
}
