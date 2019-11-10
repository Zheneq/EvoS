using EvoS.Framework.Constants.Enums;

namespace EvoS.Framework.Misc
{
    public static class GameModeUtils
    {
        public static bool IsCtfGameModeEvent(ClientGameModeEvent gameModeEvent)
        {
            if (gameModeEvent == null)
                return false;
            return IsCtfGameModeEventType(gameModeEvent.m_eventType);
        }

        public static bool IsCtcGameModeEvent(ClientGameModeEvent gameModeEvent)
        {
            if (gameModeEvent == null)
                return false;
            return IsCtcGameModeEventType(gameModeEvent.m_eventType);
        }

        public static bool IsCtfGameModeEventType(GameModeEventType gameModeEventType)
        {
            return gameModeEventType == GameModeEventType.Ctf_FlagPickedUp ||
                   gameModeEventType == GameModeEventType.Ctf_FlagDropped ||
                   gameModeEventType == GameModeEventType.Ctf_FlagTurnedIn ||
                   gameModeEventType == GameModeEventType.Ctf_FlagSentToSpawn;
        }

        public static bool IsCtcGameModeEventType(GameModeEventType gameModeEventType)
        {
            return gameModeEventType == GameModeEventType.Ctc_CoinPickedUp ||
                   gameModeEventType == GameModeEventType.Ctc_CoinsDropped ||
                   gameModeEventType == GameModeEventType.Ctc_NonCoinPowerupTouched ||
                   gameModeEventType == GameModeEventType.Ctc_CoinPowerupTouched;
        }
    }
}
