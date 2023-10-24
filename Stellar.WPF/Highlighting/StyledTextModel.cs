using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Stellar.WPF.Document;

namespace Stellar.WPF.Highlighting;

/// <summary>
/// A container of text styles at given offsets.
/// </summary>
public sealed class StyledTextModel
{
    #region fields
    private readonly List<int> offsets = new();
    private readonly List<Style> styles = new();
    #endregion

    #region constructors
    /// <summary>
    /// Creates a new instance with a single style for the entire text span.
    /// </summary>
    public StyledTextModel()
    {
        offsets.Add(0);
        styles.Add(new Style());
    }

    /// <summary>
    /// Creates a StyledTextModel from a CONTIGUOUS list of styled sections.
    /// </summary>
    internal StyledTextModel(int[] offsets, Style[] styles)
    {
        Debug.Assert(offsets[0] == 0);

        this.offsets.AddRange(offsets);
		this.styles.AddRange(styles);
    }
    #endregion

    #region methods
    /// <summary>
    /// Get the index of cached style entries by offset.
    /// </summary>
    /// <param name="offset">The offset to look for.</param>
    /// <param name="create">Whether to create a style if non exists at the specified offset.</param>
    /// <remarks>
    /// If the offset is not in the cache, the create flag dictates whether
    /// to create new entries or simply return the index of the style that contains offset.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">offset is negative.</exception>
    private int IndexOf(int offset, bool create = true)
	{
		if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var index = offsets.BinarySearch(offset);

		if (index < 0)
		{
            index = ~index;

			if (create)
			{
				styles.Insert(index, styles[index - 1].Clone());
				offsets.Insert(index, offset);
			}
			else
			{
				index--;
			}
		}

		return index;
	}

	#region Update offsets
	/// <summary>
	/// Updates the start and end offsets of all segments stored in this collection.
	/// </summary>
	/// <param name="e">TextChangeEventArgs instance describing the change to the document.</param>
	public void UpdateOffsets(TextChangeEventArgs e)
	{
        UpdateOffsets((e ?? throw new ArgumentNullException(nameof(e))).ComputeOffset);
	}

	/// <summary>
	/// Updates the start and end offsets of all segments stored in this collection.
	/// </summary>
	/// <param name="changeOffsets">Document changes to the document.</param>
	public void UpdateOffsets(ChangeOffsetCollection changeOffsets)
	{
        UpdateOffsets((changeOffsets ?? throw new ArgumentNullException(nameof(changeOffsets))).ComputeOffset);
	}

	/// <summary>
	/// Updates the start and end offsets of all segments stored in this collection.
	/// </summary>
	/// <param name="change">OffsetChangeMapEntry instance describing the change to the document.</param>
	public void UpdateOffsets(ChangeOffset change)
	{
		UpdateOffsets(change.ComputeOffset);
	}

    private void UpdateOffsets(Func<int, AnchorMovementType, int> compute)
	{
		var read = 1;
		var written = 1;

		while (read < offsets.Count)
		{
			Debug.Assert(written <= read);
			
			var newOffset = compute(offsets[read], AnchorMovementType.Default);

			if (newOffset == offsets[written - 1])
			{
				// the previous segment has 0 length, so it gets overwritten
				styles[written - 1] = styles[read];
			}
			else
			{
				offsets[written] = newOffset;
				styles[written] = styles[read];
				
				written++;
			}

			read++;
		}

		// remove all entries that were not updated
		offsets.RemoveRange(written, offsets.Count - written);
		styles.RemoveRange(written, styles.Count - written);
	}
	#endregion

	/// <summary>
	/// Append an explicit model to this one.
	/// </summary>
	internal void Append(int offset, int[] newOffsets, Style[] newColors)
	{
		Debug.Assert(newOffsets.Length == newColors.Length);
		Debug.Assert(newOffsets[0] == 0);
		
		// remove everything beyond offset
		while (offsets.Count > 0 && offset <= offsets.Last())
		{
			offsets.RemoveAt(offsets.Count - 1);
			styles.RemoveAt(styles.Count - 1);
		}

		// append the new model
		for (int i = 0; i < newOffsets.Length; i++)
		{
			offsets.Add(offset + newOffsets[i]);
			styles.Add(newColors[i]);
		}
	}

