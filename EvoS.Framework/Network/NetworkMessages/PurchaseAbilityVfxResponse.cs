using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
	[Serializable]
	[EvosMessage(286)]
	public class PurchaseAbilityVfxResponse : WebSocketResponseMessage
	{
		public PurchaseResult Result;

		public CurrencyType CurrencyType;

		public CharacterType CharacterType;

		public int AbilityId;

		public int VfxId;
	}
}
