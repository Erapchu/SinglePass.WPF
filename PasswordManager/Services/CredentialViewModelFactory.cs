using PasswordManager.Models;
using PasswordManager.ViewModels;

namespace PasswordManager.Services
{
    public class CredentialViewModelFactory
    {
        private readonly FavIconCollector _favIconCollector;

        public CredentialViewModelFactory(FavIconCollector favIconCollector)
        {
            _favIconCollector = favIconCollector;
        }

        public CredentialViewModel ProvideNew(Credential credential)
        {
            return new CredentialViewModel(credential, _favIconCollector);
        }
    }
}
