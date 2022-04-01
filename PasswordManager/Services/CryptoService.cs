using PasswordManager.Helpers;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PasswordManager.Services
{
    public class CryptoService
    {
        private readonly byte[] _predefinedKeyPart = new byte[AesCryptographyHelper.KeyLength]
        {
            97, 238, 238, 23, 235, 212, 131, 197, 191, 5, 236, 111, 81, 47, 125, 191,
            211, 41, 121, 148, 132, 70, 204, 94, 133, 220, 255, 225, 169, 242, 67, 114
        };

        public CryptoService()
        {

        }

        public T DecryptFromStream<T>(Stream stream, string password)
        {
            stream.Seek(0, SeekOrigin.Begin);

            var br = new BinaryReader(stream);
            var ivLength = AesCryptographyHelper.IVLength;
            var ivBytes = new byte[ivLength];
            br.Read(ivBytes);
            var encryptedBytes = new byte[stream.Length - ivLength];
            br.Read(encryptedBytes);

            // During loading, it's required to set key bytes for future
            var keyBytes = GetRestructuredKeyBytes(password);

            var jsonText = AesCryptographyHelper.DecryptStringFromBytes(encryptedBytes, keyBytes, ivBytes);
            return JsonSerializer.Deserialize<T>(jsonText);
        }

        public void EncryptToStream<T>(T obj, Stream stream, string password)
        {
            stream.Seek(0, SeekOrigin.Begin);

            var keyBytes = GetRestructuredKeyBytes(password);

            // Generate new IV for each new saving
            using var aesObj = Aes.Create();
            var ivBytes = aesObj.IV;

            // Get copy and serialize
            var jsonText = JsonSerializer.Serialize(obj);
            var encryptedBytes = AesCryptographyHelper.EncryptStringToBytes(jsonText, keyBytes, ivBytes);

            using var bw = new BinaryWriter(stream);
            bw.Write(ivBytes);
            bw.Write(encryptedBytes);
        }

        /// <summary>
        /// Replaces pre-defined key bytes according to user password.
        /// </summary>
        /// <param name="password">User password.</param>
        private byte[] GetRestructuredKeyBytes(string password)
        {
            var keyBytes = new byte[AesCryptographyHelper.KeyLength];
            Array.Copy(_predefinedKeyPart, keyBytes, AesCryptographyHelper.KeyLength);

            var passBytes = Encoding.UTF8.GetBytes(password);
            for (int i = 0; i < passBytes.Length; i++)
            {
                keyBytes[i] = passBytes[i];
            }
            return keyBytes;
        }
    }
}
