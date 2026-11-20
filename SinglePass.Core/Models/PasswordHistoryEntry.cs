using System;
using System.Text.Json.Serialization;

namespace SinglePass.Core.Models
{
    public sealed class PasswordHistoryEntry
    {
        public string Password { get; init; }
        public DateTimeOffset ChangedAtUtc { get; init; }

        [JsonIgnore]
        public DateTime ChangedAtLocal => ChangedAtUtc.LocalDateTime;
    }
}
