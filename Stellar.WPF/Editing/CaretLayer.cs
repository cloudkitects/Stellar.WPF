using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using Stellar.WPF.Rendering;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Editing
{
    internal sealed class CaretLayer : Layer
    {
        private TextArea textArea;
        private bool isVisible;
        private Rect caretRectangle;
        private readonly DispatcherTimer caretBlinkTimer = new();
        private bool blink;

        public CaretLayer(TextArea textArea) : base(textArea.TextView, KnownLayer.Caret)
        {
            this.textArea = textArea;
            IsHitTestVisible = false;
            caretBlinkTimer.Tick += new EventHandler(CaretBlinkTimer_Tick);
        }

        private void CaretBlinkTimer_Tick(object? sender, EventArgs e)
        {
            blink = !blink;

            InvalidateVisual();
        }

        public void Show(Rect rect)
        {
            caretRectangle = rect;
            isVisible = true;

            StartBlinkAnimation();
            
            InvalidateVisual();
        }

        public void Hide()
        {
            if (isVisible)
            {
                isVisible = false;

                StopBlinkAnimation();
                
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Enable caret blinking.
        /// </summary>
        /// <remarks>
        /// the caret should visible initially; the system reports a negative blink time if blinking is disabled
        /// </remarks>
        private void StartBlinkAnimation()
        {
            var blinkTime = Win32.CaretBlinkTime;
            
            blink = true;
            
            if (blinkTime.TotalMilliseconds > 0)
            {
                caretBlinkTimer.Interval = blinkTime;
                
                caretBlinkTimer.Start();
            }
        }

        private void StopBlinkAnimation()
        {
            caretBlinkTimer.Stop();
        }

        internal Brush CaretBrush;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (isVisible && blink)
            {
                var brush = CaretBrush;

                brush ??= (Brush)textView.GetValue(TextBlock.ForegroundProperty);

                if (textArea.OverstrikeMode)
                {
                    if (brush is SolidColorBrush scBrush)
                    {
                        var color = scBrush.Color;
                        var newColor = Color.FromArgb(100, color.R, color.G, color.B);
                        
                        brush = new SolidColorBrush(newColor);
                        brush.Freeze();
                    }
                }

                var r = new Rect(caretRectangle.X - textView.HorizontalOffset,
                                  caretRectangle.Y - textView.VerticalOffset,
                                  caretRectangle.Width,
                                  caretRectangle.Height);

                drawingContext.DrawRectangle(brush, null, r.Round(this.GetPixelSize()));
            }
        }
    }
}
