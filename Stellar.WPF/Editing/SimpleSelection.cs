using System;
using System.Collections.Generic;

using Stellar.WPF.Document;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Editing;

/// <summary>
/// A simple selection.
/// </summary>
internal sealed class SimpleSelection : Selection
{
    private readonly TextViewPosition start, end;
    private readonly int startOffset, endOffset;

    /// <summary>
    /// Creates a new SimpleSelection instance.
    /// </summary>
    internal SimpleSelection(TextArea textArea, TextViewPosition start, TextViewPosition end)
        : base(textArea)
    {
        this.start = start;
        this.end = end;

        startOffset = textArea.Document.GetOffset(start.Location);
        endOffset = textArea.Document.GetOffset(end.Location);
    }

    /// <inheritdoc/>
    public override IEnumerable<SelectionSegment> Segments => new SelectionSegment(startOffset, start.VisualColumn, endOffset, end.VisualColumn).ToEnumerable();

    /// <inheritdoc/>
    public override ISegment SurroundingSegment => new SelectionSegment(startOffset, endOffset);

    /// <inheritdoc/>
    public override void ReplaceSelectionWithText(string newText)
    {
        if (newText is null)
        {
            throw new ArgumentNullException(nameof(newText));
        }

        using (textArea.Document.RunUpdate())
        {
            var segmentsToDelete = textArea.GetDeletableSegments(SurroundingSegment);

            for (var i = segmentsToDelete.Length - 1; i >= 0; i--)
            {
                if (i == segmentsToDelete.Length - 1)
                {
                    if (segmentsToDelete[i].Offset == SurroundingSegment.Offset &&
                        segmentsToDelete[i].Length == SurroundingSegment.Length)
                    {
                        newText = AddSpacesIfRequired(newText, start, end);
                    }
                    
                    if (string.IsNullOrEmpty(newText))
                    {
                        // place caret at the beginning of the selection
                        textArea.Caret.Position = start.CompareTo(end) <= 0
                            ? start
                            : end;
                    }
                    else
                    {
                        // place caret so that it ends up behind the new text
                        textArea.Caret.Offset = segmentsToDelete[i].EndOffset;
                    }

                    textArea.Document.Replace(segmentsToDelete[i], newText);
                }
                else
                {
                    textArea.Document.Remove(segmentsToDelete[i]);
                }
            }

            if (segmentsToDelete.Length != 0)
            {
                textArea.ClearSelection();
            }
        }
    }

    public override TextViewPosition StartPosition => start;

    public override TextViewPosition EndPosition => end;

    /// <inheritdoc/>
    public override Selection UpdateOnDocumentChange(DocumentChangeEventArgs e)
    {
        if (e is null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        int newStartOffset, newEndOffset;
        
        if (startOffset <= endOffset)
        {
            newStartOffset = e.ComputeOffset(startOffset, AnchorMovementType.Default);
            newEndOffset = Math.Max(newStartOffset, e.ComputeOffset(endOffset, AnchorMovementType.BeforeInsertion));
        }
        else
        {
            newEndOffset = e.ComputeOffset(endOffset, AnchorMovementType.Default);
            newStartOffset = Math.Max(newEndOffset, e.ComputeOffset(startOffset, AnchorMovementType.BeforeInsertion));
        }
        return Create(
            textArea,
            new TextViewPosition(textArea.Document.GetLocation(newStartOffset), start.VisualColumn),
            new TextViewPosition(textArea.Document.GetLocation(newEndOffset), end.VisualColumn)
        );
    }

    /// <inheritdoc/>
    public override bool IsEmpty => startOffset == endOffset && start.VisualColumn == end.VisualColumn;

    /// <inheritdoc/>
    public override int Length => Math.Abs(endOffset - startOffset);

    /// <inheritdoc/>
    public override Selection SetEndpoint(TextViewPosition endPosition)
    {
        return Create(textArea, start, endPosition);
    }

    public override Selection StartSelectionOrSetEndpoint(TextViewPosition startPosition, TextViewPosition endPosition)
    {
        _ = textArea.Document ?? throw new InvalidOperationException("The text area document is null"); ;

        return Create(textArea, start, endPosition);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return startOffset * 27811 + endOffset + textArea.GetHashCode();
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        if (obj is not SimpleSelection other)
        {
            return false;
        }

        return start.Equals(other.start) &&
            end.Equals(other.end) && startOffset == other.startOffset &&
            endOffset == other.endOffset &&
            textArea == other.textArea;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[SimpleSelection Start={start} End={end}]";
    }
}
