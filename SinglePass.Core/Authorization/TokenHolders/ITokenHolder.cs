using SinglePass.Core.Authorization.Responses;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.Core.Authorization.TokenHolders
{
    public interface ITokenHolder
    {
        OAuthInfo OAuthInfo { get; }
        Task SetAndSaveToken(OAuthInfo oauthInfo, CancellationToken cancellationToken);
        Task RemoveToken();
    }
}
