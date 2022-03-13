using PasswordManager.Authorization.Interfaces;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Clouds.Interfaces
{
    public interface ICloudService
    {
        IAuthorizationBroker AuthorizationBroker { get; }
        Task Upload(Stream stream, string fileName, CancellationToken cancellationToken);
    }
}
