using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;

namespace EvoS.PacketAnalysis.Cmd
{
    public abstract class BaseCmd
    {
        [JsonIgnore] public NetworkInstanceId NetId;

        public abstract void Deserialize(NetworkReader reader, GameObject context);
    }
}
