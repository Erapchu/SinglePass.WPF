using SinglePass.WPF.Authorization.Brokers;
using SinglePass.WPF.Authorization.TokenHolders;
using SinglePass.WPF.Clouds.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Clouds.Services
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
