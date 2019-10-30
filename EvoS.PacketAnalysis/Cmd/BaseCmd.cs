using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Cmd
{
    public abstract class BaseCmd
    {
        public NetworkInstanceId NetId;

        public abstract void Deserialize(NetworkReader reader);
    }
}
