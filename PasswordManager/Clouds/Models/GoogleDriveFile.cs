using System.Text.Json.Serialization;

namespace PasswordManager.Clouds.Models
{
    public class GoogleDriveFile
    {
        [JsonPropertyName("kind")]
        public string Kind { get; init; }

        [JsonPropertyName("id")]
        public string Id { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; }

        [JsonPropertyName("mimeType")]
        public string MimeType { get; init; }
    }
}
