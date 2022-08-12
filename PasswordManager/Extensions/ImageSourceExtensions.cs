using SinglePass.WPF.Helpers;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SinglePass.WPF.Extensions
{
    public static class ImageSourceExtensions
    {
        public static ImageSource GetWpfImageSource(this Image image)
        {
            if (image is null)
                throw new ArgumentNullException(nameof(image));

            //Lock the same image to ensure that Clone() and Freeze() 
            //methods are thread-safe for current image.
            lock (image)
            {
                using (var source = (Bitmap)image.Clone())
                {
                    //obtain the Hbitmap
                    IntPtr ptr = source.GetHbitmap();

                    BitmapSource bs;
                    try
                    {
                        bs = Imaging.CreateBitmapSourceFromHBitmap(
                           ptr,
                           IntPtr.Zero,
                           Int32Rect.Empty,
                           BitmapSizeOptions.FromEmptyOptions());
                    }
                    finally
                    {
                        //release the HBitmap
                        NativeMethods.DeleteObject(ptr);
                    }

                    bs?.Freeze();
                    return bs;
                }
            }
        }
    }
}
