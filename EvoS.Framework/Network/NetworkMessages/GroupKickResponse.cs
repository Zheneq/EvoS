using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(365)]
	public class GroupKickResponse : WebSocketResponseMessage
	{
		public string MemberName;

		public LocalizationPayload LocalizedFailure;
	}
}
