namespace SinglePass.WPF.Models
{
    public enum TotpAlgorithm { Sha1, Sha256, Sha512 }

    public sealed class TotpConfiguration
    {
        public string SecretBase32 { get; init; }
        public TotpAlgorithm Algorithm { get; init; }
        public int Digits { get; init; } = 6;
        public int PeriodSeconds { get; init; } = 30;
    }
}
