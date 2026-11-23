using System.Collections.ObjectModel;
using System.Collections.Specialized;
using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public class ListPatchTests
{
    private sealed class Item(string key, int version = 0)
    {
        public string Key { get; } = key;
        public int Version { get; } = version;
    }

    private static bool Same(Item old, Item fresh) => old.Key == fresh.Key && old.Version == fresh.Version;

    private static (ObservableCollection<Item> List, List<NotifyCollectionChangedAction> Changes) Make(params Item[] items)
    {
        var list = new ObservableCollection<Item>(items);
        var changes = new List<NotifyCollectionChangedAction>();
        list.CollectionChanged += (_, e) => changes.Add(e.Action);
        return (list, changes);
    }

    [Fact]
    public void UnchangedItemsStayTheSameObjectsAndNothingIsRaised()
    {
        var a = new Item("a");
        var b = new Item("b");
        var (list, changes) = Make(a, b);

        ListPatch.Apply(list, [new Item("a"), new Item("b")], Same);

        Assert.Same(a, list[0]);
        Assert.Same(b, list[1]);
        Assert.Empty(changes);
    }

    [Fact]
    public void AChangedItemIsReplacedAndTheOthersKept()
    {
        var a = new Item("a");
        var c = new Item("c");
        var (list, _) = Make(a, new Item("b"), c);
        var changedB = new Item("b", 1);

        ListPatch.Apply(list, [new Item("a"), changedB, new Item("c")], Same);

        Assert.Equal(new[] { "a", "b", "c" }, list.Select(i => i.Key));
        Assert.Same(a, list[0]);
        Assert.Same(changedB, list[1]);
        Assert.Same(c, list[2]);
    }

    [Fact]
    public void MovedItemsAreMovedNotRemade()
    {
        var a = new Item("a");
        var b = new Item("b");
        var c = new Item("c");
        var (list, changes) = Make(a, b, c);

        ListPatch.Apply(list, [new Item("c"), new Item("a"), new Item("b")], Same);

        Assert.Equal(new[] { c, a, b }, list);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, changes);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Add, changes);
    }

    [Fact]
    public void AddsAndRemovesToMatch()
    {
        var b = new Item("b");
        var (list, _) = Make(new Item("a"), b, new Item("c"));
        var d = new Item("d");

        ListPatch.Apply(list, [new Item("b"), d], Same);

        Assert.Equal(new[] { "b", "d" }, list.Select(i => i.Key));
        Assert.Same(b, list[0]);
        Assert.Same(d, list[1]);
    }

    [Fact]
    public void EmptiesAndFills()
    {
        var (list, _) = Make(new Item("a"), new Item("b"));
        ListPatch.Apply(list, [], Same);
        Assert.Empty(list);

        ListPatch.Apply(list, [new Item("x"), new Item("y")], Same);
        Assert.Equal(new[] { "x", "y" }, list.Select(i => i.Key));
    }

    [Fact]
    public void KeptItemsAreHandedToTheKeepStep()
    {
        var a = new Item("a");
        var (list, _) = Make(a, new Item("b"));
        var kept = new List<(Item Old, Item Fresh)>();
        var freshA = new Item("a");

        ListPatch.Apply(list, [freshA, new Item("b", 2)], Same, (old, fresh) => kept.Add((old, fresh)));

        Assert.Single(kept);
        Assert.Same(a, kept[0].Old);
        Assert.Same(freshA, kept[0].Fresh);
    }

    [Fact]
    public void DuplicateKeysAreEachMatchedOnce()
    {
        var first = new Item("a");
        var second = new Item("a");
        var (list, _) = Make(first, second);

        ListPatch.Apply(list, [new Item("a"), new Item("a"), new Item("a")], Same);

        Assert.Equal(3, list.Count);
        Assert.Same(first, list[0]);
        Assert.Same(second, list[1]);
    }
}
