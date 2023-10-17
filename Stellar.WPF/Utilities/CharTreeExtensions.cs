using System;
using System.IO;

namespace Stellar.WPF.Utilities;

internal static class CharTreeExtensions
{
    /// <summary>
    /// Retrieves the text for a portion of the tree. O(logN + M), where M=<paramref name="length"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">index or length is outside the valid range.</exception>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public static string ToString(this Tree<char> tree, int index, int length)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }
#if DEBUG
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"{length} < 0");
        }
#endif
        if (length == 0)
        {
            return string.Empty;
        }

        var buffer = new char[length];

        tree.CopyTo(index, buffer, 0, length);

        return new string(buffer);
    }

    /// <summary>
    /// Write a portion of the tree to the specified text writer. O(logN + M), where M=<paramref name="length"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">offset or length is outside the valid range.</exception>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public static void WriteTo(this Tree<char> tree, TextWriter output, int index, int length)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        tree.VerifyRange(index, length);

        tree.root.WriteTo(index, output, length);
    }

    /// <summary>
    /// Appends text to this tree. O(logN + M).
    /// </summary>
    public static void AddText(this Tree<char> tree, string text)
    {
        InsertText(tree, tree.Length, text);
    }

    /// <summary>
    /// Insert text into this tree. O(logN + M).
    /// </summary>
    /// <exception cref="ArgumentNullException">tree is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">index or length is outside the valid range.</exception>
    public static void InsertText(this Tree<char> tree, int index, string text)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        tree.InsertAt(index, text.ToCharArray(), 0, text.Length);
    }

    /// <summary>
    /// The Index of the first occurrence of any character in the specified array, or -1 if none found.
    /// </summary>
    /// <param name="tree">The target tree.</param>
    /// <param name="anyOf">Characters to look for.</param>
    /// <param name="index">Start index of the search.</param>
    /// <param name="length">Length of the search.</param>
    public static int IndexOfAny(this Tree<char> tree, char[] anyOf, int index, int length)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        if (anyOf is null)
        {
            throw new ArgumentNullException(nameof(anyOf));
        }

        tree.VerifyRange(index, length);

        while (length > 0)
        {
            var entry = tree.FindCachedBranch(index).PeekOrDefault();

            var contents = entry.branch.contents;

            var startIndex = index - entry.index;
            
            var nodeLength = Math.Min(entry.branch.length, startIndex + length);
            
            for (var i = index - entry.index; i < nodeLength; i++)
            {
                var character = contents![i];

                foreach (var token in anyOf)
                {
                    if (character == token)
                    {
                        return entry.index + i;
                    }
                }
            }
            
            length -= nodeLength - startIndex;
            index = entry.index + nodeLength;
        }

        return -1;
    }

    /// <summary>
    /// Gets the index of the first occurrence of text.
    /// </summary>
    public static int IndexOf(this Tree<char> tree, string text, int index, int length, StringComparison comparisonType = StringComparison.InvariantCultureIgnoreCase)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        tree.VerifyRange(index, length);

        var i = tree.ToString(index, length).IndexOf(text, comparisonType);

        return i < 0
            ? -1
            : i + index;
    }

    /// <summary>
    /// Gets the index of the last occurrence of text.
    /// </summary>
    public static int LastIndexOf(this Tree<char> tree, string text, int index, int length, StringComparison comparisonType = StringComparison.InvariantCultureIgnoreCase)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        tree.VerifyRange(index, length);

        var i = tree.ToString(index, length).LastIndexOf(text, comparisonType);
        
        return i < 0
            ? -1
            : i + index;
    }

}
