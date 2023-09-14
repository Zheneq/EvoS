using Newtonsoft.Json;

namespace EvoS.Framework.Network.Unity
{
    public abstract class MessageBase
    {
        [JsonIgnore]
        public uint msgSeqNum;
        [JsonIgnore]
        public short msgType;
        
        public virtual void Deserialize(NetworkReader reader)
        {
        }
        
        public virtual void Deserialize(NetworkReader reader, Component context)
        {
            Deserialize(reader);
        }

        public virtual void Serialize(NetworkWriter writer)
        {
        }
    }
}
