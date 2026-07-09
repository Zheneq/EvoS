using EvoS.Framework.Network.Static;

namespace EvoS.Framework.Misc;

public static class AccessUtils
{
    public static ClientAccessLevel GetClientAccessLevel(PersistedAccountData account)
    {
        return account.AccountComponent.IsDev()
            ? ClientAccessLevel.Admin
            : account.AccountComponent.IsVip()
                ? ClientAccessLevel.VIP
                : ClientAccessLevel.Full;
    }
}