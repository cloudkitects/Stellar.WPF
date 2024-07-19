using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Stellar.WPF.Document;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling;

/// <summary>
/// Represents a styled document line.
/// </summary>
public class StyledLine
{
    #region properties
    /// <summary>
    /// Gets the document associated with this styled line.
    /// </summary>
    public IDocument Document { get; private set; }

    /// <summary>
    /// Gets the document line associated with this styled line.
    /// </summary>
    public ILine Line { get; private set; }

    /// <summary>
    /// Gets the styled sections.
    /// </summary>
    /// <remarks>
    /// Sections are sorted by start offset, and they're not overlapping,
	/// but they can be nested, in which case outer sections are listed
	/// before inner sections.
    /// </remarks>
    public IList<StyledSection> Sections { get; private set; }
    #endregion

    #region constructor
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public StyledLine(IDocument document, ILine documentLine)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Line = documentLine;
        Sections = new NullSafeCollection<StyledSection>();
    }
    #endregion

    #region HtmlElement
    sealed class HtmlElement : IComparable<HtmlElement>
    {
        internal readonly int Offset;
        internal readonly int Nesting;
        internal readonly bool IsEnd;
        internal readonly Style Style;

        public HtmlElement(int offset, int nesting, bool isEnd, Style style)
        {
            Offset = offset;
            Nesting = nesting;
            IsEnd = isEnd;
            Style = style;
        }

        public int CompareTo(HtmlElement? other)
        {
            var d = Offset.CompareTo(other!.Offset);

            if (d != 0)
            {
                return d;
            }

            if (IsEnd != other.IsEnd)
            {
                return IsEnd
                    ? -1
                    : 1;
            }

            return IsEnd
                ? other.Nesting.CompareTo(Nesting)
                : Nesting.CompareTo(other.Nesting);
        }
    }
    #endregion

    #region methods
    /// <summary>
    /// Checks sections are sorted correctly and are not overlapping.
    /// </summary>
    /// <seealso cref="Sections"/>
    public void CheckInvariants()
    {
        var lineMin = Line.Offset;
        var lineMax = Line.EndOffset;

        for (var i = 0; i < Sections.Count; i++)
        {
            var section = Sections[i];

            if (section.Offset < lineMin || section.Length < 0 || lineMax < section.Offset + section.Length)
            {
                throw new InvalidOperationException("Section is outside line bounds.");
            }

            for (var j = i + 1; j < Sections.Count; j++)
            {
                var nextSection = Sections[j];

                // 2:5               |     5:5           |     5:5            | 2:5
                // +---+             |     +---+         |     +---+          | +---+
                // | s |             |     | s |         |     | s |          | | s |
                // +---+             |     +---+         |     +---+          | +---+
                //   +----+          | +--+              |       +-+          |      +---+
                //   | n  |          | |n |              |       |n|          |      | n |
                //   +----+          | +--+              |       +-+          |      +---+
                //   4:6             | 2:4               |       7:3          |      7:5
                //                   |                   |                    |
                // 4 < 7 &&          | 2 < 10 &&         | 7 < 10 &&          | 7 < 7      
                // (4 < 2 || 7 < 10) | (2 < 5)           | (7 < 5 || 10 < 10) |
                // overlap           | wrong order       | nested (OK)        | after (OK)
                if (nextSection.Offset < section.Offset + section.Length &&
                   (nextSection.Offset < section.Offset || section.Offset + section.Length < nextSection.Offset + nextSection.Length))
                {
                    throw new InvalidOperationException("Sections are overlapping or incorrectly sorted.");
                }
            }
        }
    }

    /// <summary>
    /// Merges a line into this one.
    /// </summary>
    /// <remarks>
    /// Iterates through the passed-in line's sections tracking this line sections'
    /// ends in a stack to find where to insert them. The stack enumerator reverses
    /// order; a call to Reverse() restores order in a copy to keep track of
    /// traversed sections during the insertion process.
    /// </remarks>
    public void Merge(StyledLine other)
    {
        if (other is null)
        {
            return;
        }
#if DEBUG
        CheckInvariants();

        other.CheckInvariants();
#endif

        var sectionEnds = new Stack<int>();
        
        sectionEnds.Push(Line.EndOffset);

        var i = 0;
        
        foreach (var newSection in other.Sections)
        {
            var newSectionOffset = newSection.Offset;
            
            while (i < Sections.Count)
            {
                var section = Sections[i];

                if (newSection.Offset < section.Offset)
                {
                    break;
                }

                while (section.Offset > sectionEnds.Peek())
                {
                    sectionEnds.Pop();
                }

                sectionEnds.Push(section.Offset + section.Length);
                
                i++;
            }

            var newSectionEnds = new Stack<int>(sectionEnds.Reverse());

            int j;

            for (j = i; j < Sections.Count; j++)
            {
                var section = Sections[j];

                if (newSection.Offset + newSection.Length <= section.Offset)
                {
                    break;
                }

                Insert(ref j, ref newSectionOffset, section.Offset, newSection.Style!, newSectionEnds);

                while (section.Offset > newSectionEnds.Peek())
                {
                    newSectionEnds.Pop();
                }

                newSectionEnds.Push(section.Offset + section.Length);
            }

            Insert(ref j, ref newSectionOffset, newSection.Offset + newSection.Length, newSection.Style!, newSectionEnds);
        }

#if DEBUG
        CheckInvariants();
#endif
    }

    /// <summary>
    /// Insert a new section into this line.
    /// </summary>
    /// <param name="index">Where to insert the section.</param>
    /// <param name="sectionOffset">The section offset within the line.</param>
    /// <param name="sectionEndOffset">The end offset of the section.</param>
    /// <param name="style">The section style.</param>
    /// <param name="sectionEnds">A stack of existing sections' ends.</param>
    void Insert(ref int index, ref int sectionOffset, int sectionEndOffset, Style style, Stack<int> sectionEnds)
    {
        if (sectionOffset >= sectionEndOffset)
        {
            return;
        }

        while (sectionOffset >= sectionEnds.Peek())
        {
            sectionEnds.Pop();
        }
        
        while (sectionEnds.Peek() < sectionEndOffset)
        {
            var end = sectionEnds.Pop();

            if (end > sectionOffset)
            {
                Sections.Insert(index++, new StyledSection
                {
                    Offset = sectionOffset,
                    Length = end - sectionOffset,
                    Style = style
                });

                sectionOffset = end;
            }
        }

        if (sectionEndOffset > sectionOffset)
        {
            Sections.Insert(index++, new StyledSection
            {
                Offset = sectionOffset,
                Length = sectionEndOffset - sectionOffset,
                Style = style
            });
            
            sectionOffset = sectionEndOffset;
        }
    }

    /// <summary>
    /// Writes the styled line to the StyledTextWriter.
    /// </summary>
    internal void WriteTo(StyledTextWriter writer)
    {
        WriteTo(writer, Line.Offset, Line.Offset + Line.Length);
    }

    /// <summary>
    /// Writes a part of the styled line to the StyledTextWriter.
    /// </summary>
    internal void WriteTo(StyledTextWriter writer, int start, int end)
    {
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        var min = Line.Offset;
        var max = min + Line.Length;

        if (start < min || max < start)
        {
            throw new ArgumentOutOfRangeException(nameof(start), $"{start} < {min} or {max} < {start}");
        }

        if (end < start || max < end)
        {
            throw new ArgumentOutOfRangeException(nameof(start), $"{end} < {start} or {max} < {end}");
        }

        ISegment requestedSegment = new SimpleSegment(start, end - start);

        var elements = new List<HtmlElement>();

        for (var i = 0; i < Sections.Count; i++)
        {
            var section = Sections[i];

            if (SimpleSegment.GetOverlap(section, requestedSegment).Length > 0)
            {
                elements.Add(new HtmlElement(section.Offset, i, false, section.Style!));
                elements.Add(new HtmlElement(section.Offset + section.Length, i, true, section.Style!));
            }
        }

        elements.Sort();

        IDocument document = Document;
        
        var oldStart = start;

        foreach (var element in elements)
        {
            var newStart = Math.Min(element.Offset, end);

            if (newStart > start)
            {
                document.WriteTextTo(writer, oldStart, newStart - oldStart);
            }
            
            oldStart = Math.Max(oldStart, newStart);

            if (element.IsEnd)
            {
                writer.EndSpan();
            }
            else
            {
                writer.BeginSpan(element.Style);
            }
        }

        document.WriteTextTo(writer, oldStart, end - oldStart);
    }

    /// <summary>
    /// Produces HTML code for the line, with &lt;span class="colorName"&gt; tags.
    /// </summary>
    public string ToHtml(HtmlOptions options = null!)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        using (var htmlWriter = new HtmlStyledTextWriter(writer, options))
        {
            WriteTo(htmlWriter);
        }

        return writer.ToString();
    }

    /// <summary>
    /// Produces HTML code for a section of the line, with &lt;span class="colorName"&gt; tags.
    /// </summary>
    public string ToHtml(int startOffset, int endOffset, HtmlOptions? options = null)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        
        using (var htmlWriter = new HtmlStyledTextWriter(writer, options!))
        {
            WriteTo(htmlWriter, startOffset, endOffset);
        }

        return writer.ToString();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{GetType().Name} {ToHtml()}]";
    }

    /// <summary>
    /// Creates a <see cref="StyledTextModel"/> that stores the styles of this line.
    /// </summary>
    public StyledTextModel ToStyledTextModel()
    {
        var builder = new StyledTextModel();

        var start = Line.Offset;

        foreach (var section in Sections)
        {
            builder.ApplyStyle(section.Offset - start, section.Length, section.Style!);
        }

        return builder;
    }

    /// <summary>
    /// Creates a <see cref="StyledText"/> that stores the text and styles of this line.
    /// </summary>
    public StyledText ToStyledText()
    {
        return new StyledText(Document.GetText(Line), ToStyledTextModel());
    }
    #endregion
}
