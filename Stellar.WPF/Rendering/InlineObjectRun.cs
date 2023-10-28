using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace Stellar.WPF.Rendering;

/// <summary>
/// An inline UIElement in the document.
/// </summary>
public class InlineObjectElement : VisualLineElement
{
    /// <summary>
    /// The inline element displayed.
    /// </summary>
    public UIElement Element { get; private set; }

    /// <summary>
    /// Creates a new InlineObjectElement.
    /// </summary>
    /// <param name="documentLength">The length of the element in the document. Must be non-negative.</param>
    /// <param name="element">The element to display.</param>
    public InlineObjectElement(int documentLength, UIElement element)
        : base(1, documentLength)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
    }

    /// <inheritdoc/>
    public override TextRun CreateTextRun(int startVisualColumn, ITextRunContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return new InlineObjectRun(1, TextRunProperties!, Element);
    }
}

/// <summary>
/// A text run with an embedded UIElement.
/// </summary>
public class InlineObjectRun : TextEmbeddedObject
{
    #region fields and props
    readonly UIElement element;
    readonly int length;
    readonly TextRunProperties properties;
    internal Size desiredSize;

    /// <summary>
    /// Gets the element displayed by the InlineObjectRun.
    /// </summary>
    public UIElement Element => element;

    /// <summary>
    /// Gets the VisualLine that contains this object. This property is only available after the object
    /// was added to the text view.
    /// </summary>
    public VisualLine? VisualLine { get; internal set; }

    /// <inheritdoc/>
    public override LineBreakCondition BreakBefore => LineBreakCondition.BreakDesired;

    /// <inheritdoc/>
    public override LineBreakCondition BreakAfter => LineBreakCondition.BreakDesired;

    /// <inheritdoc/>
    public override bool HasFixedSize => true;

    /// <inheritdoc/>
    public override CharacterBufferReference CharacterBufferReference => new CharacterBufferReference();

    /// <inheritdoc/>
    public override int Length => length;

    /// <inheritdoc/>
    public override TextRunProperties Properties => properties;
    #endregion

    /// <summary>
    /// Creates a new InlineObjectRun instance.
    /// </summary>
    /// <param name="length">The length of the TextRun.</param>
    /// <param name="properties">The <see cref="TextRunProperties"/> to use.</param>
    /// <param name="element">The <see cref="UIElement"/> to display.</param>
    public InlineObjectRun(int length, TextRunProperties properties, UIElement element)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException($"{length} <= 0");
        }

        this.length = length;
        this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
        this.element = element ?? throw new ArgumentNullException(nameof(element));
    }


    /// <inheritdoc/>
    public override TextEmbeddedObjectMetrics Format(double remainingParagraphWidth)
    {
        var baseline = TextBlock.GetBaselineOffset(element);

        if (double.IsNaN(baseline))
        {
            baseline = desiredSize.Height;
        }

        return new TextEmbeddedObjectMetrics(desiredSize.Width, desiredSize.Height, baseline);
    }

    /// <inheritdoc/>
    public override Rect ComputeBoundingBox(bool rightToLeft, bool sideways)
    {
        if (element.IsArrangeValid)
        {
            var baseline = TextBlock.GetBaselineOffset(element);

            if (double.IsNaN(baseline))
            {
                baseline = desiredSize.Height;
            }

            return new Rect(new Point(0, -baseline), desiredSize);
        }
        
        return Rect.Empty;
    }

    /// <inheritdoc/>
    public override void Draw(DrawingContext drawingContext, Point origin, bool rightToLeft, bool sideways)
    {
    }
}
