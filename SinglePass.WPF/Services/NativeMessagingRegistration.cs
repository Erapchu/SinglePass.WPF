using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SinglePass.Core.Helpers;
using SinglePass.Core.Services.Browser;
using System;
using System.IO;
using System.Text.Json;

namespace SinglePass.WPF.Services
{
    /// <summary>
    /// Registers <c>SinglePass.NativeHost.exe</c> as the Native Messaging host of the extension for Chrome and Edge,
    /// under the current user (docs/browser-extension.md#native-host-registration). Debug builds register their own
    /// host name, so they never take the place of the installed application.
    /// </summary>
    public sealed class NativeMessagingRegistration
    {
        public const string HostName = Constants.IsDevBuild ? "com.singlepass.host.dev" : "com.singlepass.host";

        /// <summary>
        /// Extensions allowed to start the host. Until the store listings exist, this is the unpacked extension
        /// (its manifest has a fixed key, extension/manifest.json); the store IDs join it when they are known.
        /// </summary>
        public static readonly string[] AllowedOrigins =
        [
            "chrome-extension://mhnlllocpadmflggkijecmklkooflbbe/",
        ];

        private static readonly string[] RegistryKeys =
        [
            @"Software\Google\Chrome\NativeMessagingHosts\" + HostName,
            @"Software\Microsoft\Edge\NativeMessagingHosts\" + HostName,
        ];

        private readonly ILogger<NativeMessagingRegistration> _logger;

        public NativeMessagingRegistration(ILogger<NativeMessagingRegistration> logger)
        {
            _logger = logger;
        }

        public static string ManifestPath { get; } = Path.Combine(Constants.LocalAppDataDirectoryPath, "browser", HostName + ".json");

        public static string HostPath { get; } = Path.Combine(AppContext.BaseDirectory, BrowserPipeServer.NativeHostFileName);

        /// <summary>
        /// Writes the manifest and the registry keys. Run on every start while the integration is on, so a moved
        /// portable copy or a Debug build in another folder points the browsers at the right executable.
        /// </summary>
        public bool Register()
        {
            try
            {
                if (!File.Exists(HostPath))
                {
                    _logger.LogError("The native messaging host {Path} is missing", HostPath);
                    return false;
                }

                var manifest = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    name = HostName,
                    description = Constants.DisplayName,
                    path = HostPath,
                    type = "stdio",
                    allowed_origins = AllowedOrigins,
                }, new JsonSerializerOptions { WriteIndented = true });

                Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
                if (!File.Exists(ManifestPath) || !File.ReadAllBytes(ManifestPath).AsSpan().SequenceEqual(manifest))
                    File.WriteAllBytes(ManifestPath, manifest);

                foreach (var keyPath in RegistryKeys)
                {
                    using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                    if (!string.Equals(key.GetValue(null) as string, ManifestPath, StringComparison.OrdinalIgnoreCase))
                        key.SetValue(null, ManifestPath);
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logger.LogError(ex, "Unable to register the native messaging host");
                return false;
            }
        }

        /// <summary>Removes the registry keys and the manifest; browsers then cannot start the host.</summary>
        public static void Unregister(ILogger logger = null)
        {
            foreach (var keyPath in RegistryKeys)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    logger?.LogWarning(ex, "Unable to remove {Key}", keyPath);
                }
            }

            try
            {
                File.Delete(ManifestPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger?.LogWarning(ex, "Unable to delete {Path}", ManifestPath);
            }
        }
    }
}
