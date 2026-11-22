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
        // Version 4 adds payment cards (Credential.Card), for the same reason.
        // Version 5 adds the trash (Trash). An older client would show trashed entries as regular ones
        // and drop the trash state on its next write.
        internal const int CurrentVersion = 5;

        /// <summary>How long an entry stays in the trash before it is deleted for good.</summary>
        internal static readonly TimeSpan TrashRetention = TimeSpan.FromDays(30);

        public int Version { get; init; } = CurrentVersion;
        public List<Credential> Credentials { get; init; } = new();
        public Dictionary<Guid, GroupChange> Groups { get; init; } = new();
        public Dictionary<Guid, GroupAssignment> Assignments { get; init; } = new();
        public Dictionary<Guid, DateTimeOffset> DeletedCredentials { get; init; } = new();

        /// <summary>
        /// Trash state of credentials. A credential with <see cref="TrashState.DeletedAt"/> set is in the trash:
        /// it keeps its data and group link but is hidden from the vault. Restoring keeps the record with
        /// <c>DeletedAt = null</c>, so on sync the later of moving to the trash and restoring wins.
        /// </summary>
        public Dictionary<Guid, TrashState> Trash { get; init; } = new();

        /// <summary>
        /// When the master password was last changed. On sync the most recent change wins,
        /// so a new password spreads to the cloud and other devices instead of being reverted.
        /// Null for vaults whose password has not been changed since this field was added.
        /// </summary>
        public DateTimeOffset? MasterPasswordChangedAt { get; init; }
    }

    internal sealed record GroupChange(PasswordGroup Group, DateTimeOffset ModifiedAt, bool IsDeleted = false);
    internal sealed record GroupAssignment(Guid? GroupId, DateTimeOffset ModifiedAt);
    internal sealed record TrashState(DateTimeOffset? DeletedAt, DateTimeOffset ModifiedAt)
    {
        public bool IsTrashed => DeletedAt.HasValue;
    }
}
