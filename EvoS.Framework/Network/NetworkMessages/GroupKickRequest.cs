using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(366)]
	public class GroupKickRequest : WebSocketMessage
	{
		public string MemberName;

		public long AccountId;
	}
}
