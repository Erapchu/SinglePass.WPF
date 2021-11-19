using Autofac;
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

        public static ILogger GetLogger(IComponentContext componentContext)
        {
            var config = LogManager.Configuration ?? new LoggingConfiguration();
            var loggerName = Constants.AppName;
            var fileTargetName = $"{loggerName} File Log Target";
            var logsDirectoryPath = Constants.PathToLoggerFile;

            var fileTarget = new NLog.Targets.FileTarget()
            {
                ArchiveAboveSize = 30 * 1024 * 1024,
                ArchiveDateFormat = "yyyyMMdd",
                ArchiveEvery = NLog.Targets.FileArchivePeriod.Month,
                ArchiveFileName = Path.Combine(logsDirectoryPath, "Archive", logsDirectoryPath),
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
            config.LoggingRules.Add(new LoggingRule(loggerName, LogLevel.Trace, fileTargetWrapper));

            LogManager.Configuration = config;
            var logger = LogManager.GetLogger(loggerName);
            return logger;
        }
    }
}
