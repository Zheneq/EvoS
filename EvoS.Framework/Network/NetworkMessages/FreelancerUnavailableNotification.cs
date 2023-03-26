using System;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(182)]
	public class FreelancerUnavailableNotification : WebSocketMessage
	{
		public CharacterType oldCharacterType;

		public CharacterType newCharacterType;

		public string thiefName;

		public bool ItsTooLateToChange;
	}
}
