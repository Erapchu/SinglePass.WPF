using SinglePass.Core.Clouds.Enums;
using System;

namespace SinglePass.Core.Clouds.Services
{
    public class CloudServiceProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public CloudServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ICloudService GetCloudService(CloudType cloudType)
        {
            return cloudType switch
            {
                CloudType.GoogleDrive => _serviceProvider.GetService(typeof(GoogleDriveCloudService)) as ICloudService,
                CloudType.YandexDisk => _serviceProvider.GetService(typeof(YandexDiskCloudService)) as ICloudService,
                _ => null,
            };
        }
    }
}
