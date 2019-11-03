using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(939569152)]
    public class RpcUpdateTimeRemaining : BaseRpc
    {
        public float TimeRemaining;

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            TimeRemaining = reader.ReadSingle();
        }

        public override string ToString()
        {
            return $"{nameof(RpcUpdateTimeRemaining)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   $"{nameof(TimeRemaining)}: {TimeRemaining}" +
                   ")";
        }
    }
}
