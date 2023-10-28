using System;
using System.Windows.Media;
using System.Windows;

namespace Stellar.WPF.Rendering;

sealed class CurrentLineHighlightRenderer : IBackgroundRenderer
{
    #region fields and props
    int line;
    readonly TextView textView;

    public static readonly Color DefaultBackground = Color.FromArgb(22, 20, 220, 224);
    public static readonly Color DefaultBorder = Color.FromArgb(52, 0, 255, 110);

    public int Line
    {
        get => line;
        set
        {
            if (line != value)
            {
                line = value;

                textView.InvalidateLayer(Layer);
            }
        }
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public Brush BackgroundBrush
    {
        get; set;
    }

    public Pen BorderPen
    {
        get; set;
    }

    #endregion

    public CurrentLineHighlightRenderer(TextView textView)
    {
        BorderPen = new Pen(new SolidColorBrush(DefaultBorder), 1);
        BorderPen.Freeze();

        BackgroundBrush = new SolidColorBrush(DefaultBackground);
        BackgroundBrush.Freeze();

        this.textView = textView ?? throw new ArgumentNullException(nameof(textView));
        this.textView.BackgroundRenderers.Add(this);

        line = 0;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!this.textView.Options.HighlightCurrentLine)
        {
            return;
        }

        var builder = new BackgroundGeometryBuilder();

        var line = this.textView.GetVisualLine(this.line);
        
        if (line is null)
        {
            return;
        }

        var y = line.VisualTop - this.textView.ScrollOffset.Y;

        builder.AddRectangle(textView, new Rect(0, y, textView.ActualWidth, line.Height));

        var geometry = builder.CreateGeometry();
        
        if (geometry is not null)
        {
            drawingContext.DrawGeometry(BackgroundBrush, BorderPen, geometry);
        }
    }
}
