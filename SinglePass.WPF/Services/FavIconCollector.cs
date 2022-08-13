using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SinglePass.FavIcons.Application;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Utilities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SinglePass.WPF.Services
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
                        var favIconsFromDB = await favIconCacheService.GetManyCachedImages(hosts);
                        var tempFavIconCache = favIconsFromDB.ToDictionary(fi => fi.Host);

                        // Set existing to UI
                        foreach (var processingImage in processingWrappers)
                        {
                            if (tempFavIconCache.TryGetValue(processingImage.Host, out FavIcon favIcon))
                            {
                                var imageSource = ImageSourceHelper.ToImageSource(favIcon.Bytes);
                                processingImage.SetPropertyAction.Invoke(imageSource);
                            }
                            else
                            {
                                // Download, set to DB, set to UI and save to temp local cache
                                var bitmapImage = await _imageService.GetImageAsync(string.Format(_favIconServiceUrl, processingImage.Host), cancellationToken);
                                var freshFavIcon = new FavIcon()
                                {
                                    Bytes = ImageSourceHelper.ToBytes(bitmapImage),
                                    Host = processingImage.Host,
                                };
                                await favIconCacheService.SetCachedImage(freshFavIcon);
                                tempFavIconCache.TryAdd(processingImage.Host, freshFavIcon);
                                processingImage.SetPropertyAction.Invoke(bitmapImage);
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
            private readonly string _originalString;

            public Action<ImageSource> SetPropertyAction { get; }

            private string _host;
            public string Host
            {
                get
                {
                    if (_host is null)
                    {
                        if (Uri.TryCreate(_originalString, UriKind.RelativeOrAbsolute, out Uri imageUrl))
                        {
                            _host = imageUrl.Host;
                        }
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

                _originalString = originalString;
                SetPropertyAction = setPropertyAction ?? throw new ArgumentNullException(nameof(setPropertyAction));
            }

            public override bool Equals(object obj)
            {
                return obj is ProcessingImageWrapper wrapper &&
                       Host == wrapper.Host;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Host);
            }
        }
    }
}
