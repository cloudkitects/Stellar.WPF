using System;

using Stellar.WPF.Document;

namespace Stellar.WPF.Editing;

/// <summary>
/// Represents a selected segment.
/// </summary>
public class SelectionSegment : ISegment
{
    private readonly int startOffset, endOffset;
    private readonly int startColumn, endColumn;

    /// <summary>
    /// Creates a selected segment from two offsets.
    /// </summary>
    public SelectionSegment(int startOffset, int endOffset)
    {
        this.startOffset = Math.Min(startOffset, endOffset);
        this.endOffset = Math.Max(startOffset, endOffset);
        
        startColumn = endColumn = -1;
    }

    /// <summary>
    /// Creates a SelectionSegment from two offsets and visual columns.
    /// </summary>
    public SelectionSegment(int startOffset, int startColumn, int endOffset, int endColumn)
    {
        if (startOffset < endOffset || (startOffset == endOffset && startColumn <= endColumn))
        {
            this.startOffset = startOffset;
            this.startColumn = startColumn;
            this.endOffset = endOffset;
            this.endColumn = endColumn;
        }
        else
        {
            this.startOffset = endOffset;
            this.startColumn = endColumn;
            this.endOffset = startOffset;
            this.endColumn = startColumn;
        }
    }

    /// <summary>
    /// Gets the start offset.
    /// </summary>
    public int StartOffset => startOffset;

    /// <summary>
    /// Gets the end offset.
    /// </summary>
    public int EndOffset => endOffset;

    /// <summary>
    /// Gets the start visual column.
    /// </summary>
    public int StartVisualColumn => startColumn;

    /// <summary>
    /// Gets the end visual column.
    /// </summary>
    public int EndVisualColumn => endColumn;

    /// <inheritdoc/>
    int ISegment.Offset => startOffset;

    /// <inheritdoc/>
    public int Length => endOffset - startOffset;

    /// <inheritdoc/>
    public override string ToString() => $"[SelectionSegment StartOffset={startOffset}, EndOffset={endOffset}, StartColumn={startColumn}, EndColumn={endColumn}]";
}