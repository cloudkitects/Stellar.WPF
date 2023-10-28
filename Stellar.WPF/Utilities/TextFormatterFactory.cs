using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media.TextFormatting;
using System.Windows.Media;
using System.Windows;

namespace Stellar.WPF.Utilities;

/// <summary>
/// Creates TextFormatter instances that with the correct TextFormattingMode, if running on .NET 4.0.
/// </summary>
static class TextFormatterFactory
{
    /// <summary>
    /// Creates a <see cref="TextFormatter"/> using the formatting mode used by the specified owner object.
    /// </summary>
    public static TextFormatter Create(DependencyObject owner)
    {
        return TextFormatter.Create(TextOptions.GetTextFormattingMode(owner ?? throw new ArgumentNullException(nameof(owner))));
    }

    /// <summary>
    /// Returns whether the specified dependency property affects the text formatter creation.
    /// Controls should re-create their text formatter for such property changes.
    /// </summary>
    public static bool PropertyChangeAffectsTextFormatter(DependencyProperty dp)
    {
        return dp == TextOptions.TextFormattingModeProperty;
    }

    /// <summary>
    /// Creates formatted text.
    /// </summary>
    /// <param name="element">The owner element. The text formatter setting are read from this element.</param>
    /// <param name="text">The text.</param>
    /// <param name="typeface">The typeface to use. If this parameter is null, the typeface of the <paramref name="element"/> will be used.</param>
    /// <param name="emSize">The font size. If this parameter is null, the font size of the <paramref name="element"/> will be used.</param>
    /// <param name="foreground">The foreground color. If this parameter is null, the foreground of the <paramref name="element"/> will be used.</param>
    /// <returns>A FormattedText object using the specified settings.</returns>
    public static FormattedText CreateFormattedText(FrameworkElement element, string text, Typeface typeface, double? emSize, Brush foreground)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        typeface ??= element.CreateTypeface();

        emSize ??= TextBlock.GetFontSize(element);
        foreground ??= TextBlock.GetForeground(element);
        
        return new FormattedText(
            text ?? throw new ArgumentNullException(nameof(text)),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            emSize.Value,
            foreground,
            null,
            TextOptions.GetTextFormattingMode(element),
            VisualTreeHelper.GetDpi(element).PixelsPerDip
        );
    }
}
