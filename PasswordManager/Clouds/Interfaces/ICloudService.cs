using PasswordManager.Authorization.Interfaces;
using PasswordManager.Clouds.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Clouds.Interfaces
{
    public interface ICloudService
    {
        IAuthorizationBroker AuthorizationBroker { get; }
        Task Upload(Stream stream, string fileName, CancellationToken cancellationToken);
        Task<Stream> Download(string fileName, CancellationToken cancellationToken);
        Task<BaseUserInfo> GetUserInfo(CancellationToken cancellationToken);
    }
}
