using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(-2062592578)]
    public class RpcUpdateClientFog : BaseRpc
    {
        public override void Deserialize(NetworkReader reader, GameObject context)
        {
        }

        public override string ToString()
        {
            return $"{nameof(RpcUpdateClientFog)}(" +
                   $"{nameof(NetId)}: {NetId.Value}" +
                   ")";
        }
    }
}
