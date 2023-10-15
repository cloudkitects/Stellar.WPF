using Stellar.WPF.Utilities;
using System.Diagnostics;
using System.Linq;
using System;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace Stellar.WPF.Tests;

public class TreeTests
{
    #region helpers
    private static string GetLongText(int lines = 1000, string format = "{0}")
    {
        var text = new StringWriter();

        for (var i = 0; i < lines; i++)
        {
            text.Write(string.Format(format, i));
        }

        return text.ToString();
    }

    private static string GetDummyText()
    {
        return File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "data", "dummy.txt"));
    }

    private Tree<char> DummyInitializer()
    {
        return new Tree<char>("hello");
    }

    private static string UpdateStringAt(string text, int index, char c)
    {
        return text[..index] + c + text[(index + 1)..];
    }
    #endregion

    [Fact]
    public void ConstructorThrows()
    {
        IEnumerable<string> input = null!;

        Assert.Throws<ArgumentNullException>(() => _ = new Tree<string>(input));

        object[] arr = null!;

        Assert.Throws<ArgumentNullException>(() => _ = new Tree<object>(arr, 0, 3));

        arr = new[] { (object)'1', 2, "III" };

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Tree<object>(arr, -1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Tree<object>(arr, 0, 4));

        Func<Tree<char>> func = null!;

        Assert.Throws<ArgumentNullException>(() => _ = new Tree<char>(0, func));

        func = new Func<Tree<char>>(DummyInitializer);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Tree<char>(-1, func));
    }

    [Fact]
    public void NewIsEmpty()
    {
        var tree = new Tree<char>();

        Assert.Empty(tree);
        Assert.Equal(0, tree.Length);
        Assert.Equal(string.Empty, tree.ToString());
    }

    [Fact]
    public void NewFromEmptyStringIsEmpty()
    {
        var tree = new Tree<char>(string.Empty);

        Assert.Empty(tree);
        Assert.Equal(0, tree.Length);
        Assert.Equal(string.Empty, tree.ToString());
    }

    [Fact]
    public void NewFromShortStringIsSound()
    {
        var text = "Hello, world.";
        var tree = new Tree<char>(text);

        Assert.NotEmpty(tree);
        Assert.Equal(text.Length, tree.Length);
        Assert.Equal(text, tree.ToString());
        Assert.Equal(text.ToCharArray(), tree.ToArray());
    }

    [Fact]
    public void NewFromLongStringIsSound()
    {
        var text = GetLongText();
        var tree = new Tree<char>(text);

        Assert.NotEmpty(tree);
        Assert.Equal(text.Length, tree.Length);
        Assert.Equal(text, tree.ToString());
        Assert.Equal(text.ToCharArray(), tree.ToArray());
    }

    [Fact]
    public void NewFromArrayIsSound()
    {
        var arr = new[] { (object)'1', 2, "III" };
        var tree = new Tree<object>(arr, 0, arr.Length);

        Assert.NotEmpty(tree);
        Assert.Equal(arr.Length, tree.Length);
        Assert.Equal(2, tree[1]);
    }

    [Fact]
    public void NewFromEnumerableIsSound()
    {
        IEnumerable<object> input1 = new[] { (object)'1', 2, "III" };
        var tree = new Tree<object>(input1);

        Assert.NotEmpty(tree);
        Assert.Equal(input1.Count(), tree.Count);
        Assert.Equal(2, tree[1]);

        var subtree = new Tree<object>(tree.Slice(2, 1));

        Assert.Equal("III", subtree[0]);

        // test explicit cloning here
        var clone = (ICloneable)tree.Clone();

        Assert.NotNull(clone);
        Assert.Equal(input1, (IEnumerable<object>)clone);
    }

    [Fact]
    public void NewFromInitializerIsSound()
    {
        var tree = new Tree<char>(5, DummyInitializer);

        Assert.NotEmpty(tree);

        var empty = new Tree<char>(0, DummyInitializer);

        Assert.Empty(empty);
    }

    [Fact]
    public void ClonesAndClears()
    {
        IEnumerable<object> input1 = new[] { (object)'1', 2, "III" };
        var tree = new Tree<object>(input1);

        var clone1 = tree.Clone();

        Assert.NotEmpty(clone1);
        Assert.Equal(input1.ToList()[2], clone1[2]);

        var clone2 = ((ICloneable)tree).Clone();

        Assert.NotNull(clone2);
        Assert.Equal(input1, (IEnumerable<object>)clone2);

        tree.Clear();

        Assert.Empty(tree);
    }


    [Theory]
    [InlineData(1200, 600)]
    public void PartitioningIsSound(int index, int length)
    {
        var text = GetLongText();
        var tPart = text.Substring(index, length);
        var aPart = tPart.ToCharArray();

        var tree = new Tree<char>(text);
        var subtree = tree.Slice(index, length);

        Assert.Equal(tPart, tree.ToString(1200, 600));
        Assert.Equal(aPart, tree.ToArray(1200, 600));

        Assert.Equal(tPart, subtree.ToString());
        Assert.Equal(aPart, subtree.ToArray());
    }

    [Fact]
    public void AppendsText()
    {
        var text = GetLongText();
        var tree = new Tree<char>();

        tree.Append(text);

        Assert.Equal(text, tree.ToString());
    }

    [Theory]
    [InlineData(1000, "{0} ")]
    public void AppendsTrees(int length, string format)
    {
        var text = GetLongText(length, format);
        var tree = new Tree<char>();

        for (var i = 0; i < length; i++)
        {
            var t = string.Format(format, i);

            tree.Append(new Tree<char>(t));
        }

        Assert.Equal(text, tree.ToString());
    }

    [Theory]
    [InlineData(25, "{0,-2}\n")]
    public void InsertsText(int length, string format)
    {
        var text = GetLongText(length, format);
        var tree = new Tree<char>();

        for (var i = length - 1; i >= 0; i--)
        {
            var t = string.Format(format, i);

            tree.InsertText(0, t);
        }

        Assert.Equal(text, tree.ToString());
    }

    [Theory]
    [InlineData(250, "{0,-3}\n")]
    public void InsertsTrees(int length, string format)
    {
        var text = GetLongText(length, format);
        var tree = new Tree<char>();

        for (var i = length - 1; i >= 0; i--)
        {
            var t = string.Format(format, i);

            tree.InsertAt(0, new Tree<char>(t));
        }

        Assert.Equal(text, tree.ToString());
    }

    [Theory]
    [InlineData(100, "{0,-3}\n")]
    public void InsertThrows(int length, string format)
    {
        var tree = new Tree<char>(GetLongText(length, format));

        Tree<char> insert1 = null!;
        IEnumerable<char> insert2 = null!;

        Assert.Throws<ArgumentNullException>(() => tree.InsertAt(50, insert1));
        Assert.Throws<ArgumentNullException>(() => tree.InsertAt(50, insert2));
    }

    [Theory]
    [InlineData("hello world.", 5, ",")]
    public void InsertsInTheMiddle(string text, int index, string insert)
    {
        var tree1 = new Tree<char>(text);
        var tree2 = new Tree<char>(text);
        var tree3 = new Tree<char>(text);

        var newText = text.Insert(index, insert);

        tree1.InsertText(index, insert);
        tree2.InsertAt(index, new Tree<char>(insert));
        tree3.InsertAt(index, (IEnumerable<char>)new Tree<char>(insert));

        Assert.Equal(newText, tree1.ToString());
        Assert.Equal(newText, tree2.ToString());
        Assert.Equal(newText, tree3.ToString());
    }

    [Theory]
    [InlineData("hello; world.", 5, ',')]
    public void Updates(string text, int index, char update)
    {
        var tree = new Tree<char>(text);

        var newText = UpdateStringAt(text, index, update);

        tree.UpdateAt(index, new[] { update }, 0, 1);

        Assert.Equal(newText, tree.ToString());
    }

    [Theory]
    [InlineData("hello, world.")]
    public void Removes(string text)
    {
        var tree = new Tree<char>(text);

        var newText = text.TrimEnd('.');

        tree.RemoveAt(tree.Length - 1);

        Assert.Equal(newText, tree.ToString());
    }

    [Theory]
    [InlineData("hello world")]
    public void Appends(string text)
    {
        var tree = new Tree<char>(text);

        var newText = $"{text} of wonder.";

        tree.Append(new Tree<char>($" of wonder."));

        Assert.Equal(newText, tree.ToString());

        var coda = "..";

        var finalText = newText + coda;

        tree.Append(coda.ToCharArray(), 0, 2);

        Assert.Equal(finalText, tree.ToString());
    }

    [Theory]
    [InlineData("hello, world", " of wonder ", ", carpe diem!")]
    public void Concats(string left, string middle, string right)
    {
        var tree1 = new Tree<char>(left);
        var tree2 = new Tree<char>(middle);
        var tree3 = new Tree<char>(right);

        var text1 = $"{left}{right}";
        var text2 = $"{left}{middle}{right}";

        var tree4 = Tree<char>.Concat(tree1, tree3);
        var tree5 = Tree<char>.Concat(tree1, tree2, tree3);

        Assert.Equal(text1, tree4.ToString());
        Assert.Equal(text2, tree5.ToString());
    }

    [Fact]
    public void ConcatThrows()
    {
        var l = new Tree<char>("Hello,");
        var r = new Tree<char>("world.");
        Tree<char> e = null!;
        Tree<char>[] c = null!;

        Assert.Throws<ArgumentNullException>(() => Tree<char>.Concat(l, e));
        Assert.Throws<ArgumentNullException>(() => Tree<char>.Concat(e, l));
        Assert.Throws<ArgumentNullException>(() => Tree<char>.Concat(l, e, r));
        Assert.Throws<ArgumentNullException>(() => Tree<char>.Concat(c));
    }


    [Fact]
    public void IndexingThrows()
    {
        var text = "Hello, world.";
        var tree = new Tree<char>(text);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = tree[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[-1] = '.');
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[13] = '.');
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[100] = '.');
    }

    [Fact]
    public void IndexingWorks()
    {
        var text = "Hello, world.";
        var tree = new Tree<char>(text)
        {
            [1] = 'u'
        };

        Assert.Equal('.', tree[12]);
        Assert.Equal("Hullo, world.", tree.ToString());
    }

    [Fact]
    public void IndexOfWorks()
    {
        var text = "Hello, world.";
        var tree = new Tree<char>(text);

        Assert.Equal(-1, tree.IndexOf('a'));
        Assert.Equal(-1, tree.IndexOf('p'));
        Assert.Equal(-1, tree.LastIndexOf('p'));
        Assert.Equal(10, tree.LastIndexOf('l'));
        Assert.Equal(1, tree.LastIndexOf('e'));
        Assert.Equal(12, tree.LastIndexOf('.'));
        Assert.Equal(12, tree.IndexOf('.'));

        // test extensions
        Assert.Equal(7, tree.IndexOf("wo", 0, tree.Length));
        Assert.Equal(3, tree.LastIndexOf("lo", 0, 5));

    }

    [Fact]
    public void ListWorks()
    {
        var text = "Hello, world.";
        IList<char> tree = new Tree<char>(text);

        tree.Remove(',');
        tree.Insert(5, ';');
        tree.Add('.');
        tree.Add('.');

        Assert.Equal("Hello; world...", tree.ToString());
        Assert.False(tree.Remove('a'));
    }

    [Fact]
    public void CollectionWorks()
    {
        var text = "Hello, world.";
        ICollection<char> tree = new Tree<char>(text);

        Assert.False(tree.Contains('a'));
        Assert.True(tree.Contains('e'));
        Assert.False(tree.IsReadOnly);
    }

    [Fact]
    public void ToStringWorks()
    {
        var words = new[] { "hello", "world", "of", "code" };

        var tree = new Tree<string>(words);

        Assert.Equal("{ hello, world, of, code }", tree.ToString());
    }

    [Conditional("DEBUG")]
    [Fact]
    public void GetsTreeAsString()
    {
        var words = new[] { "Hello", "world", "of", "code" };

        var tree = new Tree<string>(words);

        Assert.Equal("[leaf length=4, shared=False]\r\n", tree.GetTreeAsString());
    }

    [Fact]
    public void VerifyRangeThrows()
    {
        var words = new[] { "Hello", "world", "of", "code" };

        var tree = new Tree<string>(words);

        Assert.Throws<ArgumentNullException>(() => Tree<string>.VerifyRange(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Tree<string>.VerifyRange(words, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Tree<string>.VerifyRange(words, 5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Tree<string>.VerifyRange(words, 2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Tree<string>.VerifyRange(words, 2, 6));

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.VerifyRange(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.VerifyRange(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.VerifyRange(4, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.VerifyRange(4, -4));
    }

    [Fact]
    public void CopiesToArray()
    {
        var words = new[] { "hello", "world", "of", "code" };

        // TODO?: Enumerable.Append(T item) behavior cannot be overriden:
        // tree.Append(", ") has no effect on the tree whatsoever
        var tree = new Tree<string>(words) { "|" };

        tree.Append(words.Reverse());

        var array = new string[9];

        tree.CopyTo(array, 0);

        Assert.Equal("hello world of code | code of world hello", string.Join(' ', array));
    }

    #region char tree extensions
    [Theory]
    [InlineData("yummy", 0, null, -1, -1)]
    [InlineData("platea", 1000, null, 1611, 2237)]
    public void Indexes(string value, int index, int? length, int expected, int lastExpected)
    {
        var text = GetDummyText();

        var tree = new Tree<char>(text);

        length ??= tree.Length;

        Assert.Equal(expected, tree.IndexOf(value, index, length.Value - index));
        Assert.Equal(lastExpected, tree.LastIndexOf(value, 0, length.Value));
    }

    [Theory]
    [InlineData("y", -1, 0, null)]
    [InlineData("VY", 256, 0, 700)]
    public void IndexesAnyOf(string any, int expected, int index, int? length)
    {
        var text = GetDummyText();

        var tree = new Tree<char>(text);

        length ??= tree.Length;

        Assert.Equal(expected, tree.IndexOfAny(any.ToCharArray(), index, length.Value));
    }

    [Fact]
    public void IndexOfAnyThrows()
    {
        Tree<char> tree1 = null!;
        char[] any1 = null!;

        var tree2 = new Tree<char>("hello");
        var any2 = "lo".ToCharArray();

        Assert.Throws<ArgumentNullException>(() => tree1.IndexOfAny(any2, 0, 3));
        Assert.Throws<ArgumentNullException>(() => tree2.IndexOfAny(any1, 0, 3));
    }

    [Theory]
    [InlineData("Felis eget nunc lobortis mattis aliquam faucibus.", 1286, 49)]
    public void WritesTo(string segment, int index, int length)
    {
        var text = GetDummyText();
        var tree = new Tree<char>(text);

        var output = new StringWriter();

        tree.WriteTo(output, 0, tree.Length);

        Assert.Equal(segment, output.ToString()[index..(index + length)]);
    }

    [Theory]
    [InlineData("Felis eget nunc lobortis mattis aliquam faucibus.", 49)]
    public void AddsText(string text, int index)
    {
        var tree = new Tree<char>(text);

        var addition = " Nulla facilisi";

        tree.AddText(addition);

        Assert.Equal(string.Concat(text.AsSpan(index), addition), tree.ToString(index, addition.Length));
    }

    [Fact]
    public void CharTreeExtensionsThrow()
    {
        var text = GetDummyText();
        var tree = new Tree<char>(text);
        var output = new StringWriter();

        Tree<char> nullTree = null!;
        StringWriter nullStringWriter = null!;

        Assert.Throws<ArgumentNullException>(() => tree.WriteTo(nullStringWriter, 0, 10));
        Assert.Throws<ArgumentNullException>(() => nullTree.WriteTo(output, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.ToString(0, -1));
#if DEBUG
        Assert.Throws<ArgumentNullException>(() => nullTree.ToString(0, 1));
#endif
        Assert.Throws<ArgumentNullException>(() => nullTree.InsertText(10, "hi"));
        Assert.Throws<ArgumentNullException>(() => nullTree.IndexOf("hit", 0, 10));
        Assert.Throws<ArgumentNullException>(() => tree.IndexOf(null!, 0, 10));
        Assert.Throws<ArgumentNullException>(() => nullTree.LastIndexOf("hit", 0, 10));
        Assert.Throws<ArgumentNullException>(() => tree.LastIndexOf(null!, 0, 10));
    }
    #endregion
}