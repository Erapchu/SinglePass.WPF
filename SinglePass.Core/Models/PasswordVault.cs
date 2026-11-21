using System;
using System.Collections.Generic;

namespace SinglePass.Core.Models
{
    internal sealed record PasswordVault
    {
        // Version 2 adds credential tombstones. Older clients must refuse it: they would drop the
        // unknown field on their next write and deleted credentials would come back on sync.
        // Version 3 adds entry types (Credential.Type, Credential.SshKey). An older client would
        // drop the SSH key data on its next write and turn the entry into an empty login.
        internal const int CurrentVersion = 3;

        public int Version { get; init; } = CurrentVersion;
        public List<Credential> Credentials { get; init; } = new();
        public Dictionary<Guid, GroupChange> Groups { get; init; } = new();
        public Dictionary<Guid, GroupAssignment> Assignments { get; init; } = new();
        public Dictionary<Guid, DateTimeOffset> DeletedCredentials { get; init; } = new();

        /// <summary>
        /// When the master password was last changed. On sync the most recent change wins,
        /// so a new password spreads to the cloud and other devices instead of being reverted.
        /// Null for vaults whose password has not been changed since this field was added.
        /// </summary>
        public DateTimeOffset? MasterPasswordChangedAt { get; init; }
    }

    internal sealed record GroupChange(PasswordGroup Group, DateTimeOffset ModifiedAt, bool IsDeleted = false);
    internal sealed record GroupAssignment(Guid? GroupId, DateTimeOffset ModifiedAt);
}
