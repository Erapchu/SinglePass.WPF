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
        private readonly FavIconCacheService _favIconCacheService;
        private readonly Thread _getImagesThread;

        public FavIconCollector(
            ILogger<FavIconCollector> logger,
            ImageService imageService,
            FavIconCacheService favIconCacheService)
        {
            _imageService = imageService;
            _favIconCacheService = favIconCacheService;
            _logger = logger;

            _getImagesThread = new Thread(ImageProcessing);
            _getImagesThread.IsBackground = true;
            _getImagesThread.Start();
        }

        public void ScheduleGetImage(string imageUrlString, Action<ImageSource> setPropertyAction)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageUrlString)
                    || !Uri.TryCreate(imageUrlString, UriKind.RelativeOrAbsolute, out Uri imageUrl)
                    || setPropertyAction is null)
                    return;

                var host = imageUrl.Host;
                var cachedImage = _favIconCacheService.GetCachedImage(host).Result;
                if (cachedImage is not null)
                {
                    setPropertyAction.Invoke(cachedImage);
                }
                else
                {
                    _processingImages.Add(new ProcessingImageWrapper(host, setPropertyAction));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
        }

        private void ImageProcessing()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(_processingTimeout);

                    var imagesProcWrappers = _processingImages.PopAll();
                    if (imagesProcWrappers != null)
                    {
                        foreach (var imageProcWrapper in imagesProcWrappers)
                        {
                            var cachedImage = _favIconCacheService.GetCachedImage(imageProcWrapper.Host).Result;
                            if (cachedImage is not null)
                            {
                                imageProcWrapper.SetPropertyAction.Invoke(cachedImage);
                            }
                            else
                            {
                                var bitmapImage = _imageService.GetImageAsync(string.Format(_favIconServiceUrl, imageProcWrapper.Host), CancellationToken.None).Result;
                                _favIconCacheService.SetCachedImage(imageProcWrapper.Host, bitmapImage);
                                imageProcWrapper.SetPropertyAction.Invoke(bitmapImage);
                            }
                        }
                    }
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
            public string Host { get; }
            public Action<ImageSource> SetPropertyAction { get; }

            public ProcessingImageWrapper(string host, Action<ImageSource> setPropertyAction)
            {
                Host = host;
                SetPropertyAction = setPropertyAction;
            }
        }
    }
}
