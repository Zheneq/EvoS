using System.Reflection;
using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;

namespace EvoS.PacketAnalysis
{
    public class SyncListOperation
    {
        public uint NetId;
        public int Hash;
        [JsonIgnore] public FieldInfo SyncListField;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string SyncListComponent => SyncListField?.DeclaringType?.Name;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string SyncListName => SyncListField?.Name;

        public SyncList<int>.Operation Operation;
        public int Index;
        public object Value;

        public bool ShouldSerializeHash() => SyncListField == null;

        public override string ToString()
        {
            return $"{nameof(SyncListOperation)}(" +
                   $"{nameof(NetId)}: {NetId}, " +
                   (SyncListField == null ? $"{nameof(Hash)}: {Hash}, " : $"Name: {SyncListName}, ") +
                   $"{Operation}, " +
                   $"{nameof(Index)}: {Index}, " +
                   $"{nameof(Value)}: {Value}" +
                   ")";
        }
    }
}
