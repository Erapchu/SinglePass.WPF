using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PasswordManager.Clouds.Models
{
    public class GoogleDriveFileList
    {
        [JsonPropertyName("kind")]
        public string Kind { get; init; }

        [JsonPropertyName("incompleteSearch")]
        public bool IncompleteSearch { get; init; }

        [JsonPropertyName("files")]
        public List<GoogleDriveFile> Files { get; init; }
    }
}
