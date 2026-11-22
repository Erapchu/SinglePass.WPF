using System;

namespace SinglePass.Core.Models
{
    /// <summary>Data of a <see cref="CredentialType.Host"/> entry: an SSH server to connect to.</summary>
    public sealed class HostData
    {
        public const int DefaultPort = 22;

        public string Hostname { get; set; }

        /// <summary>SSH port; null means <see cref="DefaultPort"/>.</summary>
        public int? Port { get; set; }

        public string Username { get; set; }

        /// <summary>
        /// Id of the <see cref="CredentialType.SshKey"/> entry used to sign in, or null. The key entry may
        /// have been deleted since; the host then shows the link as missing.
        /// </summary>
        public Guid? SshKeyId { get; set; }

        public HostData Clone() => new()
        {
            Hostname = Hostname,
            Port = Port,
            Username = Username,
            SshKeyId = SshKeyId,
        };
    }
}
