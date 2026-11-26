using System.Net.Http;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Erapkey.Desktop.Helpers;
using Erapkey.Core.Helpers;

namespace Erapkey.Desktop.Services
{
    public class ImageService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ImageService> _logger;

        public ImageService(IHttpClientFactory httpClientFactory, ILogger<ImageService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ImageSource> GetProfileImageAsync(string imageUrl, CancellationToken cancellationToken)
        {
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imageUrl)));
            var directory = Path.Combine(Constants.CacheDirectoryPath, "CloudAvatars");
            var path = Path.Combine(directory, key + ".png");

            try
            {
                if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(7))
                {
                    var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                    return ImageSourceHelper.ToImageSource(bytes);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Unable to read the cached cloud avatar");
            }

            var image = await GetImageAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(directory);
                var temporaryPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    await File.WriteAllBytesAsync(temporaryPath, ImageSourceHelper.ToBytes(image), cancellationToken)
                        .ConfigureAwait(false);
                    File.Move(temporaryPath, path, overwrite: true);
                }
                finally
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Unable to cache the cloud avatar");
            }

            return image;
        }

        public async Task<ImageSource> GetImageAsync(string imageUrl, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
    }
}
