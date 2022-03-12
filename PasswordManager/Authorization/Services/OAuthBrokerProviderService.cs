using PasswordManager.Authorization.Brokers;
using PasswordManager.Authorization.Enums;
using PasswordManager.Authorization.Interfaces;
using System;

namespace PasswordManager.Authorization.Services
{
    public class OAuthBrokerProviderService
    {
        private readonly IServiceProvider _serviceProvider;

        public OAuthBrokerProviderService(IServiceProvider serviceProvider)
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
