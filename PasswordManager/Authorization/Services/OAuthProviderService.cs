using PasswordManager.Authorization.Enums;
using PasswordManager.Authorization.Interfaces;
using PasswordManager.Authorization.Providers;
using System;

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
                CloudType.GoogleDrive => _serviceProvider.GetService(typeof(GoogleAuthorizationBroker)) as IAuthorizationBroker,
                _ => null,
            };
        }
    }
}
