using Microsoft.Extensions.Logging;
using Erapkey.Core.Models;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Erapkey.Core.Services.Browser
{
    /// <summary>
    /// One native host connection: plain <c>associate</c> and <c>hello</c>, then encrypted requests and events
    /// (docs/browser-extension.md). Frames are a 32-bit big-endian length and UTF-8 JSON, at most 1 MB.
    /// </summary>
    [SupportedOSPlatform("windows")] // bound to BrowserPipeServer; the protocol itself is portable
    internal sealed class BrowserConnection
    {
        internal const int MaxMessageSize = 1024 * 1024;
        private static readonly TimeSpan PairingTimeout = TimeSpan.FromMinutes(2);

        private readonly BrowserPipeServer _server;
        private readonly Stream _stream;
        private readonly PipeClient _client;
        private readonly SemaphoreSlim _write = new(1, 1);
        private readonly CancellationTokenSource _closed = new();
        private Session _session;

        private sealed class Session
        {
            public Guid PairingId { get; init; }
            public byte[] ReceiveKey { get; init; }
            public byte[] SendKey { get; init; }
            public ulong ReceiveCounter;
            public ulong SendCounter;
        }

        public BrowserConnection(BrowserPipeServer server, Stream stream, PipeClient client)
        {
            _server = server;
            _stream = stream;
            _client = client;
        }

        /// <summary>The pairing of the session, or empty before <c>hello</c>.</summary>
        public Guid PairingId => _session?.PairingId ?? Guid.Empty;

        private ILogger Log => _server.Log;

        public void Close()
        {
            try
            {
                _closed.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closed.Token);
            var token = linked.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var frame = await ReadFrameAsync(token).ConfigureAwait(false);
                    if (frame == null)
                        return;

                    if (_session == null)
                    {
                        if (!await HandlePlainAsync(frame, token).ConfigureAwait(false))
                            return;
                        continue;
                    }

                    var request = Decrypt(frame);
                    if (request == null)
                    {
                        Log?.LogWarning("Browser session of {Client} sent a message that does not decrypt; closing", _client.DisplayName);
                        return;
                    }

                    // Requests run side by side: an unlock that waits for the user must not hold up the others.
                    _ = Task.Run(() => AnswerAsync(request, token), CancellationToken.None);
                }
            }
            finally
            {
                _closed.Dispose();
            }
        }

        public async Task SendEventAsync(string name)
        {
            if (_session == null)
                return;

            try
            {
                await SendEncryptedAsync(new JsonObject { ["event"] = name }, _closed.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
            }
        }

        #region Plain phase: associate, hello

        /// <summary>Returns false when the connection must close.</summary>
        private async Task<bool> HandlePlainAsync(byte[] frame, CancellationToken cancellationToken)
        {
            JsonObject message;
            try
            {
                message = JsonNode.Parse(frame) as JsonObject;
            }
            catch (JsonException)
            {
                message = null;
            }

            var type = Str(message?["type"]);
            switch (type)
            {
                case "associate":
                    await AssociateAsync(message, cancellationToken).ConfigureAwait(false);
                    return true;
                case "hello":
                    return await HelloAsync(message, cancellationToken).ConfigureAwait(false);
                default:
                    await SendPlainAsync(Error(type ?? "unknown", "bad_request"), cancellationToken).ConfigureAwait(false);
                    return false;
            }
        }

        private async Task AssociateAsync(JsonObject message, CancellationToken cancellationToken)
        {
            if (!IsSupportedVersion(message))
            {
                await SendPlainAsync(Error("associated", "unsupported_version"), cancellationToken).ConfigureAwait(false);
                return;
            }

            var name = Truncate(Str(message["name"]), 64) ?? "Browser";
            if (!TryBase64(message["publicKey"], out var extensionPublic))
            {
                await SendPlainAsync(Error("associated", "bad_request"), cancellationToken).ConfigureAwait(false);
                return;
            }

            using var appKey = BrowserCrypto.CreateKey();
            try
            {
                using var extensionKey = BrowserCrypto.ImportPublicKey(extensionPublic);
            }
            catch (CryptographicException)
            {
                await SendPlainAsync(Error("associated", "bad_request"), cancellationToken).ConfigureAwait(false);
                return;
            }

            var appPublic = appKey.ExportSubjectPublicKeyInfo();
            // The extension shows the code from this key while the user compares it with the window.
            await SendPlainAsync(new JsonObject
            {
                ["type"] = "pairing",
                ["version"] = BrowserPipeServer.ProtocolVersion,
                ["appPublicKey"] = Convert.ToBase64String(appPublic),
            }, cancellationToken).ConfigureAwait(false);

            var browser = BrowserPipeServer.GetBrowserName(_client);
            var request = new BrowserPairingRequest(name, browser, BrowserCrypto.PairingCode(appPublic, extensionPublic));
            bool allowed;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(PairingTimeout);
                try
                {
                    allowed = await _server.Backend.ConfirmPairingAsync(request, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    allowed = false;
                }
            }

            if (!allowed)
            {
                Log?.LogInformation("Pairing of {Name} ({Browser}) was not allowed", name, browser);
                await SendPlainAsync(Error("associated", "denied"), cancellationToken).ConfigureAwait(false);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var pairing = new BrowserPairing(Guid.NewGuid(), name, browser,
                Convert.ToBase64String(extensionPublic), Convert.ToBase64String(appKey.ExportPkcs8PrivateKey()), now, now);
            _server.Pairings.Add(pairing);
            Log?.LogInformation("Paired {Name} ({Browser}) as {Id}", name, browser, pairing.Id);

            await SendPlainAsync(new JsonObject
            {
                ["type"] = "associated",
                ["ok"] = true,
                ["pairingId"] = pairing.Id.ToString(),
            }, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> HelloAsync(JsonObject message, CancellationToken cancellationToken)
        {
            if (!IsSupportedVersion(message))
            {
                await SendPlainAsync(Error("hello", "unsupported_version"), cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (!Guid.TryParse(Str(message["pairingId"]), out var pairingId) ||
                !TryBase64(message["ephemeralKey"], out var extensionEphemeralPublic) ||
                !TryBase64(message["nonce"], out var extensionNonce) || extensionNonce.Length != BrowserCrypto.NonceSize)
            {
                await SendPlainAsync(Error("hello", "bad_request"), cancellationToken).ConfigureAwait(false);
                return false;
            }

            var pairing = _server.Pairings.Find(pairingId);
            if (pairing == null)
            {
                await SendPlainAsync(Error("hello", "not_paired"), cancellationToken).ConfigureAwait(false);
                // The extension may pair again on this connection.
                return true;
            }

            byte[] receiveKey, sendKey, appEphemeralPublic;
            var appNonce = RandomNumberGenerator.GetBytes(BrowserCrypto.NonceSize);
            try
            {
                using var appStatic = ECDiffieHellman.Create();
                appStatic.ImportPkcs8PrivateKey(Convert.FromBase64String(pairing.AppPrivateKey), out _);
                using var extensionStatic = BrowserCrypto.ImportPublicKey(Convert.FromBase64String(pairing.ExtensionPublicKey));
                using var extensionEphemeral = BrowserCrypto.ImportPublicKey(extensionEphemeralPublic);
                using var appEphemeral = BrowserCrypto.CreateKey();
                appEphemeralPublic = appEphemeral.ExportSubjectPublicKeyInfo();
                (receiveKey, sendKey) = BrowserCrypto.DeriveSessionKeys(
                    appStatic, extensionStatic, appEphemeral, extensionEphemeral, extensionNonce, appNonce);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                await SendPlainAsync(Error("hello", "bad_request"), cancellationToken).ConfigureAwait(false);
                return false;
            }

            await SendPlainAsync(new JsonObject
            {
                ["type"] = "hello",
                ["ok"] = true,
                ["version"] = BrowserPipeServer.ProtocolVersion,
                ["ephemeralKey"] = Convert.ToBase64String(appEphemeralPublic),
                ["nonce"] = Convert.ToBase64String(appNonce),
            }, cancellationToken).ConfigureAwait(false);

            _session = new Session { PairingId = pairingId, ReceiveKey = receiveKey, SendKey = sendKey };
            _server.Pairings.MarkUsed(pairingId, DateTimeOffset.UtcNow);
            return true;
        }

        private static bool IsSupportedVersion(JsonObject message) =>
            message["version"] is JsonValue value && value.TryGetValue(out int version) && version == BrowserPipeServer.ProtocolVersion;

        #endregion

        #region Session: requests

        private async Task AnswerAsync(JsonObject request, CancellationToken cancellationToken)
        {
            var id = request["id"]?.DeepClone();
            JsonObject answer;
            try
            {
                answer = await HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log?.LogError(ex, "Browser request {Type} failed", Str(request["type"]));
                answer = Failure("internal");
            }

            answer["id"] = id;
            try
            {
                await SendEncryptedAsync(answer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
            }
        }

        private async Task<JsonObject> HandleRequestAsync(JsonObject request, CancellationToken cancellationToken)
        {
            var backend = _server.Backend;
            switch (Str(request["type"]))
            {
                case "status":
                {
                    var credentials = await backend.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
                    return Success(new JsonObject { ["locked"] = credentials == null, ["version"] = BrowserPipeServer.ProtocolVersion });
                }
                case "unlock":
                {
                    var unlocked = await backend.UnlockAsync(cancellationToken).ConfigureAwait(false);
                    return Success(new JsonObject { ["locked"] = !unlocked });
                }
                case "getLogins":
                    return await GetLoginsAsync(request, cancellationToken).ConfigureAwait(false);
                case "getCredentials":
                {
                    var (credential, error) = await FindMatchingEntryAsync(request, cancellationToken).ConfigureAwait(false);
                    if (credential == null)
                        return Failure(error);

                    var answer = new JsonObject
                    {
                        ["login"] = credential.Login.LoginField.Value ?? string.Empty,
                        ["password"] = credential.Login.PasswordField.Value ?? string.Empty,
                    };
                    if (TotpService.TryGetCode(credential.Login.Totp, DateTime.UtcNow, out var code, out _))
                        answer["totp"] = code;
                    return Success(answer);
                }
                case "openEntry":
                {
                    if (!Guid.TryParse(Str(request["entryId"]), out var entryId))
                        return Failure("bad_request");
                    await backend.OpenEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
                    return Success(new JsonObject());
                }
                default:
                    return Failure("bad_request");
            }
        }

        private async Task<JsonObject> GetLoginsAsync(JsonObject request, CancellationToken cancellationToken)
        {
            if (request["url"] is not JsonValue urlValue || !urlValue.TryGetValue(out string url))
                return Failure("bad_request");

            var credentials = await _server.Backend.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
            if (credentials == null)
                return Failure("locked");

            var page = WebsiteMatcher.ParsePage(url);
            var logins = new JsonArray();
            if (page != null)
            {
                var matches = credentials
                    .Where(credential => credential.Type == CredentialType.Login)
                    .Select(credential => (credential, match: _server.Matcher.Match(WebsiteMatcher.ParseEntrySite(credential.Login.SiteField.Value), page)))
                    .Where(item => item.match != WebsiteMatch.None)
                    .OrderByDescending(item => item.match)
                    .ThenByDescending(item => item.credential.LastModifiedTime);
                foreach (var (credential, match) in matches)
                {
                    logins.Add(new JsonObject
                    {
                        ["entryId"] = credential.Id.ToString(),
                        ["name"] = credential.NameField.Value ?? string.Empty,
                        ["login"] = credential.Login.LoginField.Value ?? string.Empty,
                        ["match"] = match == WebsiteMatch.Exact ? "exact" : "domain",
                    });
                }
            }
            return Success(new JsonObject { ["logins"] = logins });
        }

        /// <summary>The login entry <c>entryId</c> if it still matches <c>url</c>: an entry ID alone is not enough.</summary>
        private async Task<(Credential Credential, string Error)> FindMatchingEntryAsync(JsonObject request, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(Str(request["entryId"]), out var entryId) ||
                request["url"] is not JsonValue urlValue || !urlValue.TryGetValue(out string url))
                return (null, "bad_request");

            var credentials = await _server.Backend.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
            if (credentials == null)
                return (null, "locked");

            var credential = credentials.FirstOrDefault(item => item.Id == entryId && item.Type == CredentialType.Login);
            if (credential == null || _server.Matcher.Match(credential.Login.SiteField.Value, url) == WebsiteMatch.None)
                return (null, "not_found");

            return (credential, null);
        }

        private static JsonObject Success(JsonObject answer)
        {
            answer["ok"] = true;
            return answer;
        }

        private static JsonObject Failure(string error) => new() { ["ok"] = false, ["error"] = error };

        private static JsonObject Error(string type, string error) => new() { ["type"] = type, ["ok"] = false, ["error"] = error };

        #endregion

        #region Framing and encryption

        private async Task<byte[]> ReadFrameAsync(CancellationToken cancellationToken)
        {
            var header = new byte[4];
            if (!await ReadExactlyOrEndAsync(header, cancellationToken).ConfigureAwait(false))
                return null;

            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (length == 0 || length > MaxMessageSize)
            {
                Log?.LogWarning("Browser connection sent a frame of {Length} bytes; closing", length);
                return null;
            }

            var frame = new byte[length];
            return await ReadExactlyOrEndAsync(frame, cancellationToken).ConfigureAwait(false) ? frame : null;
        }

        private async Task<bool> ReadExactlyOrEndAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            try
            {
                await _stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        private JsonObject Decrypt(byte[] frame)
        {
            var session = _session;
            try
            {
                if (JsonNode.Parse(frame) is not JsonObject envelope ||
                    envelope["n"] is not JsonValue n || !n.TryGetValue(out ulong counter) ||
                    !TryBase64(envelope["c"], out var sealedMessage))
                    return null;

                // Each direction counts from 0 without gaps: a replayed or dropped message ends the session.
                if (counter != session.ReceiveCounter)
                    return null;

                var plaintext = BrowserCrypto.Open(session.ReceiveKey, counter, sealedMessage);
                session.ReceiveCounter++;
                return JsonNode.Parse(plaintext) as JsonObject;
            }
            catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException or InvalidOperationException)
            {
                return null;
            }
        }

        private async Task SendEncryptedAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var session = _session;
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(message);
            await _write.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var counter = session.SendCounter++;
                var sealedMessage = BrowserCrypto.Seal(session.SendKey, counter, plaintext);
                var envelope = new JsonObject { ["n"] = counter, ["c"] = Convert.ToBase64String(sealedMessage) };
                await WriteFrameAsync(JsonSerializer.SerializeToUtf8Bytes(envelope), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                _write.Release();
            }
        }

        private async Task SendPlainAsync(JsonObject message, CancellationToken cancellationToken)
        {
            await _write.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await WriteFrameAsync(JsonSerializer.SerializeToUtf8Bytes(message), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _write.Release();
            }
        }

        private async Task WriteFrameAsync(byte[] payload, CancellationToken cancellationToken)
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
            payload.CopyTo(frame, 4);
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        #endregion

        /// <summary>The string value of a JSON property, or null when it is missing or not a string.</summary>
        private static string Str(JsonNode node) =>
            node is JsonValue value && value.TryGetValue(out string text) ? text : null;

        private static bool TryBase64(JsonNode node, out byte[] bytes)
        {
            bytes = null;
            if (node is not JsonValue value || !value.TryGetValue(out string text) || string.IsNullOrEmpty(text))
                return false;
            try
            {
                bytes = Convert.FromBase64String(text);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string Truncate(string text, int length)
        {
            text = text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text.Length <= length ? text : text[..length];
        }
    }
}
