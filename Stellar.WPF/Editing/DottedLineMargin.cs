using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Stellar.WPF.Editing;

/// <summary>
/// A vertical dotted line separating line numbers from the text view.
/// </summary>
public static class DottedLineMargin
{
    static readonly object tag = new();

    /// <summary>
    /// Creates a vertical dotted line to separate the line numbers from the text view.
    /// </summary>
    public static UIElement Create() => new Line
    {
        X1 = 0,
        Y1 = 0,
        X2 = 0,
        Y2 = 1,
        StrokeDashArray = { 0, 2 },
        Stretch = Stretch.Fill,
        StrokeThickness = 1,
        StrokeDashCap = PenLineCap.Round,
        Margin = new Thickness(2, 0, 2, 0),
        Tag = tag
    };

    /// <summary>
    /// Whether the specified UIElement is the result of a Create call.
    /// </summary>
    public static bool IsDottedLineMargin(UIElement element) => element is Line line && line.Tag == tag;
}
