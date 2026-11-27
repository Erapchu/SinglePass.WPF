using System.Security.Cryptography;

namespace Erapkey.Core.Services
{
    /// <summary>
    /// Encrypts small local files (cloud sessions, browser pairings) so that only the current OS user on this device
    /// can read them: DPAPI on Windows (<see cref="DpapiUserDataProtector"/>), the Keychain on macOS and iOS.
    /// </summary>
    public interface IUserDataProtector
    {
        byte[] Protect(byte[] plaintext);

        /// <exception cref="CryptographicException">The data was protected by another user or device, or is damaged.</exception>
        byte[] Unprotect(byte[] protectedData);
    }
}
