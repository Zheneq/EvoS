using System;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(193)]
    public struct RankedTradeData
    {
        public TradeActionType TradeAction;
        public CharacterType DesiredCharacter;
        public int AskedPlayerId;
        public CharacterType OfferedCharacter;
        public int OfferingPlayerId;

        [EvosMessage(194)]
        public enum TradeActionType
        {
            AcceptOrOffer,
            Reject,
            StopTrading
        }
    }
}
