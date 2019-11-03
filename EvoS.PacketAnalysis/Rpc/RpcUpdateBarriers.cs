using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(73930193)]
    public class RpcUpdateBarriers : BaseRpc
    {
        public override void Deserialize(NetworkReader reader, GameObject context)
        {
        }

        public override string ToString()
        {
            return $"{nameof(RpcUpdateBarriers)}(" +
                   $"{nameof(NetId)}: {NetId.Value}" +
                   ")";
        }
    }
}
