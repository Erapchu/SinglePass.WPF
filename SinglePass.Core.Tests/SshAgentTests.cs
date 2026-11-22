using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using SinglePass.Core.Models;
using SinglePass.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SinglePass.Core.Tests
{
    public class SshAgentTests
    {
        [Fact]
        public void BlowfishInitialStateIsPi()
        {
            // First and last words of the P-array and of each S-box, from the Blowfish specification.
            var state = BcryptPbkdf.InitialStateForTests;
            Assert.Equal(0x243F6A88u, state[0]);
            Assert.Equal(0x8979FB1Bu, state[17]);
            Assert.Equal(0xD1310BA6u, state[18]);
            Assert.Equal(0x6E85076Au, state[18 + 255]);
            Assert.Equal(0x4B7A70E9u, state[18 + 256]);
            Assert.Equal(0xDB83ADF7u, state[18 + 511]);
            Assert.Equal(0xE93D5A68u, state[18 + 512]);
            Assert.Equal(0x3AC372E6u, state[18 + 1023]);
        }

        [Fact]
        public void BcryptPbkdfMatchesReference()
        {
            // Vector from the reference implementation tests (password "password", salt "salt", 4 rounds).
            var key = BcryptPbkdf.Derive(Encoding.ASCII.GetBytes("password"), Encoding.ASCII.GetBytes("salt"), 4, 32);
            Assert.Equal("5bbf0cc293587f1c3635555c27796598d47e579071bf427e9d8fbe842aba34d9", Convert.ToHexStringLower(key));
        }

        [Theory]
        [MemberData(nameof(SshTestKeys.All), MemberType = typeof(SshTestKeys))]
        public void LoadsKeysMadeBySshKeygen(string name)
        {
            var test = SshTestKeys.Get(name);

            var key = SshSigningKey.Load(test.PrivateKey, test.Passphrase);

            Assert.Equal(PublicBlob(test.PublicKey), key.PublicKeyBlob);
            Assert.Equal(test.PublicKey.Split(' ')[0], key.KeyType);
        }

        [Theory]
        [MemberData(nameof(SshTestKeys.All), MemberType = typeof(SshTestKeys))]
        public void SignaturesVerify(string name)
        {
            var test = SshTestKeys.Get(name);
            var key = SshSigningKey.Load(test.PrivateKey, test.Passphrase);
            var data = Encoding.UTF8.GetBytes("session data to sign");

            foreach (var flags in new uint[] { 0, SshSigningKey.RsaSha256Flag, SshSigningKey.RsaSha512Flag })
                Assert.True(Verify(PublicBlob(test.PublicKey), data, key.Sign(data, flags), flags), $"flags {flags}");
        }

        [Theory]
        [InlineData(nameof(SshTestKeys.Ed25519Ctr))]
        [InlineData(nameof(SshTestKeys.RsaGcm))]
        [InlineData(nameof(SshTestKeys.EcdsaP521Pkcs8))]
        public void WrongOrMissingPassphraseFails(string name)
        {
            var test = SshTestKeys.Get(name);

            Assert.Throws<SshKeyException>(() => SshSigningKey.Load(test.PrivateKey, "wrong passphrase"));
            Assert.Throws<SshKeyException>(() => SshSigningKey.Load(test.PrivateKey, null));
        }

        [Fact]
        public void LoadsGeneratedKey()
        {
            var (privateKey, publicKey) = SshKeys.GenerateEd25519("test");

            var key = SshSigningKey.Load(privateKey, null);

            Assert.Equal(PublicBlob(publicKey), key.PublicKeyBlob);
        }

        [Fact]
        public async Task ListsIdentities()
        {
            var store = new FakeKeyStore(SshTestKeys.Ed25519Ctr, SshTestKeys.RsaPem);
            var agent = new SshAgent(store, null);

            var response = new Reader(await agent.HandleAsync([SshAgent.RequestIdentities], SshAgentClient.Unknown, default));

            Assert.Equal(SshAgent.IdentitiesAnswer, response.Byte());
            Assert.Equal(2u, response.UInt32());
            Assert.Equal(PublicBlob(SshTestKeys.Ed25519Ctr.PublicKey), response.Bytes());
            Assert.Equal(nameof(SshTestKeys.Ed25519Ctr), Encoding.UTF8.GetString(response.Bytes()));
            Assert.Equal(PublicBlob(SshTestKeys.RsaPem.PublicKey), response.Bytes());
            Assert.Equal(nameof(SshTestKeys.RsaPem), Encoding.UTF8.GetString(response.Bytes()));
        }

        [Fact]
        public async Task SignsWhenAllowed()
        {
            var store = new FakeKeyStore(SshTestKeys.EcdsaP256Ctr);
            var agent = new SshAgent(store, null);
            var blob = PublicBlob(SshTestKeys.EcdsaP256Ctr.PublicKey);
            var data = Encoding.UTF8.GetBytes("data");

            var response = new Reader(await agent.HandleAsync(SignRequest(blob, data, 0), SshAgentClient.Unknown, default));

            Assert.Equal(SshAgent.SignResponse, response.Byte());
            Assert.True(Verify(blob, data, response.Bytes(), 0));
            Assert.Equal(1, store.AuthorizeCalls);
        }

        [Fact]
        public async Task RefusesWhenDenied()
        {
            var store = new FakeKeyStore(SshTestKeys.EcdsaP256Ctr) { Allow = false };
            var agent = new SshAgent(store, null);
            var blob = PublicBlob(SshTestKeys.EcdsaP256Ctr.PublicKey);

            var response = await agent.HandleAsync(SignRequest(blob, [1, 2, 3], 0), SshAgentClient.Unknown, default);

            Assert.Equal([SshAgent.Failure], response);
        }

        [Fact]
        public async Task RefusesUnknownKeyWithoutAsking()
        {
            var store = new FakeKeyStore(SshTestKeys.EcdsaP256Ctr);
            var agent = new SshAgent(store, null);

            var response = await agent.HandleAsync(SignRequest(PublicBlob(SshTestKeys.RsaPem.PublicKey), [1], 0),
                SshAgentClient.Unknown, default);

            Assert.Equal([SshAgent.Failure], response);
            Assert.Equal(0, store.AuthorizeCalls);
        }

        [Theory]
        [InlineData(new byte[] { 17 })] // add identity: keys are added in the vault, not through the agent
        [InlineData(new byte[] { 27, 0, 0, 0, 1, (byte)'x' })] // extension
        [InlineData(new byte[] { SshAgent.SignRequest, 0, 0, 0, 200 })] // truncated
        public async Task OtherRequestsFail(byte[] request)
        {
            var agent = new SshAgent(new FakeKeyStore(SshTestKeys.RsaPem), null);

            var response = await agent.HandleAsync(request, SshAgentClient.Unknown, default);

            Assert.Equal([SshAgent.Failure], response);
        }

        [Fact]
        public async Task ServesOverNamedPipe()
        {
            var store = new FakeKeyStore(SshTestKeys.Ed25519Ctr);
            using var server = new SshAgentServer(new SshAgent(store, null), null, "singlepass-test-" + Guid.NewGuid());
            server.Start();

            using var client = new NamedPipeClientStream(".", server.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(5000);
            var blob = PublicBlob(SshTestKeys.Ed25519Ctr.PublicKey);
            var data = Encoding.UTF8.GetBytes("over the pipe");

            await Send(client, [SshAgent.RequestIdentities]);
            var identities = new Reader(await Receive(client));
            await Send(client, SignRequest(blob, data, 0));
            var signature = new Reader(await Receive(client));

            Assert.Equal(SshAgent.IdentitiesAnswer, identities.Byte());
            Assert.Equal(1u, identities.UInt32());
            Assert.Equal(SshAgent.SignResponse, signature.Byte());
            Assert.True(Verify(blob, data, signature.Bytes(), 0));
            Assert.Equal(Environment.ProcessId, store.LastClient.ProcessId);
            Assert.Equal(Environment.ProcessId, store.LastClient.Chain[0].Id);
        }

        [Fact]
        public void SecondServerWithSameNameFails()
        {
            var name = "singlepass-test-" + Guid.NewGuid();
            using var first = new SshAgentServer(new SshAgent(new FakeKeyStore(), null), null, name);
            using var second = new SshAgentServer(new SshAgent(new FakeKeyStore(), null), null, name);
            first.Start();

            Assert.ThrowsAny<IOException>(second.Start);
            first.Stop();
            second.Start();
        }

        [Fact]
        public void PublicKeyBlobFallsBackToStoredPublicKey()
        {
            // An encrypted PEM key cannot give its public key without the passphrase.
            var key = new SshKeyData { PrivateKey = SshTestKeys.EcdsaP521Pkcs8.PrivateKey, PublicKey = SshTestKeys.EcdsaP521Pkcs8.PublicKey };

            Assert.Equal(PublicBlob(SshTestKeys.EcdsaP521Pkcs8.PublicKey), SshAgent.TryGetPublicKeyBlob(key));
            Assert.Null(SshAgent.TryGetPublicKeyBlob(new SshKeyData { PublicKey = SshTestKeys.RsaPem.PublicKey }));
        }

        private static byte[] PublicBlob(string publicKey) => Convert.FromBase64String(publicKey.Split(' ')[1]);

        private static byte[] SignRequest(byte[] blob, byte[] data, uint flags)
        {
            var writer = new SshWriter();
            writer.WriteByte(SshAgent.SignRequest);
            writer.WriteBytes(blob);
            writer.WriteBytes(data);
            writer.WriteUInt32(flags);
            return writer.ToArray();
        }

        private static async Task Send(Stream stream, byte[] message)
        {
            var writer = new SshWriter();
            writer.WriteBytes(message);
            await stream.WriteAsync(writer.ToArray());
        }

        private static async Task<byte[]> Receive(Stream stream)
        {
            var header = new byte[4];
            await stream.ReadExactlyAsync(header);
            var body = new byte[System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header)];
            await stream.ReadExactlyAsync(body);
            return body;
        }

        /// <summary>Checks an SSH signature with the public key, independently of <see cref="SshSigningKey"/>.</summary>
        private static bool Verify(byte[] publicBlob, byte[] data, byte[] signatureBlob, uint flags)
        {
            var key = new Reader(publicBlob);
            var keyType = key.String();
            var signature = new Reader(signatureBlob);
            var algorithm = signature.String();
            var value = signature.Bytes();

            switch (keyType)
            {
                case "ssh-ed25519":
                    {
                        var verifier = new Ed25519Signer();
                        verifier.Init(false, new Ed25519PublicKeyParameters(key.Bytes()));
                        verifier.BlockUpdate(data, 0, data.Length);
                        return algorithm == keyType && verifier.VerifySignature(value);
                    }
                case "ssh-rsa":
                    {
                        var exponent = key.Bytes();
                        var modulus = key.Bytes();
                        using var rsa = RSA.Create(new RSAParameters
                        {
                            Exponent = exponent.SkipWhile(b => b == 0).ToArray(),
                            Modulus = modulus.SkipWhile(b => b == 0).ToArray()
                        });
                        var (expected, hash) = (flags & SshSigningKey.RsaSha512Flag) != 0 ? ("rsa-sha2-512", HashAlgorithmName.SHA512)
                            : (flags & SshSigningKey.RsaSha256Flag) != 0 ? ("rsa-sha2-256", HashAlgorithmName.SHA256)
                            : ("ssh-rsa", HashAlgorithmName.SHA1);
                        return algorithm == expected && value.Length == rsa.KeySize / 8 &&
                            rsa.VerifyData(data, value, hash, RSASignaturePadding.Pkcs1);
                    }
                default:
                    {
                        var curve = key.String();
                        var point = key.Bytes();
                        var size = (point.Length - 1) / 2;
                        var (ecCurve, hash) = curve switch
                        {
                            "nistp256" => (ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256),
                            "nistp384" => (ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384),
                            _ => (ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512)
                        };
                        using var ecdsa = ECDsa.Create(new ECParameters
                        {
                            Curve = ecCurve,
                            Q = new ECPoint { X = point[1..(1 + size)], Y = point[(1 + size)..] }
                        });
                        var rs = new Reader(value);
                        var r = Fixed(rs.Bytes(), size);
                        var s = Fixed(rs.Bytes(), size);
                        byte[] p1363 = [.. r, .. s];
                        return algorithm == keyType && ecdsa.VerifyData(data, p1363, hash);
                    }
            }

            static byte[] Fixed(byte[] mpint, int size)
            {
                var trimmed = mpint.SkipWhile(b => b == 0).ToArray();
                return [.. new byte[size - trimmed.Length], .. trimmed];
            }
        }

        private sealed class Reader(byte[] data)
        {
            private readonly SshReader _reader = new(data);
            public byte Byte() => _reader.ReadByte();
            public uint UInt32() => _reader.ReadUInt32();
            public byte[] Bytes() => _reader.ReadBytes();
            public string String() => _reader.ReadString();
        }

        private sealed class FakeKeyStore(params SshTestKeys.TestKey[] keys) : ISshAgentKeyStore
        {
            private readonly Dictionary<Guid, SshTestKeys.TestKey> _keys = keys.ToDictionary(_ => Guid.NewGuid());

            public bool Allow { get; init; } = true;
            public int AuthorizeCalls { get; private set; }
            public SshAgentClient LastClient { get; private set; }

            public Task<IReadOnlyList<SshAgentIdentity>> GetIdentitiesAsync(SshAgentClient client, CancellationToken cancellationToken)
            {
                LastClient = client;
                IReadOnlyList<SshAgentIdentity> identities = _keys
                    .Select(pair => new SshAgentIdentity(pair.Key, pair.Value.Name, PublicBlob(pair.Value.PublicKey)))
                    .ToList();
                return Task.FromResult(identities);
            }

            public Task<SshKeyData> AuthorizeAsync(SshAgentIdentity identity, SshAgentClient client, CancellationToken cancellationToken)
            {
                AuthorizeCalls++;
                var key = _keys[identity.CredentialId];
                return Task.FromResult(Allow ? new SshKeyData { PrivateKey = key.PrivateKey, Passphrase = key.Passphrase } : null);
            }
        }
    }
}
