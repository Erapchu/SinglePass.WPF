using SinglePass.WPF.Models;
using System;
using System.Collections.Generic;

namespace SinglePass.WPF.Settings
{
    public sealed record PasswordGroupSettings
    {
        public List<PasswordGroup> Groups { get; init; } = new();
        public Dictionary<Guid, Guid> Assignments { get; init; } = new();
    }
}
