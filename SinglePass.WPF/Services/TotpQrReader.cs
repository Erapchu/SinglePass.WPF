using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;

namespace SinglePass.WPF.Services
{
    internal static class TotpQrReader
    {
        internal static string ReadSetupLink(BitmapSource image)
        {
            var width = image.PixelWidth;
            var height = image.PixelHeight;
            if ((long)width * height > 16_000_000)
                return null;

            var bitmap = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
            var stride = width * 3;
            var pixels = new byte[stride * height];
            try
            {
                bitmap.CopyPixels(pixels, stride, 0);
                var reader = new BarcodeReaderGeneric
                {
                    AutoRotate = true,
                    Options = { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true, TryInverted = true }
                };
                var source = new RGBLuminanceSource(pixels, width, height, RGBLuminanceSource.BitmapFormat.BGR24);
                var result = reader.Decode(source)?.Text;
                return result is { Length: <= 4096 } &&
                    result.StartsWith("otpauth://totp/", System.StringComparison.OrdinalIgnoreCase) &&
                    TotpService.TryParse(result, out _) ? result : null;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(pixels);
            }
        }
    }
}
