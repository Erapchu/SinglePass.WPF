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

        public void EnsureCreated()
        {
            _favIconRepository.EnsureCreated();
        }

        public async Task<ImageSource> GetCachedImage(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return null;

            var favIcon = await _favIconRepository.Get(host);
            if (favIcon is not null)
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = new MemoryStream(favIcon.Bytes);
                image.EndInit();
                image.Freeze();
                return image;
            }

            return null;
        }

        public void SetCachedImage(string host, ImageSource image)
        {
            if (string.IsNullOrWhiteSpace(host))
                return;

            if (image is BitmapImage bitmapImage)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                using var imageFileStream = new MemoryStream();
                encoder.Save(imageFileStream);
                var favIcon = new FavIcon()
                {
                    Bytes = imageFileStream.ToArray(),
                    Host = host
                };
                _favIconRepository.Add(favIcon).Wait();
            }
        }
    }
}
