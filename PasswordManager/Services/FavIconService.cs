using Microsoft.Extensions.Logging;
using PasswordManager.Helpers;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PasswordManager.Services
{
    public class FavIconService
    {
        private const string _favIconServiceUrl = "http://www.google.com/s2/favicons?domain_url={0}";
        private readonly ConcurrentDictionary<string, ImageSource> _imagesDict = new();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FavIconService> _logger;

        public FavIconService(
            IHttpClientFactory httpClientFactory,
            ILogger<FavIconService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public ImageSource GetImage(string imageUrlString)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageUrlString) || !Uri.TryCreate(imageUrlString, UriKind.RelativeOrAbsolute, out Uri imageUrl))
                    return null;

                var host = imageUrl.Host;
                using var disposableLocker = new DisposableLocker(host);
                if (_imagesDict.TryGetValue(host, out ImageSource image))
                    return image;

                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, string.Format(_favIconServiceUrl, host));
                using var response = client.SendAsync(request, CancellationToken.None).Result;
                using var stream = response.Content.ReadAsStreamAsync(CancellationToken.None).Result;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                _imagesDict.TryAdd(host, bitmapImage);
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
