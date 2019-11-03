using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(-559523706)]
    public class RpcSetMatchTime : BaseRpc
    {
        public float TimeSinceMatchStart;

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            TimeSinceMatchStart = reader.ReadSingle();
        }

        public override string ToString()
        {
            return $"{nameof(RpcSetMatchTime)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   $"{nameof(TimeSinceMatchStart)}: {TimeSinceMatchStart}" +
                   ")";
        }
    }
}
