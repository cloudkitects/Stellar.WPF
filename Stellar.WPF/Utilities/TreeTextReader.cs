using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Stellar.WPF.Utilities;

/// <summary>
/// TextReader implementation that reads text from a tree.
/// </summary>
public sealed class TreeTextReader : TextReader
{
    private readonly Stack<Branch<char>> branchStack = new();
    private Branch<char>? currentBranch;
    private int contentIndex;

    /// <summary>
    /// Creates a new tree text reader.
    /// </summary>
    /// <remarks>
    /// A clone of the tree is created internally, so the reader always reads through
    /// the old version of the tree if it is modified. <seealso cref="Tree{T}.Clone()"/>.
    /// This also keeps the reader's API contract simple: no code handling changes while
    /// simply iterating through it.
	/// The reader doesn't support empty nodes; an empty tree skips setting the current branch.
    /// </remarks>
    public TreeTextReader(Tree<char> tree)
	{
		(tree ?? throw new ArgumentNullException(nameof(tree))).root.Publish();

		if (tree.Length != 0)
		{
			currentBranch = tree.root;
			
			GoToLeftMostLeaf();
		}
	}

    private void GoToLeftMostLeaf()
	{
		while (currentBranch?.contents is null)
		{
			if (currentBranch?.height == 0)
			{
				// function branch
				currentBranch = currentBranch?.Create();
				
				continue;
			}
			
			// an actual branch, keep left
			Debug.Assert(currentBranch?.right is not null);
			
			branchStack.Push(currentBranch.right);
			
			currentBranch = currentBranch.left;
		}

		// a leaf
		Debug.Assert(currentBranch?.height == 0);
	}

    private void GoToNextBranch()
    {
        if (branchStack.Count == 0)
        {
            currentBranch = null;
        }
        else
        {
            contentIndex = 0;

            currentBranch = branchStack.Pop();

            GoToLeftMostLeaf();
        }
    }

    /// <inheritdoc/>
    public override int Peek()
	{
		return currentBranch is null
			? -1
			: currentBranch.contents![contentIndex];
	}

	/// <inheritdoc/>
	public override int Read()
	{
		if (currentBranch is null)
		{
			return -1;
		}

		var read = currentBranch.contents![contentIndex++];

		if (contentIndex >= currentBranch.length)
		{
			GoToNextBranch();
		}

		return read;
	}

	/// <inheritdoc/>
	public override int Read(char[] buffer, int index, int count)
	{
		if (currentBranch is null)
		{
			return 0;
		}

		var readCount = currentBranch.length - contentIndex;
		
		if (count < readCount)
		{
			Array.Copy(currentBranch.contents!, contentIndex, buffer, index, count);
			
			contentIndex += count;
			
			return count;
		}

		// read to the end of the current branch
		Array.Copy(currentBranch.contents!, contentIndex, buffer, index, readCount);
		
		GoToNextBranch();
		
		return readCount;
	}
}
