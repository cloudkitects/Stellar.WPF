using System;
using System.Windows.Media.TextFormatting;
using System.Windows.Media;
using System.Windows;

namespace Stellar.WPF.Rendering;

/// <summary>
/// This is the TextRun implementation used by the <see cref="FormattedTextElement"/> class.
/// </summary>
public class FormattedTextRun : TextEmbeddedObject
{
    readonly FormattedTextElement element;
    TextRunProperties properties;

    /// <summary>
    /// Creates a new FormattedTextRun.
    /// </summary>
    public FormattedTextRun(FormattedTextElement element, TextRunProperties properties)
    {
        this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
        this.element = element ?? throw new ArgumentNullException(nameof(element));
    }

    /// <summary>
    /// Gets the element for which the FormattedTextRun was created.
    /// </summary>
    public FormattedTextElement Element => element;

    /// <inheritdoc/>
    public override LineBreakCondition BreakBefore => element.BreakBefore;

    /// <inheritdoc/>
    public override LineBreakCondition BreakAfter => element.BreakAfter;

    /// <inheritdoc/>
    public override bool HasFixedSize => true;

    /// <inheritdoc/>
    public override CharacterBufferReference CharacterBufferReference => new CharacterBufferReference();

    /// <inheritdoc/>
    public override int Length => element.VisualLength;

    /// <inheritdoc/>
    public override TextRunProperties Properties => properties;

    /// <inheritdoc/>
    public override TextEmbeddedObjectMetrics Format(double remainingParagraphWidth)
    {
        var formattedText = element.formattedText;

        if (formattedText is not null)
        {
            return new TextEmbeddedObjectMetrics(formattedText.WidthIncludingTrailingWhitespace,
                                                 formattedText.Height,
                                                 formattedText.Baseline);
        }

        var text = element.textLine;

        return new TextEmbeddedObjectMetrics(text.WidthIncludingTrailingWhitespace,
                                                text.Height,
                                                text.Baseline);
    }

    /// <inheritdoc/>
    public override Rect ComputeBoundingBox(bool rightToLeft, bool sideways)
    {
        var formattedText = element.formattedText;

        if (formattedText is not null)
        {
            return new Rect(0, 0, formattedText.WidthIncludingTrailingWhitespace, formattedText.Height);
        }

        var text = element.textLine;

        return new Rect(0, 0, text.WidthIncludingTrailingWhitespace, text.Height);
    }

    /// <inheritdoc/>
    public override void Draw(DrawingContext drawingContext, Point origin, bool rightToLeft, bool sideways)
    {
        if (element.formattedText is not null)
        {
            origin.Y -= element.formattedText.Baseline;
            drawingContext.DrawText(element.formattedText, origin);
        }
        
        origin.Y -= element.textLine.Baseline;
        
        element.textLine.Draw(drawingContext, origin, InvertAxes.None);
    }
}
