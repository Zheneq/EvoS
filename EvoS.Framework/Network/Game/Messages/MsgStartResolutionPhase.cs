using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(serverMsgIds: new short[] {58})]
    public class MsgStartResolutionPhase : MessageBase
    {
        public int CurrentTurnIndex;
        public AbilityPriority CurrentAbilityPhase;
        public int NumResolutionActionsThisPhase;

        public override void Serialize(NetworkWriter writer)
        {
             writer.Write(CurrentTurnIndex);
             writer.Write((sbyte) CurrentAbilityPhase);
             writer.Write((sbyte) NumResolutionActionsThisPhase);
        }

        public override void Deserialize(NetworkReader reader)
        {
            CurrentTurnIndex = reader.ReadInt32();
            CurrentAbilityPhase = (AbilityPriority) reader.ReadSByte();
            NumResolutionActionsThisPhase = reader.ReadSByte();
        }

        public override string ToString()
        {
            return $"{nameof(MsgStartResolutionPhase)}(" +
                   $"{nameof(CurrentTurnIndex)}: {CurrentTurnIndex}, " +
                   $"{nameof(CurrentAbilityPhase)}: {CurrentAbilityPhase}, " +
                   $"{nameof(NumResolutionActionsThisPhase)}: {NumResolutionActionsThisPhase}" +
                   ")";
        }
    }
}
