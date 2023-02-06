using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(376)]
	public class GroupPromoteResponse : WebSocketResponseMessage
	{
		public LocalizationPayload LocalizedFailure;
	}
}
