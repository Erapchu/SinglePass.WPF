using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Services;
using PasswordManager.Helpers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class SyncService
    {
        private readonly CloudServiceProvider _cloudServiceProvider;

        public SyncService(CloudServiceProvider cloudServiceProvider)
        {
            _cloudServiceProvider = cloudServiceProvider;
        }

        public async Task Synchronize()
        {
            var cloudService = _cloudServiceProvider.GetCloudService(CloudType.GoogleDrive);
            using var fileStream = File.Open(Constants.PasswordsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await cloudService.Upload(fileStream, Constants.PasswordsFileName, CancellationToken.None);
        }
    }
}
