using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SinglePass.FavIcons.Application;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private const int _processingTimeout = 100;
        private const int _maxConcurrentDownloads = 4;
        private const int _favIconSize = 32;
        private const string _favIconServiceUrl = "http://www.google.com/s2/favicons?domain_url={0}&sz={1}";

        private readonly RegeneratedList<ImageRequest> _processingImages = new();
        private readonly ILogger<FavIconCollector> _logger;
        private readonly ImageService _imageService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly CancellationTokenSource _processingCTS = new();
        private readonly ConcurrentDictionary<string, ImageSource> _imagesCache = new();

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
                if (setPropertyAction is null ||
                    !Uri.TryCreate(imageUrlString, UriKind.Absolute, out var imageUrl) ||
                    string.IsNullOrEmpty(imageUrl.Host))
                    return;

                var host = imageUrl.Host;

                if (_imagesCache.TryGetValue(host, out ImageSource image))
                {
                    setPropertyAction(image);
                }
                else
                {
                    _processingImages.Add(new ImageRequest(host, setPropertyAction));
                }
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
                await favIconCacheService.Migrate();
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

                    var requests = _processingImages.PopAll();
                    if (requests != null)
                        await ProcessBatch(requests, cancellationToken);

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

        private async Task ProcessBatch(List<ImageRequest> requests, CancellationToken cancellationToken)
        {
            var pending = new List<IGrouping<string, ImageRequest>>();
            foreach (var group in requests.GroupBy(request => request.Host))
            {
                if (_imagesCache.TryGetValue(group.Key, out var image))
                    PublishImage(group, image);
                else
                    pending.Add(group);
            }

            if (pending.Count == 0)
                return;

            var keys = pending.Select(group => new FavIconDto { Host = group.Key, Size = _favIconSize }).ToList();
            List<FavIcon> cachedIcons;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<FavIconCacheService>();
                cachedIcons = await cache.GetMany(keys);
            }

            var iconsByHost = cachedIcons.ToLookup(icon => icon.Host);
            var downloads = new List<IGrouping<string, ImageRequest>>();
            foreach (var group in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cachedIcon = iconsByHost[group.Key].FirstOrDefault();
                if (cachedIcon == null)
                {
                    downloads.Add(group);
                    continue;
                }

                try
                {
                    var image = ImageSourceHelper.ToImageSource(cachedIcon.Bytes);
                    _imagesCache.TryAdd(group.Key, image);
                    PublishImage(group, image);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, null);
                }
            }

            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _maxConcurrentDownloads,
            };
            await Parallel.ForEachAsync(downloads, options, async (group, token) =>
            {
                try
                {
                    var host = group.Key;
                    var url = string.Format(_favIconServiceUrl, host, _favIconSize);
                    var image = await _imageService.GetImageAsync(url, token);
                    _imagesCache.TryAdd(group.Key, image);
                    PublishImage(group, image);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var cache = scope.ServiceProvider.GetRequiredService<FavIconCacheService>();
                    await cache.Add(new FavIcon { Host = host, Size = _favIconSize, Bytes = ImageSourceHelper.ToBytes(image) });
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    _logger.LogError(ex, null);
                }
            });
        }

        private void PublishImage(IEnumerable<ImageRequest> requests, ImageSource image)
        {
            foreach (var request in requests)
            {
                try
                {
                    request.SetPropertyAction(image);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, null);
                }
            }
        }

        private sealed record ImageRequest(string Host, Action<ImageSource> SetPropertyAction);
    }
}
