using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Stellar.WPF.Document;

/// <summary>
/// Inserts/Removes lines when text is inserted/removed.
/// </summary>
internal sealed class LineManager
{
    private readonly Document document;
    private readonly LineTree lineTree;

    /// <summary>
    /// A copy of line trackers since the actual line trackers may remove themselves
    /// while being notified (e.g., as by WeakLineTracker)
    /// </summary>
    private ILineTracker[] lineTrackers;

    internal void UpdateLineTrackers()
    {
        lineTrackers = document.LineTrackers.ToArray();
    }

    #region Constructor
    public LineManager(LineTree lineTree, Document document)
    {
        this.document = document;
        this.lineTree = lineTree;

        UpdateLineTrackers();

        Rebuild();
    }
    #endregion

    #region Rebuild
    public void Rebuild()
    {
        // keep the first line and mark all others as deleted and detached
        var firstLine = lineTree.LineAt(0);
        var line = firstLine.NextLine;

        for (; line != null; line = line.NextLine)
        {
            line.isDeleted = true;
            line.parent = line.left = line.right = null!;
        }

        // reset the first line to detach it from deleted lines
        firstLine.Reset();

        List<Line> lines = new();
        var nextLine = NewLineFinder.Next(document, 0);
        line = firstLine;

        var lastSeparatorEnd = 0;

        while (nextLine != SimpleSegment.Invalid)
        {
            line.ExactLength = nextLine.Offset + nextLine.Length - lastSeparatorEnd;
            line.SeparatorLength = nextLine.Length;

            lines.Add(line);

            line = new Line(document);

            lastSeparatorEnd = nextLine.Offset + nextLine.Length;

            nextLine = NewLineFinder.Next(document, lastSeparatorEnd);
        }

        line.ExactLength = document.TextLength - lastSeparatorEnd;

        lines.Add(line);

        lineTree.RebuildTree(lines);

        foreach (var lineTracker in lineTrackers)
        {
            lineTracker.Rebuild();
        }
    }
    #endregion

    #region Remove
    public void Remove(int offset, int length)
    {
        Debug.Assert(length >= 0);

        if (length == 0)
        {
            return;
        }

        var startLine = lineTree.LineBy(offset);
        int startLineOffset = startLine.Offset;

        Debug.Assert(offset < startLineOffset + startLine.ExactLength);

        if (offset > startLineOffset + startLine.Length)
        {
            Debug.Assert(startLine.SeparatorLength == 2);
            // we are deleting starting in the middle of a separator

            // remove last separator part
            SetLineLength(startLine, startLine.ExactLength - 1);

            // remove remaining text
            Remove(offset, length - 1);

            return;
        }

        if (offset + length < startLineOffset + startLine.ExactLength)
        {
            // just removing a part of this line
            //startLine.RemovedLinePart(ref deferredEventList, offset - startLineOffset, length);
            SetLineLength(startLine, startLine.ExactLength - length);

            return;
        }

        // merge startLine with another line because startLine's delimiter was deleted
        // possibly remove lines in between if multiple delimiters were deleted
        var charactersRemovedInStartLine = startLineOffset + startLine.ExactLength - offset;

        Debug.Assert(charactersRemovedInStartLine > 0);

        var endLine = lineTree.LineBy(offset + length);

        if (endLine == startLine)
        {
            // special case: we are removing a part of the last line up to the
            // end of the document
            SetLineLength(startLine, startLine.ExactLength - length);

            return;
        }
        var endLineOffset = endLine.Offset;
        var charactersLeftInEndLine = endLineOffset + endLine.ExactLength - (offset + length);

        // remove all lines between startLine (excl.) and endLine (incl.)
        var tmp = startLine.NextLine;

        Line lineToRemove;

        do
        {
            lineToRemove = tmp;
            tmp = tmp.NextLine;

            RemoveLine(lineToRemove);

        } while (lineToRemove != endLine);

        SetLineLength(startLine, startLine.ExactLength - charactersRemovedInStartLine + charactersLeftInEndLine);
    }

    private void RemoveLine(Line lineToRemove)
    {
        foreach (ILineTracker lt in lineTrackers)
        {
            lt.BeforeRemoving(lineToRemove);
        }

        lineTree.RemoveLine(lineToRemove);
    }

    #endregion

