using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(-107921272)]
    public class RpcTurnMessage : BaseRpc
    {
        public TurnMessage Msg;
        public int ExtraData;

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            Msg = (TurnMessage) reader.ReadPackedUInt32();
            ExtraData = (int) reader.ReadPackedUInt32();
        }

        public override string ToString()
        {
            return $"{nameof(RpcTurnMessage)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   $"{nameof(Msg)}: {Msg}, " +
                   $"{nameof(ExtraData)}: {ExtraData}" +
                   ")";
        }
    }
}
