namespace SinglePass.Core.Models
{
    /// <summary>SSH key of a <see cref="CredentialType.SshKey"/> entry.</summary>
    public sealed class SshKeyData
    {
        /// <summary>Private key file contents (OpenSSH or PEM), possibly encrypted with <see cref="Passphrase"/>.</summary>
        public string PrivateKey { get; set; }

        /// <summary>Public key in <c>authorized_keys</c> format: <c>type base64 [comment]</c>.</summary>
        public string PublicKey { get; set; }

        public string Passphrase { get; set; }

        public SshKeyData Clone() => new()
        {
            PrivateKey = PrivateKey,
            PublicKey = PublicKey,
            Passphrase = Passphrase,
        };
    }
}
