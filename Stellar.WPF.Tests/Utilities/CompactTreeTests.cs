using Stellar.WPF.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Stellar.WPF.Tests.Utilities;

public class CompactTreeTests
{
    [Fact]
    public void EmptyOnCreateAndCopy()
    {
        var tree = new CompactTree<string>(string.Equals);

        Assert.Empty(tree);

        var array = Array.Empty<string>();

        tree.CopyTo(array, 0);

        Assert.Empty(array);
    }

    [Fact]
    public void TakesUpTo2BillionNodes()
    {
        const int billion = 1000000000;
        
        var tree = new CompactTree<string>(string.Equals);
        
        tree.InsertRange(0, billion, "A");
        tree.InsertRange(1, billion, "B");
        
        Assert.Equal(2 * billion, tree.Count);
        
        Assert.Throws<OverflowException>(delegate { tree.InsertRange(2, billion, "C"); });
    }

    [Fact]
    public void AddsAndInserts()
    {
        var tree = new CompactTree<int>((a,b) => a == b) { 42, 42, 42 };

        tree.Insert(0, 42);
        tree.Insert(1, 42);

        Assert.Equal(new[] { 42, 42, 42, 42, 42 }, tree.ToArray());
    }

    [Fact]
    public void RemovesRange()
    {
        var tree = new CompactTree<int>((a, b) => a == b);

        for (var i = 1; i <= 3; i++)
        {
            tree.InsertRange(tree.Count, 2, i);
        }
        Assert.Equal(new[] { 1, 1, 2, 2, 3, 3 }, tree.ToArray());
        
        tree.RemoveRange(1, 4);
        
        Assert.Equal(new[] { 1, 3 }, tree.ToArray());
        
        tree.Insert(1, 1);
        tree.InsertRange(2, 2, 2);
        tree.Insert(4, 1);
        
        Assert.Equal(new[] { 1, 1, 2, 2, 1, 3 }, tree.ToArray());
        
        tree.RemoveRange(2, 2);
        
        Assert.Equal(new[] { 1, 1, 1, 3 }, tree.ToArray());

        // TODO: count could be capped to tree.Count
        tree.RemoveRange(3, 1);

        Assert.Equal(new[] { 1, 1, 1 }, tree.ToArray());

        tree.RemoveRange(0, 1);

        Assert.Equal(new[] { 1, 1 }, tree.ToArray());
        
        tree.RemoveRange(0, 2);

        Assert.Equal(Array.Empty<int>(), tree.ToArray());
    }

    [Fact]
    public void Transforms()
    {
        var tree = new CompactTree<int>((a, b) => a == b) { 0, 1, 1, 0 };
        var calls = 0;

        tree.Transform(i => { calls++; return 9 - i; });

        Assert.Equal(3, calls);
        Assert.Equal(new[] { 9, 8, 8, 9 }, tree.ToArray());
    }

    [Fact]
    public void AddsAndTransformsRange()
    {
        var tree = new CompactTree<int>((a, b) => a == b) { 0, 1, 0 };
        var calls = 0;

        tree.AddRange(new[] { 1, 1 });

        tree.TransformRange(1, 4, i => { calls++; return 0; });

        Assert.Equal(3, calls);
        Assert.Equal(new[] { 0, 0, 0, 0, 0 }, tree.ToArray());
    }
}
