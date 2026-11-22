using Microsoft.Extensions.Logging;
using SinglePass.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.Core.Services
{
    /// <summary>A key the agent offers: an SSH key entry of the vault.</summary>
    public sealed record SshAgentIdentity(Guid CredentialId, string Name, byte[] PublicKeyBlob);

    /// <summary>
    /// The program connected to the agent. <see cref="Chain"/> starts with the program itself and goes up
    /// through the programs that started it, for example <c>ssh.exe</c>, <c>git.exe</c>, <c>Code.exe</c>.
    /// </summary>
    public sealed record SshAgentClient(int ProcessId, IReadOnlyList<SshAgentClientProcess> Chain)
    {
        public static SshAgentClient Unknown { get; } = new(0, []);

        public string DisplayName => Chain.Count == 0 ? "?" : string.Join(" ← ", Chain.Select(p => p.Name));
    }

    public sealed record SshAgentClientProcess(int Id, string Name, string Path);

    /// <summary>Where the agent takes keys from and who allows their use. Implemented by the application.</summary>
    public interface ISshAgentKeyStore
    {
        /// <summary>Keys to offer; empty while the vault is locked. May wait for the user to unlock it.</summary>
        Task<IReadOnlyList<SshAgentIdentity>> GetIdentitiesAsync(SshAgentClient client, CancellationToken cancellationToken);

        /// <summary>
        /// Asks the user whether <paramref name="client"/> may sign with the key. Returns the key data
        /// when allowed, null when denied or when the entry is gone.
        /// </summary>
        Task<SshKeyData> AuthorizeAsync(SshAgentIdentity identity, SshAgentClient client, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The SSH agent protocol (draft-miller-ssh-agent): lists the SSH keys of the vault and signs with them.
    /// Keys are never handed out, and keys cannot be added through the agent.
    /// </summary>
    public sealed class SshAgent
    {
        internal const byte Failure = 5;
        internal const byte RequestIdentities = 11;
        internal const byte IdentitiesAnswer = 12;
        internal const byte SignRequest = 13;
        internal const byte SignResponse = 14;

        /// <summary>Largest message accepted. Real requests are a few kilobytes.</summary>
        internal const int MaxMessageLength = 256 * 1024;

        private readonly ISshAgentKeyStore _keyStore;
        private readonly ILogger _logger;

        public SshAgent(ISshAgentKeyStore keyStore, ILogger<SshAgent> logger)
        {
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _logger = logger;
        }

        /// <summary>Answers requests on <paramref name="stream"/> until the client disconnects.</summary>
        public async Task ServeAsync(Stream stream, SshAgentClient client, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false))
                    return;

                var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header);
                if (length is 0 or > MaxMessageLength)
                    return;

                var request = new byte[length];
                if (!await ReadExactlyAsync(stream, request, cancellationToken).ConfigureAwait(false))
                    return;

                var response = await HandleAsync(request, client, cancellationToken).ConfigureAwait(false);
                var framed = new byte[4 + response.Length];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(framed, (uint)response.Length);
                response.CopyTo(framed, 4);
                await stream.WriteAsync(framed, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>Answers one request (without the length prefix).</summary>
        internal async Task<byte[]> HandleAsync(byte[] request, SshAgentClient client, CancellationToken cancellationToken)
        {
            try
            {
                var reader = new SshReader(request);
                return reader.ReadByte() switch
                {
                    RequestIdentities => await ListIdentitiesAsync(client, cancellationToken).ConfigureAwait(false),
                    SignRequest => await SignAsync(reader, client, cancellationToken).ConfigureAwait(false),
                    // Extensions such as session-bind@openssh.com are optional: clients go on without them.
                    _ => [Failure]
                };
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
            {
                _logger?.LogWarning("Malformed SSH agent request from {Client}", client.DisplayName);
                return [Failure];
            }
        }

        private async Task<byte[]> ListIdentitiesAsync(SshAgentClient client, CancellationToken cancellationToken)
        {
            var identities = await _keyStore.GetIdentitiesAsync(client, cancellationToken).ConfigureAwait(false);
            var writer = new SshWriter();
            writer.WriteByte(IdentitiesAnswer);
            writer.WriteUInt32((uint)identities.Count);
            foreach (var identity in identities)
            {
                writer.WriteBytes(identity.PublicKeyBlob);
                writer.WriteBytes(Encoding.UTF8.GetBytes(identity.Name ?? string.Empty));
            }
            return writer.ToArray();
        }

        private async Task<byte[]> SignAsync(SshReader reader, SshAgentClient client, CancellationToken cancellationToken)
        {
            var publicKey = reader.ReadBytes();
            var data = reader.ReadBytes();
            var flags = reader.ReadUInt32();

            var identities = await _keyStore.GetIdentitiesAsync(client, cancellationToken).ConfigureAwait(false);
            var identity = identities.FirstOrDefault(item => item.PublicKeyBlob.AsSpan().SequenceEqual(publicKey));
            if (identity == null)
                return [Failure];

            var keyData = await _keyStore.AuthorizeAsync(identity, client, cancellationToken).ConfigureAwait(false);
            if (keyData == null)
            {
                _logger?.LogInformation("Use of SSH key {Key} by {Client} was not allowed", identity.Name, client.DisplayName);
                return [Failure];
            }

            try
            {
                var key = SshSigningKey.Load(keyData.PrivateKey, keyData.Passphrase);
                if (!key.PublicKeyBlob.AsSpan().SequenceEqual(publicKey))
                {
                    _logger?.LogWarning("SSH key {Key} does not match its public key", identity.Name);
                    return [Failure];
                }

                var writer = new SshWriter();
                writer.WriteByte(SignResponse);
                writer.WriteBytes(key.Sign(data, flags));
                _logger?.LogInformation("SSH key {Key} signed for {Client}", identity.Name, client.DisplayName);
                return writer.ToArray();
            }
            catch (SshKeyException ex)
            {
                _logger?.LogWarning("SSH key {Key} cannot sign: {Reason}", identity.Name, ex.Message);
                return [Failure];
            }
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            try
            {
                await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>
        /// Public key of an SSH key entry in the wire format, or null when the entry has no usable key.
        /// Taken from the private key when it can be read without the passphrase, otherwise from the
        /// public key of the entry; signing checks that the two match.
        /// </summary>
        public static byte[] TryGetPublicKeyBlob(SshKeyData key)
        {
            if (key == null || string.IsNullOrWhiteSpace(key.PrivateKey))
                return null;

            var publicKey = SshKeys.TryGetPublicKey(key.PrivateKey, out var derived) ? derived : key.PublicKey;
            var parts = publicKey?.Trim().Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts is not { Length: >= 2 } || !SshKeys.TryParsePublicKey(publicKey, out _, out _, out _))
                return null;

            return Convert.FromBase64String(parts[1]);
        }
    }
}
