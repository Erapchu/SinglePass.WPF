using System;
using System.IO;

namespace PasswordManager.Helpers
{
    public static class Constants
    {
        public const string AppName = "PasswordManager";
        public const string PasswordsFileName = "PassMan.dat";
        public const string CommonSettingsFileName = "CommonSettings.json";
        public const string GoogleDriveFileName = "GoogleDrive.json";

        public static string LocalAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        public static string RoamingAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
        public static string PasswordsFilePath { get; } = Path.Combine(RoamingAppDataDirectoryPath, PasswordsFileName);
        public static string CommonSettingsFilePath { get; } = Path.Combine(RoamingAppDataDirectoryPath, CommonSettingsFileName);
        public static string GoogleDriveFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, GoogleDriveFileName);

        static Constants()
        {
            CreateIfNotExists(RoamingAppDataDirectoryPath);
            CreateIfNotExists(LocalAppDataDirectoryPath);
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
