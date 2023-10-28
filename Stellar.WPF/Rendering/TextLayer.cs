using System.Collections.Generic;
using System.Windows.Media;
using System.Windows;

namespace Stellar.WPF.Rendering;

/// <summary>
/// The text container control that allows other UIElements to be placed inside the TextView but
/// behind the text. TextView controls text rendering process--the VisualLine creation; this
/// class displays the visual lines.
/// </summary>
/// <remarks>
/// This class is invisible to hit testing so that UIElements behind the text can react to
/// mouse input.
/// </remarks>
internal sealed class TextLayer : Layer
{
    /// <summary>
    /// the index of the text layer in the layers collection.
    /// </summary>
    internal int index;
    private readonly List<VisualLineDrawingVisual> visuals = new();
    
    public TextLayer(TextView textView) : base(textView, KnownLayer.Text)
    {
    }

    internal void SetVisuals(ICollection<VisualLine> lines)
    {
        foreach (var visual in visuals)
        {
            if (visual.VisualLine.IsDisposed)
            {
                RemoveVisualChild(visual);
            }
        }

        visuals.Clear();
        
        foreach (var line in lines)
        {
            var visual = line.Render();

            if (!visual.IsAdded)
            {
                AddVisualChild(visual);
                
                visual.IsAdded = true;
            }

            visuals.Add(visual);
        }

        InvalidateArrange();
    }

    protected override int VisualChildrenCount => visuals.Count;

    protected override Visual GetVisualChild(int index) => visuals[index];

    protected override void ArrangeCore(Rect finalRect) => textView.ArrangeTextLayer(visuals);
}