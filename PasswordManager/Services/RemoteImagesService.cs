using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PasswordManager.Services
{
    public class RemoteImagesService
    {
        private const string _favIconService = "http://www.google.com/s2/favicons?domain_url={0}";
        private readonly ConcurrentDictionary<string, ImageSource> _images = new();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RemoteImagesService> _logger;

        public RemoteImagesService(
            IHttpClientFactory httpClientFactory,
            ILogger<RemoteImagesService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public ImageSource GetImage(string imageUrlString)
        {
            try
            {
                if (!Uri.TryCreate(imageUrlString, UriKind.RelativeOrAbsolute, out Uri imageUrl))
                    return null;

                var host = imageUrl.Host;

                if (_images.TryGetValue(host, out ImageSource image))
                    return image;

                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, string.Format(_favIconService, host));
                using var response = client.SendAsync(request, CancellationToken.None).Result;
                using var stream = response.Content.ReadAsStreamAsync(CancellationToken.None).Result;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                _images.TryAdd(host, bitmapImage);

                return bitmapImage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
                return null;
            }
        }
    }
}
