using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.BridgeServer;

// A game for a single human that is launched right away, without a queue or character select
public abstract class SoloGame : Game
{
    private static readonly ILog log = LogManager.GetLogger(typeof(SoloGame));

    protected readonly long Owner;

    protected SoloGame(
        long owner,
        BridgeServerProtocol server,
        GameType gameType,
        string map,
        List<GameSubType> subTypes,
        List<LobbyServerPlayerInfo> players)
    {
        Owner = owner;
        GameSubType = subTypes[0];
        Map = map;

        TeamInfo = new LobbyServerTeamInfo
        {
            TeamPlayerInfo = players
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
            ActivePlayers = players.Count,
            CreateTimestamp = DateTime.UtcNow.Ticks,
            GameConfig = new LobbyGameConfig
            {
                GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect,
                GameServerShutdownTime = -1,
                GameType = gameType,
                InstanceSubTypeBit = 1,
                IsActive = true,
                Map = map,
                ResolveTimeoutLimit = 1600,
                RoomName = "",
                Spectators = 0,
                SubTypes = subTypes,
                TeamABots = GameSubType.TeamABots,
                TeamAPlayers = GameSubType.TeamAPlayers,
                TeamBBots = GameSubType.TeamBBots,
                TeamBPlayers = GameSubType.TeamBPlayers,
            },
            GameResult = GameResult.NoResult,
            GameServerAddress = server.URI,
            GameServerProcessCode = server.ProcessCode,
            GameStatus = GameStatus.Assembling,
        };

        AssignServer(server);
    }

    // Reserves a server and launches the game. Failures are reported through error so that the caller
    // can put them into the CreateGameResponse.
    public static T Start<T>(
        long accountId,
        Func<BridgeServerProtocol, T> create,
        LocalizationPayload noServerError,
        out LocalizationPayload error)
        where T : SoloGame
    {
        BridgeServerProtocol server = ServerManager.GetServer(true);
        if (server is null)
        {
            log.Warn($"No available server for {typeof(T).Name} of {LobbyServerUtils.GetHandle(accountId)}");
            error = noServerError;
            return null;
        }

        T game = create(server);
        if (!GameManager.RegisterGame(game.ProcessCode, game))
        {
            log.Error($"Failed to register {typeof(T).Name} for {LobbyServerUtils.GetHandle(accountId)}");
            server.Shutdown();
            error = LocalizationPayload.Create("UnknownErrorTryAgain@Frontend");
            return null;
        }
        game.Assign();
        _ = game.StartAsync();
        error = null;
        return game;
    }

    private void Assign()
    {
        GetClients().ForEach(c => c.JoinGame(this));
        SendGameAssignmentNotification(Owner);
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
        LobbyServerProtocol conn = SessionManager.GetClientConnection(Owner);
        if (conn?.CurrentGame == this)
        {
            conn.LeaveGame(this);
        }
    }

    private async Task StartAsync()
    {
        await Task.Yield(); // let CreateGameResponse be delivered before GameInfoNotification

        try
        {
            Launch();
        }
        catch (Exception e)
        {
            log.Error($"Failed to start {GameInfo.GameConfig.GameType} game for {LobbyServerUtils.GetHandle(Owner)}", e);
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

        log.Info($"{GameInfo.GameConfig.GameType} game started for {LobbyServerUtils.GetHandle(Owner)}");
    }
}
