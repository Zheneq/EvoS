using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(287)]
	public class PurchaseAbilityVfxRequest : WebSocketMessage
	{
		public CurrencyType CurrencyType;

		public CharacterType CharacterType;

		public int AbilityId;

		public int VfxId;
	}
}
