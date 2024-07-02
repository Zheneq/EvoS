using System;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(540)]
    public struct CharacterModInfo
    {
        public int ModForAbility0;
        public int ModForAbility1;
        public int ModForAbility2;
        public int ModForAbility3;
        public int ModForAbility4;
        public static int AbilityCount = 5;

        public CharacterModInfo Reset()
        {
            ModForAbility0 = -1;
            ModForAbility1 = -1;
            ModForAbility2 = -1;
            ModForAbility3 = -1;
            ModForAbility4 = -1;
            return this;
        }

        public int GetModForAbility(int abilityIndex)
        {
            switch (abilityIndex)
            {
                case 0:
                    return ModForAbility0;
                case 1:
                    return ModForAbility1;
                case 2:
                    return ModForAbility2;
                case 3:
                    return ModForAbility3;
                case 4:
                    return ModForAbility4;
                default:
                    return -1;
            }
        }

        public void SetModForAbility(int abilityIndex, int mod)
        {
            switch (abilityIndex)
            {
                case 0:
                    ModForAbility0 = mod;
                    break;
                case 1:
                    ModForAbility1 = mod;
                    break;
                case 2:
                    ModForAbility2 = mod;
                    break;
                case 3:
                    ModForAbility3 = mod;
                    break;
                case 4:
                    ModForAbility4 = mod;
                    break;
            }
        }

        public override string ToString()
        {
            return $"{ModForAbility0}/{ModForAbility1}/{ModForAbility2}/{ModForAbility3}/{ModForAbility4}";
        }

        public override bool Equals(object obj)
        {
            if (!(obj is CharacterModInfo))
            {
                return false;
            }

            CharacterModInfo characterModInfo = (CharacterModInfo) obj;
            return ModForAbility0 == characterModInfo.ModForAbility0 &&
                   ModForAbility1 == characterModInfo.ModForAbility1 &&
                   ModForAbility2 == characterModInfo.ModForAbility2 &&
                   ModForAbility3 == characterModInfo.ModForAbility3 &&
                   ModForAbility4 == characterModInfo.ModForAbility4;
        }

        public override int GetHashCode()
        {
            return ModForAbility0.GetHashCode() ^ ModForAbility1.GetHashCode() ^ ModForAbility2.GetHashCode() ^
                   ModForAbility3.GetHashCode() ^ ModForAbility4.GetHashCode();
        }
    }
}
