using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(serverMsgIds: new short[] {64})]
    public class MsgSingleResolutionAction : MessageBase
    {
        public int TurnIndex;
        public AbilityPriority PhaseIndex;
        public ClientResolutionActionMessageData Item;

        public override void Serialize(NetworkWriter writer)
        {
             writer.WritePackedUInt32((uint) TurnIndex);
             writer.Write((sbyte) PhaseIndex);
        }

        public override void Deserialize(NetworkReader reader)
        {
            Deserialize(reader, null);
        }

        public override void Deserialize(NetworkReader reader, Component context)
        {
            TurnIndex = (int) reader.ReadPackedUInt32();
            PhaseIndex = (AbilityPriority) reader.ReadSByte();

            if (context != null)
            {
                // deserializers are not implemented
                // IBitStream stream = new NetworkReaderAdapter(reader);
                // ClientResolutionAction action = ClientResolutionAction.ClientResolutionAction_DeSerializeFromStream(context, ref stream);
                // Item = new ClientResolutionActionMessageData(action, TurnIndex, (int)PhaseIndex);
            }
        }

        public override string ToString()
        {
            return $"{nameof(MsgSingleResolutionAction)}(" +
                   $"{nameof(TurnIndex)}: {TurnIndex}, " +
                   $"{nameof(PhaseIndex)}: {PhaseIndex}" +
                   ")";
        }
        
        public class ClientResolutionActionMessageData
        {
            public ClientResolutionAction m_action;
            public int m_turnIndex;
            public AbilityPriority m_phase;

            public ClientResolutionActionMessageData(ClientResolutionAction action, int turnIndex, int phaseIndex)
            {
                m_action = action;
                m_turnIndex = turnIndex;
                m_phase = (AbilityPriority)phaseIndex;
            }
        }
    }
}
