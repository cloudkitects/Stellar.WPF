using System.Windows.Media;

namespace Stellar.WPF.Rendering;

/// <summary>
/// A contract for renderers drawing in the background of a known layer.
/// Used to draw non-interactive elements on the TextView without
/// introducing new UIElements.
/// </summary>
public interface IBackgroundRenderer
{
    /// <summary>
    /// The layer on which this background renderer should draw.
    /// </summary>
    KnownLayer Layer { get; }

    /// <summary>
    /// The rendering action.
    /// </summary>
    void Draw(TextView textView, DrawingContext drawingContext);
}
