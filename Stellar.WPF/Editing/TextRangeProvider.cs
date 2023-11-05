using Stellar.WPF.Document;
using Stellar.WPF.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Documents;
using System.Windows;

namespace Stellar.WPF.Editing;

internal class TextRangeProvider : ITextRangeProvider
{
    private readonly TextArea textArea;
    private readonly Document.Document document;
    private ISegment segment;

    public TextRangeProvider(TextArea textArea, Document.Document document, ISegment segment)
    {
        this.textArea = textArea;
        this.document = document;
        this.segment = segment;
    }

    public TextRangeProvider(TextArea textArea, Document.Document document, int offset, int length)
    {
        this.textArea = textArea;
        this.document = document;
        this.segment = new AnchorSegment(document, offset, length);
    }

    private string ID
    {
        get
        {
            return $"({GetHashCode():x8)}, {segment})";
        }
    }

    [Conditional("DEBUG")]
    private static void Log(string format, params object[] args) => Debug.WriteLine(string.Format(format, args));

    public void AddToSelection() => Log($"{ID}.AddToSelection()");

    public ITextRangeProvider Clone()
    {
        var result = new TextRangeProvider(textArea, document, segment);
        
        Log($"{ID}.Clone() = {result.ID}");
        
        return result;
    }

    public bool Compare(ITextRangeProvider range)
    {
        var other = (TextRangeProvider)range;
        
        var result = document == other.document &&
            segment.Offset == other.segment.Offset &&
            segment.EndOffset == other.segment.EndOffset;
        
        Log($"{ID}.Compare({other.ID}) = {result}");
        
        return result;
    }

    private int GetEndpoint(TextPatternRangeEndpoint endpoint) => endpoint switch
    {
        TextPatternRangeEndpoint.Start => segment.Offset,
        TextPatternRangeEndpoint.End => segment.EndOffset,
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
    };

    public int CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
    {
        var other = (TextRangeProvider)targetRange;
        var result = GetEndpoint(endpoint).CompareTo(other.GetEndpoint(targetEndpoint));
        
        Log($"{ID}.CompareEndpoints({endpoint}, {other.ID}, {targetEndpoint}) = {result}");
        
        return result;
    }

    public void ExpandToEnclosingUnit(TextUnit unit)
    {
        Log("{0}.ExpandToEnclosingUnit({1})", ID, unit);

        switch (unit)
        {
            case TextUnit.Character:
                ExpandToEnclosingUnit(CaretPositioningMode.Normal);
                break;
            case TextUnit.Format:
            case TextUnit.Word:
                ExpandToEnclosingUnit(CaretPositioningMode.WordStartOrSymbol);
                break;
            case TextUnit.Line:
            case TextUnit.Paragraph:
                segment = document.GetLineByOffset(segment.Offset);
                break;
            case TextUnit.Document:
                segment = new AnchorSegment(document, 0, document.TextLength);
                break;
        }
    }

    private void ExpandToEnclosingUnit(CaretPositioningMode mode)
    {
        var start = document.GetNextCaretPosition(segment.Offset + 1, LogicalDirection.Backward, mode);
        
        if (start < 0)
        {
            return;
        }

        var end = document.GetNextCaretPosition(start, LogicalDirection.Forward, mode);
        
        if (end < 0)
        {
            return;
        }

        segment = new AnchorSegment(document, start, end - start);
    }

    public ITextRangeProvider FindAttribute(int attribute, object value, bool backward)
    {
        Log($"{ID}.FindAttribute({attribute}, {value}, {backward})");
        
        return null!;
    }

    public ITextRangeProvider FindText(string text, bool backward, bool ignoreCase)
    {
        Log($"{ID}.FindText({text}, {backward}, {ignoreCase})");
        
        var segmentText = document.GetText(segment);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var pos = backward
            ? segmentText.LastIndexOf(text, comparison)
            : segmentText.IndexOf(text, comparison);
        
        if (pos >= 0)
        {
            return new TextRangeProvider(textArea, document, segment.Offset + pos, text.Length);
        }
        
        return null!;
    }

    public object GetAttributeValue(int attribute)
    {
        Log($"{ID}.GetAttributeValue({attribute})");
        
        return null!;
    }

