using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace EvoS.Framework.Misc
{
    public static class Utils
    {
        public static byte[] ToByteArray(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte) ((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            var val = (int) hex;
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        public static string SerializeObject(
            object obj,
            bool includePrivate = true,
            SerializeDebugLevel debugTrace = SerializeDebugLevel.None)
        {
            var settings = GetJsonSerializerSettings(includePrivate, debugTrace, out var serializer);

            var stringWriter = new StringWriter(new StringBuilder(256), CultureInfo.InvariantCulture);
            using var jsonTextWriter = new JsonTextWriter(stringWriter)
            {
                Formatting = serializer.Formatting
            };
            try
            {
                serializer.Serialize(jsonTextWriter, obj, null);
            }
            catch (Exception)
            {
                if (settings.TraceWriter != null)
                    Console.WriteLine(settings.TraceWriter);
                throw;
            }

            if (debugTrace == SerializeDebugLevel.Always)
            {
                Console.WriteLine(settings.TraceWriter);
            }

            return stringWriter.ToString();
        }

        public static JObject SerializeObjectAsJObject(
            object obj,
            bool includePrivate = true,
            SerializeDebugLevel debugTrace = SerializeDebugLevel.None)
        {
            var settings = GetJsonSerializerSettings(includePrivate, debugTrace, out var serializer);

            JObject jObject;
            try
            {
                jObject = JObject.FromObject(obj, serializer);
            }
            catch (Exception)
            {
                if (settings.TraceWriter != null)
                    Console.WriteLine(settings.TraceWriter);
                throw;
            }

            if (debugTrace == SerializeDebugLevel.Always)
            {
                Console.WriteLine(settings.TraceWriter);
            }

            return jObject;
        }

        private static JsonSerializerSettings GetJsonSerializerSettings(bool includePrivate,
            SerializeDebugLevel debugTrace,
            out JsonSerializer serializer)
        {
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            if (includePrivate)
            {
                settings.ContractResolver = new PrivateMemberContractResolver();
            }

            if (debugTrace != SerializeDebugLevel.None)
            {
                settings.TraceWriter = new MemoryTraceWriter();
            }

            serializer = JsonSerializer.CreateDefault(settings);
            serializer.Converters.Add(new StringEnumConverter());
            serializer.Converters.Add(new BoardSquareConverter());
            return settings;
        }

        public enum SerializeDebugLevel
        {
            None,
            OnException,
            Always
        }

        public class BoardSquareConverter : JsonConverter<BoardSquare>
        {
            public override void WriteJson(JsonWriter writer, BoardSquare value, JsonSerializer serializer)
            {
                writer.WriteValue($"(x={value.X}, y={value.Y})");
            }

            public override BoardSquare ReadJson(JsonReader reader, Type objectType, BoardSquare existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        public class PrivateMemberContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(p => base.CreateProperty(p, memberSerialization))
                    .Union(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(f => !typeof(MulticastDelegate).IsAssignableFrom(f.FieldType.BaseType))
                        .Select(f => base.CreateProperty(f, memberSerialization))
                    )
                    .ToList();

                props.ForEach(p =>
                {
                    p.Writable = true;
                    p.Readable = true;
                });
                return props;
            }
        }

        public static string ToHex(byte[] bytes, in int numBytes)
        {
            var dest = new byte[numBytes];
            Array.Copy(bytes, dest, numBytes);
            return ToHex(dest);
        }

        public static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
