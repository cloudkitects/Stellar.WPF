using System.Windows.Media.TextFormatting;
using System.Windows.Media;
using System.Windows;

namespace Stellar.WPF.Rendering;

sealed class GlobalTextRunProperties : TextRunProperties
{
    internal Typeface? typeface;
    internal double fontRenderingEmSize;
    internal Brush? foregroundBrush;
    internal Brush? backgroundBrush;
    internal System.Globalization.CultureInfo? cultureInfo;

    public override Typeface Typeface => typeface!;
    public override double FontRenderingEmSize => fontRenderingEmSize;
    public override double FontHintingEmSize => fontRenderingEmSize;
    public override TextDecorationCollection TextDecorations => null!;
    public override Brush ForegroundBrush => foregroundBrush!;
    public override Brush BackgroundBrush => backgroundBrush!;
    public override System.Globalization.CultureInfo CultureInfo => cultureInfo!;
    public override TextEffectCollection TextEffects => null!;
}