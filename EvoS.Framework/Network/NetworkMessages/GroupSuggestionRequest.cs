using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(371)]
	public class GroupSuggestionRequest : WebSocketMessage
	{
		public long LeaderAccountId;

		public string SuggestedAccountFullHandle;

		public string SuggesterAccountName;

		public long SuggesterAccountId;
	}
}
