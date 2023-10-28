using System.Windows.Media.TextFormatting;
using System.Windows.Media;
using System.Windows;

namespace Stellar.WPF.Rendering;

internal sealed class VisualLineDrawingVisual : DrawingVisual
{
    public readonly VisualLine VisualLine;
    public readonly double Height;
    internal bool IsAdded;

    public VisualLineDrawingVisual(VisualLine visualLine, FlowDirection flow)
    {
        VisualLine = visualLine;

        var drawingContext = RenderOpen();
        double pos = 0;

        foreach (var line in visualLine.TextLines)
        {
            if (flow == FlowDirection.LeftToRight)
            {
                line.Draw(drawingContext, new Point(0, pos), InvertAxes.None);
            }
            else
            {
                // invert axis for Arabic language support
                line.Draw(drawingContext, new Point(0, pos), InvertAxes.Horizontal);
            }

            pos += line.Height;
        }

        Height = pos;

        drawingContext.Close();
    }

    protected override GeometryHitTestResult HitTestCore(GeometryHitTestParameters hitTestParameters) => null!;

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) => null!;
}
