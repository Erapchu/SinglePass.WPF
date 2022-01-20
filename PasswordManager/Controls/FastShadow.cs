using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PasswordManager.Controls
{
    /// <summary>
    /// Emulates the System.Windows.Media.Effects.DropShadowEffect using
    /// rectangles and gradients, which performs a million times better
    /// and won't randomly crash a good percentage of your end-user's 
    /// video drivers.
    /// </summary>
    public class FastShadow : Decorator
    {
        #region Dynamic Properties

        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register(
                "Color",
                typeof(Color),
                typeof(FastShadow),
                new FrameworkPropertyMetadata(
                    Color.FromArgb(0x71, 0x00, 0x00, 0x00),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// The Color property defines the Color used to fill the shadow region. 
        /// </summary> 
        [Category("Common Properties")]
        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Distance from centre, why MS don't call this "distance" beats
        /// me.. Kept same as other Effects for consistency.
        /// </summary>
        [Category("Common Properties"), Description("Distance from centre")]
        public double ShadowDepth
        {
            get => (double)GetValue(ShadowDepthProperty);
            set => SetValue(ShadowDepthProperty, value);
        }

        // Using a DependencyProperty as the backing store for ShadowDepth.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ShadowDepthProperty =
            DependencyProperty.Register(
                "ShadowDepth",
                typeof(double),
                typeof(FastShadow),
                new FrameworkPropertyMetadata(
                    5.0,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    new PropertyChangedCallback((o, e) =>
                    {
                        FastShadow f = o as FastShadow;
                        if ((double)e.NewValue < 0)
                            f.ShadowDepth = 0;
                    })));


        /// <summary>
        /// Size of the shadow
        /// </summary>
        [Category("Common Properties"), Description("Size of the drop shadow")]
        public double BlurRadius
        {
            get => (double)GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }

        // Using a DependencyProperty as the backing store for BlurRadius.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BlurRadiusProperty =
            DependencyProperty.Register(
                "BlurRadius",
                typeof(double),
                typeof(FastShadow),
                new FrameworkPropertyMetadata(
                    10.0,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    new PropertyChangedCallback((o, e) =>
                    {
                        FastShadow f = o as FastShadow;
                        if ((double)e.NewValue < 0)
                            f.BlurRadius = 0;
                    })));


        /// <summary>
        /// Angle of the shadow
        /// </summary>
        [Category("Common Properties"), Description("Angle of the shadow")]
        public int Direction
        {
            get => (int)GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        // Using a DependencyProperty as the backing store for Direction.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(
                "Direction",
                typeof(int),
                typeof(FastShadow),
                new FrameworkPropertyMetadata(
                    315,
                    FrameworkPropertyMetadataOptions.AffectsRender));


        #endregion Dynamic Properties

        #region Protected Methods

        protected override void OnRender(DrawingContext drawingContext)
        {
            double distance = Math.Max(0, ShadowDepth);
            double blurRadius = Math.Max(BlurRadius, 0);
            double angle = Direction + 45; // Make it behave the same as DropShadowEffect

            Rect shadowBounds = new Rect(new Point(0, 0),
                             new Size(RenderSize.Width, RenderSize.Height));

            shadowBounds.Inflate(blurRadius, blurRadius);

            Color color = Color;

            // Transform angle for "Direction"
            double angleRad = angle * Math.PI / 180.0;
            double xDispl = distance;
            double yDispl = distance;
            double newX = xDispl * Math.Cos(angleRad) - yDispl * Math.Sin(angleRad);
            double newY = yDispl * Math.Cos(angleRad) + xDispl * Math.Sin(angleRad);

            TranslateTransform translate = new TranslateTransform(newX, newY);
            Rect transformed = translate.TransformBounds(shadowBounds);

            // Hint: you can make the blur radius consume more "centre"
            //       region of the bounding box by doubling this here
            // blurRadius = blurRadius * 2;

            // Build a set of rectangles for the shadow box
            Rect[] edges = new Rect[] {
                new Rect(new Point(transformed.X,transformed.Y), new Size(blurRadius,blurRadius)), // TL
                new Rect(new Point(transformed.X+blurRadius,transformed.Y), new Size(Math.Max(transformed.Width-(blurRadius*2),0),blurRadius)), // T
                new Rect(new Point(transformed.Right-blurRadius,transformed.Y), new Size(blurRadius,blurRadius)), // TR
                new Rect(new Point(transformed.Right-blurRadius,transformed.Y+blurRadius), new Size(blurRadius,Math.Max(transformed.Height-(blurRadius*2),0))), // R
                new Rect(new Point(transformed.Right-blurRadius,transformed.Bottom-blurRadius), new Size(blurRadius,blurRadius)), // BR
                new Rect(new Point(transformed.X+blurRadius,transformed.Bottom-blurRadius), new Size(Math.Max(transformed.Width-(blurRadius*2),0),blurRadius)), // B
                new Rect(new Point(transformed.X,transformed.Bottom-blurRadius), new Size(blurRadius,blurRadius)), // BL
                new Rect(new Point(transformed.X,transformed.Y+blurRadius), new Size(blurRadius,Math.Max(transformed.Height-(blurRadius*2),0))), // L
                new Rect(new Point(transformed.X+blurRadius,transformed.Y+blurRadius), new Size(Math.Max(transformed.Width-(blurRadius*2),0),Math.Max(transformed.Height-(blurRadius*2),0))), // C
            };

            // Gradient stops look a lot prettier than
            // a perfectly linear gradient..
            GradientStopCollection gsc = new GradientStopCollection();
            Color stopColor = color;
            stopColor.A = (byte)(color.A);
            gsc.Add(new GradientStop(color, 0.0));
            stopColor.A = (byte)(.74336 * color.A);
            gsc.Add(new GradientStop(stopColor, 0.1));
            stopColor.A = (byte)(.38053 * color.A);
            gsc.Add(new GradientStop(stopColor, 0.3));
            stopColor.A = (byte)(.12389 * color.A);
            gsc.Add(new GradientStop(stopColor, 0.5));
            stopColor.A = (byte)(.02654 * color.A);
            gsc.Add(new GradientStop(stopColor, 0.7));
            stopColor.A = (byte)(0);
            gsc.Add(new GradientStop(stopColor, 0.9));

            gsc.Freeze();

            Brush[] colors = new Brush[]{
                // TL
                new RadialGradientBrush(gsc){ Center = new Point(1, 1), GradientOrigin = new Point(1, 1), RadiusX=1, RadiusY=1},
                // T
                new LinearGradientBrush(gsc, 0){ StartPoint = new Point(0,1), EndPoint=new Point(0,0)},
                // TR
                new RadialGradientBrush(gsc){ Center = new Point(0, 1), GradientOrigin = new Point(0, 1), RadiusX=1, RadiusY=1},
                // R
                new LinearGradientBrush(gsc, 0){ StartPoint = new Point(0,0), EndPoint=new Point(1,0)},
                // BR
                new RadialGradientBrush(gsc){ Center = new Point(0, 0), GradientOrigin = new Point(0, 0), RadiusX=1, RadiusY=1},
                // B
                new LinearGradientBrush(gsc, 0){ StartPoint = new Point(0,0), EndPoint=new Point(0,1)},
                // BL
                new RadialGradientBrush(gsc){ Center = new Point(1, 0), GradientOrigin = new Point(1, 0), RadiusX=1, RadiusY=1},
                // L
                new LinearGradientBrush(gsc, 0){ StartPoint = new Point(1,0), EndPoint=new Point(0,0)},
                // C
                new SolidColorBrush(color),
            };

            // This is a test pattern, uncomment to see how I'm drawing this
            //Brush[] colors = new Brush[]{
            //    Brushes.Red,
            //    Brushes.Green,
            //    Brushes.Blue,
            //    Brushes.Fuchsia,
            //    Brushes.Gainsboro,
            //    Brushes.LimeGreen,
            //    Brushes.Navy,
            //    Brushes.Orange,
            //    Brushes.White,
            //};
            double[] guidelineSetX = new double[] { transformed.X,
                                                    transformed.X+blurRadius,
                                                    transformed.Right-blurRadius,
                                                    transformed.Right};

            double[] guidelineSetY = new double[] { transformed.Y,
                                                    transformed.Y+blurRadius,
                                                    transformed.Bottom-blurRadius,
                                                    transformed.Bottom};

            drawingContext.PushGuidelineSet(new GuidelineSet(guidelineSetX, guidelineSetY));
            for (int i = 0; i < edges.Length; i++)
            {
                drawingContext.DrawRoundedRectangle(colors[i], null, edges[i], 0.0, 0.0);
            }
            drawingContext.Pop();
        }

        #endregion
    }
}
