﻿using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(284)]
    public class PurchaseOverconResponse : WebSocketResponseMessage
    {
        public PurchaseResult Result;

        public CurrencyType CurrencyType;

        public int OverconId;
    }
}
