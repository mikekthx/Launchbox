namespace Launchbox.Tests;

using Xunit;
using System.Collections.Generic;
using Launchbox;

public class VisualTreeFinderTests
{
    private class TestNode
    {
        public string Name { get; set; } = "";
        public List<TestNode> Children { get; } = new();
    }

    private class TargetNode : TestNode { }
    private class OtherNode : TestNode { }

    private class TestVisualTreeService : IVisualTreeService
    {
        public int GetChildrenCount(object parent)
        {
            if (parent is TestNode node) return node.Children.Count;
            return 0;
        }

        public object GetChild(object parent, int index)
        {
            if (parent is TestNode node) return node.Children[index];
            throw new System.ArgumentOutOfRangeException();
        }
    }

    [Fact]
    public void FindFirstDescendant_ReturnsNull_WhenRootIsNull()
    {
        var finder = new VisualTreeFinder(new TestVisualTreeService());
        var result = finder.FindFirstDescendant<TargetNode>(null!);
        Assert.Null(result);
    }

    [Fact]
    public void FindFirstDescendant_ReturnsNull_WhenNotFound()
    {
        var root = new TestNode();
        root.Children.Add(new OtherNode());

        var finder = new VisualTreeFinder(new TestVisualTreeService());
        var result = finder.FindFirstDescendant<TargetNode>(root);

        Assert.Null(result);
    }

    [Fact]
    public void FindFirstDescendant_FindsDirectChild()
    {
        var root = new TestNode();
        var target = new TargetNode { Name = "Target" };
        root.Children.Add(target);

        var finder = new VisualTreeFinder(new TestVisualTreeService());
        var result = finder.FindFirstDescendant<TargetNode>(root);

        Assert.NotNull(result);
        Assert.Same(target, result);
    }

    [Fact]
    public void FindFirstDescendant_FindsNestedChild()
    {
        var root = new TestNode();
        var intermediate = new OtherNode();
        var target = new TargetNode { Name = "Target" };

        root.Children.Add(intermediate);
        intermediate.Children.Add(target);

        var finder = new VisualTreeFinder(new TestVisualTreeService());
        var result = finder.FindFirstDescendant<TargetNode>(root);

        Assert.NotNull(result);
        Assert.Same(target, result);
    }

    [Fact]
    public void FindFirstDescendant_FindsFirstMatchInDFS()
    {
        // Structure:
        // Root
        //  - Branch1
        //     - Target1
        //  - Branch2
        //     - Target2

        var root = new TestNode();
        var branch1 = new OtherNode();
        var branch2 = new OtherNode();
        var target1 = new TargetNode { Name = "Target1" };
        var target2 = new TargetNode { Name = "Target2" };

        root.Children.Add(branch1);
        root.Children.Add(branch2);

        branch1.Children.Add(target1);
        branch2.Children.Add(target2);

        var finder = new VisualTreeFinder(new TestVisualTreeService());
        var result = finder.FindFirstDescendant<TargetNode>(root);

        Assert.NotNull(result);
        Assert.Same(target1, result);
    }
}
