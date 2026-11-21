using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SinglePass.Core.Services
{
    /// <summary>Why a backup was made. The name is part of the backup file name.</summary>
    public enum VaultBackupReason
    {
        /// <summary>First change of the vault after a day without backups.</summary>
        Daily,
        /// <summary>Before a cloud sync changes the local vault.</summary>
        Sync,
        /// <summary>Before the master password is changed or replaced by the cloud one.</summary>
        Password,
        /// <summary>Before a backup is restored.</summary>
        Restore
    }

    public sealed record VaultBackupInfo(string Path, DateTimeOffset CreatedAt, VaultBackupReason Reason);

    /// <summary>
    /// Copies of the encrypted vault file. A backup is the file exactly as it was, encrypted with the
    /// master password of that moment. Callers hold the vault lock, so the file does not change meanwhile.
    /// </summary>
    internal sealed class VaultBackups
    {
        internal const int MaxBackups = 20;
        internal static readonly TimeSpan DailyInterval = TimeSpan.FromDays(1);

        private const string Prefix = "singlePass-";
        private const string Extension = ".dat";
        private const string TimeFormat = "yyyyMMdd-HHmmss-fff";

        private readonly string _vaultPath;
        private readonly string _directory;

        public VaultBackups(string vaultPath, string directory)
        {
            _vaultPath = vaultPath;
            _directory = directory;
        }

        /// <summary>Newest first. Files with foreign names are ignored.</summary>
        public IReadOnlyList<VaultBackupInfo> List()
        {
            if (!Directory.Exists(_directory))
                return [];

            return Directory.EnumerateFiles(_directory, Prefix + "*" + Extension)
                .Select(Parse)
                .Where(info => info is not null)
                .OrderByDescending(info => info.CreatedAt)
                .ToList();
        }

        /// <summary>Copies the current vault file. Does nothing when there is no vault file yet.</summary>
        public VaultBackupInfo Create(VaultBackupReason reason, DateTimeOffset now)
        {
            if (!File.Exists(_vaultPath))
                return null;

            Directory.CreateDirectory(_directory);
            var createdAt = now.ToUniversalTime();
            var path = System.IO.Path.Combine(_directory,
                $"{Prefix}{createdAt.ToString(TimeFormat, CultureInfo.InvariantCulture)}-{reason.ToString().ToLowerInvariant()}{Extension}");
            // Two backups in the same millisecond keep the first one: it holds the same file.
            if (!File.Exists(path))
                File.Copy(_vaultPath, path);

            Prune();
            return new VaultBackupInfo(path, TruncateToMilliseconds(createdAt), reason);
        }

        /// <summary>Makes a daily backup when the newest backup of any kind is older than a day.</summary>
        public VaultBackupInfo CreateDailyIfDue(DateTimeOffset now)
        {
            var newest = List().FirstOrDefault();
            return newest is null || now - newest.CreatedAt >= DailyInterval
                ? Create(VaultBackupReason.Daily, now)
                : null;
        }

        private void Prune()
        {
            foreach (var old in List().Skip(MaxBackups))
            {
                try { File.Delete(old.Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static VaultBackupInfo Parse(string path)
        {
            // singlePass-20261121-140533-123-sync.dat
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.Length <= Prefix.Length + TimeFormat.Length + 1)
                return null;

            var time = name.Substring(Prefix.Length, TimeFormat.Length);
            var reason = name[(Prefix.Length + TimeFormat.Length + 1)..];
            if (!DateTime.TryParseExact(time, TimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var createdAt) ||
                !Enum.TryParse<VaultBackupReason>(reason, ignoreCase: true, out var parsedReason) ||
                !Enum.IsDefined(parsedReason))
                return null;

            return new VaultBackupInfo(path, new DateTimeOffset(createdAt, TimeSpan.Zero), parsedReason);
        }

        private static DateTimeOffset TruncateToMilliseconds(DateTimeOffset value) =>
            new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Offset);
    }
}
