﻿using Stellar.WPF.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Media.TextFormatting;
using System.Windows.Media;
using System.Windows;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Helper for creating a PathGeometry.
/// </summary>
public sealed class BackgroundGeometryBuilder
{
    private double cornerRadius;
    private PathFigureCollection figures = new();
    private PathFigure figure;
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
        
        foreach (var rect in GetRectsForSegment(textView, segment, ExtendToFullWidthAtLineEnd))
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

            AddRectangle((rect.Left   - halfBorder).Round(pixelSize.Width)  + halfBorder,
                         (rect.Top    - halfBorder).Round(pixelSize.Height) + halfBorder,
                         (rect.Right  + halfBorder).Round(pixelSize.Width)  - halfBorder,
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
        return GetRectsForSegmentImpl(
            textView ?? throw new ArgumentNullException(nameof(textView)),
            segment ?? throw new ArgumentNullException(nameof(segment)),
            extendToFullWidthAtLineEnd);
    }

    private static IEnumerable<Rect> GetRectsForSegmentImpl(TextView textView, ISegment segment, bool extendToFullWidthAtLineEnd)
    {
        var segmentStart = segment.Offset;
        var segmentEnd = segment.Offset + segment.Length;

        segmentStart = segmentStart.Rectify(0, textView.Document.TextLength);
        segmentEnd = segmentEnd.Rectify(0, textView.Document.TextLength);

        TextViewPosition start;
        TextViewPosition end;

        if (segment is SelectionSegment)
        {
            var sel = (SelectionSegment)segment;

            start = new TextViewPosition(textView.Document.GetLocation(sel.StartOffset), sel.StartVisualColumn);
            end = new TextViewPosition(textView.Document.GetLocation(sel.EndOffset), sel.EndVisualColumn);
        }
        else
        {
            start = new TextViewPosition(textView.Document.GetLocation(segmentStart));
            end = new TextViewPosition(textView.Document.GetLocation(segmentEnd));
        }

        foreach (VisualLine vl in textView.VisualLines)
        {
            int vlStartOffset = vl.FirstLine.Offset;
            if (vlStartOffset > segmentEnd)
                break;
            int vlEndOffset = vl.LastLine.Offset + vl.LastLine.Length;
            if (vlEndOffset < segmentStart)
                continue;

            int segmentStartVC;
            if (segmentStart < vlStartOffset)
                segmentStartVC = 0;
            else
                segmentStartVC = vl.ValidateVisualColumn(start, extendToFullWidthAtLineEnd);

            int segmentEndVC;
            if (segmentEnd > vlEndOffset)
                segmentEndVC = extendToFullWidthAtLineEnd ? int.MaxValue : vl.VisualLengthWithEndOfLineMarker;
            else
                segmentEndVC = vl.ValidateVisualColumn(end, extendToFullWidthAtLineEnd);

            foreach (var rect in ProcessTextLines(textView, vl, segmentStartVC, segmentEndVC))
                yield return rect;
        }
    }

    /// <summary>
    /// Calculates the rectangles for the visual column segment.
    /// This returns one rectangle for each line inside the segment.
    /// </summary>
    public static IEnumerable<Rect> GetRectsFromVisualSegment(TextView textView, VisualLine line, int startVC, int endVC)
    {
        return ProcessTextLines(
            textView ?? throw new ArgumentNullException(nameof(textView)),
            line ?? throw new ArgumentNullException(nameof(line)),
            startVC,
            endVC);
    }

