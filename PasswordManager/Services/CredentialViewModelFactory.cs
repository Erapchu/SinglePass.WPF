using PasswordManager.Models;
using PasswordManager.ViewModels;
using System;
using System.Collections.Concurrent;

namespace PasswordManager.Services
{
    public class CredentialViewModelFactory
    {
        private readonly FavIconCollector _favIconCollector;
        private readonly ConcurrentDictionary<Guid, CredentialViewModel> _credentialVMs;

        public CredentialViewModelFactory(FavIconCollector favIconCollector)
        {
            _favIconCollector = favIconCollector;
            _credentialVMs = new ConcurrentDictionary<Guid, CredentialViewModel>();
        }

        public CredentialViewModel ProvideNew(Credential credential)
        {
            if (_credentialVMs.TryGetValue(credential.Id, out CredentialViewModel res))
                return res;

            var newRes = new CredentialViewModel(credential, _favIconCollector);
            _credentialVMs.TryAdd(newRes.Model.Id, newRes);

            return newRes;
        }

        public bool RemoveCached(Guid id)
        {
            return _credentialVMs.TryRemove(id, out CredentialViewModel _);
        }
    }
}
