using PasswordManager.Models;
using PasswordManager.ViewModels;

namespace PasswordManager.Services
{
    public class CredentialViewModelFactory
    {
        private readonly IFavIconCollector _favIconCollector;

        public CredentialViewModelFactory(IFavIconCollector favIconCollector)
        {
            _favIconCollector = favIconCollector;
        }

        public CredentialViewModel ProvideNew(Credential credential)
        {
            return new CredentialViewModel(credential, _favIconCollector);
        }
    }
}
