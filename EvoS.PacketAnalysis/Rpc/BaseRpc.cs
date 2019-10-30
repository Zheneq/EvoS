using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    public abstract class BaseRpc
    {
        public NetworkInstanceId NetId;

        public abstract void Deserialize(NetworkReader reader);
    }
}
