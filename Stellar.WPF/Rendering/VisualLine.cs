using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.TextFormatting;

using Stellar.WPF.Document;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Represents a visual line in the document.
/// A visual line usually corresponds to one DocumentLine, but it can span multiple lines if
/// all but the first are collapsed.
/// </summary>
public sealed class VisualLine
{
    private enum LifetimePhase : byte
    {
        Generating,
        Transforming,
        Live,
        Disposed
    }

    private readonly TextView textView;
    private List<VisualLineElement> elements;
    internal bool hasInlineObjects;
    private LifetimePhase phase;

    /// <summary>
    /// Gets the document to which this VisualLine belongs.
    /// </summary>
    public Document.Document Document { get; private set; }

    /// <summary>
    /// Gets the first document line displayed by this visual line.
    /// </summary>
    public Line FirstLine { get; private set; }

    /// <summary>
    /// Gets the last document line displayed by this visual line.
    /// </summary>
    public Line LastLine { get; private set; }

    /// <summary>
    /// Gets a read-only collection of line elements.
    /// </summary>
    public ReadOnlyCollection<VisualLineElement> Elements { get; private set; }

    private ReadOnlyCollection<TextLine> textLines;

    /// <summary>
    /// Gets a read-only collection of text lines.
    /// </summary>
    public ReadOnlyCollection<TextLine> TextLines
    {
        get
        {
            if (phase < LifetimePhase.Live)
            {
                throw new InvalidOperationException();
            }

            return textLines;
        }
    }

    /// <summary>
    /// Gets the start offset of the VisualLine inside the document.
    /// This is equivalent to <c>FirstDocumentLine.Offset</c>.
    /// </summary>
    public int StartOffset => FirstLine.Offset;

    /// <summary>
    /// Length in visual line coordinates.
    /// </summary>
    public int VisualLength { get; private set; }

    /// <summary>
    /// Length in visual line coordinates including the end of line marker, if TextEditorOptions.ShowEndOfLine is enabled.
    /// </summary>
    public int VisualLengthWithEndOfLineMarker
    {
        get
        {
            var length = VisualLength;

            if (textView.Options.ShowEndOfLine && LastLine.NextLine is not null)
            {
                length++;
            }

            return length;
        }
    }

    /// <summary>
    /// Gets the height of the visual line in device-independent pixels.
    /// </summary>
    public double Height { get; private set; }

    /// <summary>
    /// Gets the Y position of the line. This is measured in device-independent pixels relative to the start of the document.
    /// </summary>
    public double VisualTop { get; internal set; }

    internal VisualLine(TextView textView, Line firstLine)
    {
        Debug.Assert(textView is not null);
        Debug.Assert(firstLine is not null);

        this.textView = textView;
        Document = textView.Document;
        FirstLine = firstLine;
    }

    internal void ConstructVisualElements(ITextRunContext context, VisualLineGenerator[] generators)
    {
        Debug.Assert(phase == LifetimePhase.Generating);

        foreach (var g in generators)
        {
            g.StartGeneration(context);
        }

        elements = new List<VisualLineElement>();

        PerformVisualElementConstruction(generators);

        foreach (var g in generators)
        {
            g.FinishGeneration();
        }

        var globalTextRunProperties = context.GlobalTextRunProperties;

        foreach (var element in elements)
        {
            element.SetTextRunProperties(new VisualLineTextRunProperties(globalTextRunProperties));
        }

        Elements = elements.AsReadOnly();

        CalculateOffsets();

        phase = LifetimePhase.Transforming;
    }

