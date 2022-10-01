using System;
using System.IO;

namespace SinglePass.WPF.Helpers
{
    public static class Constants
    {
        public const string InterprocessWindowName = "HiddenInterprocessWindow";
        public const string ProcessName = "SinglePass.WPF";
        public const string AppName = "SinglePass";
        public const string PasswordsFileName = "singlePass.dat";
        public const string CommonSettingsFileName = "commonSettings.json";
        public const string GoogleDriveFileName = "googleDrive.json";
        public const string CacheDirectoryName = "Cache";

        public static string LocalAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        //public static string RoamingAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
        public static string PasswordsFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, PasswordsFileName);
        public static string CommonSettingsFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, CommonSettingsFileName);
        public static string GoogleDriveFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, GoogleDriveFileName);
        public static string CacheDirectoryPath { get; } = Path.Combine(LocalAppDataDirectoryPath, CacheDirectoryName);

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
