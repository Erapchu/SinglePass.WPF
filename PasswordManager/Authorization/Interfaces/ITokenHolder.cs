using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Authorization.Interfaces
{
    public interface ITokenHolder
    {
        ITokenResponse Token { get; }
        Task SetAndSaveToken(string tokenResponse, CancellationToken cancellationToken);
        Task RemoveToken();
    }
}
