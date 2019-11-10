using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(-701731415)]
    public class RpcMarkForRecalculateClientVisibility : BaseRpc
    {
        public override void Deserialize(NetworkReader reader, GameObject context)
        {
        }

        public override string ToString()
        {
            return $"{nameof(RpcMarkForRecalculateClientVisibility)}(" +
                   $"{nameof(NetId)}: {NetId.Value}" +
                   ")";
        }
    }
}
