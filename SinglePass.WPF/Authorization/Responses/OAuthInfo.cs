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
        public DateTimeOffset ExpirationTime => CreationTime + TimeSpan.FromSeconds(ExpiresIn);

        public OAuthInfo()
        {
        }

        /// <summary>
        /// Whether the access token can be used for at least one more request,
        /// with a margin so it does not expire in flight.
        /// </summary>
        public bool HasUsableAccessToken()
        {
            return !string.IsNullOrWhiteSpace(AccessToken)
                && ExpirationTime > DateTimeOffset.Now.AddMinutes(1);
        }
    }
}
