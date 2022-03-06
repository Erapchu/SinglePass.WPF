using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Authorization.Interfaces
{
    public interface IAuthorizationBroker
    {
        Task AuthorizeAsync(CancellationToken cancellationToken);
    }
}
