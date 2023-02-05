using EvoS.Framework.Network.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(383)]
    public class GroupChatRequest : WebSocketMessage
    {
        public string Text;

        public List<int> RequestedEmojis;

        public long AccountId;
    }
}