	/// <summary>
	/// Get a copy of the Style at the specified offset.
	/// </summary>
	public Style GetStyleAt(int offset)
	{
		return styles[IndexOf(offset, false)].Clone();
	}

	/// <summary>
	/// Applies a non-null, non-empty style to the specified text range.
	/// </summary>
	public void ApplyStyle(int offset, int length, Style style)
	{
        if (style is null || style.IsEmpty)
		{
			return;
		}

		var start = IndexOf(offset);
		var end = IndexOf(offset + length);

		for (var i = start; i < end; i++)
		{
			styles[i].Merge(style);
		}
	}

	/// <summary>
	/// Replaces all styles within the specified text range with
	/// the specified style.
	/// </summary>
	public void ReplaceStyles(int offset, int length, Style style)
	{
		if (length <= 0)
        {
            return;
        }

        var start = IndexOf(offset);
		var end = IndexOf(offset + length);

		styles[start] = style is not null
			? style.Clone()
			: new Style();

        styles.RemoveRange(start + 1, end - (start + 1));
		offsets.RemoveRange(start + 1, end - (start + 1));
	}

	/// <summary>
	/// Set the foreground brush on the specified text range.
	/// </summary>
	public void SetForeground(int offset, int length, Brush brush)
	{
		var start = IndexOf(offset);
		var end = IndexOf(offset + length);

		for (var i = start; i < end; i++)
		{
			styles[i].Foreground = brush;
		}
	}

	/// <summary>
	/// Set the background brush on the specified text range.
	/// </summary>
	public void SetBackground(int offset, int length, Brush brush)
	{
        var start = IndexOf(offset);
        var end = IndexOf(offset + length);

        for (var i = start; i < end; i++)
        {
            styles[i].Background = brush;
        }
    }

    /// <summary>
    /// Sets the font weight on the specified text range.
    /// </summary>
    public void SetFontWeight(int offset, int length, FontWeight weight)
	{
		var start = IndexOf(offset);
		var end = IndexOf(offset + length);
		
		for (var i = start; i < end; i++)
		{
			styles[i].FontWeight = weight;
		}
	}

	/// <summary>
	/// Sets the font style on the specified text range.
	/// </summary>
	public void SetFontStyle(int offset, int length, FontStyle style)
	{
        var start = IndexOf(offset);
        var end = IndexOf(offset + length);

        for (var i = start; i < end; i++)
        {
            styles[i].FontStyle = style;
        }
    }

    /// <summary>
    /// Get the styled sections in the specified range sorted by offset
    /// and without any nesting or overlaps.
    /// </summary>
    public IEnumerable<StyledSection> GetStyledSections(int offset, int length)
	{
		var index = IndexOf(offset, false);
		var start = offset;
		var end = offset + length;

		while (start < end)
		{
            // stop at the next offset if found in the cache
            var stop = Math.Min(end, index + 1 < offsets.Count ? offsets[index + 1] : int.MaxValue);
			
			yield return new StyledSection {
				Offset = start,
				Length = stop - start,
				Style = styles[index].Clone()
			};

			start = stop;
			
			index++;
		}
	}

	/// <summary>
	/// Creates WPF Run instances that can be used for TextBlock.Inlines.
	/// </summary>
	/// <param name="textSource">The text source that holds the text for this model.</param>
	public Run[] CreateRuns(ITextSource textSource)
	{
		var runs = new Run[styles.Count];

		for (var i = 0; i < runs.Length; i++)
		{
			var offset = offsets[i];
			var endOffset = i + 1 < offsets.Count ? offsets[i + 1] : textSource.TextLength;

			var run = new Run(textSource.GetText(offset, endOffset - offset));

			var style = styles[i];
			
			StyledText.ApplyStyle(run, style);
			
			runs[i] = run;
		}

		return runs;
	}
    #endregion
}
