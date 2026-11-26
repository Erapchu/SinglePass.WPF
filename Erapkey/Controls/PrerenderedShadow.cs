using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Erapkey.Controls
{
    /// <summary>
    /// Draws a pre-blurred shadow for an 8-DIP rounded rectangle.
    /// Place it behind the surface and use Opacity and RenderTransform to position it.
    /// </summary>
    public sealed class PrerenderedShadow : FrameworkElement
    {
        // The 2x asset has a one-pixel center strip between two 80-pixel caps.
        private const int CapPixels = 80;
        private const double Spread = 22;
        private const double InnerCap = CapPixels / 2.0 - Spread;
        private static readonly Lazy<BitmapSource[,]> Slices = new(LoadSlices);

        public static readonly DependencyProperty ShadowBoundsProperty = DependencyProperty.Register(
            nameof(ShadowBounds), typeof(Rect), typeof(PrerenderedShadow),
            new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Overrides the rectangle casting the shadow without affecting layout.
        /// An empty rectangle uses the control's arranged size.
        /// </summary>
        public Rect ShadowBounds
        {
            get => (Rect)GetValue(ShadowBoundsProperty);
            set => SetValue(ShadowBoundsProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var bounds = ShadowBounds;
            if (bounds.IsEmpty)
                bounds = new Rect(RenderSize);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var insetX = Math.Min(InnerCap, bounds.Width / 2);
            var insetY = Math.Min(InnerCap, bounds.Height / 2);
            Span<double> x = stackalloc double[] { bounds.Left - Spread, bounds.Left + insetX, bounds.Right - insetX, bounds.Right + Spread };
            Span<double> y = stackalloc double[] { bounds.Top - Spread, bounds.Top + insetY, bounds.Bottom - insetY, bounds.Bottom + Spread };
            var slices = Slices.Value;
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var width = x[column + 1] - x[column];
                    var height = y[row + 1] - y[row];
                    if (width > 0 && height > 0)
                        drawingContext.DrawImage(slices[row, column], new Rect(x[column], y[row], width, height));
                }
            }
        }

        private static BitmapSource[,] LoadSlices()
        {
            var uri = new Uri("pack://application:,,,/Erapkey;component/Resources/CardShadow.png", UriKind.Absolute);
            var source = new BitmapImage(uri);
            source.Freeze();

            int[] offsets = [0, CapPixels, CapPixels + 1];
            int[] lengths = [CapPixels, 1, CapPixels];
            var slices = new BitmapSource[3, 3];
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var crop = new Int32Rect(offsets[column], offsets[row], lengths[column], lengths[row]);
                    var slice = new CroppedBitmap(source, crop);
                    slice.Freeze();
                    slices[row, column] = slice;
                }
            }

            return slices;
        }
    }
}
