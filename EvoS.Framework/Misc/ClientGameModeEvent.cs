using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Misc
{
    public class ClientGameModeEvent
    {
        public GameModeEventType m_eventType;
        public byte m_objectGuid;
        public BoardSquare m_square;
        public ActorData m_primaryActor;
        public ActorData m_secondaryActor;
        public int m_eventGuid;

        public ClientGameModeEvent(
            GameModeEventType eventType,
            byte objectGuid,
            BoardSquare square,
            ActorData primaryActor,
            ActorData secondaryActor,
            int eventGuid)
        {
            m_eventType = eventType;
            m_objectGuid = objectGuid;
            m_square = square;
            m_primaryActor = primaryActor;
            m_secondaryActor = secondaryActor;
            m_eventGuid = eventGuid;
        }

        public void ExecuteClientGameModeEvent()
        {
            var behav = (MonoBehaviour) m_square?.Board ?? m_primaryActor ?? m_secondaryActor;
            if (GameModeUtils.IsCtfGameModeEvent(this))
            {
                if (behav.CaptureTheFlag == null)
                    return;
                behav.CaptureTheFlag.ExecuteClientGameModeEvent(this);
            }
            else
            {
                if (!GameModeUtils.IsCtcGameModeEvent(this) || behav.CollectTheCoins == null)
                    return;
                behav.CollectTheCoins.ExecuteClientGameModeEvent(this);
            }
        }
    }
}
