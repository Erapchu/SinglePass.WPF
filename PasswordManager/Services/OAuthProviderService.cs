using PasswordManager.Authorization.Enums;
using PasswordManager.Authorization.Providers;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class OAuthProviderService
    {
        public async Task AuthorizeAsync(CloudType cloudType, CancellationToken cancellationToken)
        {
            switch (cloudType)
            {
                case CloudType.GoogleDrive:
                    await new GoogleAuthProvider().AuthorizeAsync(cancellationToken);
                    break;
            }
        }
    }
}
