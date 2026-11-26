using Erapkey.Core.Helpers;
using Erapkey.Core.Models;
using Erapkey.Desktop.ViewModels.Entries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Erapkey.Desktop.Helpers
{
    /// <summary>
    /// <c>ssh</c> command lines for hosts: copied to the clipboard or started in a terminal.
    /// With a linked key, ssh gets <c>-i key.pub</c> and the Erapkey agent: it offers exactly that key
    /// and the agent signs with it, so the private key never leaves the vault. A public key is not a secret.
    /// </summary>
    internal static class SshCommands
    {
        /// <summary>Name of the Windows Terminal window that collects all connections as tabs.</summary>
        private const string TerminalWindowName = "Erapkey";

        public static string PublicKeysDirectoryPath { get; } = Path.Combine(Constants.LocalAppDataDirectoryPath, "ssh");

        /// <summary>
        /// The OpenSSH client of Windows. Only it talks to named pipes; the ssh of Git for Windows,
        /// which may come first in PATH, cannot use the agent.
        /// </summary>
        public static string SshPath { get; } = Path.Combine(Environment.SystemDirectory, "OpenSSH", "ssh.exe");

        private static string WindowsTerminalPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");

        /// <summary>Writes the public key of the entry to its file, if it changed, and returns the path.</summary>
        public static string WritePublicKey(Guid keyId, string publicKey)
        {
            var path = Path.Combine(PublicKeysDirectoryPath, keyId.ToString("N") + ".pub");
            var content = publicKey.Trim() + "\n";
            if (File.Exists(path) && File.ReadAllText(path) == content)
                return path;

            Directory.CreateDirectory(PublicKeysDirectoryPath);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        /// <summary>
        /// Arguments of ssh (without the program) that connect to the host. With <paramref name="identityFile"/>
        /// and <paramref name="agentPath"/> ssh signs in with that key through the Erapkey agent only.
        /// </summary>
        public static IReadOnlyList<string> BuildArguments(HostViewModel host, string identityFile, string agentPath)
        {
            var arguments = new List<string>();
            if (identityFile != null && agentPath != null)
            {
                arguments.AddRange(["-o", "IdentityAgent=" + agentPath, "-o", "IdentitiesOnly=yes", "-i", identityFile]);
            }

            if (host.Port != HostData.DefaultPort)
                arguments.AddRange(["-p", host.Port.ToString(CultureInfo.InvariantCulture)]);

            // "--": a host or user starting with "-" (for example "-oProxyCommand=…" in a synced vault)
            // must stay an address, not become an option that runs a command.
            arguments.AddRange(["--", host.Destination]);
            return arguments;
        }

        /// <summary>The command to paste into a terminal.</summary>
        public static string BuildCommandLine(IReadOnlyList<string> arguments) =>
            string.Join(' ', arguments.Prepend("ssh").Select(Quote));

        /// <summary>
        /// Starts ssh in a new tab of the Erapkey window of Windows Terminal, or in a console window
        /// when Windows Terminal is not installed.
        /// </summary>
        /// <exception cref="FileNotFoundException">The OpenSSH client of Windows is not installed.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception">The terminal could not be started.</exception>
        public static void Connect(string title, IReadOnlyList<string> arguments)
        {
            if (!File.Exists(SshPath))
                throw new FileNotFoundException("The OpenSSH client is not installed.", SshPath);

            ProcessStartInfo start;
            if (File.Exists(WindowsTerminalPath))
            {
                start = new ProcessStartInfo(WindowsTerminalPath);
                // wt splits commands at ';', also inside arguments, unless it is escaped.
                foreach (var argument in new[] { "-w", TerminalWindowName, "new-tab", "--title", title, SshPath }.Concat(arguments))
                    start.ArgumentList.Add(argument.Replace(";", "\\;"));
            }
            else
            {
                start = new ProcessStartInfo(SshPath);
                foreach (var argument in arguments)
                    start.ArgumentList.Add(argument);
            }

            start.UseShellExecute = false;
            start.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Process.Start(start)?.Dispose();
        }

        private static string Quote(string argument) =>
            argument.IndexOfAny([' ', '\t', '\'', '"', '$', '`', ';']) >= 0 ? "\"" + argument.Replace("\"", "\\\"") + "\"" : argument;
    }
}
