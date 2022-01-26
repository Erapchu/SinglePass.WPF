using NLog;
using NLog.Config;
using NLog.Targets.Wrappers;
using PasswordManager.Helpers;
using System.IO;
using System.Text;

namespace PasswordManager.Services
{
    public static class LoggerResolver
    {
        public const string DefaultLogLayout = "${date}\t| ${level}\t| TID:#${threadid}> ${message}";

        private static ILogger _logger;

        public static ILogger GetLogger()
        {
            if (_logger != null)
            {
                return _logger;
            }

            var config = LogManager.Configuration ?? new LoggingConfiguration();
            var loggerName = Constants.AppName;
            var fileTargetName = $"{loggerName} File Log Target";
            var logsDirectoryPath = Constants.LoggerFilePath;

            var fileTarget = new NLog.Targets.FileTarget()
            {
                ArchiveAboveSize = 30 * 1024 * 1024,
                ArchiveDateFormat = "yyyyMMdd",
                ArchiveEvery = NLog.Targets.FileArchivePeriod.Month,
                ArchiveFileName = Path.Combine(Constants.LoggerDirectoryPath, "Archive", Constants.LoggerFileName),
                ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.DateAndSequence,
                AutoFlush = true,
                ConcurrentWrites = true,
                CreateDirs = true,
                DeleteOldFileOnStartup = false,
                Encoding = Encoding.UTF8,
                FileName = Path.Combine(logsDirectoryPath),
                Layout = DefaultLogLayout,
                MaxArchiveFiles = 30,
            };
            var fileTargetWrapper = new AsyncTargetWrapper(fileTarget);
            config.AddTarget(fileTargetName, fileTargetWrapper);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTargetWrapper, loggerName);

            LogManager.Configuration = config;
            _logger = LogManager.GetLogger(loggerName);
            return _logger;
        }
    }
}
