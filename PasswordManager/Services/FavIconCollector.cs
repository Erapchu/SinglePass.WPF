using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PasswordManager.Application;
using PasswordManager.Utilities;
using System;
using System.Threading;
using System.Windows.Media;

namespace PasswordManager.Services
{
    public class FavIconCollector
    {
        private const int _processingTimeout = 200;
        private const string _favIconServiceUrl = "http://www.google.com/s2/favicons?domain_url={0}";

        private readonly RegeneratedList<ProcessingImageWrapper> _processingImages = new();
        private readonly ILogger<FavIconCollector> _logger;
        private readonly ImageService _imageService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly Thread _getImagesThread;

        public FavIconCollector(
            ILogger<FavIconCollector> logger,
            ImageService imageService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _imageService = imageService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;

            _getImagesThread = new Thread(ImageProcessing);
            _getImagesThread.IsBackground = true;
            _getImagesThread.Start();
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

        private void ImageProcessing()
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var favIconCacheService = scope.ServiceProvider.GetService<FavIconCacheService>();
                favIconCacheService.EnsureCreated();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }

            while (true)
            {
                try
                {
                    var imagesProcWrappers = _processingImages.PopAll();
                    if (imagesProcWrappers != null)
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var favIconCacheService = scope.ServiceProvider.GetService<FavIconCacheService>();
                        foreach (var imageProcWrapper in imagesProcWrappers)
                        {
                            var cachedImage = favIconCacheService.GetCachedImage(imageProcWrapper.Host).Result;
                            if (cachedImage is not null)
                            {
                                imageProcWrapper.SetPropertyAction.Invoke(cachedImage);
                            }
                            else
                            {
                                var bitmapImage = _imageService.GetImageAsync(string.Format(_favIconServiceUrl, imageProcWrapper.Host), CancellationToken.None).Result;
                                favIconCacheService.SetCachedImage(imageProcWrapper.Host, bitmapImage);
                                imageProcWrapper.SetPropertyAction.Invoke(bitmapImage);
                            }
                        }
                    }

                    Thread.Sleep(_processingTimeout);
                }
                catch (ThreadAbortException)
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
        }
    }
}
