using System.Collections.Generic;
using CentralServer.BridgeServer;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Tutorial;

public class TutorialGame(long accountId, BridgeServerProtocol server) : SoloGame(
    accountId,
    server,
    GameType.Tutorial,
    Maps.CargoShip_Tutorial1,
    SubTypes,
    [CreatePlayerInfo(accountId)])
{
    public static readonly List<GameSubType> SubTypes =
    [
        new()
        {
            LocalizedName = "GenericTutorial@SubTypes",
            GameMapConfigs = [new GameMapConfig(Maps.CargoShip_Tutorial1, true)],
            RewardBucket = GameBalanceVars.GameRewardBucketType.NoRewards,
            PersistedStatBucket = PersistedStatBucket.DoNotPersist,
            TeamAPlayers = 1,
            TeamABots = 0,
            TeamBPlayers = 0,
            Mods = [GameSubType.SubTypeMods.AllowPlayingLockedCharacters, GameSubType.SubTypeMods.AntiSocial]
        }
    ];

    private static LobbyServerPlayerInfo CreatePlayerInfo(long accountId)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);

        PersistedCharacterData charData = account.CharacterData.TryGetValue(CharacterType.Scoundrel, out var existing)
            ? existing
            : new PersistedCharacterData(CharacterType.Scoundrel);

        LobbyServerPlayerInfo playerInfo = LobbyServerPlayerInfo.Of(account);
        playerInfo.CharacterInfo = LobbyCharacterInfo.Of(charData);
        playerInfo.ReadyState = ReadyState.Ready;
        playerInfo.TeamId = Team.TeamA;
        playerInfo.PlayerId = 1;
        playerInfo.IsGameOwner = true;
        return playerInfo;
    }
}
