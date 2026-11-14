using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Stats;
using CentralServer.LobbyServer.TrustWar;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.GameData;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using MoreLinq;

namespace CentralServer.BridgeServer;

public abstract class Game : IDraftHost
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Game));
    public static readonly object characterSelectionLock = new object();
    private readonly Lock ggPackLock = new();
    public bool IsControlAllBots => GameSubType?.Mods.Contains(GameSubType.SubTypeMods.ControlAllBots) ?? false;

    public LobbyGameInfo GameInfo { protected set; get; } // TODO check it is set when needed
    public LobbyServerTeamInfo TeamInfo { protected set; get; } = new LobbyServerTeamInfo() { TeamPlayerInfo = new List<LobbyServerPlayerInfo>() };
    public string Map { protected set; get; } // TODO check it is set when needed

    public ServerGameMetrics GameMetrics { get; private set; } = new ServerGameMetrics();
    public LobbyGameSummary GameSummary { get; protected set; }
    private List<MatchPlayerData> TeamA;
    private List<MatchPlayerData> TeamB;
    protected IEnumerable<MatchPlayerData> Players => (TeamA ?? []).Concat(TeamB ?? []);
    public DateTime StopTime { protected set; get; }
    
    // Set when the game server reports Started, i.e. once every player has loaded (or timed out loading).
    // GameStatus can't tell this reliably because the lobby sets Started itself right after launching the game.
    // TODO fix GameStatus?
    public bool MatchStarted { private set; get; }

    // Players penalized for leaving this game, with the block they got and when they last left,
    // so that it can be re-applied if they leave again, or pardoned later
    public Dictionary<long, QueuePenaltyManager.GamePenalty> PenalizedPlayers { get; } = new();

    // Set when an admin ends the game
    private bool EndedByAdmin;

    // Set when an admin ends the game with no result. The game server can't do that, so it ends the game as a tie.
    private bool EndedByAdminWithNoResult;

    // Whether the game has ended with a win or a tie. Losing the game server and an admin ending the game
    // with no result are recorded as a tie, but they are not a result.
    public bool HasResult => !EndedByAdminWithNoResult
                             && GameSummary?.GameResult is GameResult.TieGame or GameResult.TeamAWon or GameResult.TeamBWon;

    // Whether a team has won the game by itself. Ties and games ended by an admin don't count.
    public bool EndedNaturally => !EndedByAdmin
                                  && GameSummary?.GameResult is GameResult.TeamAWon or GameResult.TeamBWon;
    
    public BridgeServerProtocol Server { private set; get; } // TODO check it is set when needed

    public bool IsCancelled { private set; get; }

    public GameSubType GameSubType { protected set; get; } // can be null

    protected IReadOnlyDictionary<long, DateTime> QueueEntryTimes { get; set; } = new Dictionary<long, DateTime>();

    public string ProcessCode => GameInfo?.GameServerProcessCode;
    public GameStatus GameStatus => GameInfo?.GameStatus ?? GameStatus.None;

    private bool DodgeReported;

    // Draft
    public DraftController Draft { get; private set; }
    public FreelancerResolutionPhaseSubType PhaseSubType => Draft?.PhaseSubType ?? FreelancerResolutionPhaseSubType.UNDEFINED;
    public bool IsDraft => GameSubType?.Mods.Contains(GameSubType.SubTypeMods.RankedFreelancerSelection) ?? false;
    public bool IsDrafting => IsDraft && GameStatus == GameStatus.FreelancerSelecting;

    public void AssignServer(BridgeServerProtocol server)
    {
        if (Server is not null)
        {
            Server.OnGameEnded -= OnGameEnded;
            Server.OnStatusUpdate -= OnStatusUpdate;
            Server.OnGameMetricsUpdate -= OnGameMetricsUpdate;
            Server.OnPlayerDisconnect -= OnPlayerDisconnect;
            Server.OnServerDisconnect -= OnServerDisconnect;
        }

        Server = server;
        // TODO clear on destroy
        if (Server is not null)
        {
            Server.OnGameEnded += OnGameEnded;
            Server.OnStatusUpdate += OnStatusUpdate;
            Server.OnGameMetricsUpdate += OnGameMetricsUpdate;
            Server.OnPlayerDisconnect += OnPlayerDisconnect;
            Server.OnServerDisconnect += OnServerDisconnect;
        }

        log.Info($"{GetType().Name} {GameUtils.GameIdString(GameInfo)} is assigned to server {Server?.Name}");
    }

    public virtual void DisconnectPlayer(long accountId)
    {
        if (Server is null) return;
        GetPlayerInfos(accountId).ForEach(Server.DisconnectPlayer);
    }

    public virtual void OnPlayerDisconnectedFromLobby(long accountId)
    {

    }

    protected void OnGameEnded(BridgeServerProtocol server, LobbyGameSummary summary, LobbyGameSummaryOverrides overrides)
    {
        DateTime matchEndTime = DateTime.UtcNow;
        if (GameInfo is not null) // we can end up here before the game started assembling
        {
            GameInfo.GameResult = summary?.GameResult ?? GameResult.TieGame;
            
            if (GameInfo.GameResult is GameResult.NoResult or GameResult.TieGame)
            {
                GrantQueuePriorityToInnocents(null);
            }
        }

        GameSummary = summary;

        if (GameSummary != null)
        {
            log.Info($"Game {GameInfo?.Name} at {GameSummary.GameServerAddress} finished " +
                     $"({GameSummary.NumOfTurns} turns), " +
                     $"{GameSummary.GameResult} {GameSummary.TeamAPoints}-{GameSummary.TeamBPoints}");

            if (GameSummary.GameResult is >= GameResult.TieGame and <= GameResult.TeamBWon)
            {
                if (ShouldProcessAccolades())
                {
                    try
                    {
                        GameSummary.BadgeAndParticipantsInfo = AccoladeUtils.ProcessGameSummary(GameSummary);
                    }
                    catch (Exception ex)
                    {
                        log.Error("Failed to process game summary", ex);
                    }
                }
                DB.Get().MatchHistoryDao.Save(MatchHistoryDao.MatchEntry.Cons(GameInfo, GameSummary, matchEndTime));
            }
        }
        else
        {
            log.Info($"Game {GameInfo?.Name} at {server.Name} ({server.URI}) finished abruptly");
        }
        try
        {
            MatchmakingManager.OnGameEnded(GameInfo, GameSummary, GameSubType, Players.ToList());
        }
        catch (Exception e)
        {
            log.Error("Failed to update elo", e);
        }

        if (GameInfo is not null) // we can end up here before the game started assembling
        {
            GameInfo.GameStatus = GameStatus.Stopped;
        }
        StopTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(8));

        QueuePenaltyManager.OnGameEnded(this);

        _ = FinalizeGame();
    }

    // The game server only ends a running match on an admin's request, so a game that isn't running is shut down.
    // It can't end a match with no result either, so that is sent as a tie, while the lobby still treats it
    // as having no result.
    public void AdminEndGame(GameResult gameResult)
    {
        if (GameStatus == GameStatus.Started)
        {
            EndedByAdmin = true;
            if (gameResult == GameResult.NoResult)
            {
                EndedByAdminWithNoResult = true;
                gameResult = GameResult.TieGame;
            }
            Server?.AdminShutdown(gameResult);
        }
        else
        {
            Server?.Shutdown();
        }
    }

    public void AdminClearCooldowns()
    {
        Server?.AdminClearCooldown();
    }

    protected void OnStatusUpdate(BridgeServerProtocol server, GameStatus newStatus)
    {
        log.Info($"Game {GameInfo?.Name} {newStatus}");

        GameInfo.GameStatus = newStatus;

        if (newStatus == GameStatus.Started)
        {
            MatchStarted = true;
        }

        if (GameInfo.GameStatus == GameStatus.Stopped)
        {
            foreach (LobbyServerProtocol client in GetClients())
            {
                if (!client.LeaveGame(this))
                {
                    continue;
                }

                if (GameInfo != null)
                {
                    //Unready people when game is finisht
                    ForceMatchmakingQueueNotification forceMatchmakingQueueNotification =
                        new ForceMatchmakingQueueNotification()
                        {
                            Action = ForceMatchmakingQueueNotification.ActionType.Leave,
                            GameType = GameInfo.GameConfig.GameType
                        };
                    client.Send(forceMatchmakingQueueNotification);
                }
            }
        }
    }

    private void OnGameMetricsUpdate(BridgeServerProtocol server, ServerGameMetrics gameMetrics)
    {
        GameMetrics = gameMetrics;
        log.Info($"Game {GameInfo?.Name} Turn {GameMetrics.CurrentTurn}, " +
                 $"{GameMetrics.TeamAPoints}-{GameMetrics.TeamBPoints}, " +
                 $"frame time: {GameMetrics.AverageFrameTime}");
    }

    protected void OnPlayerDisconnect(BridgeServerProtocol server, LobbyServerPlayerInfo playerInfo, LobbySessionInfo sessionInfo)
    {
        log.Info($"{LobbyServerUtils.GetHandle(playerInfo.AccountId)} left game {GameInfo?.GameServerProcessCode}");

        foreach (LobbyServerProtocol client in GetClients())
        {
            if (client.AccountId == playerInfo.AccountId)
            {
                client.LeaveGame(this);
                break;
            }
        }

        LobbyServerPlayerInfo lobbyPlayerInfo = GetPlayerInfo(playerInfo.AccountId);
        if (lobbyPlayerInfo != null)
        {
            lobbyPlayerInfo.ReplacedWithBots = true;
        }

        QueuePenaltyManager.IssueQueuePenalties(playerInfo.AccountId, this);
    }

    protected async void OnServerDisconnect(BridgeServerProtocol server)
    {
        await Task.Delay(LobbyConfiguration.GetServerReconnectionTimeout());
        if (Server != server || GameStatus == GameStatus.Stopped)
        {
            return;
        }

        // If the server doesn't come back, a game that is still being set up can't start,
        // and a launched one ends with no result
        if (GameStatus is >= GameStatus.FreelancerSelecting and <= GameStatus.Launching)
        {
            CancelMatch();
        }
        else
        {
            OnGameEnded(server, null, null);
        }
    }

    protected void StartGame()
    {
        Dictionary<int, LobbySessionInfo> sessionInfos = TeamInfo.TeamPlayerInfo
            .ToDictionary(
                playerInfo => playerInfo.PlayerId,
                playerInfo => SessionManager.GetSessionInfo(playerInfo.AccountId) ?? new LobbySessionInfo());  // fallback for bots TODO something smarter

        Server.SendJoinGameRequests(TeamInfo, sessionInfos, GameInfo.GameServerProcessCode);
        Server.LaunchGame(GameInfo, TeamInfo, sessionInfos);
    }

    protected void SetGameStatus(GameStatus status)
    {
        GameStatus oldStatus = GameInfo.GameStatus;
        GameInfo.GameStatus = status;
        if (oldStatus <= GameStatus.FreelancerSelecting && status > GameStatus.FreelancerSelecting)
        {
            UpdateFriendStatuses();
        }
    }

    protected virtual bool SetSecondaryCharacter(long accountId, int playerId, LobbyCharacterInfo characterInfo)
    {
        LobbyServerPlayerInfo lobbyServerPlayerInfo = TeamInfo.TeamPlayerInfo.Find(p => p.PlayerId == playerId);
        if (lobbyServerPlayerInfo is null)
        {
            log.Error($"Failed to set secondary character: {playerId} not found");
            return false;
        }
        if (lobbyServerPlayerInfo.AccountId != accountId)
        {
            log.Error($"Failed to set secondary character: {playerId} does not belong to {LobbyServerUtils.GetHandle(accountId)}");
            return false;
        }
        lobbyServerPlayerInfo.CharacterInfo = characterInfo;
        return true;
    }

    // TODO there can be multiple
    public LobbyServerPlayerInfo GetPlayerInfo(long accountId)
    {
        return TeamInfo.TeamPlayerInfo.Find(p => p.AccountId == accountId && !p.IsRemoteControlled);
    }
    
    public IEnumerable<LobbyServerPlayerInfo> GetPlayerInfos(long accountId)
    {
        return TeamInfo.TeamPlayerInfo.Where(p => p.AccountId == accountId);
    }

    public LobbyServerPlayerInfo GetPlayerById(int playerId)
    {
        return TeamInfo.TeamPlayerInfo.Find(p => p.PlayerId == playerId);
    }

    // TODO distinct?
    public IEnumerable<long> GetPlayers(Team team)
    {
        return from p in TeamInfo.TeamInfo(team) select p.AccountId;
    }
    
    public IEnumerable<long> GetPlayersDistinct(Team team)
    {
        return GetPlayers(team).Distinct();
    }

    // TODO distinct?
    public IEnumerable<long> GetPlayers()
    {
        return from p in TeamInfo.TeamPlayerInfo select p.AccountId;
    }
    
    public IEnumerable<long> GetPlayersDistinct()
    {
        return GetPlayers().Distinct();
    }

    // No bots, no remote-controlled characters
    // IsAIControlled/IsHumanControlled can't be used, as they also account for ReplacedWithBots.
    public IEnumerable<LobbyServerPlayerInfo> GetHumanPlayerInfos()
    {
        return TeamInfo.TeamPlayerInfo.Where(p =>
            !p.IsNPCBot && !p.IsLoadTestBot && !p.IsRemoteControlled && !p.IsSpectator);
    }

    public List<LobbyServerProtocol> GetClients()
    {
        List<LobbyServerProtocol> clients = new List<LobbyServerProtocol>();

        if (TeamInfo?.TeamPlayerInfo == null)
        {
            return clients;
        }

        HashSet<long> accountIds = new HashSet<long>();
        foreach (LobbyServerPlayerInfo player in TeamInfo.TeamPlayerInfo)
        {
            if (player.IsNPCBot
                || player.ReplacedWithBots
                || accountIds.Contains(player.AccountId))
            {
                continue;
            }
            LobbyServerProtocol client = SessionManager.GetClientConnection(player.AccountId);
            if (client != null)
            {
                accountIds.Add(client.AccountId);
                clients.Add(client);
            }
        }

        return clients;
    }

    protected void ForceReady()
    {
        TeamInfo.TeamPlayerInfo.ForEach(p => p.ReadyState = ReadyState.Ready);
    }

    public void ForceUnReady()
    {
        TeamInfo.TeamPlayerInfo.ForEach(p => p.ReadyState = ReadyState.Unknown);
    }

    public virtual void OnAccountVisualsUpdated(long accountId)
    {
        LobbyServerPlayerInfo serverPlayerInfo = GetPlayerInfo(accountId);
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
        if (account != null)
        {
            serverPlayerInfo.TitleID = account.AccountComponent.SelectedTitleID;
            serverPlayerInfo.TitleLevel = account.AccountComponent.TitleLevels.GetValueOrDefault(account.AccountComponent.SelectedTitleID, 1);
            serverPlayerInfo.BannerID = account.AccountComponent.SelectedBackgroundBannerID;
            serverPlayerInfo.EmblemID = account.AccountComponent.SelectedForegroundBannerID;
            serverPlayerInfo.RibbonID = account.AccountComponent.SelectedRibbonID;
        }
    }

    public int OnPlayerUsedGGPack(long accountId)
    {
        lock (ggPackLock)
        {
            Dictionary<long, int> ggPackUsedAccountIDs = new Dictionary<long, int>(GameInfo.ggPackUsedAccountIDs);
            int count = ggPackUsedAccountIDs.GetValueOrDefault(accountId) + 1;
            ggPackUsedAccountIDs[accountId] = count;
            GameInfo.ggPackUsedAccountIDs = ggPackUsedAccountIDs;
            return count;
        }
    }

    protected virtual void SendGameAssignmentNotification(MatchPlayerData data, bool reconnection = false)
    {
        LobbyServerPlayerInfo playerInfo = GetPlayerInfo(data.AccountId);
        GameAssignmentNotification notification = new GameAssignmentNotification
        {
            GameInfo = BuildGameInfo(GameInfo.GameConfig.GameType, GameInfo.GameConfig.SubTypes, data.SubTypeIndex, GameInfo.GameStatus),
            GameResult = GameInfo.GameResult,
            Observer = false,
            PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig()),
            Reconnection = reconnection,
            GameplayOverrides = GameConfig.GetGameplayOverrides()
        };

        ClientNotifier.Get().Send(data.AccountId, notification);
    }

    public void SendGameInfoNotifications()
    {
        if (GameInfo is null)
        {
            log.Warn($"Attempting to send game info notifications before creating game info!");
            return;
        }

        GameInfo.ActivePlayers = TeamInfo.TeamPlayerInfo.Count;
        GameInfo.UpdateTimestamp = DateTime.UtcNow.Ticks;
        foreach (long player in GetPlayersDistinct())
        {
            LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
            if (playerConnection != null && playerConnection.CurrentGame == this)
            {
                SendGameInfo(playerConnection);
            }
        }
    }

    public void SendGameInfo(IClientConnection playerConnection, GameStatus gamestatus = GameStatus.None)
    {
        // TODO do not mutate on send
        if (gamestatus != GameStatus.None)
        {
            GameInfo.GameStatus = gamestatus;
        }

        // TODO we need a common way to track these personalized subtype bits
        var gameInfo = GameInfo.Clone();
        var player = Players.FirstOrDefault(d => d.AccountId == playerConnection.AccountId);
        if (player != null)
        {
            gameInfo.GameConfig.InstanceSubTypeBit = (ushort)(1 << player.SubTypeIndex);
        }

        LobbyServerPlayerInfo playerInfo = GetPlayerInfo(playerConnection.AccountId);
        GameInfoNotification notification = new GameInfoNotification
        {
            GameInfo = gameInfo,
            TeamInfo = LobbyTeamInfo.FromServer(TeamInfo, 0, new MatchmakingQueueConfig()),
            PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig())
        };

        playerConnection.Send(notification);
    }

    protected virtual void SendGameAssignmentNotification(long accountId, bool reconnection = false)
    {
        var player = Players.FirstOrDefault(p => p.AccountId == accountId);
        if (player is null)
        {
            log.Error($"Failed to send game assignment to {LobbyServerUtils.GetHandle(accountId)}");
            return;
        }
        SendGameAssignmentNotification(player, reconnection);
    }

    public virtual void SetPlayerReady(long accountId)
    {
        GetPlayerInfo(accountId).ReadyState = ReadyState.Ready;
    }

    public virtual void SetPlayerUnReady(long accountId)
    {
        GetPlayerInfo(accountId).ReadyState = ReadyState.Unknown;
    }

    protected bool CheckIfAllParticipantsAreConnected()
    {
        return CheckIfServerIsConnected() && CheckIfPlayersAreConnected();
    }

    private bool CheckIfServerIsConnected()
    {
        bool res = true;
        if (Server is null)
        {
            log.Error($"Failed to find server reserved for game {GameInfo.Name}");
            res = false;
        }
        else if (!Server.IsConnected)
        {
            log.Error($"Server {Server.URI} reserved for game {GameInfo.Name} has disconnected");
            res = false;
        }

        if (!res)
        {
            CancelMatch();
        }

        return res;
    }

    private bool CheckIfPlayersAreConnected()
    {
        foreach (LobbyServerPlayerInfo playerInfo in TeamInfo.TeamPlayerInfo)
        {
            if (playerInfo.IsAIControlled || playerInfo.TeamId != Team.TeamA && playerInfo.TeamId != Team.TeamB)
            {
                continue;
            }
            LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(playerInfo.AccountId);
            if (playerConnection == null || !playerConnection.IsConnected || playerConnection.CurrentGame != this)
            {
                log.Info($"Player {playerInfo.Handle}/{playerInfo.AccountId} who was to participate in game {GameInfo.Name} has disconnected");
                CancelMatch(playerInfo.Handle, playerInfo.AccountId);
                return false;
            }
        }

        return true;
    }

    protected void CancelMatch(string dodgerHandle = null, long? dodgerAccountId = null)
    {
        // Once canceled, every player has left the game, so any later connectivity check would cancel it again,
        // blaming whoever it happens to check first
        lock (this)
        {
            if (IsCancelled)
            {
                return;
            }
            IsCancelled = true;
        }

        foreach (LobbyServerProtocol client in GetClients())
        {
            if (dodgerHandle != null)
            {
                client.SendSystemMessage(LocalizationPayload.Create(
                    "PlayerDisconnected", "Disconnect", LocalizationArg_Handle.Create(dodgerHandle)));
            }
            else
            {
                client.SendSystemMessage(LocalizationPayload.Create("FailedStartGameServer", "Frontend"));
            }

            if (client.LeaveGame(this))
            {
                // Players who have already left (and maybe joined another game) must not be unassigned again
                client.Send(new GameAssignmentNotification 
                {
                    GameInfo = null,
                    GameResult = GameResult.NoResult,
                    Reconnection = false
                });
            }
        }

        if (dodgerHandle != null)
        {
            LogDodge(dodgerHandle);
        }

        // Fallback for callers that only know the handle; the dodger has usually already dropped
        // their session by now, so an online lookup alone would come back null.
        dodgerAccountId ??= dodgerHandle != null
            ? SessionManager.GetOnlinePlayerByHandleOrUsername(dodgerHandle)
            : null;
        if (dodgerAccountId.HasValue)
        {
            QueuePenaltyManager.IssueQueuePenalties(dodgerAccountId.Value, this);
        }
        GrantQueuePriorityToInnocents(dodgerAccountId);

        UpdateFriendStatuses();
        Terminate();
    }

    // Match "formation" time; used to decide whether a cancellation happened early enough to
    // warrant retaining queue priority. Null GameInfo means we never got past assembling.
    private bool WithinQueuePriorityWindow()
    {
        if (GameInfo is null)
        {
            return true;
        }
        DateTime formedAt = new DateTime(GameInfo.CreateTimestamp, DateTimeKind.Utc);
        return DateTime.UtcNow - formedAt <= LobbyConfiguration.GetQueuePriorityMatchAgeWindow();
    }

    // Grants a queue priority credit to every matched human who is not at fault for the
    // cancellation (i.e. not the dodger and not someone who left / was replaced with bots).
    private void GrantQueuePriorityToInnocents(long? culpritAccountId)
    {
        if (!WithinQueuePriorityWindow())
        {
            return;
        }
        foreach (KeyValuePair<long, DateTime> entry in QueueEntryTimes)
        {
            long accountId = entry.Key;
            if (accountId == culpritAccountId)
            {
                continue;
            }
            if (GetPlayerInfo(accountId)?.ReplacedWithBots == true)
            {
                continue;
            }
            QueuePriorityManager.GrantCredit(accountId, entry.Value);
        }
    }

    protected virtual void LogDodge(string dodgerHandle)
    {
        if (DodgeReported)
        {
            return;
        }

        DodgeReported = true;
        
        string statusString = "unknown";
        long? accountId = SessionManager.GetOnlinePlayerByHandleOrUsername(dodgerHandle);
        if (accountId.HasValue)
        {
            LobbyServerProtocol conn = SessionManager.GetClientConnection(accountId.Value);
            statusString = FriendManager.GetStatusString(conn);
        }

        DiscordManager.Get().SendAdminLogMessageAsync($"Game {ProcessCode} was cancelled because of {dodgerHandle
        } (status: {statusString})");
    }

    public virtual void Terminate()
    {
        log.Info($"Terminating {ProcessCode}");
        Server?.Shutdown();
        GameManager.UnregisterGame(ProcessCode);
    }

    protected virtual TimeSpan GetFinalizeGameDelay()
    {
        return LobbyConfiguration.GetServerGGTime();
    }

    protected virtual bool ShouldProcessAccolades()
    {
        return true;
    }

    protected virtual bool ShouldSendStats()
    {
        return true;
    }

    public async Task FinalizeGame()
    {
        if (GameSummary != null)
        {
            //Wait 5 seconds for gg Usages
            // TODO game server is supposed to send game summary after GGs (see ObjectivePoints.EndGame)
            await Task.Delay(GetFinalizeGameDelay());

            foreach (LobbyServerProtocol client in GetClients())
            {
                MatchResultsNotification response = new MatchResultsNotification
                {
                    BadgeAndParticipantsInfo = GameSummary.BadgeAndParticipantsInfo,
                    //Todo xp and stuff
                    BaseXpGained = 0,
                    CurrencyRewards = new List<MatchResultsNotification.CurrencyReward>()
                };
                client?.Send(response);
            }

            // TrustWar
            TrustWarManager.CalculateTrustWar(this, GameSummary);

            // Send stats to api, no longer rely on discord
            if (ShouldSendStats())
            {
                await StatsApi.Get().ParseStats(GameInfo, Server?.Name, Server?.BuildVersion, GameSummary);
            }

            DiscordManager.Get().SendGameReport(GameInfo, Server?.Name, Server?.BuildVersion, GameSummary);
            DiscordManager.Get().SendAdminGameReport(GameInfo, Server?.Name, Server?.BuildVersion, GameSummary);

            //Wait a bit so people can look at stuff but we do have to send it so server can restart
            await Task.Delay(LobbyConfiguration.GetServerShutdownTime());
        }
        SendGameInfoNotifications(); // sending GameStatus.Stopped to the client triggers leaving the game
        Terminate();
    }

    protected bool FillTeam(List<MatchPlayerData> players, Team team, GameSubType gameSubType)
    {
        switch (team)
        {
            case Team.TeamA:
                TeamA = players;
                break;
            case Team.TeamB:
                TeamB = players;
                break;
        }
        
        int botNum = team == Team.TeamA ? gameSubType.TeamABots : gameSubType.TeamBBots;
        int multiCharacterAdjustment = players.Select(data => data.NumControlledCharacters).Sum() - players.Count;
        botNum += multiCharacterAdjustment;
        
        int playerNum = (team == Team.TeamA ? gameSubType.TeamAPlayers : gameSubType.TeamBPlayers) - botNum;

        if (playerNum < 0)
        {
            log.Error($"Misconfigured sub type {gameSubType.LocalizedName} with {playerNum} human players in {team}");
            playerNum = 0;
        }

        if (team == Team.TeamA
            && gameSubType.Mods is not null
            && gameSubType.Mods.Contains(GameSubType.SubTypeMods.AntiSocial) 
            && !IsControlAllBots)
        {
            int botsForAntiSocial = playerNum - players.Count;
            botNum += botsForAntiSocial;
            playerNum = players.Count;
            log.Info($"Adding {botsForAntiSocial} bots for an antisocial game");
        }

        if (playerNum != players.Count)
        {
            log.Error($"Expected {playerNum} players in {team} but got {players.Count}");
        }

        foreach (MatchPlayerData playerData in players)
        {
            long accountId = playerData.AccountId;
            LobbyServerProtocol client = SessionManager.GetClientConnection(accountId);
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (client == null)
            {
                log.Error($"Tried to add {account.Handle} to a game but they are not connected!");
                CancelMatch(account.Handle, accountId);
                return false;
            }
            int Playerid = TeamInfo.TeamPlayerInfo.Count + 1;
            LobbyServerPlayerInfo playerInfo = LobbyServerPlayerInfo.Of(account);
            playerInfo.ReadyState = ReadyState.Ready;
            playerInfo.TeamId = team;
            playerInfo.PlayerId = Playerid;
            log.Info($"adding player {client.UserName} ({playerInfo.CharacterType}), {client.AccountId} to {team}. readystate: {playerInfo.ReadyState}");
            TeamInfo.TeamPlayerInfo.Add(playerInfo);
            
            for (int i = 0; i < playerData.NumControlledCharacters - 1; i++)
            {
                LobbyServerPlayerInfo proxy = AddBot(team, playerInfo, i);
                log.Info($"Adding asymmetric proxy {proxy.CharacterType} for {client.UserName} to {team}");
                botNum--;
            }
        }

        bool isAntiSocial = gameSubType.Mods is not null && gameSubType.Mods.Contains(GameSubType.SubTypeMods.AntiSocial);
        bool isPlayerControllingAllCharacters = IsControlAllBots && !(isAntiSocial && team == Team.TeamB);
        LobbyServerPlayerInfo controllingPlayer = isPlayerControllingAllCharacters ? GetControllingPlayer(team) : null;
        for (int i = 0; i < botNum; i++)
        {
            LobbyServerPlayerInfo playerInfo = AddBot(team, controllingPlayer, i);
            log.Info($"Adding bot {playerInfo.CharacterType} to {team}");
        }

        return true;
    }

    protected LobbyServerPlayerInfo AddBot(Team team, LobbyServerPlayerInfo controllingPlayer, int botNr)
    {
        CharacterType characterType = PickCharacter(team, true);
        LobbyCharacterInfo lobbyCharacterInfo = InitializeDefaultCharacterInfo(characterType);

        if (controllingPlayer != null)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(controllingPlayer.AccountId);
            if (account != null)
            {
                if (account.AccountComponent?.LastRemoteCharacters != null
                    && botNr >= 0
                    && botNr < account.AccountComponent.LastRemoteCharacters.Count
                    && account.AccountComponent.LastRemoteCharacters[botNr] != CharacterType.None)
                {
                    characterType = account.AccountComponent.LastRemoteCharacters[botNr];
                }
                CharacterComponent characterComponent = (CharacterComponent)account.CharacterData[characterType].CharacterComponent.Clone();
                lobbyCharacterInfo = LobbyCharacterInfo.Of(account.CharacterData[characterType], characterComponent);
            }
            else
            {
                log.Error($"Controlling player account {controllingPlayer.AccountId} not found. Adding a bot instead.");
            }
        }

        LobbyServerPlayerInfo playerInfo = new LobbyServerPlayerInfo
        {
            ReadyState = ReadyState.Ready,
            IsGameOwner = false,
            TeamId = team,
            PlayerId = TeamInfo.TeamPlayerInfo.Count + 1,
            IsNPCBot = controllingPlayer == null,
            Handle = GameWideData.Get().GetCharacterResourceLink(characterType).m_displayName, // TODO localization?
            CharacterInfo = lobbyCharacterInfo,
            ControllingPlayerId = controllingPlayer?.PlayerId ?? 0,
            ControllingPlayerInfo = controllingPlayer,
            CustomGameVisualSlot = 0,
            Difficulty = BotDifficulty.Medium,
            BotCanTaunt = true,
        };

        if (controllingPlayer != null)
        {
            controllingPlayer.ProxyPlayerIds.Add(playerInfo.PlayerId);
            playerInfo.AccountId = controllingPlayer.AccountId;
            playerInfo.Handle = controllingPlayer.Handle;
        }

        TeamInfo.TeamPlayerInfo.Add(playerInfo);
        return playerInfo;
    }

    private LobbyCharacterInfo InitializeDefaultCharacterInfo(CharacterType characterType)
    {
        return new LobbyCharacterInfo
        {
            CharacterType = characterType,
            CharacterAbilityVfxSwaps = new CharacterAbilityVfxSwapInfo(),
            CharacterCards = CharacterCardInfo.MakeDefault(),
            CharacterLevel = 1,
            CharacterLoadouts = new List<CharacterLoadout>(),
            CharacterMatches = 0,
            CharacterMods = EvoS.DirectoryServer.Character.CharacterManager.GetDefaultMods(characterType),
            CharacterSkin = new CharacterVisualInfo(),
            CharacterTaunts = new List<PlayerTauntData>()
        };
    }

    private LobbyServerPlayerInfo GetControllingPlayer(Team team)
    {
        int controllingPlayerId = GetControllingPlayerIdInternal(team);
        return TeamInfo.TeamPlayerInfo.FirstOrDefault(p => p.PlayerId == controllingPlayerId)
               ?? TeamInfo.TeamPlayerInfo.FirstOrDefault();
    }

    private int GetControllingPlayerIdInternal(Team team)
    {
        // Default hardcoded values
        int defaultTeamAPlayerId = 1;
        int defaultTeamBPlayerId = 5;

        LobbyServerPlayerInfo teamALeader = GetTeamLeader(TeamInfo.TeamAPlayerInfo);
        LobbyServerPlayerInfo teamBLeader = GetTeamLeader(TeamInfo.TeamBPlayerInfo);

        int teamAPlayerId = teamALeader?.PlayerId ?? defaultTeamAPlayerId;
        int teamBPlayerId = teamBLeader?.PlayerId ?? defaultTeamBPlayerId;

        return team == Team.TeamA ? teamAPlayerId : teamBPlayerId;
    }

    private static LobbyServerPlayerInfo GetTeamLeader(IEnumerable<LobbyServerPlayerInfo> teamPlayerInfo)
    {
        return teamPlayerInfo.FirstOrDefault(p => p.GroupLeader);
    }

    protected CharacterType PickCharacter(Team team, bool forBot)
    {
        HashSet<CharacterType> teammateCharacters = TeamInfo.TeamInfo(team)
            .Select(tm => tm.CharacterType)
            .ToHashSet();
        return PickCharacter(teammateCharacters, teammateCharacters, forBot);
    }

    // Picks a role the team does not have yet: tank, then support, then assassin
    internal static CharacterType PickCharacter(
        IReadOnlySet<CharacterType> teammateCharacters,
        IReadOnlySet<CharacterType> unavailableCharacters,
        bool forBot)
    {
        List<CharacterRole> teammateRoles = teammateCharacters
            .Select(ct => CharacterConfigs.Characters[ct].CharacterRole)
            .ToList();
        CharacterRole roleToPick = teammateRoles.All(r => r != CharacterRole.Tank)
            ? CharacterRole.Tank
            : teammateRoles.All(r => r != CharacterRole.Support)
                ? CharacterRole.Support
                : CharacterRole.Assassin;
        List<KeyValuePair<CharacterType, CharacterConfig>> allowed = (
                from keyValuePair in CharacterConfigs.Characters
                where (forBot || keyValuePair.Value.AllowForPlayers)
                      && (!forBot || keyValuePair.Value.AllowForBots)
                      && keyValuePair.Value.CharacterRole != CharacterRole.None
                      && !unavailableCharacters.Contains(keyValuePair.Key)
                select keyValuePair)
            .ToList();
        List<CharacterType> options = allowed
            .Where(kv => kv.Value.CharacterRole == roleToPick)
            .Select(kv => kv.Key)
            .ToList();
        
        if (options.Count == 0)
        {
            options = allowed.Select(kv => kv.Key).ToList();
        }

        return options[Random.Shared.Next(options.Count)];
    }

    // characterDataUpdate: the update changes character data (skin, cards, mods, etc.)
    public virtual bool UpdateCharacterInfo(
        long accountId,
        LobbyCharacterInfo characterInfo,
        LobbyPlayerInfoUpdate update,
        bool isCharacterDataUpdate)
    {
        LobbyServerPlayerInfo serverPlayerInfo = GetPlayerInfo(accountId);
        LobbyCharacterInfo serverCharacterInfo = serverPlayerInfo.CharacterInfo;

        if (GameInfo.GameStatus >= GameStatus.LoadoutSelecting
            && update.CharacterType != null
            && update.CharacterType.HasValue
            && update.CharacterType != serverCharacterInfo.CharacterType)
        {
            log.Warn($"{accountId} attempted to switch from {serverCharacterInfo.CharacterType} " +
                     $"to {update.CharacterType} while in game");
            return false;
        }

        if (GameInfo.GameStatus >= GameStatus.Launching)
        {
            log.Warn($"{accountId} attempted to update character info while in game");
            return false;
        }

        lock (characterSelectionLock)
        {
            CharacterType characterType = update.CharacterType ?? serverCharacterInfo.CharacterType;
            if (update.ContextualReadyState != null
                && update.ContextualReadyState.HasValue
                && update.ContextualReadyState.Value.ReadyState == ReadyState.Ready // why didn't it trigger before?
                && GameInfo.GameStatus == GameStatus.FreelancerSelecting
                && !ValidateSelectedCharacter(accountId, characterType))
            {
                log.Warn($"{accountId} attempted to ready up while in game using illegal character {characterType}");
                return false;
            }

            // Custom game if we update ourself or if we have to update a bot
            // 0 is set if we update our own charachter, PlayerId starts with 1
            if (update.PlayerId == 0)
            {
                serverPlayerInfo.CharacterInfo = characterInfo;
            }
            else if (update.CharacterType.HasValue || isCharacterDataUpdate)
            {
                // Loadout updates do not specify the character, e.g. when editing
                // another slot controlled by this account during multilancer draft
                if (!SetSecondaryCharacter(accountId, update.PlayerId, characterInfo))
                {
                    return false;
                }
            }

            SendGameInfoNotifications();
            return true;
        }
    }

    protected bool CheckDuplicatedAndFill()
    {
        lock (characterSelectionLock)
        {
            bool didWeHadFillOrDuplicate = false;
            for (Team team = Team.TeamA; team <= Team.TeamB; ++team)
            {
                ILookup<CharacterType, LobbyServerPlayerInfo> characters = GetCharactersByTeam(team);
                log.Info($"{team}: {string.Join(", ", characters.Select(e => e.Key + ": [" + string.Join(", ", e.Select(x => x.Handle)) + "]"))}");

                bool allowDuplicates = GameInfo.GameConfig.HasGameOption(GameOptionFlag.AllowDuplicateCharacters);
                List<LobbyServerPlayerInfo> playersRequiredToSwitch = characters
                    .Where(players => !allowDuplicates && players.Count() > 1 
                            && players.Key != CharacterType.PendingWillFill 
                            && players.Key != CharacterType.TestFreelancer1
                            && players.Key != CharacterType.TestFreelancer2)
                    .SelectMany(players => players.Skip(1))
                    .Concat(
                        characters
                            .Where(players => players.Key == CharacterType.PendingWillFill
                                                || players.Key == CharacterType.TestFreelancer1
                                                || players.Key == CharacterType.TestFreelancer2)
                            .SelectMany(players => players))
                    .ToList();

                foreach (LobbyServerPlayerInfo character in characters.SelectMany(x => x))
                {
                    CharacterConfigs.Characters.TryGetValue(character.CharacterInfo.CharacterType, out CharacterConfig characterConfig);
                    if (characterConfig is null || !characterConfig.AllowForPlayers)
                    {
                        log.Info($"{character.Handle} is not allowed to play {character.CharacterType} forcing change");
                        playersRequiredToSwitch.Add(character);
                    }
                }

                Dictionary<CharacterType, string> thiefNames = GetThiefNames(characters);

                foreach (LobbyServerPlayerInfo playerInfo in playersRequiredToSwitch)
                {
                    LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(playerInfo.AccountId);
                    if (playerConnection == null)
                    {
                        log.Error($"Player {playerInfo.Handle}/{playerInfo.AccountId} is in game but has no connection.");
                        continue;
                    }

                    string thiefName = thiefNames.GetValueOrDefault(playerInfo.CharacterType, "");

                    log.Info($"Forcing {playerInfo.Handle} to switch character as {playerInfo.CharacterType} is already picked by {thiefName}");
                    playerConnection.Send(new FreelancerUnavailableNotification
                    {
                        oldCharacterType = playerInfo.CharacterType,
                        thiefName = thiefName,
                        ItsTooLateToChange = false
                    });

                    playerInfo.ReadyState = ReadyState.Unknown;

                    didWeHadFillOrDuplicate = true;
                }
            }

            if (didWeHadFillOrDuplicate)
            {
                log.Info("We have duplicates/fills, going into DUPLICATE_FREELANCER subphase");
                foreach (long player in GetPlayers())
                {
                    ClientNotifier.Get().Send(player, new EnterFreelancerResolutionPhaseNotification()
                    {
                        SubPhase = FreelancerResolutionPhaseSubType.DUPLICATE_FREELANCER
                    });
                    LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
                    if (playerConnection != null)
                    {
                        SendGameInfo(playerConnection);
                    }
                }
            }

            return didWeHadFillOrDuplicate;
        }
    }

    protected ILookup<CharacterType, LobbyServerPlayerInfo> GetCharactersByTeam(Team team, long? excludeAccountId = null) // TODO we can have multiple characters with same account id
    {
        return TeamInfo.TeamPlayerInfo
            .Where(p => p.TeamId == team && p.AccountId != excludeAccountId)
            .ToLookup(p => p.CharacterInfo.CharacterType);
    }

    protected IEnumerable<LobbyServerPlayerInfo> GetDuplicateCharacters(ILookup<CharacterType, LobbyServerPlayerInfo> characters)
    {
        return characters.Where(c => c.Count() > 1).SelectMany(c => c);
    }

    private bool IsCharacterUnavailable(LobbyServerPlayerInfo playerInfo, IEnumerable<LobbyServerPlayerInfo> duplicateCharsA, IEnumerable<LobbyServerPlayerInfo> duplicateCharsB)
    {
        IEnumerable<LobbyServerPlayerInfo> duplicateChars = playerInfo.TeamId == Team.TeamA ? duplicateCharsA : duplicateCharsB;
        return !CharacterRules.IsPlayable(playerInfo.CharacterType)
               || (!GameInfo.GameConfig.HasGameOption(GameOptionFlag.AllowDuplicateCharacters) && duplicateChars.Contains(playerInfo) && duplicateChars.First() != playerInfo);
    }

    private Dictionary<CharacterType, string> GetThiefNames(ILookup<CharacterType, LobbyServerPlayerInfo> characters)
    {
        return characters
            .Where(players => players.Count() > 1 && players.Key != CharacterType.PendingWillFill)
            .ToDictionary(
                players => players.Key,
                players => players.First().Handle);
    }

    protected void CheckIfAllSelected()
    {
        lock (characterSelectionLock)
        {
            ILookup<CharacterType, LobbyServerPlayerInfo> teamACharacters = GetCharactersByTeam(Team.TeamA);
            ILookup<CharacterType, LobbyServerPlayerInfo> teamBCharacters = GetCharactersByTeam(Team.TeamB);

            IEnumerable<LobbyServerPlayerInfo> duplicateCharsA = GetDuplicateCharacters(teamACharacters);
            IEnumerable<LobbyServerPlayerInfo> duplicateCharsB = GetDuplicateCharacters(teamBCharacters);

            HashSet<CharacterType> usedFillCharacters = new HashSet<CharacterType>();

            foreach (long player in GetPlayers())
            {
                LobbyServerPlayerInfo playerInfo = GetPlayerInfo(player);

                if (IsCharacterUnavailable(playerInfo, duplicateCharsA, duplicateCharsB)
                    && playerInfo.ReadyState != ReadyState.Ready)
                {
                    CharacterType randomType = AssignRandomCharacter(
                        playerInfo,
                        playerInfo.TeamId == Team.TeamA ? teamACharacters : teamBCharacters,
                        usedFillCharacters);
                    log.Info($"{playerInfo.Handle} switched from {playerInfo.CharacterType} to {randomType}");

                    usedFillCharacters.Add(randomType);

                    LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
                    if (playerConnection != null)
                    {
                        NotifyCharacterChange(playerConnection, playerInfo, randomType);
                        SetPlayerReady(playerConnection, playerInfo, randomType);
                    }
                }
            }
        }
    }

    protected bool ValidateSelectedCharacter(long accountId, CharacterType character)
    {
        lock (characterSelectionLock)
        {
            LobbyServerPlayerInfo playerInfo = GetPlayerInfo(accountId);
            ILookup<CharacterType, LobbyServerPlayerInfo> teamCharacters = GetCharactersByTeam(playerInfo.TeamId, accountId);
            bool isValid = CharacterRules.IsPlayable(character)
                           && (!teamCharacters.Contains(character) || GameInfo.GameConfig.HasGameOption(GameOptionFlag.AllowDuplicateCharacters));
            log.Info($"Character validation: {playerInfo.Handle} is {(isValid ? "" : "not ")}allowed to use {character}"
                     + $"(teammates are {string.Join(", ", teamCharacters.Select(x => x.Key))})");
            return isValid;
        }
    }

    protected CharacterType AssignRandomCharacter(
        LobbyServerPlayerInfo playerInfo,
        ILookup<CharacterType, LobbyServerPlayerInfo> teammates,
        HashSet<CharacterType> usedFillCharacters)
    {
        HashSet<CharacterType> usedCharacters = teammates.Select(ct => ct.Key).ToHashSet();

        List<CharacterType> availableTypes = CharacterConfigs.Characters
            .Where(cc =>
                cc.Value.AllowForPlayers
                && cc.Value.CharacterRole != CharacterRole.None
                && !usedCharacters.Contains(cc.Key)
                && !usedFillCharacters.Contains(cc.Key))
            .Select(cc => cc.Key)
            .ToList();

        Random rand = new Random();
        CharacterType randomType = availableTypes[rand.Next(availableTypes.Count)];

        log.Info($"Selected random character {randomType} for {playerInfo.Handle} " +
                 $"(was {playerInfo.CharacterType}), options were {string.Join(", ", availableTypes)}, " +
                 $"teammates: {string.Join(", ", usedCharacters)}, " +
                 $"used fill characters: {string.Join(", ", usedFillCharacters)})");
        return randomType;
    }

    private void NotifyCharacterChange(LobbyServerProtocol playerConnection, LobbyServerPlayerInfo playerInfo, CharacterType randomType)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(playerInfo.AccountId);

        playerConnection.Send(new ForcedCharacterChangeFromServerNotification()
        {
            ChararacterInfo = LobbyCharacterInfo.Of(account.CharacterData[randomType]),
        });

        playerConnection.Send(new FreelancerUnavailableNotification()
        {
            oldCharacterType = playerInfo.CharacterType,
            newCharacterType = randomType,
            ItsTooLateToChange = true,
        });
    }

    private void SetPlayerReady(LobbyServerProtocol playerConnection, LobbyServerPlayerInfo playerInfo, CharacterType randomType)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(playerInfo.AccountId);

        playerInfo.CharacterInfo = LobbyCharacterInfo.Of(account.CharacterData[randomType]);
        playerInfo.ReadyState = ReadyState.Ready;
        SendGameInfo(playerConnection);
    }

    public bool ReconnectPlayer(IClientConnection conn)
    {
        LobbyServerPlayerInfo playerInfo = GetPlayerInfo(conn.AccountId);
        var playerData = Players.FirstOrDefault(p => p.AccountId == conn.AccountId);
        if (playerInfo == null || playerData == null)
        {
            log.Error($"Cannot reconnect player {LobbyServerUtils.GetHandle(conn.AccountId)} to {ProcessCode}");
            return false;
        }

        conn.JoinGame(this);
        playerInfo.ReplacedWithBots = false;
        SendGameAssignmentNotification(playerData, true);
        conn.OnStartGame(this);
        SendGameInfo(conn);
        Server.StartGameForReconnection(conn.AccountId);

        return true;
    }

    // Add RankedFreelancerSelection in any of the GameSubTypes to enable drafting
    protected Task RunDraft()
    {
        return IsDraft ? CreateDraft().RunAsync() : Task.CompletedTask;
    }

    internal DraftController CreateDraft()
    {
        Draft = new DraftController(this);
        return Draft;
    }

    IReadOnlyList<LobbyServerPlayerInfo> IDraftHost.Roster => TeamInfo.TeamPlayerInfo;

    LobbyServerPlayerInfo IDraftHost.GetMainSlot(long accountId) => GetPlayerInfo(accountId);

    bool IDraftHost.HasBans => GameModeManager.GetBans(GameSubType.LocalizedName);

    DraftTimeouts IDraftHost.Timeouts => new(
        Ban1: GameInfo.SelectSubPhaseBan1Timeout,
        Ban2: GameInfo.SelectSubPhaseBan2Timeout,
        Pick: GameInfo.SelectSubPhaseFreelancerSelectTimeout,
        Trade: GameInfo.SelectSubPhaseTradeTimeout);

    bool IDraftHost.CheckParticipantsConnected() => CheckIfAllParticipantsAreConnected();

    void IDraftHost.CancelMatch(string dodgerHandle, long? dodgerAccountId) => CancelMatch(dodgerHandle, dodgerAccountId);

    void IDraftHost.SendDraftSnapshot(FreelancerResolutionPhaseSubType phase, RankedResolutionPhaseData data)
    {
        GetClients().ForEach(c =>
        {
            if (c.CurrentGame != this)
            {
                return;
            }

            LobbyServerPlayerInfo player = GetPlayerInfo(c.AccountId);

            // Clone the data for each player to prevent data sharing between players
            RankedResolutionPhaseData rankedResolutionPhaseDataClone = data.Clone();

            // Client always thinks they are TeamA! we need to swap values if we are TeamB (this was a funny one to figure out)
            // Server side we already check if players are in TeamA or TeamB so we are fine
            if (player.TeamId == Team.TeamB)
            {
                // Swap EnemyBans and FriendlyBans
                (rankedResolutionPhaseDataClone.EnemyBans, rankedResolutionPhaseDataClone.FriendlyBans) =
                    (rankedResolutionPhaseDataClone.FriendlyBans, rankedResolutionPhaseDataClone.EnemyBans);

                // Swap EnemyTeamSelections and FriendlyTeamSelections
                (rankedResolutionPhaseDataClone.EnemyTeamSelections, rankedResolutionPhaseDataClone.FriendlyTeamSelections) =
                    (rankedResolutionPhaseDataClone.FriendlyTeamSelections, rankedResolutionPhaseDataClone.EnemyTeamSelections);
            }

            c.Send(new EnterFreelancerResolutionPhaseNotification()
            {
                SubPhase = phase,
                RankedData = rankedResolutionPhaseDataClone,
            });
        });
    }

    LobbyCharacterInfo IDraftHost.GetCharacterInfo(LobbyServerPlayerInfo slot, CharacterType characterType)
    {
        if (!slot.IsAIControlled)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(slot.AccountId);
            return LobbyCharacterInfo.Of(account.CharacterData[characterType]);
        }

        return InitializeDefaultCharacterInfo(characterType);
    }

    protected void UpdateFriendStatuses()
    {
        foreach (LobbyServerProtocol client in GetClients())
        {
            if (client is not null && client.IsConnected)
            {
                client.BroadcastRefreshFriendList();
            }
        }
    }

    protected LobbyGameInfo BuildGameInfo(GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex, GameStatus gameStatus)
    {
        GameSubType subType = gameSubTypes[subTypeIndex];

        TimeSpan? turnTime = Players
            .Select(p => p.SubTypeIndex)
            .Select(i => gameSubTypes[i].GameOverrides.TurnTimeSpan)
            .Max();
        if (turnTime != null)
        {
            subType.GameOverrides.TurnTimeSpan = turnTime;
        }

        return new LobbyGameInfo
        {
            AcceptedPlayers = TeamInfo.TeamPlayerInfo.Count(p => p.IsReady),
            AcceptTimeout = new TimeSpan(0, 0, 0),
            SelectTimeout = TimeSpan.FromSeconds(30),
            LoadoutSelectTimeout = TimeSpan.FromSeconds(30),
            SelectSubPhaseBan1Timeout = TimeSpan.FromSeconds(60),
            SelectSubPhaseBan2Timeout = TimeSpan.FromSeconds(30),
            SelectSubPhaseFreelancerSelectTimeout = TimeSpan.FromSeconds(30),
            SelectSubPhaseTradeTimeout = TimeSpan.FromSeconds(15),
            ActiveHumanPlayers = TeamInfo.TeamPlayerInfo.Count(p => p.IsHumanControlled),
            ActivePlayers = TeamInfo.TeamPlayerInfo.Count,
            CreateTimestamp = DateTime.UtcNow.Ticks,
            GameConfig = new LobbyGameConfig
            {
                GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect,
                GameServerShutdownTime = -1,
                GameType = gameType,
                InstanceSubTypeBit = (ushort)(1 << subTypeIndex),
                IsActive = true,
                Map = Map,
                ResolveTimeoutLimit = 1600, // TODO ?
                RoomName = "",
                Spectators = 0,
                SubTypes = gameSubTypes,
                TeamABots = subType.TeamABots, // TODO update with actual values (for antisocial)?
                TeamAPlayers = subType.TeamAPlayers,
                TeamBBots = subType.TeamBBots,
                TeamBPlayers = subType.TeamBPlayers,
            },
            GameResult = GameResult.NoResult,
            GameServerAddress = GetGameServerURI(),
            GameServerProcessCode = GetGameServerProcessCode(),
            GameStatus = gameStatus,
        };
    }
    

    protected virtual string GetGameServerProcessCode()
    {
        return Server.ProcessCode;
    }

    protected virtual string GetGameServerURI()
    {
        return Server.URI;
    }
}