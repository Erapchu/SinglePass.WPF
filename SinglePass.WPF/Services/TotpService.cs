using OtpNet;
using SinglePass.WPF.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace SinglePass.WPF.Services
{
    internal static class TotpService
    {
        internal static bool TryParse(string input, out TotpConfiguration configuration)
        {
            configuration = null;
            var secret = input.Trim();
            var algorithm = TotpAlgorithm.Sha1;
            var digits = 6;
            var period = 30;

            if (secret.StartsWith("otpauth:", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(secret, UriKind.Absolute, out var uri) || uri.Host != "totp")
                    return false;

                var query = HttpUtility.ParseQueryString(uri.Query);
                secret = query["secret"] ?? string.Empty;
                algorithm = query["algorithm"]?.ToUpperInvariant() switch
                {
                    null or "SHA1" => TotpAlgorithm.Sha1,
                    "SHA256" => TotpAlgorithm.Sha256,
                    "SHA512" => TotpAlgorithm.Sha512,
                    _ => (TotpAlgorithm)(-1)
                };
                if (!int.TryParse(query["digits"] ?? "6", NumberStyles.None, CultureInfo.InvariantCulture, out digits) ||
                    !int.TryParse(query["period"] ?? "30", NumberStyles.None, CultureInfo.InvariantCulture, out period))
                    return false;
            }

            var candidate = new TotpConfiguration
            {
                SecretBase32 = string.Concat(secret.Where(c => !char.IsWhiteSpace(c))).TrimEnd('=').ToUpperInvariant(),
                Algorithm = algorithm,
                Digits = digits,
                PeriodSeconds = period
            };
            if (!TryGetCode(candidate, DateTime.UtcNow, out _, out _))
                return false;

            configuration = candidate;
            return true;
        }

        internal static bool TryGetCode(TotpConfiguration configuration, DateTime utcNow, out string code, out int seconds)
        {
            code = null;
            seconds = 0;
            if (configuration == null || configuration.Digits is not (6 or 8) ||
                configuration.PeriodSeconds <= 0 || !Enum.IsDefined(configuration.Algorithm))
                return false;

            var secret = configuration.SecretBase32;
            if (string.IsNullOrEmpty(secret) || secret.Any(c => c is not (>= 'A' and <= 'Z' or >= '2' and <= '7')))
                return false;

            var key = Base32Encoding.ToBytes(secret);
            try
            {
                if (key.Length == 0 || Base32Encoding.ToString(key).TrimEnd('=') != secret)
                    return false;

                var hash = configuration.Algorithm switch
                {
                    TotpAlgorithm.Sha256 => OtpHashMode.Sha256,
                    TotpAlgorithm.Sha512 => OtpHashMode.Sha512,
                    _ => OtpHashMode.Sha1
                };
                var generator = new Totp(key, step: configuration.PeriodSeconds, mode: hash, totpSize: configuration.Digits);
                code = generator.ComputeTotp(utcNow);
                seconds = generator.RemainingSeconds(utcNow);
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
