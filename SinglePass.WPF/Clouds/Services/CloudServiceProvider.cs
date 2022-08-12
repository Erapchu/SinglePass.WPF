using SinglePass.WPF.Cloud.Enums;
using System;

namespace SinglePass.WPF.Clouds.Services
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
                _ => null,
            };
        }
    }
}
