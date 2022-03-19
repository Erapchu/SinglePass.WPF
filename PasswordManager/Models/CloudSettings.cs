using PasswordManager.Cloud.Enums;
using PasswordManager.Clouds.Models;

namespace PasswordManager.Models
{
    public class CloudSettings
    {
        public BaseUserInfo UserInfo { get; set; }

        public CloudSettings()
        {
            UserInfo = new BaseUserInfo();
        }
    }
}
