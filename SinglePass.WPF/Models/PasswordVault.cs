using System;
using System.Collections.Generic;

namespace SinglePass.WPF.Models
{
    internal sealed record PasswordVault
    {
        public int Version { get; init; } = 1;
        public List<Credential> Credentials { get; init; } = new();
        public Dictionary<Guid, GroupChange> Groups { get; init; } = new();
        public Dictionary<Guid, GroupAssignment> Assignments { get; init; } = new();
    }

    internal sealed record GroupChange(PasswordGroup Group, DateTimeOffset ModifiedAt, bool IsDeleted = false);
    internal sealed record GroupAssignment(Guid? GroupId, DateTimeOffset ModifiedAt);
}
