using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SinglePass.WPF.Helpers
{
    internal static class ImageSourceHelper
    {
        public static byte[] ToBytes(ImageSource imageSource)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imageSource));
            using var imageFileStream = new MemoryStream();
            encoder.Save(imageFileStream);
            return imageFileStream.ToArray();
        }

        public static ImageSource ToImageSource(byte[] bytes)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
