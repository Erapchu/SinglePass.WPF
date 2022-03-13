using PasswordManager.Authorization.Interfaces;
using System;
using System.Text.Json.Serialization;

namespace PasswordManager.Authorization.Responses
{
    public class GoogleDriveTokenResponse : ITokenResponse
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
        public bool RefreshRequired => ExpirationDate <= DateTime.Now;
    }
}
