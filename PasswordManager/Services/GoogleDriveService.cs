using PasswordManager.Authorization.Responses;

namespace PasswordManager.Services
{
    public class GoogleDriveService
    {
        private readonly GoogleDriveTokenHolder _googleDriveTokenHolder;

        public GoogleDriveService(GoogleDriveTokenHolder googleDriveTokenHolder)
        {
            _googleDriveTokenHolder = googleDriveTokenHolder;
        }

        public void Synchronize()
        {

        }
    }
}
