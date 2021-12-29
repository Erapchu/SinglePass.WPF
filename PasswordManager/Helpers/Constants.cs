using System;
using System.IO;

namespace PasswordManager.Helpers
{
    public static class Constants
    {
        public const string AppName = "PasswordManager";
        public const string PasswordsFileName = "PassMan.dat";
        public const string LoggerFileName = "PasswordManager.log";
        public const string LoggerDirectoryName = "Logs";

        public static string LocalAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        public static string RoamingAppDataDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
        public static string PasswordsFilePath { get; } = Path.Combine(RoamingAppDataDirectoryPath, PasswordsFileName);
        public static string LoggerDirectoryPath { get; } = Path.Combine(LocalAppDataDirectoryPath, LoggerDirectoryName);
        public static string LoggerFilePath { get; } = Path.Combine(LoggerDirectoryPath, LoggerFileName);
    }
}
