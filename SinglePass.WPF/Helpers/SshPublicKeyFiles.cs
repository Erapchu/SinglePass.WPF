using SinglePass.Core.Helpers;
using SinglePass.WPF.ViewModels.Entries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SinglePass.WPF.Helpers
{
    /// <summary>
    /// Public keys of SSH key entries written to files, and <c>ssh</c> command lines that use them.
    /// <c>ssh -i key.pub</c> with the SinglePass agent makes ssh offer exactly that key and have the agent
    /// sign with it; the private key never leaves the vault. A public key is not a secret.
    /// </summary>
    internal static class SshPublicKeyFiles
    {
        public static string DirectoryPath { get; } = Path.Combine(Constants.LocalAppDataDirectoryPath, "ssh");

        /// <summary>Writes the public key of the entry to its file, if it changed, and returns the path.</summary>
        public static string Write(Guid keyId, string publicKey)
        {
            var path = Path.Combine(DirectoryPath, keyId.ToString("N") + ".pub");
            var content = publicKey.Trim() + "\n";
            if (File.Exists(path) && File.ReadAllText(path) == content)
                return path;

            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        /// <summary>
        /// The command that connects to the host. With <paramref name="identityFile"/> and
        /// <paramref name="agentPipe"/> ssh signs in with that key through the SinglePass agent only.
        /// </summary>
        public static string BuildCommand(HostViewModel host, string identityFile, string agentPipe)
        {
            var arguments = new List<string> { "ssh" };
            if (identityFile != null && agentPipe != null)
            {
                arguments.Add("-o");
                arguments.Add("IdentityAgent=" + agentPipe);
                arguments.Add("-o");
                arguments.Add("IdentitiesOnly=yes");
                arguments.Add("-i");
                arguments.Add(Quote(identityFile));
            }

            if (host.Port != Core.Models.HostData.DefaultPort)
            {
                arguments.Add("-p");
                arguments.Add(host.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            arguments.Add(Quote(host.Destination));
            return string.Join(' ', arguments);
        }

        private static string Quote(string argument) =>
            argument.IndexOfAny([' ', '\t', '\'', '"', '$', '`']) >= 0 ? "\"" + argument.Replace("\"", "\\\"") + "\"" : argument;
    }
}
