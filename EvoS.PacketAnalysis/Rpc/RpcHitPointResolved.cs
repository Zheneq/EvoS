using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(189834458)]
    public class RpcHitPointResolved : BaseRpc
    {
        public uint ResolvedHitPoints;

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            ResolvedHitPoints = reader.ReadPackedUInt32();
        }

        public override string ToString()
        {
            return $"{nameof(RpcHitPointResolved)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   $"{nameof(ResolvedHitPoints)}: {ResolvedHitPoints}" +
                   ")";
        }
    }
}
