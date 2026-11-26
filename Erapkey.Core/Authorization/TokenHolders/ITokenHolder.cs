using Erapkey.Core.Authorization.Responses;
using System.Threading;
using System.Threading.Tasks;

namespace Erapkey.Core.Authorization.TokenHolders
{
    public interface ITokenHolder
    {
        OAuthInfo OAuthInfo { get; }
        Task SetAndSaveToken(OAuthInfo oauthInfo, CancellationToken cancellationToken);
        Task RemoveToken();
    }
}
