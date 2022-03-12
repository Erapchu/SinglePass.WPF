using System;

namespace PasswordManager.Authorization.Interfaces
{
    public interface ITokenResponse
    {
        string AccessToken { get; }
        string RefreshToken { get; }
        DateTime ExpirationDate { get; }
        bool RefreshRequired { get; }
    }
}
