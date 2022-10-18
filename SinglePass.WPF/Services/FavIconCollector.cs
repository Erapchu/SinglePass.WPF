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
        //private const int _processingTimeout = 100;
        private const string _favIconServiceUrl = "http://www.google.com/s2/favicons?domain_url={0}&sz={1}";

        private readonly ILogger<FavIconCollector> _logger;
        private readonly ImageService _imageService;
        //private readonly IServiceScopeFactory _serviceScopeFactory;
        //private readonly CancellationTokenSource _processingCTS = new();
        private readonly ConcurrentDictionary<ProcessingImageWrapper, ImageSource> _imagesCache = new();
        private readonly ConcurrentDictionary<ProcessingImageWrapper, Task<ImageSource>> _processingImages = new();

        public FavIconCollector(
            ILogger<FavIconCollector> logger,
            ImageService imageService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _imageService = imageService;
            //_serviceScopeFactory = serviceScopeFactory;
            _logger = logger;

            //_ = Task.Run(() => ImageProcessing(_processingCTS.Token));
        }

        public void ScheduleGetImage(string imageUrlString, Action<ImageSource> setPropertyAction, int size = 16)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageUrlString) || setPropertyAction is null)
                    return;

                var processingImageWrapper = new ProcessingImageWrapper(imageUrlString, setPropertyAction, size);

                if (_imagesCache.TryGetValue(processingImageWrapper, out ImageSource cachedImage))
                {
                    processingImageWrapper.SetPropertyAction.Invoke(cachedImage);
                }
                else
                {
                    if (_processingImages.TryGetValue(processingImageWrapper, out Task<ImageSource> task))
                    {
                        task.ContinueWith(t => processingImageWrapper.SetPropertyAction.Invoke(task.Result));
                    }
                    else
                    {
                        var t = Task.Run<ImageSource>(async () =>
                        {
                            var imageSource = await _imageService.GetImageAsync(
                                string.Format(_favIconServiceUrl, processingImageWrapper.Host, processingImageWrapper.Size),
                                CancellationToken.None);
                            processingImageWrapper.SetPropertyAction(imageSource);
                            _imagesCache.TryAdd(processingImageWrapper, imageSource);
                            _processingImages.TryRemove(processingImageWrapper, out _);
                            return imageSource;
                        });
                        _processingImages.TryAdd(processingImageWrapper, t);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
        }

        //private async Task ImageProcessing(CancellationToken cancellationToken)
        //{
        //    try
        //    {
        //        using var scope = _serviceScopeFactory.CreateScope();
        //        var favIconCacheService = scope.ServiceProvider.GetService<FavIconCacheService>();
        //        await favIconCacheService.Migrate();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, null);
        //    }

        //    while (true)
        //    {
        //        try
        //        {
        //            cancellationToken.ThrowIfCancellationRequested();

        //            var processingWrappers = _processingImages.PopAll();
        //            if (processingWrappers != null)
        //            {
        //                using var scope = _serviceScopeFactory.CreateScope();
        //                var favIconCacheService = scope.ServiceProvider.GetService<FavIconCacheService>();
        //                var uniqueHosts = processingWrappers
        //                    .Distinct()
        //                    .Select(ipw => new FavIconDto() { Host = ipw.Host, Size = ipw.Size })
        //                    .ToList();
        //                var favIconsFromDB = await favIconCacheService.GetMany(uniqueHosts);
        //                var tempFavIconCacheFromDB = favIconsFromDB.ToDictionary(fi => new ProcessingImageWrapper(fi.Host, fi.Size));

        //                // Set existing to UI
        //                foreach (var processingWrapper in processingWrappers)
        //                {
        //                    ImageSource cachedImageSource;
        //                    if (tempFavIconCacheFromDB.TryGetValue(processingWrapper, out FavIcon favIcon))
        //                    {
        //                        var imageSource = ImageSourceHelper.ToImageSource(favIcon.Bytes);
        //                        processingWrapper.SetPropertyAction.Invoke(imageSource);
        //                        cachedImageSource = imageSource;
        //                    }
        //                    else
        //                    {
        //                        // Download, set to DB, set to UI and save to temp local cache
        //                        var bitmapImage = await _imageService.GetImageAsync(
        //                            string.Format(_favIconServiceUrl, processingWrapper.Host, processingWrapper.Size), cancellationToken);
        //                        var freshFavIcon = new FavIcon()
        //                        {
        //                            Bytes = ImageSourceHelper.ToBytes(bitmapImage),
        //                            Host = processingWrapper.Host,
        //                            Size = processingWrapper.Size,
        //                        };
        //                        await favIconCacheService.Add(freshFavIcon);
        //                        tempFavIconCacheFromDB.TryAdd(processingWrapper, freshFavIcon);
        //                        processingWrapper.SetPropertyAction.Invoke(bitmapImage);
        //                        cachedImageSource = bitmapImage;
        //                    }

        //                    _imagesCache.TryAdd(processingWrapper, cachedImageSource);
        //                }
        //            }

        //            await Task.Delay(_processingTimeout, cancellationToken);
        //        }
        //        catch (OperationCanceledException)
        //        {
        //            break;
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, null);
        //        }
        //    }
        //}

        [DebuggerDisplay("{Size} - {_originalString}")]
        private class ProcessingImageWrapper
        {
            private readonly string _originalString;

            public Action<ImageSource> SetPropertyAction { get; }
            public int Size { get; }

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

            public ProcessingImageWrapper(string host, int size)
            {
                _host = host ?? throw new ArgumentNullException(nameof(host));
                Size = size;
            }

            public ProcessingImageWrapper(string originalString, Action<ImageSource> setPropertyAction, int size = 16)
            {
                if (string.IsNullOrWhiteSpace(originalString))
                    throw new ArgumentException($"'{nameof(originalString)}' cannot be null or empty.", nameof(originalString));

                _originalString = originalString;
                SetPropertyAction = setPropertyAction ?? throw new ArgumentNullException(nameof(setPropertyAction));
                Size = size;
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
