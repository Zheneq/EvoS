using EvoS.Framework.Network;

namespace EvoS.Framework.Constants.Enums
{
    [EvosMessage(438)]
    public enum ConsoleMessageType
    {
        GlobalChat,
        GameChat,
        TeamChat,
        GroupChat,
        WhisperChat,
        CombatLog,
        SystemMessage,
        Error,
        Exception,
        BroadcastMessage,
        PingChat,
        ScriptedChat,
        NUM_VALUES
    }
}
