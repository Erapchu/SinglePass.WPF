using System;
using System.IO;

namespace PasswordManager.Helpers
{
    public static class Constants
    {
        public const string AppName = "Purple";
        public const string PasswordsFileName = "purple.dat";
        public const string CommonSettingsFileName = "commonSettings.json";
        public const string GoogleDriveFileName = "googleDrive.json";

        public static string LocalAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        //public static string RoamingAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
        public static string PasswordsFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, PasswordsFileName);
        public static string CommonSettingsFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, CommonSettingsFileName);
        public static string GoogleDriveFilePath { get; } = Path.Combine(LocalAppDataDirectoryPath, GoogleDriveFileName);

        static Constants()
        {
            //CreateIfNotExists(RoamingAppDataDirectoryPath);
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
