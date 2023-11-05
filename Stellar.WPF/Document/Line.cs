using System;
using System.Diagnostics;
using System.Globalization;

namespace Stellar.WPF.Document;

/// <summary>
/// Represents a line inside a <see cref="Document"/>.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Document.Lines"/> collection contains one DocumentLine instance
/// for every line in the document. This collection is read-only to user code and is automatically
/// updated to reflect the current document content.
/// </para>
/// <para>
/// Internally, the DocumentLine instances are arranged in a binary tree that allows for both efficient updates and lookup.
/// Converting between offset and line number is possible in O(lg N) time,
/// and the data structure also updates all offsets in O(lg N) whenever a line is inserted or removed.
/// </para>
/// </remarks>
public sealed partial class Line : ILine
{
    #region Constructor
#if DEBUG
    // Required for thread safety check which is done only in debug builds.
    // To save space, we don't store the document reference in release builds as we don't need it there.
    private readonly Document document;
#endif

    internal bool isDeleted;

    internal Line(Document document)
    {
#if DEBUG
        Debug.Assert(document is not null);
        
        this.document = document;
#endif
    }

    [Conditional("DEBUG")]
    private void DebugVerifyAccess()
    {
#if DEBUG
        document.DebugVerifyAccess();
#endif
    }
    #endregion

    #region Tree members
    /// <summary>
    /// Relative lines.
    /// </summary>
    internal Line? left, right, parent;

    internal bool color;

    /// <summary>
    /// The number of lines in this branch.
    /// Invariant:
    ///   lineCount = 1 + left.lineCount + right.lineCount
    /// </summary>
    internal int lineCount;

    /// <summary>
    /// The total text length of this branch.
    /// Invariant:
    ///   branchLength = left.branchLength + documentLine.ExactLength + right.branchLength
    /// </summary>
    internal int totalLength;

    /// <summary>
    /// Resets the line to enable its reuse after a document rebuild.
    /// </summary>
    internal void Reset()
    {
        exactLength = separatorLength = 0;
        isDeleted = color = false;
        left = null!;
        right = null!;
        parent = null!;
    }

    internal Line Init()
    {
        lineCount = 1;
        totalLength = ExactLength;

        return this;
    }

    internal Line LeftMost
    {
        get
        {
            var line = this;

            while (line.left is not null)
            {
                line = line.left;
            }

            return line;
        }
    }

    internal Line RightMost
    {
        get
        {
            var line = this;

            while (line.right is not null)
            {
                line = line.right;
            }

            return line;
        }
    }

    /// <summary>
    /// Wether this line was deleted from the document.
    /// </summary>
    public bool IsDeleted
    {
        get
        {
            DebugVerifyAccess();

            return isDeleted;
        }
    }

    /// <summary>
    /// The O(logN) number of this line.
    /// </summary>
    /// <exception cref="InvalidOperationException">The line was deleted.</exception>
    public int Number => !IsDeleted
                ? LineTree.IndexOf(this) + 1
                : throw new InvalidOperationException();

    /// <summary>
    /// Gets the starting offset of the line in the document's text.
    /// Runtime: O(log n)
    /// </summary>
    /// <exception cref="InvalidOperationException">The line was deleted.</exception>
    public int Offset => !IsDeleted
                ? LineTree.OffsetOf(this)
                : throw new InvalidOperationException();

    /// <summary>
    /// Gets the end offset of the line in the document's text (the offset before the line separator).
    /// Runtime: O(log n)
    /// </summary>
    /// <exception cref="InvalidOperationException">The line was deleted.</exception>
    /// <remarks>EndOffset = <see cref="Offset"/> + <see cref="Length"/>.</remarks>
    public int EndOffset => Offset + Length;
    #endregion

    #region Length
    private int exactLength;
    private byte separatorLength;

    /// <summary>
    /// The length of this line. O(1)
    /// </summary>
    /// <remarks>Accessible after deletion.</remarks>
    public int Length
    {
        get
        {
            DebugVerifyAccess();

            return exactLength - separatorLength;
        }
    }

    /// <summary>
    /// The length of this line including the line separator. O(1)
    /// </summary>
    /// <remarks>Accessible after deletion.</remarks>
    public int ExactLength
    {
        get
        {
            DebugVerifyAccess();

            return exactLength;
        }
        internal set
        {
            // set by the line tree
            exactLength = value;
        }
    }

    /// <summary>
    /// <para>Gets the length of the line separator.</para>
    /// <para>The value is 1 for single <c>"\r"</c> or <c>"\n"</c>, 2 for the <c>"\r\n"</c> sequence;
    /// and 0 for the last line in the document.</para>
    /// </summary>
    /// <remarks>Accessible after deletion.</remarks>
    public int SeparatorLength
    {
        get
        {
            DebugVerifyAccess();

            return separatorLength;
        }
        internal set
        {
            Debug.Assert(0 <= value && value <= 2);

            separatorLength = (byte)value;
        }
    }
    #endregion

    #region Previous / Next Line
    /// <summary>
    /// Gets the next line in the document.
    /// </summary>
    /// <returns>The line following this line, or null if this is the last line.</returns>
    public Line NextLine
    {
        get
        {
            DebugVerifyAccess();

            if (right != null)
            {
                return right.LeftMost;
            }

            var line = this;
            Line oldLine;

            do
            {
                oldLine = line;
                line = line.parent;
            } while (line is not null && line.right == oldLine);

            return line!;
        }
    }

    /// <summary>
    /// Gets the previous line in the document.
    /// </summary>
    /// <returns>The line before this line, or null if this is the first line.</returns>
    public Line PreviousLine
    {
        get
        {
            DebugVerifyAccess();

            if (left != null)
            {
                return left.RightMost;
            }

            var line = this;
            Line oldLine;

            do
            {
                oldLine = line;
                line = line.parent;
            } while (line is not null && line.left == oldLine);

            return line!;
        }
    }

    ILine ILine.NextLine => NextLine;

    ILine ILine.PreviousLine => PreviousLine;
    #endregion

    #region ToString
    /// <summary>
    /// Gets a string with debug output showing the line number and offset.
    /// Does not include the line's text.
    /// </summary>
    public override string ToString()
    {
        return IsDeleted
            ? $"[DocumentLine <deleted>]"
            : $"[DocumentLine Number={Number} Offset={Offset} Length={Length}]";
    }
    #endregion
}
