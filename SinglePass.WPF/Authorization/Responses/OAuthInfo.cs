using System;

namespace SinglePass.WPF.Authorization.Responses
{
    public class OAuthInfo
    {
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTimeOffset CreationTime { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }
        public DateTimeOffset ExpirationTime => CreationTime + TimeSpan.FromSeconds(ExpiresIn);

        public OAuthInfo()
        {
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(AccessToken)
                && !string.IsNullOrWhiteSpace(RefreshToken)
                && !string.IsNullOrWhiteSpace(ClientId)
                && !string.IsNullOrWhiteSpace(ClientSecret)
                && !string.IsNullOrWhiteSpace(RedirectUri)
                && ExpirationTime > DateTimeOffset.Now;
        }
    }
}
