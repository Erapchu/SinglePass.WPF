using Newtonsoft.Json;

namespace Erapkey.Core.Authorization.Responses
{
    public class TokenErrorResponse
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }
    }
}
