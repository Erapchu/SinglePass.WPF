using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SinglePass.FavIcons.Application;
using SinglePass.WPF.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using SinglePass.Core.Utilities;

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
        private const string _favIconServiceUrl = "https://www.google.com/s2/favicons?domain_url={0}&sz={1}&alt=404";
        private static readonly TimeSpan AvailableRefreshInterval = TimeSpan.FromDays(30);
        private static readonly TimeSpan UnavailableRetryInterval = TimeSpan.FromDays(1);

        private readonly RegeneratedList<ImageRequest> _processingImages = new();
        private readonly ILogger<FavIconCollector> _logger;
        private readonly ImageService _imageService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly CancellationTokenSource _processingCTS = new();
        private readonly ConcurrentDictionary<string, CachedImage> _imagesCache = new();

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

                if (_imagesCache.TryGetValue(host, out var cached) && cached.RefreshAfterUtc > DateTime.UtcNow)
                    setPropertyAction(cached.Image);
                else
                    _processingImages.Add(new ImageRequest(host, setPropertyAction));
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
                if (_imagesCache.TryGetValue(group.Key, out var cached) && cached.RefreshAfterUtc > DateTime.UtcNow)
                    PublishImage(group, cached.Image);
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

            var iconsByHost = cachedIcons.ToDictionary(icon => icon.Host);
            var downloads = new List<(IGrouping<string, ImageRequest> Requests, FavIcon Icon, ImageSource Image)>();
            foreach (var group in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!iconsByHost.TryGetValue(group.Key, out var icon))
                    icon = new FavIcon { Host = group.Key, Size = _favIconSize };

                var image = DecodeCachedImage(icon);
                var refreshAfter = GetRefreshAfterUtc(icon);
                if (refreshAfter > DateTime.UtcNow &&
                    (image != null || icon.Status != FavIconStatus.Available))
                {
                    _imagesCache[group.Key] = new CachedImage(image, refreshAfter);
                    PublishImage(group, image);
                    continue;
                }

                PublishImage(group, image);
                downloads.Add((group, icon, image));
            }

            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _maxConcurrentDownloads,
            };
            await Parallel.ForEachAsync(downloads, options, async (download, token) =>
            {
                await RefreshImage(download.Icon, download.Image, download.Requests, token);
            });
        }

        private async Task RefreshImage(FavIcon icon, ImageSource image, IEnumerable<ImageRequest> requests,
            CancellationToken cancellationToken)
        {
            try
            {
                var url = string.Format(_favIconServiceUrl, Uri.EscapeDataString(icon.Host), icon.Size);
                var downloadedImage = await _imageService.GetImageAsync(url, cancellationToken);
                var bytes = ImageSourceHelper.ToBytes(downloadedImage);
                if (icon.Bytes == null || !bytes.AsSpan().SequenceEqual(icon.Bytes))
                {
                    icon.Bytes = bytes;
                    icon.LastUpdatedUtc = DateTime.UtcNow;
                    image = downloadedImage;
                }
                image ??= downloadedImage;
                icon.Status = FavIconStatus.Available;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                icon.Status = FavIconStatus.Missing;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                icon.Status = FavIconStatus.TemporaryError;
                _logger.LogDebug(ex, "Unable to refresh a favicon");
            }

            cancellationToken.ThrowIfCancellationRequested();
            icon.LastCheckedUtc = DateTime.UtcNow;
            _imagesCache[icon.Host] = new CachedImage(image, GetRefreshAfterUtc(icon));
            PublishImage(requests, image);

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var cache = scope.ServiceProvider.GetRequiredService<FavIconCacheService>();
                await cache.Save(icon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to save a favicon cache entry");
            }
        }

        private ImageSource DecodeCachedImage(FavIcon icon)
        {
            if (icon.Bytes is not { Length: > 0 })
                return null;

            try
            {
                return ImageSourceHelper.ToImageSource(icon.Bytes);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to decode a cached favicon");
                return null;
            }
        }

        private static DateTime GetRefreshAfterUtc(FavIcon icon)
        {
            if (icon.LastCheckedUtc is not { } lastChecked)
                return DateTime.MinValue;

            return lastChecked + (icon.Status == FavIconStatus.Available
                ? AvailableRefreshInterval
                : UnavailableRetryInterval);
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
        private sealed record CachedImage(ImageSource Image, DateTime RefreshAfterUtc);
    }
}
