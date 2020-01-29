namespace px_dotnet.Common
{
    public class TrafficLight
    {
        [JsonProperty("send-data")]
        public bool SendData {get;set;}

        [JsonProperty("ttl")]
        public int TTL {get;set;}

        [JsonProperty("endpoint-whitelist")]
        public List<string> EndpointWhiteList {get;set;}

        [JsonProperty("base64-encode-data")]
        public bool Base64Encode {get;set;}

        public DateTime ExpirationTime {get ;  set;}

    }
}