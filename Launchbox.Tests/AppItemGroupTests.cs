using Launchbox.Models;
using System.Linq;
using Xunit;

namespace Launchbox.Tests;

public class AppItemGroupTests
{
    private static AppItem MakeItem(string name) => new() { Name = name, Path = $@"C:\{name}.lnk" };

    private static AppItemGroup MakeGroup(params string[] names) =>
        new("TestFolder", @"C:\TestFolder", names.Select(MakeItem));

    [Fact]
    public void Constructor_PopulatesItems()
    {
        var group = MakeGroup("Alpha", "Beta");
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void ApplyFilter_FiltersItems()
    {
        var group = MakeGroup("Alpha", "Beta", "Alphabet");
        group.ApplyFilter("alp");
        Assert.Equal(2, group.Count);
        Assert.All(group, item => Assert.Contains("Alp", item.Name));
    }

    [Fact]
    public void ApplyFilter_ClearsFilter_WhenEmpty()
    {
        var group = MakeGroup("Alpha", "Beta");
        group.ApplyFilter("alp");
        Assert.Single(group);

        group.ApplyFilter(null);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void ApplyFilter_EmptiesGroup_WhenNoMatches()
    {
        var group = MakeGroup("Alpha", "Beta");
        group.ApplyFilter("zzz");
        Assert.Empty(group);
    }

    [Fact]
    public void IsCollapsed_ShowsPlaceholder()
    {
        var group = MakeGroup("Alpha", "Beta");
        group.IsCollapsed = true;
        Assert.Single(group);
        Assert.Equal(string.Empty, group[0].Name);
    }

    [Fact]
    public void IsCollapsed_Expand_RestoresItems()
    {
        var group = MakeGroup("Alpha", "Beta");
        group.IsCollapsed = true;
        group.IsCollapsed = false;
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void IsCollapsed_Expand_ReappliesFilter()
    {
        var group = MakeGroup("Alpha", "Beta", "Alphabet");
        group.ApplyFilter("alp");
        Assert.Equal(2, group.Count);

        group.IsCollapsed = true;
        group.IsCollapsed = false;

        Assert.Equal(2, group.Count);
        Assert.All(group, item => Assert.Contains("Alp", item.Name));
    }

    [Fact]
    public void ApplyFilter_CollapsedGroup_HidesHeader_WhenNoMatches()
    {
        var group = MakeGroup("Alpha", "Beta");
        group.IsCollapsed = true;
        Assert.Single(group); // placeholder

        group.ApplyFilter("zzz");
        Assert.Empty(group); // no matches → CVS hides group
    }

    [Fact]
    public void ApplyFilter_CollapsedGroup_KeepsPlaceholder_WhenHasMatches()
    {
        var group = MakeGroup("Alpha", "Beta");
        group.IsCollapsed = true;

        group.ApplyFilter("alp");
        Assert.Single(group); // placeholder stays — items match
        Assert.Equal(string.Empty, group[0].Name);
    }

    [Fact]
    public void ApplyFilter_CollapsedGroup_RestoresPlaceholder_WhenFilterCleared()
    {
        var group = MakeGroup("Alpha", "Beta");
        group.IsCollapsed = true;

        group.ApplyFilter("zzz");
        Assert.Empty(group); // hidden

        group.ApplyFilter(null);
        Assert.Single(group); // placeholder restored
    }

    [Fact]
    public void ApplyFilter_MinimizesChurn_WhenAlreadyFiltered()
    {
        var group = MakeGroup("Alpha", "Beta");
        group.ApplyFilter("alp");

        int changeCount = 0;
        group.CollectionChanged += (_, _) => changeCount++;

        // Same filter — should not trigger change
        group.ApplyFilter("alp");
        Assert.Equal(0, changeCount);
    }

    [Fact]
    public void ApplyFilter_PrefixMatchRanksBeforeSubstringMatch()
    {
        var group = MakeGroup("VisualStudio", "Studio", "AnotherStudio");
        group.ApplyFilter("studio");

        var names = group.Select(a => a.Name).ToList();
        Assert.Equal("Studio", names[0]);
        Assert.Equal(3, names.Count);
    }

    [Fact]
    public void Add_Throws_NotSupportedException()
    {
        // Bug: inherited Add bypasses _allItems, silently corrupting filter/expand state.
        var group = MakeGroup("Alpha");
        Assert.Throws<NotSupportedException>(() => group.Add(MakeItem("Beta")));
    }

    [Fact]
    public void Remove_Throws_NotSupportedException()
    {
        var group = MakeGroup("Alpha", "Beta");
        Assert.Throws<NotSupportedException>(() => group.Remove(group[0]));
    }

    [Fact]
    public void Clear_Throws_NotSupportedException()
    {
        var group = MakeGroup("Alpha");
        Assert.Throws<NotSupportedException>(() => group.Clear());
    }

    [Fact]
    public void IndexerSet_Throws_NotSupportedException()
    {
        var group = MakeGroup("Alpha");
        Assert.Throws<NotSupportedException>(() => group[0] = MakeItem("Replacement"));
    }

    [Fact]
    public void ApplyFilter_StillWorks_AfterGuardActivation()
    {
        // Internal ReplaceAll must still work via the guarded helper.
        var group = MakeGroup("Alpha", "Beta");
        group.ApplyFilter("alp");
        Assert.Single(group);
        group.ApplyFilter(null);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void ReplaceAll_BatchesNotifications()
    {
        var group = MakeGroup("Alpha", "Beta", "Gamma", "Delta");
        int changeCount = 0;
        group.CollectionChanged += (_, _) => changeCount++;

        // "alp" matches only Alpha — removes 3 items in a single batched Reset
        group.ApplyFilter("alp");
        Assert.Equal(1, changeCount);
    }
}
