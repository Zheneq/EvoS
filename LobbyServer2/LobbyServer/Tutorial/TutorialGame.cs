using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Tutorial;

public class TutorialGame : Game
{
    private static readonly ILog log = LogManager.GetLogger(typeof(TutorialGame));

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

    private readonly long _owner;

    public TutorialGame(long accountId, BridgeServerProtocol server)
    {
        _owner = accountId;
        GameSubType = SubTypes[0];
        Map = Maps.CargoShip_Tutorial1;

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

        TeamInfo = new LobbyServerTeamInfo
        {
            TeamPlayerInfo = [playerInfo]
        };

        GameInfo = new LobbyGameInfo
        {
            AcceptedPlayers = 1,
            AcceptTimeout = TimeSpan.Zero,
            SelectTimeout = TimeSpan.Zero,
            LoadoutSelectTimeout = TimeSpan.Zero,
            SelectSubPhaseBan1Timeout = TimeSpan.Zero,
            SelectSubPhaseBan2Timeout = TimeSpan.Zero,
            SelectSubPhaseFreelancerSelectTimeout = TimeSpan.Zero,
            SelectSubPhaseTradeTimeout = TimeSpan.Zero,
            ActiveHumanPlayers = 1,
            ActivePlayers = 1,
            CreateTimestamp = DateTime.UtcNow.Ticks,
            GameConfig = new LobbyGameConfig
            {
                GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect,
                GameServerShutdownTime = -1,
                GameType = GameType.Tutorial,
                InstanceSubTypeBit = 1,
                IsActive = true,
                Map = Maps.CargoShip_Tutorial1,
                ResolveTimeoutLimit = 1600,
                RoomName = "",
                Spectators = 0,
                SubTypes = SubTypes,
                TeamABots = 0,
                TeamAPlayers = 1,
                TeamBBots = 0,
                TeamBPlayers = 0,
            },
            GameResult = GameResult.NoResult,
            GameServerAddress = server.URI,
            GameServerProcessCode = server.ProcessCode,
            GameStatus = GameStatus.Assembling,
        };

        AssignServer(server);
    }

    public void Assign()
    {
        GetClients().ForEach(c => c.JoinGame(this));
        SendGameAssignmentNotification(_owner);
    }

    protected sealed override void SendGameAssignmentNotification(long accountId, bool reconnection = false)
    {
        LobbyServerPlayerInfo playerInfo = GetPlayerInfo(accountId);
        if (playerInfo is null) return;

        ClientNotifier.Get().Send(accountId, new GameAssignmentNotification
        {
            GameInfo = GameInfo,
            GameResult = GameInfo.GameResult,
            Observer = false,
            PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig()),
            Reconnection = reconnection,
            GameplayOverrides = GameConfig.GetGameplayOverrides()
        });
    }

    protected override void SendGameAssignmentNotification(MatchPlayerData data, bool reconnection = false)
    {
        SendGameAssignmentNotification(data.AccountId, reconnection);
    }

    protected override TimeSpan GetFinalizeGameDelay() => TimeSpan.Zero;

    protected override bool ShouldProcessAccolades() => false;

    protected override bool ShouldSendStats() => false;

    protected override void LogDodge(string dodgerHandle) { }

    public override void Terminate()
    {
        base.Terminate();
        LobbyServerProtocol conn = SessionManager.GetClientConnection(_owner);
        if (conn?.CurrentGame == this)
        {
            conn.LeaveGame(this);
        }
    }

    public async Task StartTutorialAsync()
    {
        await Task.Yield(); // let CreateGameResponse be delivered before GameInfoNotification

        try
        {
            Launch();
        }
        catch (Exception e)
        {
            log.Error($"Failed to start tutorial game for {LobbyServerUtils.GetHandle(_owner)}", e);
            CancelMatch();
        }
    }

    private void Launch()
    {
        SetGameStatus(GameStatus.Launching);
        SendGameInfoNotifications();

        if (!CheckIfAllParticipantsAreConnected()) return;

        StartGame();

        SetGameStatus(GameStatus.Launched);
        SendGameInfoNotifications();

        GetClients().ForEach(c => c.OnStartGame(this));

        SetGameStatus(GameStatus.Started);
        SendGameInfoNotifications();

        log.Info($"Tutorial game started for {LobbyServerUtils.GetHandle(_owner)}");
    }
}