using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(serverMsgIds: new short[] {64})]
    public class MsgSingleResolutionAction : MessageBase
    {
        public int TurnIndex;
        public AbilityPriority PhaseIndex;

        public override void Serialize(NetworkWriter writer)
        {
             writer.WritePackedUInt32((uint) TurnIndex);
             writer.Write((sbyte) PhaseIndex);
        }

        public override void Deserialize(NetworkReader reader)
        {
            TurnIndex = (int) reader.ReadPackedUInt32();
            PhaseIndex = (AbilityPriority) reader.ReadSByte();
        }

        public override string ToString()
        {
            return $"{nameof(MsgSingleResolutionAction)}(" +
                   $"{nameof(TurnIndex)}: {TurnIndex}, " +
                   $"{nameof(PhaseIndex)}: {PhaseIndex}" +
                   ")";
        }
    }
}