    private void PerformVisualElementConstruction(VisualLineGenerator[] generators)
    {
        var document = Document;
        var offset = FirstLine.Offset;
        var currentLineEnd = offset + FirstLine.Length;

        LastLine = FirstLine;

        var askInterestOffset = 0;

        while (offset + askInterestOffset <= currentLineEnd)
        {
            var textPieceEndOffset = currentLineEnd;

            foreach (var g in generators)
            {
                g.cachedInterest = g.GetFirstInterestedOffset(offset + askInterestOffset);

                if (g.cachedInterest != -1)
                {
                    if (g.cachedInterest < offset)
                    {
                        throw new ArgumentOutOfRangeException(
                            $"{g.GetType().Name}.GetFirstInterestedOffset = {g.cachedInterest} < {offset}. Return -1 to signal no interest.");
                    }

                    if (g.cachedInterest < textPieceEndOffset)
                    {
                        textPieceEndOffset = g.cachedInterest;
                    }
                }
            }

            Debug.Assert(textPieceEndOffset >= offset);

            if (textPieceEndOffset > offset)
            {
                var textPieceLength = textPieceEndOffset - offset;

                elements.Add(new TextElement(this, textPieceLength));

                offset = textPieceEndOffset;
            }

            // asking the generators for the same location again would create an endless loop
            // if no elements are constructed or only zero-length elements are constructed,
            // so skip
            askInterestOffset = 1;

            foreach (var g in generators)
            {
                if (g.cachedInterest == offset)
                {
                    var element = g.ConstructElement(offset);

                    if (element is not null)
                    {
                        elements.Add(element);

                        if (element.DocumentLength > 0)
                        {
                            // a non-zero-length element was constructed
                            askInterestOffset = 0;
                            offset += element.DocumentLength;

                            if (offset > currentLineEnd)
                            {
                                var newEndLine = document.GetLineByOffset(offset);
                                currentLineEnd = newEndLine.Offset + newEndLine.Length;

                                LastLine = newEndLine;

                                if (currentLineEnd < offset)
                                {
                                    throw new InvalidOperationException($"{g.GetType().Name} produced an element which ends within the line delimiter");
                                }
                            }

                            break;
                        }
                    }
                }
            }
        }
    }

    private void CalculateOffsets()
    {
        var visualOffset = 0;
        var textOffset = 0;

        foreach (var element in elements)
        {
            element.VisualColumn = visualOffset;
            element.RelativeTextOffset = textOffset;

            visualOffset += element.VisualLength;
            textOffset += element.DocumentLength;
        }

        VisualLength = visualOffset;

        Debug.Assert(textOffset == LastLine.EndOffset - FirstLine.Offset);
    }

    internal void RunTransformers(ITextRunContext context, IRenderer[] renderers)
    {
        Debug.Assert(phase == LifetimePhase.Transforming);

        foreach (var renderer in renderers)
        {
            renderer.Initialize(context, elements);
        }

        // WPF requires that either all or none of the typography properties to be set
        foreach (var element in elements.Where(e => e.TextRunProperties is not null && e.TextRunProperties.TypographyProperties is null))
        {
            element.TextRunProperties!.SetTypographyProperties(new DefaultTextRunTypographyProperties());
        }

        phase = LifetimePhase.Live;
    }

    /// <summary>
    /// Replaces the single element at <paramref name="elementIndex"/> with the specified elements.
    /// The replacement operation must preserve the document length, but may change the visual length.
    /// </summary>
    /// <remarks>
    /// This method may only be called by line transformers.
    /// </remarks>
    public void ReplaceElement(int elementIndex, params VisualLineElement[] newElements) => ReplaceElement(elementIndex, 1, newElements);

    /// <summary>
    /// Replaces <paramref name="count"/> elements starting at <paramref name="elementIndex"/> with the specified elements.
    /// The replacement operation must preserve the document length, but may change the visual length.
    /// </summary>
    /// <remarks>
    /// This method may only be called by line transformers.
    /// </remarks>
    public void ReplaceElement(int elementIndex, int count, params VisualLineElement[] newElements)
    {
        if (phase != LifetimePhase.Transforming)
        {
            throw new InvalidOperationException("This method should only be called by line transformers.");
        }

        var oldDocumentLength = 0;

        for (var i = elementIndex; i < elementIndex + count; i++)
        {
            oldDocumentLength += elements[i].DocumentLength;
        }

        var newDocumentLength = 0;

        foreach (var newElement in newElements)
        {
            newDocumentLength += newElement.DocumentLength;
        }

        if (oldDocumentLength != newDocumentLength)
        {
            throw new InvalidOperationException($"Old elements have document length = {oldDocumentLength}, but new elements have length = {newDocumentLength}");
        }

        elements.RemoveRange(elementIndex, count);
        elements.InsertRange(elementIndex, newElements);

        CalculateOffsets();
    }

