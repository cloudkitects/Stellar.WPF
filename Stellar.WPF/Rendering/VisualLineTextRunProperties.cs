using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// A <see cref="TextRunProperties"/> implementation that allows changing the properties 
/// and is usually assigned to a single <see cref="VisualLineElement"/>.
/// </summary>
public class VisualLineTextRunProperties : TextRunProperties, ICloneable
{
    #region fields
    private BaselineAlignment baselineAlignment;
    private Brush background;
    private Brush foreground;
    private CultureInfo cultureInfo;
    private double fontHintingEmSize;
    private double fontRenderingEmSize;
    private NumberSubstitution numberSubstitution;
    private TextDecorationCollection? decorations;
    private TextEffectCollection? effects;
    private TextRunTypographyProperties typographyProperties;
    private Typeface typeface;
    #endregion

    #region constructor
    /// <summary>
    /// Creates a new VisualLineElementTextRunProperties instance that copies its values
    /// from the specified <paramref name="textRunProperties"/>.
    /// For the <see cref="TextDecorations"/> and <see cref="TextEffects"/> collections, deep copies
    /// are created if those collections are not frozen.
    /// </summary>
    public VisualLineTextRunProperties(TextRunProperties textRunProperties)
    {
        if (textRunProperties is null)
        {
            throw new ArgumentNullException(nameof(textRunProperties));
        }

        background = textRunProperties.BackgroundBrush;
        baselineAlignment = textRunProperties.BaselineAlignment;
        cultureInfo = textRunProperties.CultureInfo;
        fontHintingEmSize = textRunProperties.FontHintingEmSize;
        fontRenderingEmSize = textRunProperties.FontRenderingEmSize;
        foreground = textRunProperties.ForegroundBrush;
        typeface = textRunProperties.Typeface;
        decorations = textRunProperties.TextDecorations;

        if (decorations is not null && !decorations.IsFrozen)
        {
            decorations = decorations.Clone();
        }

        effects = textRunProperties.TextEffects;

        if (effects is not null && !effects.IsFrozen)
        {
            effects = effects.Clone();
        }

        typographyProperties = textRunProperties.TypographyProperties;
        numberSubstitution = textRunProperties.NumberSubstitution;
    }
    #endregion

    #region prop getters
    /// <inheritdoc/>
    public override BaselineAlignment BaselineAlignment => baselineAlignment;

    /// <inheritdoc/>
    public override Brush BackgroundBrush => background;

    /// <inheritdoc/>
    public override CultureInfo CultureInfo => cultureInfo;

    /// <inheritdoc/>
    public override double FontHintingEmSize => fontHintingEmSize;

    /// <inheritdoc/>
    public override double FontRenderingEmSize => fontRenderingEmSize;

    /// <inheritdoc/>
    public override Brush ForegroundBrush => foreground;

    /// <inheritdoc/>
    public override NumberSubstitution NumberSubstitution => numberSubstitution;

    /// <inheritdoc/>
    /// <remarks>
    /// The value may be null, frozen or unfrozen. If the latter, it's safe to
    /// assume the instance is only used by this <see cref="VisualLineTextRunProperties"/>
    /// instance and in turn safe to add decorations to it.
    /// </remarks>
    public override TextDecorationCollection TextDecorations => decorations!;

    /// <inheritdoc/>
    /// <remarks>
    /// The value may be null, frozen or unfrozen. If the latter, it's safe to
    /// assume the instance is only used by this <see cref="VisualLineTextRunProperties"/>
    /// instance and in turn safe to add effects to it.
    /// </remarks>
    public override TextEffectCollection TextEffects => effects!;

    /// <inheritdoc/>
    public override Typeface Typeface => typeface;

    /// <inheritdoc/>
    public override TextRunTypographyProperties TypographyProperties => typographyProperties;
    #endregion

    #region prop setters
    /// <summary>
    /// Sets the <see cref="BaselineAlignment"/>.
    /// </summary>
    public void SetBaselineAlignment(BaselineAlignment value)
    {
        baselineAlignment = value;
    }

    /// <summary>
    /// Sets the <see cref="BackgroundBrush"/>.
    /// </summary>
    public void SetBackgroundBrush(Brush value)
	{
        value.WarnIfNotFrozen();

        background = value;
	}

    /// <summary>
    /// Sets the <see cref="CultureInfo"/>.
    /// </summary>
    public void SetCultureInfo(CultureInfo value)
	{
        cultureInfo = value ?? throw new ArgumentNullException(nameof(value));
	}

    /// <summary>
    /// Sets the <see cref="FontHintingEmSize"/>.
    /// </summary>
    public void SetFontHintingEmSize(double value)
	{
		fontHintingEmSize = value;
	}

    /// <summary>
    /// Sets the <see cref="ForegroundBrush"/>.
    /// </summary>
    public void SetForegroundBrush(Brush value)
    {
        value.WarnIfNotFrozen();

        foreground = value;
    }

    /// <summary>
    /// Sets the <see cref="FontRenderingEmSize"/>.
    /// </summary>
    public void SetFontRenderingEmSize(double value)
	{
		fontRenderingEmSize = value;
	}

    /// <summary>
    /// Sets the <see cref="NumberSubstitution"/>.
    /// </summary>
    public void SetNumberSubstitution(NumberSubstitution value)
    {
        numberSubstitution = value;
    }

    /// <summary>
    /// Sets the <see cref="TextDecorations"/>.
    /// </summary>
    public void SetTextDecorations(TextDecorationCollection value)
	{
        value.WarnIfNotFrozen();

		decorations = decorations is null
			? value
			: new TextDecorationCollection(decorations.Union(value));
    }

    /// <summary>
    /// Sets the <see cref="TextEffects"/>.
    /// </summary>
    public void SetTextEffects(TextEffectCollection value)
	{
        value.WarnIfNotFrozen();
        
		effects = value;
	}

    /// <summary>
    /// Sets the <see cref="Typeface"/>.
    /// </summary>
    public void SetTypeface(Typeface value)
    {
        typeface = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Sets the <see cref="TypographyProperties"/>.
    /// </summary>
    public void SetTypographyProperties(TextRunTypographyProperties value)
	{
		typographyProperties = value;
	}

    #endregion

    #region methods
    /// <summary>
    /// Creates a copy of this instance.
    /// </summary>
    public virtual VisualLineTextRunProperties Clone()
    {
        return new VisualLineTextRunProperties(this);
    }

    object ICloneable.Clone()
    {
        return Clone();
    }
    #endregion
}
