using System;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Gets hash string for current string using the specified hash algorithm and UTF-8 encoding.
        /// </summary>
        /// <typeparam name="T">A type of hash algorithm being used for hash computing.</typeparam>
        /// <param name="sourceString">Source string.</param>
        /// <returns>Returns hash-string of the source string.</returns>
        public static string GetHashString<T>(this string sourceString) where T : HashAlgorithm, new()
        {
            if (sourceString == null)
                throw new ArgumentNullException(nameof(sourceString));

            return GetHashString<T>(sourceString, Encoding.UTF8);
        }

        /// <summary>
        /// Gets hash string for current string using the specified hash algorithm and encoding.
        /// </summary>
        /// <typeparam name="T">A type of hash algorithm being used for hash computing.</typeparam>
        /// <param name="sourceString">Source string.</param>
        /// <param name="encoding">Encoding to be used for obtaining the specified string bytes.</param>
        /// <returns>Returns hash-string of the source string.</returns>
        public static string GetHashString<T>(this string sourceString, Encoding encoding) where T : HashAlgorithm, new()
        {
            if (sourceString == null)
                throw new ArgumentNullException(nameof(sourceString));

            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            using var hash = new T();
            var sourceBytes = encoding.GetBytes(sourceString);
            var shaBytes = hash.ComputeHash(sourceBytes);
            var shaString = BitConverter.ToString(shaBytes).Replace("-", "");
            return shaString;
        }
    }
}
