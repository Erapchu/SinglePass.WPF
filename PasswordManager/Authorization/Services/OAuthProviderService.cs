using PasswordManager.Authorization.Enums;
using PasswordManager.Authorization.Interfaces;
using PasswordManager.Authorization.Providers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Authorization.Services
{
    public class OAuthProviderService
    {
        private readonly IServiceProvider _serviceProvider;

        public OAuthProviderService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IAuthorizationBroker GetAuthorizationBroker(CloudType cloudType)
        {
            return cloudType switch
            {
                CloudType.GoogleDrive => new GoogleAuthorizationBroker(),
                _ => null,
            };
        }
    }
}
