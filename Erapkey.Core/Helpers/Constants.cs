using System;
using System.IO;

namespace Erapkey.Core.Helpers
{
    public static class Constants
    {
        /// <summary>
        /// Debug builds are a separate instance: their own data folder, pipes, mutex and native messaging host,
        /// so they can run next to the installed application without touching its vault (docs/browser-extension.md#development).
        /// </summary>
#if DEBUG
        public const bool IsDevBuild = true;
#else
        public const bool IsDevBuild = false;
#endif

        /// <summary>Appended to names shared between processes: pipes, the mutex, the interprocess window.</summary>
        public const string InstanceSuffix = IsDevBuild ? "-dev" : "";

        public const string InterprocessWindowName = "HiddenInterprocessWindow" + InstanceSuffix;
        public const string SingleInstanceMutexName = "Erapkey_CBD9AADE-1A82-48A2-9F7F-4F0EAAABEA30" + InstanceSuffix;
        public const string ProcessName = "Erapkey";
        public const string AppName = "Erapkey";
        public const string DataDirectoryName = IsDevBuild ? AppName + ".Dev" : AppName;
        /// <summary>Name shown where both instances may appear side by side, such as the tray.</summary>
        public const string DisplayName = IsDevBuild ? AppName + " (Dev)" : AppName;
        public const string PasswordsFileName = "erapkey.dat";
        public const string CommonSettingsFileName = "commonSettings.json";
        public const string GoogleDriveFileName = "googleDrive.json";
        public const string CacheDirectoryName = "Cache";
        public const string BackupsDirectoryName = "Backups";

        public static string LocalAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataDirectoryName);
        public static string LogsDirectoryPath { get; } = Path.Combine(LocalAppDataDirectoryPath, "Logs");
        //public static string RoamingAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
        public static string PasswordsFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, PasswordsFileName);
        public static string CommonSettingsFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, CommonSettingsFileName);
        public static string GoogleDriveFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, GoogleDriveFileName);
        public static string CacheDirectoryPath { get; } = Path.Combine(LocalAppDataDirectoryPath, CacheDirectoryName);
        public static string BackupsDirectoryPath { get; } = Path.Combine(LocalAppDataDirectoryPath, BackupsDirectoryName);

        public static void EnsurePaths()
        {
            //CreateIfNotExists(RoamingAppDataDirectoryPath);
            CreateIfNotExists(LocalAppDataDirectoryPath);
            CreateIfNotExists(CacheDirectoryPath);
        }

        private static void CreateIfNotExists(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }
        }
    }
}
