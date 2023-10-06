using System;
using System.IO;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Document;

/// <summary>
/// Implements ITextSource using a char tree.
/// </summary>
[Serializable]
public sealed class TextSource : ITextSource
{
	readonly Tree<char> tree;
	readonly ICheckpoint? checkpoint;

	/// <summary>
	/// Create a new TextSource from a tree.
	/// </summary>
	public TextSource(Tree<char> tree)
	{
		this.tree = (tree ?? throw new ArgumentNullException(nameof(tree))).Clone();
	}

	/// <summary>
	/// Creates a new TextSource from a tree with a given checkpoint.
	/// </summary>
	public TextSource(Tree<char> tree, ICheckpoint checkpoint)
		: this(tree)
	{
		this.checkpoint = checkpoint;
	}

	/// <summary>
	/// Returns a clone of the tree used by this text source.
	/// </summary>
	/// <remarks>
	/// Cloning protects the underlying tree from changes.
	/// </remarks>
	public Tree<char> GetTree()
	{
		return tree.Clone();
	}

    /// <inheritdoc/>
    public string Text => tree.ToString();

    /// <inheritdoc/>
    public int TextLength => tree.Length;

    /// <inheritdoc/>
    public char GetCharAt(int offset)
	{
		return tree[offset];
	}

	/// <inheritdoc/>
	public string GetText(int offset, int length)
	{
		return tree.ToString(offset, length);
	}

	/// <inheritdoc/>
	public string GetText(ISegment segment)
	{
		return tree.ToString(segment.Offset, segment.Length);
	}

	/// <inheritdoc/>
	public TextReader CreateReader()
	{
		return new TreeTextReader(tree);
	}

	/// <inheritdoc/>
	public TextReader CreateReader(int offset, int length)
	{
		return new TreeTextReader(tree.Slice(offset, length));
	}

	/// <inheritdoc/>
	public ITextSource CreateSnapshot()
	{
		return this;
	}

	/// <inheritdoc/>
	public ITextSource CreateSnapshot(int offset, int length)
	{
		return new TextSource(tree.Slice(offset, length));
	}

	/// <inheritdoc/>
	public int IndexOf(char c, int startIndex, int count)
	{
		return tree.IndexOf(c, startIndex, count);
	}

	/// <inheritdoc/>
	public int IndexOfAny(char[] anyOf, int startIndex, int count)
	{
		return tree.IndexOfAny(anyOf, startIndex, count);
	}

	/// <inheritdoc/>
	public int LastIndexOf(char c, int startIndex, int count)
	{
		return tree.LastIndexOf(c, startIndex, count);
	}

    /// <inheritdoc/>
    public ICheckpoint? Checkpoint => checkpoint;

    /// <inheritdoc/>
    public int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
	{
		return tree.IndexOf(searchText, startIndex, count, comparisonType);
	}

	/// <inheritdoc/>
	public int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
	{
		return tree.LastIndexOf(searchText, startIndex, count, comparisonType);
	}

	/// <inheritdoc/>
	public void WriteTextTo(TextWriter writer)
	{
		tree.WriteTo(writer, 0, tree.Length);
	}

	/// <inheritdoc/>
	public void WriteTextTo(TextWriter writer, int offset, int length)
	{
		tree.WriteTo(writer, offset, length);
	}
}
