using Erapkey.Core.Models;
using Erapkey.Core.Services;
using Erapkey.Core.Services.Browser;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Erapkey.Core.Tests
{
    public sealed class BrowserPipeServerTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "erapkey-tests-" + Guid.NewGuid());
        private readonly FakeBackend _backend = new();
        private readonly BrowserPairingStore _pairings;
        private readonly BrowserPipeServer _server;

        public BrowserPipeServerTests()
        {
            _pairings = new BrowserPairingStore(Path.Combine(_directory, "browserPairings.dat"), null);
            _server = new BrowserPipeServer(_backend, _pairings, new WebsiteMatcher(PublicSuffixList.Default), null,
                "erapkey-browser-test-" + Guid.NewGuid(), _ => true);
            _server.Start();
        }

        public void Dispose()
        {
            _server.Dispose();
            try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
        }

        private static Credential Login(string name, string site, string login = "user", string password = "secret", TotpConfiguration totp = null)
        {
            var credential = Credential.CreateNew();
            credential.NameField.Value = name;
            credential.Login.SiteField.Value = site;
            credential.Login.LoginField.Value = login;
            credential.Login.PasswordField.Value = password;
            credential.Login.Totp = totp;
            return credential;
        }

        [Fact]
        public async Task PairsAndAnswersInSession()
        {
            var example = Login("Example", "example.com", "alice", "p@ss");
            var other = Login("Other", "other.com");
            _backend.Credentials = [example, other];

            await using var extension = await TestExtension.ConnectAsync(_server.PipeName);
            var pairingId = await extension.PairAsync("Chrome", _backend);
            Assert.Equal(_backend.LastPairing.Code, extension.LastPairingCode);
            Assert.Single(_pairings.GetAll());

            await extension.HelloAsync(pairingId);

            var status = await extension.RequestAsync(new JsonObject { ["type"] = "status" });
            Assert.True((bool)status["ok"]);
            Assert.False((bool)status["locked"]);

            var logins = await extension.RequestAsync(new JsonObject { ["type"] = "getLogins", ["url"] = "https://login.example.com/signin" });
            var list = logins["logins"]!.AsArray();
            var only = Assert.Single(list);
            Assert.Equal(example.Id.ToString(), (string)only!["entryId"]);
            Assert.Equal("alice", (string)only["login"]);
            Assert.Equal("domain", (string)only["match"]);
            Assert.Null(only["password"]);

            var credentials = await extension.RequestAsync(new JsonObject
            {
                ["type"] = "getCredentials", ["entryId"] = example.Id.ToString(), ["url"] = "https://example.com/",
            });
            Assert.True((bool)credentials["ok"]);
            Assert.Equal("p@ss", (string)credentials["password"]);

            // The entry ID alone is not enough: the page must match the entry.
            var elsewhere = await extension.RequestAsync(new JsonObject
            {
                ["type"] = "getCredentials", ["entryId"] = example.Id.ToString(), ["url"] = "https://example.com.evil.net/",
            });
            Assert.False((bool)elsewhere["ok"]);
            Assert.Equal("not_found", (string)elsewhere["error"]);
        }

        [Fact]
        public async Task LookAlikePageGetsNoLogins()
        {
            _backend.Credentials = [Login("Example", "https://example.com")];
            await using var extension = await TestExtension.ConnectAsync(_server.PipeName);
            await extension.HelloAsync(await extension.PairAsync("Edge", _backend));

            var logins = await extension.RequestAsync(new JsonObject { ["type"] = "getLogins", ["url"] = "https://example.com.evil.net/" });
            Assert.Empty(logins["logins"]!.AsArray());

            var downgrade = await extension.RequestAsync(new JsonObject { ["type"] = "getLogins", ["url"] = "http://example.com/" });
            Assert.Empty(downgrade["logins"]!.AsArray());
        }

        [Fact]
        public async Task LockedVaultAnswersLocked()
        {
            await using var extension = await TestExtension.ConnectAsync(_server.PipeName);
            await extension.HelloAsync(await extension.PairAsync("Chrome", _backend));

            _backend.Credentials = null;
            var logins = await extension.RequestAsync(new JsonObject { ["type"] = "getLogins", ["url"] = "https://example.com/" });
            Assert.False((bool)logins["ok"]);
            Assert.Equal("locked", (string)logins["error"]);

            var status = await extension.RequestAsync(new JsonObject { ["type"] = "status" });
            Assert.True((bool)status["locked"]);
        }

        [Fact]
        public async Task DeniedPairingIsNotStored()
        {
            _backend.AllowPairing = false;
            await using var extension = await TestExtension.ConnectAsync(_server.PipeName);
            var result = await extension.TryPairAsync("Chrome");
            Assert.False((bool)result["ok"]);
            Assert.Equal("denied", (string)result["error"]);
            Assert.Empty(_pairings.GetAll());
        }

        [Fact]
        public async Task UnknownPairingIsNotPaired()
        {
            await using var extension = await TestExtension.ConnectAsync(_server.PipeName);
            var hello = await extension.TryHelloAsync(Guid.NewGuid());
            Assert.False((bool)hello["ok"]);
            Assert.Equal("not_paired", (string)hello["error"]);
        }

        [Fact]
        public async Task OtherVersionIsRefused()
        {
            await using var extension = await TestExtension.ConnectAsync(_server.PipeName);
            await extension.SendPlainAsync(new JsonObject { ["type"] = "hello", ["version"] = 2 });
            var answer = await extension.ReadPlainAsync();
            Assert.Equal("unsupported_version", (string)answer["error"]);
        }

        [Fact]
        public async Task WrongKeyCannotOpenSession()
        {
            await using var first = await TestExtension.ConnectAsync(_server.PipeName);
            var pairingId = await first.PairAsync("Chrome", _backend);

            // Another program knows the pairing ID but not the extension's private key.
            await using var intruder = await TestExtension.ConnectAsync(_server.PipeName);
            await intruder.HelloAsync(pairingId);
            await intruder.SendEncryptedAsync(new JsonObject { ["id"] = 1, ["type"] = "status" });
            Assert.True(await intruder.IsClosedAsync());
        }

        [Fact]
        public async Task ReplayedMessageClosesSession()
        {
            _backend.Credentials = [];
            await using var extension = await TestExtension.ConnectAsync(_server.PipeName);
            await extension.HelloAsync(await extension.PairAsync("Chrome", _backend));
            await extension.RequestAsync(new JsonObject { ["type"] = "status" });

            await extension.ReplayLastAsync();
            Assert.True(await extension.IsClosedAsync());
        }

        [Fact]
        public async Task LockedEventReachesSession()
        {
            _backend.Credentials = [];
            await using var extension = await TestExtension.ConnectAsync(_server.PipeName);
            await extension.HelloAsync(await extension.PairAsync("Chrome", _backend));

            _server.NotifyLocked();
            var message = await extension.ReadEncryptedAsync();
            Assert.Equal("locked", (string)message["event"]);
        }

        [Fact]
        public void PairingCode_IsSixDigits()
        {
            using var a = BrowserCrypto.CreateKey();
            using var b = BrowserCrypto.CreateKey();
            var code = BrowserCrypto.PairingCode(a.ExportSubjectPublicKeyInfo(), b.ExportSubjectPublicKeyInfo());
            Assert.Matches("^[0-9]{6}$", code);
        }

        [Fact]
        public void BrowserName_SkipsCmd()
        {
            var client = new PipeClient(10,
            [
                new PipeClientProcess(10, "Erapkey.NativeHost.exe", null),
                new PipeClientProcess(9, "cmd.exe", null),
                new PipeClientProcess(8, "chrome.exe", null),
            ]);
            Assert.Equal("chrome.exe", BrowserPipeServer.GetBrowserName(client));
        }

        private sealed class FakeBackend : IBrowserBackend
        {
            public IReadOnlyList<Credential> Credentials { get; set; } = [];
            public bool AllowPairing { get; set; } = true;
            public BrowserPairingRequest LastPairing { get; private set; }

            public Task<IReadOnlyList<Credential>> GetCredentialsAsync(CancellationToken cancellationToken) => Task.FromResult(Credentials);
            public Task<bool> UnlockAsync(CancellationToken cancellationToken) => Task.FromResult(Credentials != null);

            public Task<bool> ConfirmPairingAsync(BrowserPairingRequest request, CancellationToken cancellationToken)
            {
                LastPairing = request;
                return Task.FromResult(AllowPairing);
            }

            public Task OpenEntryAsync(Guid entryId, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        /// <summary>Plays the extension: the same steps the TypeScript code does with WebCrypto.</summary>
        private sealed class TestExtension : IAsyncDisposable
        {
            private readonly NamedPipeClientStream _pipe;
            private readonly ECDiffieHellman _static = BrowserCrypto.CreateKey();
            private byte[] _appStaticPublic;
            private byte[] _sendKey, _receiveKey;
            private ulong _sendCounter, _receiveCounter;
            private byte[] _lastFrame;
            private int _nextId = 1;

            public string LastPairingCode { get; private set; }

            private TestExtension(NamedPipeClientStream pipe) => _pipe = pipe;

            public static async Task<TestExtension> ConnectAsync(string pipeName)
            {
                var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(5000);
                return new TestExtension(pipe);
            }

            public async ValueTask DisposeAsync()
            {
                _static.Dispose();
                await _pipe.DisposeAsync();
            }

            public async Task<Guid> PairAsync(string name, FakeBackend backend)
            {
                var result = await TryPairAsync(name);
                Assert.True((bool)result["ok"], result.ToJsonString());
                return Guid.Parse((string)result["pairingId"]);
            }

            public async Task<JsonObject> TryPairAsync(string name)
            {
                var publicKey = _static.ExportSubjectPublicKeyInfo();
                await SendPlainAsync(new JsonObject
                {
                    ["type"] = "associate", ["version"] = 1, ["name"] = name, ["publicKey"] = Convert.ToBase64String(publicKey),
                });
                var pairing = await ReadPlainAsync();
                Assert.Equal("pairing", (string)pairing["type"]);
                _appStaticPublic = Convert.FromBase64String((string)pairing["appPublicKey"]);
                LastPairingCode = BrowserCrypto.PairingCode(_appStaticPublic, publicKey);
                return await ReadPlainAsync();
            }

            public async Task HelloAsync(Guid pairingId)
            {
                var hello = await TryHelloAsync(pairingId);
                Assert.True((bool)hello["ok"], hello.ToJsonString());
            }

            public async Task<JsonObject> TryHelloAsync(Guid pairingId)
            {
                using var ephemeral = BrowserCrypto.CreateKey();
                var nonce = RandomNumberGenerator.GetBytes(16);
                await SendPlainAsync(new JsonObject
                {
                    ["type"] = "hello", ["version"] = 1, ["pairingId"] = pairingId.ToString(),
                    ["ephemeralKey"] = Convert.ToBase64String(ephemeral.ExportSubjectPublicKeyInfo()),
                    ["nonce"] = Convert.ToBase64String(nonce),
                });
                var answer = await ReadPlainAsync();
                if (!(bool)answer["ok"])
                    return answer;

                // The app's static key is known only after pairing on this object; an intruder uses its own pairing-less key.
                using var appStatic = BrowserCrypto.ImportPublicKey(_appStaticPublic ?? _static.ExportSubjectPublicKeyInfo());
                using var appEphemeral = BrowserCrypto.ImportPublicKey(Convert.FromBase64String((string)answer["ephemeralKey"]));
                // ECDH is symmetric: the extension's private keys with the app's public keys give the same secrets.
                var (extensionToApp, appToExtension) = BrowserCrypto.DeriveSessionKeys(
                    _static, appStatic, ephemeral, appEphemeral, nonce, Convert.FromBase64String((string)answer["nonce"]));
                _sendKey = extensionToApp;
                _receiveKey = appToExtension;
                return answer;
            }

            public async Task<JsonObject> RequestAsync(JsonObject request)
            {
                var id = _nextId++;
                request["id"] = id;
                await SendEncryptedAsync(request);
                while (true)
                {
                    var message = await ReadEncryptedAsync();
                    if (message["id"] is JsonValue value && (int)value == id)
                        return message;
                }
            }

            public async Task SendEncryptedAsync(JsonObject message)
            {
                var sealedMessage = BrowserCrypto.Seal(_sendKey, _sendCounter, JsonSerializer.SerializeToUtf8Bytes(message));
                await SendPlainAsync(new JsonObject { ["n"] = _sendCounter, ["c"] = Convert.ToBase64String(sealedMessage) });
                _sendCounter++;
            }

            public async Task<JsonObject> ReadEncryptedAsync()
            {
                var envelope = await ReadPlainAsync();
                Assert.Equal(_receiveCounter, (ulong)envelope["n"]);
                var plaintext = BrowserCrypto.Open(_receiveKey, _receiveCounter++, Convert.FromBase64String((string)envelope["c"]));
                return JsonNode.Parse(plaintext)!.AsObject();
            }

            public Task ReplayLastAsync() => WriteFrameAsync(_lastFrame);

            public async Task<bool> IsClosedAsync()
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    var buffer = new byte[1];
                    return await _pipe.ReadAsync(buffer, timeout.Token) == 0;
                }
                catch (IOException)
                {
                    return true;
                }
            }

            public async Task SendPlainAsync(JsonObject message)
            {
                _lastFrame = JsonSerializer.SerializeToUtf8Bytes(message);
                await WriteFrameAsync(_lastFrame);
            }

            private async Task WriteFrameAsync(byte[] payload)
            {
                var header = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
                await _pipe.WriteAsync(header);
                await _pipe.WriteAsync(payload);
                await _pipe.FlushAsync();
            }

            public async Task<JsonObject> ReadPlainAsync()
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var header = new byte[4];
                await _pipe.ReadExactlyAsync(header, timeout.Token);
                var payload = new byte[BinaryPrimitives.ReadUInt32BigEndian(header)];
                await _pipe.ReadExactlyAsync(payload, timeout.Token);
                return JsonNode.Parse(payload)!.AsObject();
            }
        }
    }
}
