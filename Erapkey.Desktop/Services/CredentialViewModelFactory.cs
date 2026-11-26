using Erapkey.Core.Models;
using Erapkey.Desktop.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Erapkey.Core.Services;

namespace Erapkey.Desktop.Services
{
    public class CredentialViewModelFactory
    {
        private readonly IFavIconCollector _favIconCollector;
        private readonly CredentialsCryptoService _credentialsCryptoService;

        public CredentialViewModelFactory(
            IFavIconCollector favIconCollector,
            CredentialsCryptoService credentialsCryptoService)
        {
            _favIconCollector = favIconCollector;
            _credentialsCryptoService = credentialsCryptoService;
        }

        public CredentialViewModel ProvideNew(Credential credential)
        {
            return new CredentialViewModel(credential, _favIconCollector);
        }

        public IReadOnlyCollection<CredentialViewModel> ProvideAllNew()
        {
            return _credentialsCryptoService.Credentials
                .Select(cr => ProvideNew(cr))
                .ToList();
        }
    }
}
