using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Helpers
{
    internal class HashHelper
    {
        /// <summary>
        /// Get SHA256 hash for string.
        /// </summary>
        /// <param name="text">Usual text.</param>
        /// <returns>SHA256 hash representation.</returns>
        public static string GetHash(string text)
        {
            var passPathBytes = Encoding.UTF8.GetBytes(text);
            var hashData = SHA256.HashData(passPathBytes);
            var hashedPath = Encoding.UTF8.GetString(hashData);
            return hashedPath;
        }
    }
}
