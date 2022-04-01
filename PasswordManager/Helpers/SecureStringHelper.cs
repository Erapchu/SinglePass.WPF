using System;
using System.Runtime.InteropServices;
using System.Security;

namespace PasswordManager.Helpers
{
    public class SecureStringHelper
    {
        public static SecureString MakeSecureString(string sourceString, bool readOnly = true)
        {
            if (sourceString == null)
                throw new ArgumentNullException(nameof(sourceString));

            var secString = new SecureString();
            foreach (var ch in sourceString)
            {
                secString.AppendChar(ch);
            }

            if (readOnly)
            {
                secString.MakeReadOnly();
            }

            return secString;
        }

        public static string GetString(SecureString secString)
        {
            if (secString == null)
                throw new ArgumentNullException(nameof(secString));

            IntPtr bstr = Marshal.SecureStringToBSTR(secString);

            try
            {
                string source = Marshal.PtrToStringBSTR(bstr);
                return source;
            }
            finally
            {
                Marshal.ZeroFreeBSTR(bstr);
            }
        }
    }
}