    #region Insert
    public void Insert(int offset, ITextSource text)
    {
        var line = lineTree.LineBy(offset);
        var lineOffset = line.Offset;

        Debug.Assert(offset <= lineOffset + line.ExactLength);

        if (offset > lineOffset + line.Length)
        {
            Debug.Assert(line.SeparatorLength == 2);
            // we are inserting in the middle of a delimiter

            // shorten line
            SetLineLength(line, line.ExactLength - 1);

            // add new line
            line = InsertLineAfter(line, 1);
            line = SetLineLength(line, 1);
        }

        SimpleSegment ds = NewLineFinder.Next(text, 0);

        if (ds == SimpleSegment.Invalid)
        {
            // no newline is being inserted, all text is inserted in a single line
            //line.InsertedLinePart(offset - line.Offset, text.Length);
            SetLineLength(line, line.ExactLength + text.TextLength);
            return;
        }
        //DocumentLine firstLine = line;
        //firstLine.InsertedLinePart(offset - firstLine.Offset, ds.Offset);
        var lastDelimiterEnd = 0;

        while (ds != SimpleSegment.Invalid)
        {
            // split line segment at line delimiter
            int lineBreakOffset = offset + ds.Offset + ds.Length;
            lineOffset = line.Offset;
            int lengthAfterInsertionPos = lineOffset + line.ExactLength - (offset + lastDelimiterEnd);
            line = SetLineLength(line, lineBreakOffset - lineOffset);
            var newLine = InsertLineAfter(line, lengthAfterInsertionPos);
            newLine = SetLineLength(newLine, lengthAfterInsertionPos);

            line = newLine;
            lastDelimiterEnd = ds.Offset + ds.Length;

            ds = NewLineFinder.Next(text, lastDelimiterEnd);
        }
        //firstLine.SplitTo(line);
        // insert rest after last delimiter
        if (lastDelimiterEnd != text.TextLength)
        {
            //line.InsertedLinePart(0, text.Length - lastDelimiterEnd);
            SetLineLength(line, line.ExactLength + text.TextLength - lastDelimiterEnd);
        }
    }

    private Line InsertLineAfter(Line line, int length)
    {
        var newLine = lineTree.InsertLineAfter(line, length);
        foreach (ILineTracker lt in lineTrackers)
        {
            lt.AfterInserting(line, newLine);
        }

        return newLine;
    }
    #endregion

    #region SetLineLength
    /// <summary>
    /// Sets the total line length and checks the delimiter.
    /// This method can cause line to be deleted when it contains a single '\n' character
    /// and the previous line ends with '\r'.
    /// </summary>
    /// <returns>Usually returns <paramref name="line"/>, but if line was deleted due to
    /// the "\r\n" merge, returns the previous line.</returns>
    private Line SetLineLength(Line line, int newTotalLength)
    {
        //			changedLines.Add(line);
        //			deletedOrChangedLines.Add(line);
        int delta = newTotalLength - line.ExactLength;
        if (delta != 0)
        {
            foreach (ILineTracker lt in lineTrackers)
            {
                lt.ResetLength(line, newTotalLength);
            }

            line.ExactLength = newTotalLength;
            LineTree.UpdateNodeData(line);
        }
        // determine new SeparatorLength
        if (newTotalLength == 0)
        {
            line.SeparatorLength = 0;
        }
        else
        {
            int lineOffset = line.Offset;
            char lastChar = document.GetCharAt(lineOffset + newTotalLength - 1);
            if (lastChar == '\r')
            {
                line.SeparatorLength = 1;
            }
            else if (lastChar == '\n')
            {
                if (newTotalLength >= 2 && document.GetCharAt(lineOffset + newTotalLength - 2) == '\r')
                {
                    line.SeparatorLength = 2;
                }
                else if (newTotalLength == 1 && lineOffset > 0 && document.GetCharAt(lineOffset - 1) == '\r')
                {
                    // we need to join this line with the previous line
                    var previousLine = line.PreviousLine;
                    RemoveLine(line);
                    return SetLineLength(previousLine, previousLine.ExactLength + 1);
                }
                else
                {
                    line.SeparatorLength = 1;
                }
            }
            else
            {
                line.SeparatorLength = 0;
            }
        }
        return line;
    }
    #endregion

    #region ChangeComplete
    public void ChangeComplete(DocumentChangeEventArgs e)
    {
        foreach (ILineTracker lt in lineTrackers)
        {
            lt.AfterChange(e);
        }
    }
    #endregion
}