    public double[] GetBoundingRectangles()
    {
        Log($"{ID}.GetBoundingRectangles()");
        
        var textView = textArea.TextView;
        //var source = PresentationSource.FromVisual(textArea);
        var result = new List<double>();
        
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            var tl = textView.PointToScreen(rect.TopLeft);
            var br = textView.PointToScreen(rect.BottomRight);
            
            result.Add(tl.X);
            result.Add(tl.Y);
            result.Add(br.X - tl.X);
            result.Add(br.Y - tl.Y);
        }

        return result.ToArray();
    }

    public IRawElementProviderSimple[] GetChildren()
    {
        Log($"{ID}.GetChildren()");
        
        return Array.Empty<IRawElementProviderSimple>();
    }

    public IRawElementProviderSimple GetEnclosingElement()
    {
        Log($"{ID}.GetEnclosingElement()");
        
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(textArea) as TextAreaAutomationPeer ?? throw new NotSupportedException();
        
        return peer.Provider;
    }

    public string GetText(int maxLength)
    {
        Log($"{ID}.GetText({maxLength})");

        return maxLength < 0
            ? document.GetText(segment)
            : document.GetText(segment.Offset, Math.Min(segment.Length, maxLength));
    }

    public int Move(TextUnit unit, int count)
    {
        Log($"{ID}.Move({unit}, {count})");
        
        var movedCount = MoveEndpointByUnit(TextPatternRangeEndpoint.Start, unit, count);
        
        segment = new SimpleSegment(segment.Offset, 0); // Collapse to empty range
        
        ExpandToEnclosingUnit(unit);
        
        return movedCount;
    }

    public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
    {
        var other = (TextRangeProvider)targetRange;
        
        Log($"{ID}.MoveEndpointByRange({endpoint}, {other.ID}, {targetEndpoint})");
        
        SetEndpoint(endpoint, other.GetEndpoint(targetEndpoint));
    }

    private void SetEndpoint(TextPatternRangeEndpoint endpoint, int targetOffset)
    {
        if (endpoint == TextPatternRangeEndpoint.Start)
        {
            // set start of this range to targetOffset
            segment = new AnchorSegment(document, targetOffset, Math.Max(0, segment.EndOffset - targetOffset));
        }
        else
        {
            // set end of this range to targetOffset
            var newStart = Math.Min(segment.Offset, targetOffset);
            
            segment = new AnchorSegment(document, newStart, targetOffset - newStart);
        }
    }

    public int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count)
    {
        Log($"{ID}.MoveEndpointByUnit({endpoint}, {unit}, {count})");
        
        var offset = GetEndpoint(endpoint);
        
        switch (unit)
        {
            case TextUnit.Character:
                offset = MoveOffset(offset, CaretPositioningMode.Normal, count);
                break;

            case TextUnit.Format:
            case TextUnit.Word:
                offset = MoveOffset(offset, CaretPositioningMode.WordStart, count);
                break;

            case TextUnit.Line:
            case TextUnit.Paragraph:
                var line = document.GetLineByOffset(offset).Number;
                var newLine = Math.Max(1, Math.Min(document.LineCount, line + count));
                
                offset = document.GetLineByNumber(newLine).Offset;
                break;

            case TextUnit.Document:
                offset = count < 0
                    ? 0
                    : document.TextLength;
                break;
        }

        SetEndpoint(endpoint, offset);
        
        return count;
    }

    private int MoveOffset(int offset, CaretPositioningMode mode, int count)
    {
        var direction = count < 0
            ? LogicalDirection.Backward
            : LogicalDirection.Forward;
        
        count = Math.Abs(count);
        
        for (int i = 0; i < count; i++)
        {
            var newOffset = document.GetNextCaretPosition(offset, direction, mode);
            
            if (newOffset == offset || newOffset < 0)
            {
                break;
            }

            offset = newOffset;
        }

        return offset;
    }

    public void RemoveFromSelection() => Log("{0}.RemoveFromSelection()", ID);

    public void ScrollIntoView(bool alignToTop) => Log("{0}.ScrollIntoView({1})", ID, alignToTop);

    public void Select()
    {
        Log("{0}.Select()", ID);
        textArea.Selection = new SimpleSelection(textArea,
            new TextViewPosition(document.GetLocation(segment.Offset)),
            new TextViewPosition(document.GetLocation(segment.EndOffset)));
    }
}
