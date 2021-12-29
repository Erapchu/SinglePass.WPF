using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PasswordManager.Helpers;

namespace PasswordManager.Extensions
{
	public static class ImageExtensions
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
