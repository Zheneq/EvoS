using System.Collections.Generic;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(clientMsgIds: new short[] {50})]
    public class CastAbility : MessageBase
    {
        public int ActorIndex;
        public AbilityData.ActionType ActionType;
        public List<AbilityTarget> Targets;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(ActorIndex);
            writer.Write((int) ActionType);
            AbilityTarget.SerializeAbilityTargetList(Targets, writer);
        }

        public override void Deserialize(NetworkReader reader)
        {
            ActorIndex = reader.ReadInt32();
            ActionType = (AbilityData.ActionType) reader.ReadInt32();
            Targets = AbilityTarget.DeSerializeAbilityTargetList(reader);
        }

        public override string ToString()
        {
            return $"{nameof(CastAbility)}(" +
                   $"{nameof(ActorIndex)}: {ActorIndex}, " +
                   $"{nameof(ActionType)}: {ActionType}, " +
                   $"{nameof(Targets)}: {Targets.Count} entries" +
                   ")";
        }
    }
}
