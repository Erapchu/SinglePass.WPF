using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.Helpers
{
    public static class Constants
    {
        public const string AppName = "PasswordManager";
        public const string NameOfPasswordsFile = "PassMan.dat";
        public const string NameOfLoggerFile = "PasswordManager.log";

        public static string LocalAppDataDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        public static string RoamingAppDataDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
        public static string PathToPasswordsFile => Path.Combine(RoamingAppDataDirectoryPath, NameOfPasswordsFile);
        public static string PathToLoggerFile => Path.Combine(LocalAppDataDirectoryPath, NameOfLoggerFile);
    }
}
