using System.Text.Json.Serialization;

namespace SinglePass.Core.Clouds.Models
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

        [JsonPropertyName("md5Checksum")]
        public string Md5Checksum { get; init; }
    }
}
