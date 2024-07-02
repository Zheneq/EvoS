using System;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(543)]
    public struct CharacterAbilityVfxSwapInfo
    {
        public CharacterAbilityVfxSwapInfo Reset()
        {
            VfxSwapForAbility0 = 0;
            VfxSwapForAbility1 = 0;
            VfxSwapForAbility2 = 0;
            VfxSwapForAbility3 = 0;
            VfxSwapForAbility4 = 0;

            return this;
        }

        public int GetAbilityVfxSwapIdForAbility(int abilityIndex)
        {
            switch (abilityIndex)
            {
                case 0:
                    return VfxSwapForAbility0;
                case 1:
                    return VfxSwapForAbility1;
                case 2:
                    return VfxSwapForAbility2;
                case 3:
                    return VfxSwapForAbility3;
                case 4:
                    return VfxSwapForAbility4;
                default:
                    return -1;
            }
        }

        public void SetAbilityVfxSwapIdForAbility(int abilityIndex, int vfxSwapUniqueId)
        {
            switch (abilityIndex)
            {
                case 0:
                    VfxSwapForAbility0 = vfxSwapUniqueId;
                    break;
                case 1:
                    VfxSwapForAbility1 = vfxSwapUniqueId;
                    break;
                case 2:
                    VfxSwapForAbility2 = vfxSwapUniqueId;
                    break;
                case 3:
                    VfxSwapForAbility3 = vfxSwapUniqueId;
                    break;
                case 4:
                    VfxSwapForAbility4 = vfxSwapUniqueId;
                    break;
            }
        }

        public string ToIdString()
        {
            return string.Format("{0}/{1}/{2}/{3}/{4}", VfxSwapForAbility0.ToString(), VfxSwapForAbility1.ToString(),
                VfxSwapForAbility2.ToString(), VfxSwapForAbility3.ToString(), VfxSwapForAbility4.ToString());
        }

        public override bool Equals(object obj)
        {
            if (!(obj is CharacterAbilityVfxSwapInfo))
            {
                return false;
            }

            CharacterAbilityVfxSwapInfo characterAbilityVfxSwapInfo = (CharacterAbilityVfxSwapInfo) obj;
            return VfxSwapForAbility0 == characterAbilityVfxSwapInfo.VfxSwapForAbility0 &&
                   VfxSwapForAbility1 == characterAbilityVfxSwapInfo.VfxSwapForAbility1 &&
                   VfxSwapForAbility2 == characterAbilityVfxSwapInfo.VfxSwapForAbility2 &&
                   VfxSwapForAbility3 == characterAbilityVfxSwapInfo.VfxSwapForAbility3 &&
                   VfxSwapForAbility4 == characterAbilityVfxSwapInfo.VfxSwapForAbility4;
        }

        public override int GetHashCode()
        {
            return VfxSwapForAbility0.GetHashCode() ^ VfxSwapForAbility1.GetHashCode() ^
                   VfxSwapForAbility2.GetHashCode() ^ VfxSwapForAbility3.GetHashCode() ^
                   VfxSwapForAbility4.GetHashCode();
        }

        public int VfxSwapForAbility0;
        public int VfxSwapForAbility1;
        public int VfxSwapForAbility2;
        public int VfxSwapForAbility3;
        public int VfxSwapForAbility4;
        public static int AbilityCount = 5;
    }
}
