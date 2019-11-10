using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(64425877)]
    public class RpcUpdateRemainingMovement : BaseRpc
    {
        public float RemainingMovement;
        public float RemainingMovementWithQueuedAbility;

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            RemainingMovement = reader.ReadSingle();
            RemainingMovementWithQueuedAbility = reader.ReadSingle();
        }

        public override string ToString()
        {
            return $"{nameof(RpcUpdateRemainingMovement)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   $"{nameof(RemainingMovement)}: {RemainingMovement}, " +
                   $"{nameof(RemainingMovementWithQueuedAbility)}: {RemainingMovementWithQueuedAbility}" +
                   ")";
        }
    }
}
