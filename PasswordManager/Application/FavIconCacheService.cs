using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PasswordManager.Application
{
    public class FavIconCacheService
    {
        private readonly IFavIconRepository _favIconRepository;

        public FavIconCacheService(IFavIconRepository favIconRepository)
        {
            _favIconRepository = favIconRepository;
        }

        public Task EnsureCreated()
        {
            return _favIconRepository.EnsureCreated();
        }

        public async Task<FavIconWrapper> GetCachedImage(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return null;

            var favIcon = await _favIconRepository.Get(host);
            if (favIcon is not null)
            {
                return new FavIconWrapper(ToImageSource(favIcon.Bytes), favIcon.Host);
            }

            return null;
        }

        public async Task<IReadOnlyCollection<FavIconWrapper>> GetManyCachedImages(List<string> hosts)
        {
            if (hosts is null)
                return null;

            var favIcons = await _favIconRepository.GetMany(hosts);
            if (favIcons is not null)
            {
                var images = new List<FavIconWrapper>();
                foreach (var favIcon in favIcons)
                {
                    images.Add(new FavIconWrapper(ToImageSource(favIcon.Bytes), favIcon.Host));
                }
                return images;
            }

            return null;
        }

        public Task SetCachedImage(FavIconWrapper favIconWrapper)
        {
            if (favIconWrapper is null)
                throw new ArgumentNullException(nameof(favIconWrapper));

            var favIcon = new FavIcon()
            {
                Bytes = ToBytes(favIconWrapper.ImageSource),
                Host = favIconWrapper.Host
            };
            return _favIconRepository.Add(favIcon);
        }

        private static byte[] ToBytes(ImageSource imageSource)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imageSource));
            using var imageFileStream = new MemoryStream();
            encoder.Save(imageFileStream);
            return imageFileStream.ToArray();
        }

        private static ImageSource ToImageSource(byte[] bytes)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
