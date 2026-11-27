using Erapkey.Core.Models;
using Erapkey.Core.Services;
using Erapkey.Core.Services.Browser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Erapkey.Core.Tests
{
    /// <summary>
    /// The extension's TypeScript protocol code (WebCrypto in Node) against the C# server: pairing code, session keys,
    /// AES-GCM and the requests must agree byte for byte. Needs Node and <c>extension/dist-test/pipe-client.js</c>
    /// (<c>npm run build</c> in <c>extension/</c>, then <c>node build.mjs --test</c>); without them the test only says so.
    /// </summary>
    public sealed class BrowserExtensionInteropTests(ITestOutputHelper output)
    {
        [Fact]
        public async Task NodeClient_TalksToServer()
        {
            var script = FindScript();
            if (script == null)
            {
                output.WriteLine("Not run: extension/dist-test/pipe-client.js is not built.");
                return;
            }

            var directory = Path.Combine(Path.GetTempPath(), "erapkey-tests-" + Guid.NewGuid());
            var entry = Credential.CreateNew();
            entry.NameField.Value = "Example";
            entry.Login.SiteField.Value = "example.com";
            entry.Login.LoginField.Value = "alice";
            entry.Login.PasswordField.Value = "p@ss \"quoted\" пароль";
            var backend = new Backend([entry, Credential.CreateNew()]);

            using var server = new BrowserPipeServer(backend, new BrowserPairingStore(Path.Combine(directory, "pairings.dat"), new DpapiUserDataProtector(), null),
                new WebsiteMatcher(PublicSuffixList.Default), null, "erapkey-browser-test-" + Guid.NewGuid(), _ => true);
            server.Start();
            try
            {
                var start = new ProcessStartInfo("node")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                foreach (var argument in new[] { script, server.PipeName, entry.Id.ToString(), "alice", entry.Login.PasswordField.Value })
                    start.ArgumentList.Add(argument);

                Process node;
                try
                {
                    node = Process.Start(start)!;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    output.WriteLine("Not run: node is not installed.");
                    return;
                }

                using (node)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    var stdout = node.StandardOutput.ReadToEndAsync(timeout.Token);
                    var stderr = node.StandardError.ReadToEndAsync(timeout.Token);
                    await node.WaitForExitAsync(timeout.Token);
                    output.WriteLine(await stdout);
                    output.WriteLine(await stderr);

                    Assert.Equal(0, node.ExitCode);
                    Assert.Contains("OK", await stdout);
                    // The extension computed the same code as the window shows.
                    Assert.Contains("CODE " + backend.LastCode, await stdout);
                }
            }
            finally
            {
                try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
            }
        }

        private static string FindScript()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var script = Path.Combine(directory.FullName, "extension", "dist-test", "pipe-client.js");
                if (File.Exists(script))
                    return script;
            }
            return null;
        }

        private sealed class Backend(IReadOnlyList<Credential> credentials) : IBrowserBackend
        {
            public string LastCode { get; private set; }

            public Task<IReadOnlyList<Credential>> GetCredentialsAsync(CancellationToken cancellationToken) => Task.FromResult(credentials);
            public Task<bool> UnlockAsync(CancellationToken cancellationToken) => Task.FromResult(true);

            public Task<bool> ConfirmPairingAsync(BrowserPairingRequest request, CancellationToken cancellationToken)
            {
                LastCode = request.Code;
                return Task.FromResult(true);
            }

            public Task OpenEntryAsync(Guid entryId, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
