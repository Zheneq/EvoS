using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(437)]
    public class ChatNotification : WebSocketMessage
    {
        public long SenderAccountId;
        public string SenderHandle;
        public Team SenderTeam;
        public string RecipientHandle;
        public CharacterType CharacterType;
        public ConsoleMessageType ConsoleMessageType;
        public string Text;
        public LocalizationPayload LocalizedText;
        public List<int> EmojisAllowed;
        public bool DisplayDevTag;

        public ChatNotification()
        {
            RequestId = 0;
            ResponseId = 0;
        }
    }
}
