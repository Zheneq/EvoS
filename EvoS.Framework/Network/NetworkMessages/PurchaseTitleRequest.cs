using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages;

[EvosMessage(295)]
[Serializable]
public class PurchaseTitleRequest : WebSocketMessage
{
    public CurrencyType CurrencyType;
    public int TitleId;
}
