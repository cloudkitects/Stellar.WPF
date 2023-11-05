using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Media.TextFormatting;
using System.Windows.Media;
using System.Windows;

using Stellar.WPF.Document;
using Stellar.WPF.Editing;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Helper for creating a PathGeometry.
/// </summary>
public sealed class BackgroundGeometryBuilder
{
    private double cornerRadius;
    private readonly PathFigureCollection figures = new();
    private PathFigure? figure;
    private int insertionIndex;
    private double lastTop, lastBottom;
    private double lastLeft, lastRight;

    /// <summary>
    /// Gets/sets the radius of the rounded corners.
    /// </summary>
    public double CornerRadius
    {
        get => cornerRadius;
        set => cornerRadius = value;
    }

    /// <summary>
    /// Gets/Sets whether to align to whole pixels.
    /// 
    /// If BorderThickness is set to 0, the geometry is aligned to whole pixels.
    /// If BorderThickness is set to a non-zero value, the outer edge of the border is aligned
    /// to whole pixels.
    /// 
    /// The default value is <c>false</c>.
    /// </summary>
    public bool AlignToWholePixels { get; set; }

    /// <summary>
    /// Gets/sets the border thickness.
    /// 
    /// This property only has an effect if <c>AlignToWholePixels</c> is enabled.
    /// When using the resulting geometry to paint a border, set this property to the border thickness.
    /// Otherwise, leave the property set to the default value <c>0</c>.
    /// </summary>
    public double BorderThickness { get; set; }

    /// <summary>
    /// Gets/Sets whether to extend the rectangles to full width at line end.
    /// </summary>
    public bool ExtendToFullWidthAtLineEnd { get; set; }

    /// <summary>
    /// Creates a new BackgroundGeometryBuilder instance.
    /// </summary>
    public BackgroundGeometryBuilder()
    {
    }

