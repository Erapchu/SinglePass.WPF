using SinglePass.Core.Authorization.Brokers;
using SinglePass.Core.Authorization.TokenHolders;
using SinglePass.Core.Clouds.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.Core.Clouds.Services
{
    public interface ICloudService
    {
        IOAuthProvider OAuthProvider { get; }
        ITokenHolder TokenHolder { get; }
        Task Upload(Stream stream, string fileName, CancellationToken cancellationToken);

        /// <summary>Downloads the file, or returns null when it does not exist.</summary>
        Task<byte[]> Download(string fileName, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the MD5 of the stored file as lowercase hex, or null when it does not exist.
        /// Used to detect that another device changed the file between download and upload.
        /// </summary>
        Task<string> GetRevision(string fileName, CancellationToken cancellationToken);
        Task<BaseUserInfo> GetUserInfo(CancellationToken cancellationToken);
    }
}
