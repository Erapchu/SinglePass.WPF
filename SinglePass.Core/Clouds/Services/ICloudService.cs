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
        Task<Stream> Download(string fileName, CancellationToken cancellationToken);
        Task<BaseUserInfo> GetUserInfo(CancellationToken cancellationToken);
    }
}
