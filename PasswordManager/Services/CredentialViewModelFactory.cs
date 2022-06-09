using PasswordManager.Models;
using PasswordManager.ViewModels;

namespace PasswordManager.Services
{
    public class CredentialViewModelFactory
    {
        private readonly FavIconService _favIconService;

        public CredentialViewModelFactory(FavIconService favIconService)
        {
            _favIconService = favIconService;
        }

        public CredentialViewModel ProvideNew(Credential credential)
        {
            return new CredentialViewModel(credential, _favIconService);
        }
    }
}