    private static IEnumerable<Rect> ProcessTextLines(TextView textView, VisualLine visualLine, int segmentStartVC, int segmentEndVC)
    {
        TextLine lastTextLine = visualLine.TextLines.Last();
        Vector scrollOffset = textView.ScrollOffset;

        for (int i = 0; i < visualLine.TextLines.Count; i++)
        {
            TextLine line = visualLine.TextLines[i];
            double y = visualLine.GetTextLineVisualYPosition(line, VisualYPosition.Top);
            int visualStartCol = visualLine.GetTextLineVisualStartColumn(line);
            int visualEndCol = visualStartCol + line.Length;
            if (line == lastTextLine)
                visualEndCol -= 1; // 1 position for the TextEndOfParagraph
            else
                visualEndCol -= line.TrailingWhitespaceLength;

            if (segmentEndVC < visualStartCol)
                break;
            if (lastTextLine != line && segmentStartVC > visualEndCol)
                continue;
            int segmentStartVCInLine = Math.Max(segmentStartVC, visualStartCol);
            int segmentEndVCInLine = Math.Min(segmentEndVC, visualEndCol);
            y -= scrollOffset.Y;
            Rect lastRect = Rect.Empty;
            if (segmentStartVCInLine == segmentEndVCInLine)
            {
                // GetTextBounds crashes for length=0, so we'll handle this case with GetDistanceFromCharacterHit
                // We need to return a rectangle to ensure empty lines are still visible
                double pos = visualLine.GetTextLineVisualXPosition(line, segmentStartVCInLine);
                pos -= scrollOffset.X;
                // The following special cases are necessary to get rid of empty rectangles at the end of a TextLine if "Show Spaces" is active.
                // If not excluded once, the same rectangle is calculated (and added) twice (since the offset could be mapped to two visual positions; end/start of line), if there is no trailing whitespace.
                // Skip this TextLine segment, if it is at the end of this line and this line is not the last line of the VisualLine and the selection continues and there is no trailing whitespace.
                if (segmentEndVCInLine == visualEndCol && i < visualLine.TextLines.Count - 1 && segmentEndVC > segmentEndVCInLine && line.TrailingWhitespaceLength == 0)
                    continue;
                if (segmentStartVCInLine == visualStartCol && i > 0 && segmentStartVC < segmentStartVCInLine && visualLine.TextLines[i - 1].TrailingWhitespaceLength == 0)
                    continue;
                lastRect = new Rect(pos, y, textView.EmptyLineSelectionWidth, line.Height);
            }
            else
            {
                if (segmentStartVCInLine <= visualEndCol)
                {
                    foreach (TextBounds b in line.GetTextBounds(segmentStartVCInLine, segmentEndVCInLine - segmentStartVCInLine))
                    {
                        double left = b.Rectangle.Left - scrollOffset.X;
                        double right = b.Rectangle.Right - scrollOffset.X;
                        if (!lastRect.IsEmpty)
                            yield return lastRect;
                        // left>right is possible in RTL languages
                        lastRect = new Rect(Math.Min(left, right), y, Math.Abs(right - left), line.Height);
                    }
                }
            }
            // If the segment ends in virtual space, extend the last rectangle with the rectangle the portion of the selection
            // after the line end.
            // Also, when word-wrap is enabled and the segment continues into the next line, extend lastRect up to the end of the line.
            if (segmentEndVC > visualEndCol)
            {
                double left, right;
                if (segmentStartVC > visualLine.VisualLengthWithEndOfLineMarker)
                {
                    // segmentStartVC is in virtual space
                    left = visualLine.GetTextLineVisualXPosition(lastTextLine, segmentStartVC);
                }
                else
                {
                    // Otherwise, we already processed the rects from segmentStartVC up to visualEndCol,
                    // so we only need to do the remainder starting at visualEndCol.
                    // For word-wrapped lines, visualEndCol doesn't include the whitespace hidden by the wrap,
                    // so we'll need to include it here.
                    // For the last line, visualEndCol already includes the whitespace.
                    left = (line == lastTextLine ? line.WidthIncludingTrailingWhitespace : line.Width);
                }
                if (line != lastTextLine || segmentEndVC == int.MaxValue)
                {
                    // If word-wrap is enabled and the segment continues into the next line,
                    // or if the extendToFullWidthAtLineEnd option is used (segmentEndVC == int.MaxValue),
                    // we select the full width of the viewport.
                    right = Math.Max(((IScrollInfo)textView).ExtentWidth, ((IScrollInfo)textView).ViewportWidth);
                }
                else
                {
                    right = visualLine.GetTextLineVisualXPosition(lastTextLine, segmentEndVC);
                }
                Rect extendSelection = new Rect(Math.Min(left, right), y, Math.Abs(right - left), line.Height);
                if (!lastRect.IsEmpty)
                {
                    if (extendSelection.IntersectsWith(lastRect))
                    {
                        lastRect.Union(extendSelection);
                        yield return lastRect;
                    }
                    else
                    {
                        // If the end of the line is in an RTL segment, keep lastRect and extendSelection separate.
                        yield return lastRect;
                        yield return extendSelection;
                    }
                }
                else
                    yield return extendSelection;
            }
            else
                yield return lastRect;
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
                    :  cornerRadius;

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
                    ?  cornerRadius
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