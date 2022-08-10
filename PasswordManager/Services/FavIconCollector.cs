using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PasswordManager.Application;
using PasswordManager.Utilities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PasswordManager.Services
{
    public interface IFavIconCollector
    {
        void ScheduleGetImage(string imageUrlString, Action<ImageSource> setPropertyAction);
    }

    public class FavIconCollector : IFavIconCollector
    {
        private const int _processingTimeout = 200;
        private const string _favIconServiceUrl = "http://www.google.com/s2/favicons?domain_url={0}";

        private readonly RegeneratedList<ProcessingImageWrapper> _processingImages = new();
        private readonly ILogger<FavIconCollector> _logger;
        private readonly ImageService _imageService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly CancellationTokenSource _processingCTS = new();

        public FavIconCollector(
            ILogger<FavIconCollector> logger,
            ImageService imageService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _imageService = imageService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;

            _ = Task.Run(() => ImageProcessing(_processingCTS.Token));
        }

        public void ScheduleGetImage(string imageUrlString, Action<ImageSource> setPropertyAction)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageUrlString) || setPropertyAction is null)
                    return;

                _processingImages.Add(new ProcessingImageWrapper(imageUrlString, setPropertyAction));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
        }

        private async Task ImageProcessing(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var favIconCacheService = scope.ServiceProvider.GetService<FavIconCacheService>();
                await favIconCacheService.EnsureCreated();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }

            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var processingWrappers = _processingImages.PopAll();
                    if (processingWrappers != null)
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var favIconCacheService = scope.ServiceProvider.GetService<FavIconCacheService>();

                        var uniqueProcessingImages = processingWrappers.Distinct().ToList();
                        var hosts = uniqueProcessingImages.Select(ipw => ipw.Host).ToList();
                        var favIconsDB = await favIconCacheService.GetManyCachedImages(hosts);

                        // Set existing to UI
                        foreach (var processingImage in processingWrappers)
                        {
                            var favIcon = favIconsDB.FirstOrDefault(f => f.Host.Equals(processingImage.Host, StringComparison.OrdinalIgnoreCase));
                            if (favIcon is not null)
                            {
                                processingImage.SetPropertyAction.Invoke(favIcon.ImageSource);
                            }
                        }

                        // Download and set to UI only unique images
                        foreach (var processingImage in uniqueProcessingImages)
                        {
                            var favIcon = favIconsDB.FirstOrDefault(f => f.Host.Equals(processingImage.Host, StringComparison.OrdinalIgnoreCase));
                            if (favIcon is null)
                            {
                                var bitmapImage = await _imageService.GetImageAsync(string.Format(_favIconServiceUrl, processingImage.Host), cancellationToken);
                                await favIconCacheService.SetCachedImage(new FavIconWrapper(bitmapImage, processingImage.Host));
                                var images = processingWrappers.Where(p => p.Host.Equals(processingImage.Host, StringComparison.OrdinalIgnoreCase));
                                foreach (var image in images)
                                {
                                    image.SetPropertyAction.Invoke(bitmapImage);
                                }
                            }
                        }
                    }

                    await Task.Delay(_processingTimeout, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, null);
                }
            }
        }

        private class ProcessingImageWrapper
        {
            public string OriginalString { get; }
            public Action<ImageSource> SetPropertyAction { get; }

            private string _host;
            public string Host
            {
                get
                {
                    if (Uri.TryCreate(OriginalString, UriKind.RelativeOrAbsolute, out Uri imageUrl))
                    {
                        _host = imageUrl.Host;
                    }

                    return _host;
                }
            }

            public ProcessingImageWrapper(string originalString, Action<ImageSource> setPropertyAction)
            {
                if (string.IsNullOrWhiteSpace(originalString))
                {
                    throw new ArgumentException($"'{nameof(originalString)}' cannot be null or empty.", nameof(originalString));
                }

                OriginalString = originalString;
                SetPropertyAction = setPropertyAction ?? throw new ArgumentNullException(nameof(setPropertyAction));
            }

            public override bool Equals(object obj)
            {
                return obj is ProcessingImageWrapper wrapper &&
                       OriginalString == wrapper.OriginalString;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(OriginalString);
            }
        }
    }
}
