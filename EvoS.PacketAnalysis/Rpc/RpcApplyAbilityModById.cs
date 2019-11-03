using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    [Rpc(-1097840381)]
    public class RpcApplyAbilityModById : BaseRpc
    {
        public uint ActionTypeInt;
        public uint AbilityScopeId;

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            ActionTypeInt = reader.ReadPackedUInt32();
            AbilityScopeId = reader.ReadPackedUInt32();
        }

        public override string ToString()
        {
            return $"{nameof(RpcApplyAbilityModById)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   $"{nameof(ActionTypeInt)}: {ActionTypeInt}, " +
                   $"{nameof(AbilityScopeId)}: {AbilityScopeId}" +
                   ")";
        }
    }
}
