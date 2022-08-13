using System;

namespace SinglePass.WPF.Authorization.Responses
{
    public interface ITokenResponse
    {
        string AccessToken { get; init; }
        string RefreshToken { get; init; }
        DateTime ExpirationDate { get; }
        bool RefreshRequired { get; }
    }
}
