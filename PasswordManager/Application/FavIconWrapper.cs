using System;
using System.Windows.Media;

namespace PasswordManager.Application
{
    public class FavIconWrapper
    {
        public ImageSource ImageSource { get; }
        public string Host { get; }

        public FavIconWrapper(ImageSource imageSource, string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException($"'{nameof(host)}' cannot be null or empty.", nameof(host));
            }

            ImageSource = imageSource ?? throw new ArgumentNullException(nameof(imageSource));
            Host = host;
        }
    }
}
