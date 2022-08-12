using System.Threading;
using System.Threading.Tasks;
using SinglePass.WPF.Authorization.Responses;

namespace SinglePass.WPF.Authorization.TokenHolders
{
    public interface ITokenHolder
    {
        ITokenResponse Token { get; }
        Task SetAndSaveToken(string tokenResponse, CancellationToken cancellationToken);
        Task RemoveToken();
    }
}
