using System;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(189)]
    public struct RankedResolutionPlayerState
    {
        public int PlayerId;
        public CharacterType Intention;
        public ReadyState OnDeckness;

        [EvosMessage(190)]
        public enum ReadyState
        {
            Unicode001D,
            Unselected,
            Selected
        }
    }
}
