using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(377)]
	public class GroupPromoteRequest : WebSocketMessage
	{
		public string Name;

		public long AccountId;
	}
}
