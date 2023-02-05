using EvoS.Framework.Network.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(382)]
    public class GroupChatResponse : WebSocketResponseMessage
    {
        public string Text;

        public LocalizationPayload LocalizedFailure;
    }
}
