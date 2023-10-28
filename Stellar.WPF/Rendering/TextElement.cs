using System;
using System.Collections.Generic;
using System.Windows.Documents;
using System.Windows.Media.TextFormatting;

using Stellar.WPF.Document;

namespace Stellar.WPF.Rendering;

/// <summary>
/// A VisualLineElement derivative representing a piece of text.
/// </summary>
public class TextElement : VisualLineElement
{
    readonly VisualLine parentVisualLine;

    /// <summary>
    /// Gets the parent visual line.
    /// </summary>
    public VisualLine ParentVisualLine => parentVisualLine;

    /// <inheritdoc/>
    public override bool CanSplit => true;
    
	/// <summary>
    /// Creates a visual line text element with the specified length.
    /// It uses the <see cref="ITextRunConstructionContext.VisualLine"/> and its
    /// <see cref="VisualLineElement.RelativeTextOffset"/> to find the actual text string.
    /// </summary>
    public TextElement(VisualLine parentVisualLine, int length) : base(length, length)
	{
        this.parentVisualLine = parentVisualLine ?? throw new ArgumentNullException(nameof(parentVisualLine));
	}

    /// <summary>
    /// Override this method to control the type of new VisualLineText instances when
    /// the visual line is split due to syntax highlighting.
    /// </summary>
    protected virtual TextElement CreateInstance(int length) => new TextElement(parentVisualLine, length);

    /// <inheritdoc/>
    public override TextRun CreateTextRun(int startVisualColumn, ITextRunContext context)
	{
		if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var relativeOffset = startVisualColumn - VisualColumn;

		var text = context.GetText(
			context.VisualLine.FirstLine.Offset + RelativeTextOffset + relativeOffset,
			DocumentLength - relativeOffset);
		
		return new TextCharacters(text.Text, text.Offset, text.Count, TextRunProperties);
	}

	/// <inheritdoc/>
	public override bool IsWhitespace(int visualColumn)
	{
		var offset = visualColumn - VisualColumn + parentVisualLine.FirstLine.Offset + RelativeTextOffset;

		return char.IsWhiteSpace(parentVisualLine.Document.GetCharAt(offset));
	}

	/// <inheritdoc/>
	public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int visualColumnLimit, ITextRunContext context)
	{
		if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var relativeOffset = visualColumnLimit - VisualColumn;
		var text = context.GetText(context.VisualLine.FirstLine.Offset + RelativeTextOffset, relativeOffset);
		var range = new CharacterBufferRange(text.Text, text.Offset, text.Count);
		
		return new TextSpan<CultureSpecificCharacterBufferRange>(range.Length, new CultureSpecificCharacterBufferRange(TextRunProperties!.CultureInfo, range));
	}

    /// <inheritdoc/>
    public override void Split(int splitVisualColumn, IList<VisualLineElement> elements, int index)
	{
		if (splitVisualColumn <= VisualColumn || VisualColumn + VisualLength <= splitVisualColumn)
        {
            throw new ArgumentOutOfRangeException(nameof(splitVisualColumn), splitVisualColumn, $"{splitVisualColumn} <= {VisualColumn} or {VisualColumn} + {VisualLength} <= {splitVisualColumn}");
        }

        if (elements is null)
        {
            throw new ArgumentNullException(nameof(elements));
        }

        if (elements[index] != this)
        {
            throw new ArgumentException($"Invalid element index, this element is not at elements[{index}]");
        }

        var relativeSplitPos = splitVisualColumn - VisualColumn;
		var splitPart = CreateInstance(DocumentLength - relativeSplitPos);

		SplitHelper(this, splitPart, splitVisualColumn, relativeSplitPos + RelativeTextOffset);
		
		elements.Insert(index + 1, splitPart);
	}

    /// <inheritdoc/>
    public override int GetRelativeOffset(int visualColumn) => RelativeTextOffset + visualColumn - VisualColumn;

    /// <inheritdoc/>
    public override int GetVisualColumn(int relativeTextOffset) => VisualColumn + relativeTextOffset - RelativeTextOffset;

    /// <inheritdoc/>
    public override int GetNextCaretPosition(int visualColumn, LogicalDirection direction, CaretPositioningMode mode)
	{
		var textOffset = parentVisualLine.StartOffset + RelativeTextOffset;
		var pos = parentVisualLine.Document.GetNextCaretPosition(textOffset + visualColumn - VisualColumn, direction, mode);

        return pos < textOffset || textOffset + DocumentLength < pos
            ? -1
            : VisualColumn + pos - textOffset;
    }
}
