using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Documents;

using Stellar.WPF.Document;

namespace Stellar.WPF.Styling;

/// <summary>
/// An immutable text segment with multiple styling information at different offsets.
/// </summary>
public class StyledText
{
    #region fields and props
    /// <summary>
    /// The empty string without any formatting information.
    /// </summary>
    public static readonly StyledText Empty = new(string.Empty);

	readonly string text;
	internal readonly int[] offsets;
	internal readonly Style[] styles;

    /// <summary>
    /// The text.
    /// </summary>
    public string Text => text;

    /// <summary>
    /// The text length.
    /// </summary>
    public int Length => text.Length;
    #endregion

    #region constructors
    /// <summary>
    /// Creates a StyledText instance with the given text and StyledTextModel.
    /// </summary>
    /// <param name="text">
    /// The text to use in this StyledText instance.
    /// </param>
    /// <param name="model">
    /// The model that contains the formatting to use for this StyledText instance.
    /// <c>model.DocumentLength</c> should correspond to <c>text.Length</c>.
    /// This parameter may be null, in which case the StyledText instance just holds plain text.
    /// </param>
    public StyledText(string text, StyledTextModel? model = null)
	{
		this.text = text ?? throw new ArgumentNullException(nameof(text));
		
		if (model is not null)
		{
			var sections = model.GetStyledSections(0, text.Length).ToArray();
			
			offsets = new int[sections.Length];
			styles = new Style[sections.Length];
			
			for (var i = 0; i < sections.Length; i++)
			{
				offsets[i] = sections[i].Offset;
				styles[i] = sections[i].Style!;
			}
		}
		else
		{
			offsets = new int[] { 0 };
			styles = new Style[] { Style.Empty };
		}
	}

	internal StyledText(string text, int[] offsets, Style[] styles)
	{
        Debug.Assert(offsets[0] == 0);
        Debug.Assert(offsets.Last() <= text.Length);
        
		this.text = text;
		this.offsets = offsets;
		this.styles = styles;
	}
    #endregion

    #region methods
    /// <summary>
    /// Get the index of cached entries by offset.
    /// </summary>
    /// <param name="offset">The offset to look for.</param>
    /// <returns>
    /// The index of the entry that contains the offset.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">offset is negative.</exception>
    int IndexOf(int offset)
	{
		if (offset < 0 || text.Length < offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var index = Array.BinarySearch(offsets, offset);

		if (index < 0)
		{
			index = ~index - 1;
		}
		
		return index;
	}

    /// <summary>
    /// Gets the Style for the specified offset.
    /// </summary>
    public Style GetStyleAt(int offset) => styles[IndexOf(offset)];

    /// <summary>
    /// Get the styled sections in the specified range sorted by offset
    /// and without any nesting or overlaps.
    /// </summary>
    public IEnumerable<StyledSection> GetStyledSections(int offset, int length)
    {
        var index = IndexOf(offset);
        var start = offset;
        var end = offset + length;

        while (start < end)
        {
            // stop at the next offset if found in the cache
            var stop = Math.Min(end, index + 1 < offsets.Length ? offsets[index + 1] : text.Length);

            yield return new StyledSection
            {
                Offset = start,
                Length = stop - start,
                Style = styles[index]
            };

            start = stop;

            index++;
        }
    }

	/// <summary>
	/// Creates a new StyledTextModel with the formatting from this StyledText.
	/// </summary>
	public StyledTextModel ToStyledTextModel()
	{
		return new StyledTextModel(offsets, styles.Select(style => style.Clone()).ToArray());
	}

	/// <summary>
	/// Gets the text.
	/// </summary>
	public override string ToString()
	{
		return text;
	}

    /// <summary>
    /// Creates WPF Run instances that can be used for TextBlock.Inlines.
    /// </summary>
    public Run[] CreateRuns()
    {
        var runs = new Run[styles.Length];

        for (var i = 0; i < runs.Length; i++)
        {
            var offset = offsets[i];
            var endOffset = i + 1 < offsets.Length ? offsets[i + 1] : text.Length;

            var run = new Run(text[offset..endOffset]);
            var style = styles[i];

            ApplyStyle(run, style);

            runs[i] = run;
        }

        return runs;
    }

    internal static void ApplyStyle(TextElement r, Style style)
	{
		if (style.Foreground is not null)
        {
            r.Foreground = style.Foreground.GetBrush(null!);
        }

        if (style.Background is not null)
        {
            r.Background = style.Background.GetBrush(null!);
        }

        if (style.FontWeight is not null)
        {
            r.FontWeight = style.FontWeight.Value;
        }

        if (style.FontStyle is not null)
        {
            r.FontStyle = style.FontStyle.Value;
        }
    }

    /*
    /// <summary>
    /// Produces HTML code for the line, with &lt;span style="..."&gt; tags.
    /// </summary>
    public string ToHtml(HtmlOptions options = null)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
		
        using (var htmlWriter = new HtmlStyledTextWriter(writer, options))
        {
            htmlWriter.Write(this);
        }

        return writer.ToString();
    }

    /// <summary>
    /// Produces HTML code for a section of the line, with &lt;span style="..."&gt; tags.
    /// </summary>
    public string ToHtml(int offset, int length, HtmlOptions options = null)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        using (var htmlWriter = new HtmlStyledTextWriter(writer, options))
        {
            htmlWriter.Write(this, offset, length);
        }

        return writer.ToString();
    }
    */

	/// <summary>
	/// Creates a substring of the text.
	/// </summary>
	public StyledText Substring(int offset, int length)
	{
		if (offset == 0 && length == Length)
        {
            return this;
        }

        var substring = text.Substring(offset, length);
		var model = ToStyledTextModel();
        
		var changeOffsets = new ChangeOffsetCollection(2)
        {
            new ChangeOffset(offset + length, text.Length - offset - length, 0),
            new ChangeOffset(0, offset, 0)
        };

        model.UpdateOffsets(changeOffsets);
		
		return new StyledText(substring, model);
	}

	/// <summary>
	/// Concatenates the specified styled texts.
	/// </summary>
	public static StyledText Concat(params StyledText[] texts)
	{
		if (texts is null || texts.Length == 0)
        {
            return Empty;
        }
        
		if (texts.Length == 1)
        {
            return texts[0];
        }

        var newText = string.Concat(texts.Select(txt => txt.text));
		var model = texts[0].ToStyledTextModel();
		var offset = texts[0].Length;

		for (var i = 1; i < texts.Length; i++)
		{
			model.Append(offset, texts[i].offsets, texts[i].styles);
			offset += texts[i].Length;
		}

		return new StyledText(newText, model);
	}
    #endregion

    #region operators
    /// <summary>
    /// Concatenates the specified styled texts.
    /// </summary>
    public static StyledText operator +(StyledText a, StyledText b)
	{
		return Concat(a, b);
	}

	/// <summary>
	/// Implicit conversion from string to StyledText.
	/// </summary>
	public static implicit operator StyledText(string text)
	{
        return text is null
			? null!
			: new StyledText(text);
    }
    #endregion
}
