﻿using EvoS.Framework.Network.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(368)]
    public class GroupLeaveRequest : WebSocketMessage
    {
        public long AccountId;
    }
}
