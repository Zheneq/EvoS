using System.Collections.Generic;
using CentralServer.BridgeServer;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Practice;

public class PracticeGame(long accountId, BridgeServerProtocol server) : SoloGame(
    accountId,
    server,
    GameType.Practice,
    Maps.VR_Practice,
    SubTypes,
    [CreatePlayerInfo(accountId)])
{
    public static readonly List<GameSubType> SubTypes =
    [
        new()
        {
            LocalizedName = "GenericPractice@SubTypes",
            GameMapConfigs = [new GameMapConfig(Maps.VR_Practice, true)],
            RewardBucket = GameBalanceVars.GameRewardBucketType.NoRewards,
            PersistedStatBucket = PersistedStatBucket.DoNotPersist,
            TeamAPlayers = 1,
            TeamABots = 0,
            TeamBPlayers = 0,
            TeamBBots = 0,
            Mods =
            [
                GameSubType.SubTypeMods.AllowPlayingLockedCharacters,
                GameSubType.SubTypeMods.HumansHaveFirstSlots,
                GameSubType.SubTypeMods.AntiSocial
            ],
            TeamComposition = new TeamCompositionRules
            {
                Rules = new Dictionary<TeamCompositionRules.SlotTypes, FreelancerSet>
                {
                    {
                        TeamCompositionRules.SlotTypes.A1, new FreelancerSet
                        {
                            Roles = [CharacterRole.Tank, CharacterRole.Assassin, CharacterRole.Support]
                        }
                    }
                }
            }
        }
    ];

    private static LobbyServerPlayerInfo CreatePlayerInfo(long accountId)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);

        LobbyServerPlayerInfo playerInfo = LobbyServerPlayerInfo.Of(account);
        playerInfo.ReadyState = ReadyState.Ready;
        playerInfo.TeamId = Team.TeamA;
        playerInfo.PlayerId = 1;
        playerInfo.IsGameOwner = true;
        return playerInfo;
    }
}
