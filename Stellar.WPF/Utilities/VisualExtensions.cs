using System.Windows.Media;
using System.Windows;
using System;
using System.Windows.Controls;

namespace Stellar.WPF.Utilities
{
    public static class VisualExtensions
    {
        #region DPI independence
        private static Matrix TransformFromDevice(Visual visual) => PresentationSource.FromVisual(visual).CompositionTarget.TransformFromDevice;

        private static Matrix TransformToDevice(Visual visual) => PresentationSource.FromVisual(visual).CompositionTarget.TransformToDevice;

        public static Rect TransformToDevice(this Rect rect, Visual visual) => Rect.Transform(rect, TransformToDevice(visual));

        public static Rect TransformFromDevice(this Rect rect, Visual visual) => Rect.Transform(rect, TransformFromDevice(visual));

        public static Size TransformToDevice(this Size size, Visual visual)
        {
            var matrix = TransformToDevice(visual);

            return new Size(size.Width * matrix.M11, size.Height * matrix.M22);
        }

        public static Size TransformFromDevice(this Size size, Visual visual)
        {
            var matrix = TransformFromDevice(visual);

            return new Size(size.Width * matrix.M11, size.Height * matrix.M22);
        }

        public static Point TransformToDevice(this Point point, Visual visual)
        {
            var matrix = TransformToDevice(visual);

            return new Point(point.X * matrix.M11, point.Y * matrix.M22);
        }

        public static Point TransformFromDevice(this Point point, Visual visual)
        {
            var matrix = TransformFromDevice(visual);

            return new Point(point.X * matrix.M11, point.Y * matrix.M22);
        }
        #endregion

        #region proximity
        /// <summary>
        /// Epsilon for determining closeness (proximity, nearness) of two UI elements, granular enough for pixels.
        /// </summary>
        public const double Epsilon = 0.01;

        /// <summary>
        /// Whether two doubles are close based on their difference being lower than <c>Epsilon</c>.
        /// </summary>
        /// <remarks>
        /// Equality comparison required for infinity values.
        /// </remarks>
        public static bool Nears(this double d1, double d2)
        {
            if (d1 == d2)
            {
                return true;
            }

            return Math.Abs(d1 - d2) < Epsilon;
        }

        /// <summary>
        /// Whether two sizes are close.
        /// </summary>
        public static bool Nears(this Size d1, Size d2)
        {
            return d1.Width.Nears(d2.Width) && d1.Height.Nears(d2.Height);
        }

        /// <summary>
        /// Whether two vectors are close.
        /// </summary>
        public static bool Nears(this Vector d1, Vector d2)
        {
            return d1.X.Nears(d2.X) && d1.Y.Nears(d2.Y);
        }

        /// <summary>
        /// Rectify double value to the [minimum, maximum] range.
        /// </summary>
        public static double Rectify(this double value, double minimum, double maximum)
        {
            return Math.Max(Math.Min(value, maximum), minimum);
        }

        /// <summary>
        /// Rectify integer value to the [minimum, maximum] range.
        /// </summary>
        public static int Rectify(this int value, int minimum, int maximum)
        {
            return Math.Max(Math.Min(value, maximum), minimum);
        }
        #endregion

        #region typeface
        /// <summary>
        /// Creates typeface from a framework element.
        /// </summary>
        public static Typeface CreateTypeface(this FrameworkElement element)
        {
            return new Typeface((FontFamily)element.GetValue(TextBlock.FontFamilyProperty),
                                (FontStyle)element.GetValue(TextBlock.FontStyleProperty),
                                (FontWeight)element.GetValue(TextBlock.FontWeightProperty),
                                (FontStretch)element.GetValue(TextBlock.FontStretchProperty));
        }
        #endregion

        #region pixel size
        /// <summary>
        /// Gets the screen pixel size containing the visual, without
        /// taking any transforms into account.
        /// </summary>
        public static Size GetPixelSize(this Visual visual)
        {
            if (visual is null)
            {
                throw new ArgumentNullException(nameof(visual));
            }

            var source = PresentationSource.FromVisual(visual);

            if (source is null)
            {
                return new Size(1, 1);
            }

            var matrix = source.CompositionTarget.TransformFromDevice;

            return new Size(matrix.M11, matrix.M22);
        }

        /// <summary>
        /// Aligns <paramref name="value"/> on the next middle of a pixel.
        /// </summary>
        /// <param name="value">The value that should be aligned</param>
        /// <param name="pixelSize">The size of one pixel.</param>
        public static double AlignToPixelSize(this double value, double pixelSize)
        {
            return pixelSize * (Math.Round((value / pixelSize) + 0.5, MidpointRounding.AwayFromZero) - 0.5);
        }

        /// <summary>
        /// Rounds <paramref name="point"/> to whole number of pixels.
        /// </summary>
        public static Point Round(this Point point, Size pixelSize)
        {
            return new Point(Round(point.X, pixelSize.Width), Round(point.Y, pixelSize.Height));
        }

        /// <summary>
        /// Rounds val to whole number of pixels.
        /// </summary>
        public static Rect Round(this Rect rect, Size pixelSize)
        {
            return new Rect(Round(rect.X, pixelSize.Width), Round(rect.Y, pixelSize.Height),
                            Round(rect.Width, pixelSize.Width), Round(rect.Height, pixelSize.Height));
        }

        /// <summary>
        /// Rounds <paramref name="value"/> to a whole number of pixels.
        /// </summary>
        public static double Round(this double value, double pixelSize)
        {
            return pixelSize * Math.Round(value / pixelSize, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Rounds <paramref name="value"/> to an whole odd number of pixels.
        /// </summary>
        public static double RoundToOdd(this double value, double pixelSize)
        {
            return Round(value - pixelSize, pixelSize * 2) + pixelSize;
        }
        #endregion
    }
}
