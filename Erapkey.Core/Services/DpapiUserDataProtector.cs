using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Erapkey.Core.Services
{
    /// <summary>DPAPI with <see cref="DataProtectionScope.CurrentUser"/>.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class DpapiUserDataProtector : IUserDataProtector
    {
        public byte[] Protect(byte[] plaintext) =>
            ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);

        public byte[] Unprotect(byte[] protectedData) =>
            ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
    }
}
