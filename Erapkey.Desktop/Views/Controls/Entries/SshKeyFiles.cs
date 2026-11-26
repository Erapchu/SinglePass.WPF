using Erapkey.Core.Localization;
using System;
using System.IO;

namespace Erapkey.Desktop.Views.Controls.Entries
{
    /// <summary>File dialogs settings for SSH key files.</summary>
    internal static class SshKeyFiles
    {
        // Real key files are a few kilobytes; anything much larger is not a key.
        public const long MaxSize = 64 * 1024;

        public static string Filter =>
            $"{LocalizationManager.Instance["SshKeyFiles"]}|id_*;*.pem;*.key;*.pub|{LocalizationManager.Instance["AllFiles"]}|*.*";

        /// <summary>The user's <c>.ssh</c> folder, or null when there is none.</summary>
        public static string SshDirectory
        {
            get
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
                return Directory.Exists(path) ? path : null;
            }
        }

        public static string Read(string path)
        {
            if (new FileInfo(path).Length > MaxSize)
                throw new InvalidDataException("The file is too large to be an SSH key.");

            return File.ReadAllText(path);
        }
    }
}