    internal void SetTextLines(List<TextLine> textLines)
    {
        this.textLines = textLines.AsReadOnly();

        Height = 0;

        foreach (var line in textLines)
        {
            Height += line.Height;
        }
    }

    /// <summary>
    /// Gets the visual column from a document offset relative to the first line start.
    /// </summary>
    public int GetVisualColumn(int relativeTextOffset)
    {
        if (relativeTextOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeTextOffset), $"{relativeTextOffset} < 0");
        }
        
        foreach (VisualLineElement element in elements)
        {
            if (element.RelativeTextOffset <= relativeTextOffset
                && element.RelativeTextOffset + element.DocumentLength >= relativeTextOffset)
            {
                return element.GetVisualColumn(relativeTextOffset);
            }
        }
        return VisualLength;
    }

    /// <summary>
    /// Gets the document offset (relative to the first line start) from a visual column.
    /// </summary>
    public int GetRelativeOffset(int column)
    {
        if (column < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(column), $"{column} < 0");
        }

        int documentLength = 0;
        foreach (VisualLineElement element in elements)
        {
            if (element.VisualColumn <= column
                && element.VisualColumn + element.VisualLength > column)
            {
                return element.GetRelativeOffset(column);
            }
            documentLength += element.DocumentLength;
        }
        return documentLength;
    }

    /// <summary>
    /// Gets the text line containing the specified visual column.
    /// </summary>
    public TextLine GetTextLine(int visualColumn) => GetTextLine(visualColumn, false);

    /// <summary>
    /// Gets the text line containing the specified visual column.
    /// </summary>
    public TextLine GetTextLine(int visualColumn, bool isAtEndOfLine)
    {
        if (visualColumn < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visualColumn));
        }

        if (visualColumn >= VisualLengthWithEndOfLineMarker)
        {
            return TextLines[^1];
        }

        foreach (TextLine line in TextLines)
        {
            if (isAtEndOfLine ? visualColumn <= line.Length : visualColumn < line.Length)
            {
                return line;
            }
            else
            {
                visualColumn -= line.Length;
            }
        }
        throw new InvalidOperationException("Shouldn't happen (VisualLength incorrect?)");
    }

    /// <summary>
    /// Gets the visual top from the specified text line.
    /// </summary>
    /// <returns>Distance in device-independent pixels (DIP)
    /// from the top of the document to the top of the specified line.</returns>
    public double GetTextLineVisualYPosition(TextLine textLine, VisualYPosition position)
    {
        if (textLine == null)
        {
            throw new ArgumentNullException(nameof(textLine));
        }

        var pos = VisualTop;

        foreach (TextLine line in TextLines)
        {
            if (line == textLine)
            {
                return position switch
                {
                    VisualYPosition.Top => pos,
                    VisualYPosition.Middle => pos + line.Height / 2,
                    VisualYPosition.Bottom => pos + line.Height,
                    VisualYPosition.TextTop => pos + line.Baseline - textView.DefaultBaseline,
                    VisualYPosition.TextBottom => pos + line.Baseline - textView.DefaultBaseline + textView.DefaultLineHeight,
                    VisualYPosition.TextMiddle => pos + line.Baseline - textView.DefaultBaseline + textView.DefaultLineHeight / 2,
                    VisualYPosition.Baseline => pos + line.Baseline,
                    _ => throw new ArgumentException($"Invalid {nameof(position)} value: {position}")
                };
            }
            else
            {
                pos += line.Height;
            }
        }
        throw new ArgumentException("textLine is not a line in this VisualLine");
    }

    /// <summary>
    /// Gets the start visual column from the specified text line.
    /// </summary>
    public int GetTextLineStartColumn(TextLine textLine)
    {
        if (!TextLines.Contains(textLine))
        {
            throw new ArgumentException("textLine is not a line in this VisualLine");
        }

        var col = 0;

        foreach (var line in TextLines)
        {
            if (line == textLine)
            {
                break;
            }

            col += line.Length;
        }

        return col;
    }

    /// <summary>
    /// Gets a TextLine by the visual Y position.
    /// </summary>
    public TextLine GetTextLineByVisualY(double visualTop)
    {
        const double epsilon = 0.0001;
        var pos = VisualTop;

        foreach (var line in TextLines)
        {
            pos += line.Height;

            if (visualTop + epsilon < pos)
            {
                return line;
            }
        }

        return TextLines[^1];
    }

    /// <summary>
    /// Gets the visual position from the specified visualColumn.
    /// </summary>
    /// <returns>Position in device-independent pixels
    /// relative to the top left of the document.</returns>
    public Point GetVisualPosition(int column, VisualYPosition position)
    {
        var textLine = GetTextLine(column);

        var x = GetTextLineVisualXPosition(textLine, column);
        var y = GetTextLineVisualYPosition(textLine, position);

        return new Point(x, y);
    }

    internal Point GetVisualPosition(int column, bool isAtEndOfLine, VisualYPosition position)
    {
        var textLine = GetTextLine(column, isAtEndOfLine);

        var x = GetTextLineVisualXPosition(textLine, column);
        var y = GetTextLineVisualYPosition(textLine, position);

        return new Point(x, y);
    }

    /// <summary>
    /// Gets the distance to the left border of the text area of the specified visual column.
    /// The visual column must belong to the specified text line.
    /// </summary>
    public double GetTextLineVisualXPosition(TextLine textLine, int column)
    {
        var xPos = (textLine ?? throw new ArgumentNullException(nameof(textLine)))
            .GetDistanceFromCharacterHit(new CharacterHit(Math.Min(column, VisualLengthWithEndOfLineMarker), 0));

        if (column > VisualLengthWithEndOfLineMarker)
        {
            xPos += (column - VisualLengthWithEndOfLineMarker) * textView.WideSpaceWidth;
        }

        return xPos;
    }

    /// <summary>
    /// Gets the visual column from a document position (relative to top left of the document).
    /// If the user clicks between two visual columns, rounds to the nearest column.
    /// </summary>
    public int GetVisualColumn(Point point) => GetVisualColumn(point, textView.Options.EnableVirtualSpace);

    /// <summary>
    /// Gets the visual column from a document position (relative to top left of the document).
    /// If the user clicks between two visual columns, rounds to the nearest column.
    /// </summary>
    public int GetVisualColumn(Point point, bool allowVirtualSpace) => GetVisualColumn(GetTextLineByVisualY(point.Y), point.X, allowVirtualSpace);

    internal int GetVisualColumn(Point point, bool allowVirtualSpace, out bool isAtEndOfLine)
    {
        var textLine = GetTextLineByVisualY(point.Y);

        var column = GetVisualColumn(textLine, point.X, allowVirtualSpace);

        isAtEndOfLine = (column >= GetTextLineStartColumn(textLine) + textLine.Length);

        return column;
    }

    /// <summary>
    /// Gets the visual column from a document position (relative to top left of the document).
    /// If the user clicks between two visual columns, rounds to the nearest column.
    /// </summary>
    public int GetVisualColumn(TextLine textLine, double xPos, bool allowVirtualSpace)
    {
        if (xPos > textLine.WidthIncludingTrailingWhitespace)
        {
            if (allowVirtualSpace && textLine == TextLines[^1])
            {
                var virtualX = (int)Math.Round((xPos - textLine.WidthIncludingTrailingWhitespace) / textView.WideSpaceWidth, MidpointRounding.AwayFromZero);

                return VisualLengthWithEndOfLineMarker + virtualX;
            }
        }
        var hit = textLine.GetCharacterHitFromDistance(xPos);

        return hit.FirstCharacterIndex + hit.TrailingLength;
    }

    /// <summary>
    /// Validates the visual column and returns the correct one.
    /// </summary>
    public int ValidateVisualColumn(TextViewPosition position, bool allowVirtualSpace) => ValidateVisualColumn(Document.GetOffset(position.Location), position.VisualColumn, allowVirtualSpace);

    /// <summary>
    /// Validates the visual column and returns the correct one.
    /// </summary>
    public int ValidateVisualColumn(int offset, int visualColumn, bool allowVirtualSpace)
    {
        var firstLineOffset = FirstLine.Offset;

        if (visualColumn < 0)
        {
            return GetVisualColumn(offset - firstLineOffset);
        }

        var offsetFromVisualColumn = GetRelativeOffset(visualColumn) + firstLineOffset;
        
        if (offsetFromVisualColumn != offset)
        {
            return GetVisualColumn(offset - firstLineOffset);
        }
        
        if (visualColumn > VisualLength && !allowVirtualSpace)
        {
            return VisualLength;
        }

        return visualColumn;
    }

    /// <summary>
    /// Gets the visual column from a document position (relative to top left of the document).
    /// If the user clicks between two visual columns, returns the first of those columns.
    /// </summary>
    public int GetVisualColumnFloor(Point point) => GetVisualColumnFloor(point, textView.Options.EnableVirtualSpace);

    /// <summary>
    /// Gets the visual column from a document position (relative to top left of the document).
    /// If the user clicks between two visual columns, returns the first of those columns.
    /// </summary>
    public int GetVisualColumnFloor(Point point, bool allowVirtualSpace) => GetVisualColumnFloor(point, allowVirtualSpace, out _);

    internal int GetVisualColumnFloor(Point point, bool allowVirtualSpace, out bool isAtEndOfLine)
    {
        var textLine = GetTextLineByVisualY(point.Y);

        if (point.X > textLine.WidthIncludingTrailingWhitespace)
        {
            isAtEndOfLine = true;
            
            if (allowVirtualSpace && textLine == TextLines[^1])
            {
                // clicking virtual space in the last line
                var virtualX = (int)((point.X - textLine.WidthIncludingTrailingWhitespace) / textView.WideSpaceWidth);

                return VisualLengthWithEndOfLineMarker + virtualX;
            }
            
            // GetCharacterHitFromDistance returns a hit with FirstCharacterIndex=last character in line
            // and TrailingLength=1 when clicking behind the line, so the floor function needs to handle this case
            // specially and return the line's end column instead.
            return GetTextLineStartColumn(textLine) + textLine.Length;
        }
        else
        {
            isAtEndOfLine = false;
        }
        
        var hit = textLine.GetCharacterHitFromDistance(point.X);

        return hit.FirstCharacterIndex;
    }

    /// <summary>
    /// Gets the text view position from the specified visual column.
    /// </summary>
    public TextViewPosition GetTextViewPosition(int visualColumn)
    {
        var documentOffset = GetRelativeOffset(visualColumn) + FirstLine.Offset;

        return new TextViewPosition(Document.GetLocation(documentOffset), visualColumn);
    }

    /// <summary>
    /// Gets the text view position from the specified visual position.
    /// If the position is within a character, it is rounded to the next character boundary.
    /// </summary>
    /// <param name="visualPosition">The position in WPF device-independent pixels relative
    /// to the top left corner of the document.</param>
    /// <param name="allowVirtualSpace">Controls whether positions in virtual space may be returned.</param>
    public TextViewPosition GetTextViewPosition(Point visualPosition, bool allowVirtualSpace)
    {
        var column = GetVisualColumn(visualPosition, allowVirtualSpace, out bool isAtEndOfLine);
        var offset = GetRelativeOffset(column) + FirstLine.Offset;
        
        return new TextViewPosition(Document.GetLocation(offset), column)
        {
            IsAtEndOfLine = isAtEndOfLine
        };
    }

    /// <summary>
    /// Gets the text view position from the specified visual position.
    /// If the position is inside a character, the position in front of the character is returned.
    /// </summary>
    /// <param name="visualPosition">The position in WPF device-independent pixels relative
    /// to the top left corner of the document.</param>
    /// <param name="allowVirtualSpace">Controls whether positions in virtual space may be returned.</param>
    public TextViewPosition GetTextViewPositionFloor(Point visualPosition, bool allowVirtualSpace)
    {
        var column = GetVisualColumnFloor(visualPosition, allowVirtualSpace, out bool isAtEndOfLine);
        var offset = GetRelativeOffset(column) + FirstLine.Offset;
        
        return new TextViewPosition(Document.GetLocation(offset), column)
        {
            IsAtEndOfLine = isAtEndOfLine
        };
    }

    /// <summary>
    /// Gets whether the visual line was disposed.
    /// </summary>
    public bool IsDisposed => phase == LifetimePhase.Disposed;

    internal void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Debug.Assert(phase == LifetimePhase.Live);

        phase = LifetimePhase.Disposed;
        
        foreach (var line in TextLines)
        {
            line.Dispose();
        }
    }

    /// <summary>
    /// Gets the next possible caret position after visualColumn, or -1 if there is no caret position.
    /// </summary>
    public int GetNextCaretPosition(int visualColumn, LogicalDirection direction, CaretPositioningMode mode, bool allowVirtualSpace)
    {
        if (!HasStopsInVirtualSpace(mode))
        {
            allowVirtualSpace = false;
        }

        if (elements.Count == 0)
        {
            // special handling for empty visual lines:
            if (allowVirtualSpace)
            {
                if (direction == LogicalDirection.Forward)
                {
                    return Math.Max(0, visualColumn + 1);
                }

                return visualColumn > 0
                    ? visualColumn - 1
                    : -1;
            }
            else
            {
                // even though we don't have any elements,
                // there's a single caret stop at visualColumn 0
                if (visualColumn < 0 && direction == LogicalDirection.Forward)
                {
                    return 0;
                }

                return visualColumn > 0 && direction == LogicalDirection.Backward
                    ? 0
                    : -1;
            }
        }

        int i;
        
        if (direction == LogicalDirection.Backward)
        {
            // Search Backwards:
            // If the last element doesn't handle line borders, return the line end as caret stop

            if (visualColumn > VisualLength && !elements[^1].HandlesLineBorders && HasImplicitStopAtLineEnd(mode))
            {
                return allowVirtualSpace
                    ? visualColumn - 1
                    : VisualLength;
            }
            // skip elements that start after or at visualColumn
            for (i = elements.Count - 1; i >= 0; i--)
            {
                if (elements[i].VisualColumn < visualColumn)
                {
                    break;
                }
            }

            // search last element that has a caret stop
            for (; i >= 0; i--)
            {
                var pos = elements[i].GetNextCaretPosition(
                    Math.Min(visualColumn, elements[i].VisualColumn + elements[i].VisualLength + 1),
                    direction, mode);
                
                if (pos >= 0)
                {
                    return pos;
                }
            }

            // If we've found nothing, and the first element doesn't handle line borders,
            // return the line start as normal caret stop.
            if (visualColumn > 0 && !elements[0].HandlesLineBorders && HasImplicitStopAtLineStart(mode))
            {
                return 0;
            }
        }
        else
        {
            // Search Forwards:
            // If the first element doesn't handle line borders, return the line start as caret stop
            if (visualColumn < 0 && !elements[0].HandlesLineBorders && HasImplicitStopAtLineStart(mode))
            {
                return 0;
            }
            // skip elements that end before or at visualColumn
            for (i = 0; i < elements.Count; i++)
            {
                if (elements[i].VisualColumn + elements[i].VisualLength > visualColumn)
                {
                    break;
                }
            }
            // search first element that has a caret stop
            for (; i < elements.Count; i++)
            {
                var pos = elements[i].GetNextCaretPosition(
                    Math.Max(visualColumn, elements[i].VisualColumn - 1),
                    direction,
                    mode);
                
                if (pos >= 0)
                {
                    return pos;
                }
            }
            // if we've found nothing, and the last element doesn't handle line borders,
            // return the line end as caret stop
            if ((allowVirtualSpace || !elements[^1].HandlesLineBorders) && HasImplicitStopAtLineEnd(mode))
            {
                if (visualColumn < VisualLength)
                {
                    return VisualLength;
                }
                else if (allowVirtualSpace)
                {
                    return visualColumn + 1;
                }
            }
        }
        
        // we've found nothing, return -1 and let the caret search continue in the next line
        return -1;
    }

    private static bool HasStopsInVirtualSpace(CaretPositioningMode mode) => mode == CaretPositioningMode.Normal || mode == CaretPositioningMode.EveryCodepoint;

    private static bool HasImplicitStopAtLineStart(CaretPositioningMode mode) => mode == CaretPositioningMode.Normal || mode == CaretPositioningMode.EveryCodepoint;

    private static bool HasImplicitStopAtLineEnd(CaretPositioningMode _) => true;

    private VisualLineDrawingVisual visual;

    internal VisualLineDrawingVisual Render()
    {
        Debug.Assert(phase == LifetimePhase.Live);

        visual ??= new VisualLineDrawingVisual(this, textView.FlowDirection);

        return visual;
    }
}