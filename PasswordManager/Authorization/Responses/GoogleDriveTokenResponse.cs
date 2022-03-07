using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PasswordManager.Authorization.Responses
{
    public class GoogleDriveTokenResponse
    {
        [JsonPropertyName("init_date")]
        public DateTime InitDate { get; init; } = DateTime.Now;

        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; }

        [JsonPropertyName("scope")]
        public string Scope { get; init; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; }

        [JsonIgnore]
        public DateTime ExpirationDate => InitDate.AddSeconds(ExpiresIn);

        [JsonIgnore]
        public bool RefreshRequired => ExpirationDate <= DateTime.UtcNow;
    }
}
