using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Models;

namespace PasswordManager.Models
{
    public class CloudSettings
    {
        public CloudType CloudType { get; set; }
        public bool Enabled { get; set; }
        public BaseUserInfo UserInfo { get; set; } = new BaseUserInfo();

        public CloudSettings()
        {

        }

        public CloudSettings(CloudType cloudType)
        {
            CloudType = cloudType;
        }
    }
}
