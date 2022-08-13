using System.Text.Json.Serialization;

namespace SinglePass.WPF.Clouds.Models
{
    public class GoogleUserInfo
    {
        [JsonPropertyName("sub")]
        public string Sub { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("given_name")]
        public string GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string FamilyName { get; set; }

        [JsonPropertyName("picture")]
        public string Picture { get; set; }

        [JsonPropertyName("locale")]
        public string Locale { get; set; }

        public BaseUserInfo ToBaseUserInfo()
        {
            var baseInfo = new BaseUserInfo()
            {
                ProfileUrl = Picture,
                UserName = Name
            };
            return baseInfo;
        }
    }
}
