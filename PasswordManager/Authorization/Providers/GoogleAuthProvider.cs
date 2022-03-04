using PasswordManager.Authorization.Helpers;
using System;
using System.Web;

namespace PasswordManager.Authorization.Providers
{
    public class GoogleAuthProvider : BaseAuthProvider
    {
        private readonly string _scopes;

        public GoogleAuthProvider()
        {
            ClientId = "977481544425-32f220l78p3tpmg8t0bu5un78nhhvp34.apps.googleusercontent.com";
            ClientSecret = "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";
            _scopes = "https://www.googleapis.com/auth/drive.file";
        }

        protected override string BuildAuthorizationUri(string redirectUri)
        {
            return "https://accounts.google.com/o/oauth2/v2/auth?" +
                $"scope={HttpUtility.UrlEncode(_scopes)}&" +
                "access_type=offline&" +
                "include_granted_scopes=true&" +
                "response_type=code&" +
                "state=state_parameter_passthrough_value&" +
                $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}&" +
                $"client_id={ClientId}";
        }

        protected override string BuildRedirectUri()
        {
            var unusedPort = PortHelper.GetRandomUnusedPort();
            return $"http://localhost:{unusedPort}/";
        }

        protected override string BuildRefreshAccessTokenUri()
        {
            throw new NotImplementedException();
        }

        protected override string BuildRequestForToken(string code, string redirectUri)
        {
            return $"code={code}&" +
                $"client_id={ClientId}&" +
                $"client_secret={ClientSecret}&" +
                $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}&" +
                $"grant_type=authorization_code";
        }

        protected override string BuildTokenEndpointUri()
        {
            return "https://oauth2.googleapis.com/token";
        }
    }
}
