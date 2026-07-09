using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;

namespace Tests.Lib;

public static class TestAccountHelper
{
    public static PersistedAccountData MakeAccount(long accId, string username, float elo, int eloConfidenceLevel, string eloKey)
    {
        var acc = new PersistedAccountData
        {
            AccountId = accId,
            UserName = username,
            Handle = $"{username}#{accId}",
            ExperienceComponent = new ExperienceComponent
            {
                EloValues = new EloValues()
            },
            AccountComponent = new AccountComponent
            {
                LastCharacter = CharacterType.PendingWillFill
            },
            SocialComponent = new SocialComponent
            {
                BlockedAccounts = new HashSet<long>()
            },
        };

        acc.ExperienceComponent.EloValues.UpdateElo(eloKey, elo, eloConfidenceLevel);
        return acc;
    }
}
