using Stellar.WPF.Utilities;
using System;
using System.Drawing;
using System.Windows.Media;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Renders a ruler at a certain column.
/// </summary>
internal sealed class ColumnRulerRenderer : IBackgroundRenderer
{
    private System.Windows.Media.Pen pen;
    private int column;
    private readonly TextView textView;

    public static readonly System.Windows.Media.Color DefaultForeground = Colors.LightGray;

    public ColumnRulerRenderer(TextView textView)
    {
        this.textView = textView ?? throw new ArgumentNullException(nameof(textView));
        this.textView.BackgroundRenderers.Add(this);

        pen = new System.Windows.Media.Pen(new SolidColorBrush(DefaultForeground), 1);
        pen.Freeze();
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void SetRuler(int column, System.Windows.Media.Pen pen)
    {
        if (this.column != column)
        {
            this.column = column;
        }
        
        if (this.pen != pen)
        {
            this.pen = pen;
        }
        
        textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (column < 1)
        {
            return;
        }

        var offset = textView.WideSpaceWidth * column;
        var pixelSize = textView.GetPixelSize();

        var x = offset.AlignToPixelSize(pixelSize.Width) - textView.ScrollOffset.X;
        
        var p0 = new System.Windows.Point(x, 0);
        var p1 = new System.Windows.Point(x, Math.Max(textView.DocumentHeight, textView.ActualHeight));

        drawingContext.DrawLine(pen, p0, p1);
    }
}
