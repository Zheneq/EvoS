using System;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Unity.Messages
{
    [UNetMessage(serverMsgIds: new short[] {14})]
    [JsonConverter(typeof(JsonConverter))]
    public class CRCMessage : MessageBase
    {
        public CRCMessageEntry[] scripts;

        public override void Deserialize(NetworkReader reader)
        {
            scripts = new CRCMessageEntry[reader.ReadUInt16()];
            for (int index = 0; index < scripts.Length; ++index)
                scripts[index] = new CRCMessageEntry
                {
                    name = reader.ReadString(),
                    channel = reader.ReadByte()
                };
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((ushort) scripts.Length);
            for (int index = 0; index < scripts.Length; ++index)
            {
                writer.Write(scripts[index].name);
                writer.Write(scripts[index].channel);
            }
        }

        public override string ToString()
        {
            return $"{nameof(CRCMessage)}(" +
                   $"{nameof(scripts)}: {scripts.Length} entries" +
                   ")";
        }

        private class JsonConverter : Newtonsoft.Json.JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(CRCMessage);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var msg = (CRCMessage) value;

                writer.WriteStartObject();
                foreach (var script in msg.scripts)
                {
                    serializer.Serialize(writer, script);
                }

                writer.WriteEndObject();
            }

            public override object ReadJson(
                JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer
            ) => throw new NotImplementedException();
        }
    }

    [JsonConverter(typeof(JsonConverter))]
    public struct CRCMessageEntry
    {
        public string name;
        public byte channel;

        public CRCMessageEntry(string name, byte channel)
        {
            this.name = name;
            this.channel = channel;
        }

        private class JsonConverter : Newtonsoft.Json.JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(CRCMessageEntry);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var msg = (CRCMessageEntry) value;

                writer.WritePropertyName(msg.name);
                writer.WriteValue(msg.channel);
            }

            public override object ReadJson(
                JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer
            ) => throw new NotImplementedException();
        }
    }
}
