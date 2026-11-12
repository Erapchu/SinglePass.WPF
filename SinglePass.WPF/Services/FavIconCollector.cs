using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SinglePass.FavIcons.Application;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Utilities;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SinglePass.WPF.Services
{
    public interface IFavIconCollector
    {
        void ScheduleGetImage(string imageUrlString, Action<ImageSource> setPropertyAction, int size = 16);
    }

    public class FavIconCollector : IFavIconCollector
    {
        private const int _processingTimeout = 100;
        private const string _favIconServiceUrl = "http://www.google.com/s2/favicons?domain_url={0}&sz={1}";

        private readonly RegeneratedList<ProcessingImageWrapper> _processingImages = new();
        private readonly ILogger<FavIconCollector> _logger;
        private readonly ImageService _imageService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly CancellationTokenSource _processingCTS = new();
        private readonly ConcurrentDictionary<ProcessingImageWrapper, ImageSource> _imagesCache = new();

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

        public void ScheduleGetImage(string imageUrlString, Action<ImageSource> setPropertyAction, int size = 16)
        {
            try
            {
                if (setPropertyAction is null ||
                    !Uri.TryCreate(imageUrlString, UriKind.Absolute, out var imageUrl) ||
                    string.IsNullOrEmpty(imageUrl.Host))
                    return;

                var processingImageWrapper = new ProcessingImageWrapper(imageUrl.Host, size, setPropertyAction);

                if (_imagesCache.TryGetValue(processingImageWrapper, out ImageSource image))
                {
                    processingImageWrapper.SetPropertyAction.Invoke(image);
                }
                else
                {
                    _processingImages.Add(processingImageWrapper);
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

                    var processingWrappers = _processingImages.PopAll();
                    if (processingWrappers != null)
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var favIconCacheService = scope.ServiceProvider.GetService<FavIconCacheService>();
                        var uniqueHosts = processingWrappers
                            .Distinct()
                            .Select(ipw => new FavIconDto() { Host = ipw.Host, Size = ipw.Size })
                            .ToList();
                        var favIconsFromDB = await favIconCacheService.GetMany(uniqueHosts);
                        var tempFavIconCacheFromDB = favIconsFromDB.ToDictionary(fi => new ProcessingImageWrapper(fi.Host, fi.Size));

                        // Set existing to UI
                        foreach (var processingWrapper in processingWrappers)
                        {
                            try
                            {
                                ImageSource image;
                                if (tempFavIconCacheFromDB.TryGetValue(processingWrapper, out FavIcon favIcon))
                                {
                                    image = ImageSourceHelper.ToImageSource(favIcon.Bytes);
                                    _imagesCache.TryAdd(processingWrapper, image);
                                    processingWrapper.SetPropertyAction.Invoke(image);
                                    continue;
                                }

                                image = await _imageService.GetImageAsync(
                                    string.Format(_favIconServiceUrl, processingWrapper.Host, processingWrapper.Size), cancellationToken);
                                _imagesCache.TryAdd(processingWrapper, image);
                                processingWrapper.SetPropertyAction.Invoke(image);

                                var freshFavIcon = new FavIcon()
                                {
                                    Bytes = ImageSourceHelper.ToBytes(image),
                                    Host = processingWrapper.Host,
                                    Size = processingWrapper.Size,
                                };
                                await favIconCacheService.Add(freshFavIcon);
                                tempFavIconCacheFromDB.TryAdd(processingWrapper, freshFavIcon);
                            }
                            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                            {
                                _logger.LogError(ex, null);
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

        [DebuggerDisplay("{Size} - {Host}")]
        private class ProcessingImageWrapper
        {
            public Action<ImageSource> SetPropertyAction { get; }
            public int Size { get; }
            public string Host { get; }

            public ProcessingImageWrapper(string host, int size, Action<ImageSource> setPropertyAction = null)
            {
                Host = host ?? throw new ArgumentNullException(nameof(host));
                Size = size;
                SetPropertyAction = setPropertyAction;
            }

            public override bool Equals(object obj)
            {
                return obj is ProcessingImageWrapper wrapper &&
                       Size == wrapper.Size &&
                       Host == wrapper.Host;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Size, Host);
            }
        }
    }
}
