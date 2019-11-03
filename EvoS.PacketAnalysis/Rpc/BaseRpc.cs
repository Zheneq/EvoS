using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;

namespace EvoS.PacketAnalysis.Rpc
{
    public abstract class BaseRpc
    {
        [JsonIgnore] public NetworkInstanceId NetId;

        public abstract void Deserialize(NetworkReader reader, GameObject context);
    }
}