    /// <summary>
    /// Adds the specified segment to the geometry.
    /// </summary>
    public void AddSegment(TextView textView, ISegment segment)
    {
        var pixelSize = (textView ?? throw new ArgumentNullException(nameof(textView))).GetPixelSize();

        if (segment is null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        foreach (var rect in GetRectsForSegmentInternal(textView, segment, ExtendToFullWidthAtLineEnd))
        {
            AddRectangle(pixelSize, rect);
        }
    }

    /// <summary>
    /// Adds a rectangle to the geometry.
    /// </summary>
    /// <remarks>
    /// This overload will align the coordinates according to
    /// <see cref="AlignToWholePixels"/>.
    /// Use the <see cref="AddRectangle(double,double,double,double)"/>-overload instead if the coordinates should not be aligned.
    /// </remarks>
    public void AddRectangle(TextView textView, Rect rectangle)
    {
        AddRectangle(textView.GetPixelSize(), rectangle);
    }

    private void AddRectangle(Size pixelSize, Rect rect)
    {
        if (AlignToWholePixels)
        {
            var halfBorder = 0.5 * BorderThickness;

            AddRectangle((rect.Left - halfBorder).Round(pixelSize.Width) + halfBorder,
                         (rect.Top - halfBorder).Round(pixelSize.Height) + halfBorder,
                         (rect.Right + halfBorder).Round(pixelSize.Width) - halfBorder,
                         (rect.Bottom + halfBorder).Round(pixelSize.Height) - halfBorder);
        }
        else
        {
            AddRectangle(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }

    /// <summary>
    /// Calculates the list of rectangle where the segment in shown.
    /// This method usually returns one rectangle for each line inside the segment
    /// (but potentially more, e.g. when bidirectional text is involved).
    /// </summary>
    public static IEnumerable<Rect> GetRectsForSegment(TextView textView, ISegment segment, bool extendToFullWidthAtLineEnd = false)
    {
        return GetRectsForSegmentInternal(
            textView ?? throw new ArgumentNullException(nameof(textView)),
            segment ?? throw new ArgumentNullException(nameof(segment)),
            extendToFullWidthAtLineEnd);
    }

    private static IEnumerable<Rect> GetRectsForSegmentInternal(TextView textView, ISegment segment, bool extendToFullWidthAtLineEnd)
    {
        var segmentStart = segment.Offset;
        var segmentEnd = segment.Offset + segment.Length;

        segmentStart = segmentStart.Rectify(0, textView.Document.TextLength);
        segmentEnd = segmentEnd.Rectify(0, textView.Document.TextLength);

        TextViewPosition start;
        TextViewPosition end;

        if (segment is SelectionSegment selection)
        {
            start = new TextViewPosition(textView.Document.GetLocation(selection.StartOffset), selection.StartVisualColumn);
            end = new TextViewPosition(textView.Document.GetLocation(selection.EndOffset), selection.EndVisualColumn);
        }
        else
        {
            start = new TextViewPosition(textView.Document.GetLocation(segmentStart));
            end = new TextViewPosition(textView.Document.GetLocation(segmentEnd));
        }

        foreach (var line in textView.VisualLines)
        {
            var startOffset = line.FirstLine.Offset;
            
            if (startOffset > segmentEnd)
            {
                break;
            }

            var endOffset = line.LastLine.Offset + line.LastLine.Length;

            if (endOffset < segmentStart)
            {
                continue;
            }

            var startColumn = segmentStart < startOffset
                ? 0
                : line.ValidateVisualColumn(start, extendToFullWidthAtLineEnd);
            
            var endColumn = segmentEnd > endOffset
                ? extendToFullWidthAtLineEnd
                    ? int.MaxValue
                    : line.VisualLengthWithEndOfLineMarker
                : line.ValidateVisualColumn(end, extendToFullWidthAtLineEnd);

            foreach (var rect in ProcessTextLines(textView, line, startColumn, endColumn))
            {
                yield return rect;
            }
        }
    }

    /// <summary>
    /// Calculates the rectangles for the visual column segment.
    /// This returns one rectangle for each line inside the segment.
    /// </summary>
    public static IEnumerable<Rect> GetRectsFromVisualSegment(TextView textView, VisualLine line, int startColumn, int endColumn)
    {
        return ProcessTextLines(
            textView ?? throw new ArgumentNullException(nameof(textView)),
            line ?? throw new ArgumentNullException(nameof(line)),
            startColumn,
            endColumn);
    }

    private static IEnumerable<Rect> ProcessTextLines(TextView textView, VisualLine visualLine, int segmentStartColumn, int segmentEndColumn)
    {
        var lastTextLine = visualLine.TextLines.Last();
        var scrollOffset = textView.ScrollOffset;

        for (var i = 0; i < visualLine.TextLines.Count; i++)
        {
            var line = visualLine.TextLines[i];
            var y = visualLine.GetTextLineVisualYPosition(line, VisualYPosition.Top);
            
            var startColumn = visualLine.GetTextLineStartColumn(line);
            var endColumn = startColumn + line.Length -
                (line == lastTextLine
                    ? 1
                    : line.TrailingWhitespaceLength);

            if (segmentEndColumn < startColumn)
            {
                break;
            }

            if (lastTextLine != line && segmentStartColumn > endColumn)
            {
                continue;
            }

            var segmentStartColumnInLine = Math.Max(segmentStartColumn, startColumn);
            var segmentEndColumnInLine = Math.Min(segmentEndColumn, endColumn);

            y -= scrollOffset.Y;

            var lastRect = Rect.Empty;

            if (segmentStartColumnInLine == segmentEndColumnInLine)
            {
                // GetTextBounds() crashes for 0 length; handle it with GetDistanceFromCharacterHit()
                // and return a rectangle to ensure empty lines are still visible
                var pos = visualLine.GetTextLineVisualXPosition(line, segmentStartColumnInLine) - scrollOffset.X;
                
                // prevent empty rectangles at the end of a line when showing spaces; the same rectangle
                // is calculated and added twice since the offset could be mapped to two visual positions
                // if there is no trailing whitespace.
                if (segmentEndColumnInLine == endColumn &&
                    i < visualLine.TextLines.Count - 1 &&
                    segmentEndColumn > segmentEndColumnInLine
                    && line.TrailingWhitespaceLength == 0)
                {
                    continue;
                }

                if (segmentStartColumnInLine == startColumn &&
                    i > 0 &&
                    segmentStartColumn < segmentStartColumnInLine &&
                    visualLine.TextLines[i - 1].TrailingWhitespaceLength == 0)
                {
                    continue;
                }

                lastRect = new Rect(pos, y, textView.EmptyLineSelectionWidth, line.Height);
            }
            else
            {
                if (segmentStartColumnInLine <= endColumn)
                {
                    foreach (var bounds in line.GetTextBounds(segmentStartColumnInLine, segmentEndColumnInLine - segmentStartColumnInLine))
                    {
                        var left = bounds.Rectangle.Left - scrollOffset.X;
                        var right = bounds.Rectangle.Right - scrollOffset.X;

                        if (!lastRect.IsEmpty)
                        {
                            yield return lastRect;
                        }
                        
                        // left > right is possible in RTL languages
                        lastRect = new Rect(Math.Min(left, right), y, Math.Abs(right - left), line.Height);
                    }
                }
            }

            // extend the last rectangle with the portion of the selection after the line end if the segment ends in virtual space,
            // and to the end of the line when word-wrap is enabled and the segment continues into the next line
            if (segmentEndColumn > endColumn)
            {
                double left, right;

                // in virtual space
                if (segmentStartColumn > visualLine.VisualLengthWithEndOfLineMarker)
                {
                    left = visualLine.GetTextLineVisualXPosition(lastTextLine, segmentStartColumn);
                }
                else
                {
                    // segmentStartColumn to visualEndColumn rectangles are processed; process the remainder
                    // include whitespace for visualEndColumn if hidden by word wrap
                    left = line == lastTextLine
                        ? line.WidthIncludingTrailingWhitespace
                        : line.Width;
                }
                
                if (line != lastTextLine || segmentEndColumn == int.MaxValue)
                {
                    // select the full width of the viewport when word-wrap is enabled and the segment continues
                    // into the next line or if the extendToFullWidthAtLineEnd option is used
                    right = Math.Max(((IScrollInfo)textView).ExtentWidth, ((IScrollInfo)textView).ViewportWidth);
                }
                else
                {
                    right = visualLine.GetTextLineVisualXPosition(lastTextLine, segmentEndColumn);
                }
                
                var extendSelection = new Rect(Math.Min(left, right), y, Math.Abs(right - left), line.Height);
                
                if (!lastRect.IsEmpty)
                {
                    if (extendSelection.IntersectsWith(lastRect))
                    {
                        lastRect.Union(extendSelection);
                        
                        yield return lastRect;
                    }
                    else
                    {
                        // keep lastRect and extendSelection separate if the end of the line is in an RTL segment
                        yield return lastRect;
                        yield return extendSelection;
                    }
                }
                else
                {
                    yield return extendSelection;
                }
            }
            else
            {
                yield return lastRect;
            }
        }
    }

    /// <summary>
    /// Adds a rectangle to the geometry.
    /// </summary>
    /// <remarks>
    /// This overload assumes that the coordinates are aligned properly
    /// (see <see cref="AlignToWholePixels"/>).
    /// Use the <see cref="AddRectangle(TextView,Rect)"/>-overload instead if the coordinates are not yet aligned.
    /// </remarks>
    public void AddRectangle(double left, double top, double right, double bottom)
    {
        if (!top.Nears(lastBottom))
        {
            CloseFigure();
        }

        if (figure is null)
        {
            figure = new PathFigure
            {
                StartPoint = new Point(left, top + cornerRadius)
            };

            if (Math.Abs(left - right) > cornerRadius)
            {
                figure.Segments.Add(MakeArc(left + cornerRadius, top, SweepDirection.Clockwise));
                figure.Segments.Add(MakeLineSegment(right - cornerRadius, top));
                figure.Segments.Add(MakeArc(right, top + cornerRadius, SweepDirection.Clockwise));
            }

            figure.Segments.Add(MakeLineSegment(right, bottom - cornerRadius));

            insertionIndex = figure.Segments.Count;
        }
        else
        {
            if (!lastRight.Nears(right))
            {
                var radius = right < lastRight
                    ? -cornerRadius
                    : cornerRadius;

                var dir1 = right < lastRight ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
                var dir2 = right < lastRight ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

                figure.Segments.Insert(insertionIndex++, MakeArc(lastRight + radius, lastBottom, dir1));
                figure.Segments.Insert(insertionIndex++, MakeLineSegment(right - radius, top));
                figure.Segments.Insert(insertionIndex++, MakeArc(right, top + cornerRadius, dir2));
            }

            figure.Segments.Insert(insertionIndex++, MakeLineSegment(right, bottom - cornerRadius));
            figure.Segments.Insert(insertionIndex, MakeLineSegment(lastLeft, lastTop + cornerRadius));

            if (!lastLeft.Nears(left))
            {
                var radius = left < lastLeft
                    ? cornerRadius
                    : -cornerRadius;

                var dir1 = left < lastLeft ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
                var dir2 = left < lastLeft ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

                figure.Segments.Insert(insertionIndex, MakeArc(lastLeft, lastBottom - cornerRadius, dir1));
                figure.Segments.Insert(insertionIndex, MakeLineSegment(lastLeft - radius, lastBottom));
                figure.Segments.Insert(insertionIndex, MakeArc(left + radius, lastBottom, dir2));
            }
        }

        lastTop = top;
        lastBottom = bottom;
        lastLeft = left;
        lastRight = right;
    }

    private ArcSegment MakeArc(double x, double y, SweepDirection dir)
    {
        var segment = new ArcSegment(
            new Point(x, y),
            new Size(cornerRadius, cornerRadius),
            0,
            false,
            dir,
            true);

        segment.Freeze();

        return segment;
    }

    private static LineSegment MakeLineSegment(double x, double y)
    {
        var segment = new LineSegment(new Point(x, y), true);

        segment.Freeze();

        return segment;
    }

    /// <summary>
    /// Closes the current figure.
    /// </summary>
    public void CloseFigure()
    {
        if (figure is not null)
        {
            figure.Segments.Insert(insertionIndex, MakeLineSegment(lastLeft, lastTop + cornerRadius));

            if (Math.Abs(lastLeft - lastRight) > cornerRadius)
            {
                figure.Segments.Insert(insertionIndex, MakeArc(lastLeft, lastBottom - cornerRadius, SweepDirection.Clockwise));
                figure.Segments.Insert(insertionIndex, MakeLineSegment(lastLeft + cornerRadius, lastBottom));
                figure.Segments.Insert(insertionIndex, MakeArc(lastRight - cornerRadius, lastBottom, SweepDirection.Clockwise));
            }

            figure.IsClosed = true;

            figures.Add(figure);

            figure = null!;
        }
    }

    /// <summary>
    /// Creates the geometry.
    /// Returns null when the geometry is empty!
    /// </summary>
    public Geometry CreateGeometry()
    {
        CloseFigure();

        if (figures.Count != 0)
        {
            var geometry = new PathGeometry(figures);
            geometry.Freeze();

            return geometry;
        }

        return null!;
    }
}