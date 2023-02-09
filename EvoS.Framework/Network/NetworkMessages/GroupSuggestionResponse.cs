using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(369)]
	public class GroupSuggestionResponse : WebSocketResponseMessage
	{
		[EvosMessage(370)]
		public enum Status
		{
			Online,
			Away,
			Busy
		}

		public Status SuggestionStatus;

		public long SuggesterAccountId;
	}
}
