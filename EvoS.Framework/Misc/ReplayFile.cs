using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EvoS.Framework.Misc
{
    public class ReplayFile
    {
        [JsonProperty("m_gameplayOverrides_Serialized")]
        public string GameplayOverridesSerialized { get; set; }

        [JsonProperty("m_gameInfo_Serialized")]
        public string GameInfoSerialized { get; set; }

        [JsonProperty("m_teamInfo_Serialized")]
        public string TeamInfoSerialized { get; set; }

        [JsonProperty("m_versionMini")] public string VersionMini { get; set; }
        [JsonProperty("m_versionFull")] public string VersionFull { get; set; }
        [JsonProperty("m_playerInfo_Index")] public long PlayerInfoIndex { get; set; }
        [JsonProperty("m_messages")] public ReplayMessage[] Messages { get; set; }

        public static ReplayFile FromJson(string json) =>
            JsonConvert.DeserializeObject<ReplayFile>(json, Converter.Settings);

        public string ToJson() => JsonConvert.SerializeObject(this, Converter.Settings);
    }

    public class ReplayMessage
    {
        [JsonProperty("timestamp")] public double Timestamp { get; set; }
        [JsonProperty("data")] public byte[] Data { get; set; }
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter {DateTimeStyles = DateTimeStyles.AssumeUniversal}
            },
        };
    }
}
