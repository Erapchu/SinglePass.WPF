using System;
using System.Collections.Generic;

namespace SinglePass.Core.Models
{
    internal sealed record PasswordVault
    {
        // Version 2 adds credential tombstones. Older clients must refuse it: they would drop the
        // unknown field on their next write and deleted credentials would come back on sync.
        internal const int CurrentVersion = 2;

        public int Version { get; init; } = CurrentVersion;
        public List<Credential> Credentials { get; init; } = new();
        public Dictionary<Guid, GroupChange> Groups { get; init; } = new();
        public Dictionary<Guid, GroupAssignment> Assignments { get; init; } = new();
        public Dictionary<Guid, DateTimeOffset> DeletedCredentials { get; init; } = new();
    }

    internal sealed record GroupChange(PasswordGroup Group, DateTimeOffset ModifiedAt, bool IsDeleted = false);
    internal sealed record GroupAssignment(Guid? GroupId, DateTimeOffset ModifiedAt);
}
