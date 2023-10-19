using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.BridgeServer;

public abstract class Game
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Game));
    public static readonly object characterSelectionLock = new object();
    
    public LobbyGameInfo GameInfo { protected set; get; } // TODO check it is set when needed
    public LobbyServerTeamInfo TeamInfo { protected set; get; } = new LobbyServerTeamInfo() { TeamPlayerInfo = new List<LobbyServerPlayerInfo>() };
    
    public ServerGameMetrics GameMetrics { get; private set; } = new ServerGameMetrics();
    public LobbyGameSummary GameSummary { get; private set; }
    public DateTime StopTime { private set; get; }
    public BridgeServerProtocol Server { private set; get; } // TODO check it is set when needed

    public string ProcessCode => GameInfo?.GameServerProcessCode;
    public GameStatus GameStatus => GameInfo?.GameStatus ?? GameStatus.None;

    protected void AssignServer(BridgeServerProtocol server)
    {
        server.OnGameEnded += OnGameEnded;
        server.OnStatusUpdate += OnStatusUpdate;
        server.OnGameMetricsUpdate += OnGameMetricsUpdate;
        server.OnPlayerDisconnect += OnPlayerDisconnect;
        server.OnServerDisconnect += OnServerDisconnect;
    }

    public virtual void DisconnectPlayer(long accountId)
    {
        Server?.DisconnectPlayer(GetPlayerInfo(accountId));
    }
    
    protected void OnGameEnded(LobbyGameSummary summary, LobbyGameSummaryOverrides overrides)
    {
        LobbyGameSummary gameSummary = summary;
        if (gameSummary == null)
        {
            GameInfo.GameResult = GameResult.TieGame;
            gameSummary = new LobbyGameSummary();
        }
        else
        {
            GameInfo.GameResult = gameSummary.GameResult;
        }

        GameSummary = gameSummary;
        log.Info($"Game {GameInfo?.Name} at {GameSummary.GameServerAddress} finished " +
                 $"({GameSummary.NumOfTurns} turns), " +
                 $"{GameSummary.GameResult} {GameSummary.TeamAPoints}-{GameSummary.TeamBPoints}");

        try
        {
            GameSummary.BadgeAndParticipantsInfo = AccoladeUtils.ProcessGameSummary(GameSummary);
        }
        catch (Exception ex)
        {
            log.Error("Failed to process game summary", ex);
        }

        DB.Get().MatchHistoryDao.Save(MatchHistoryDao.MatchEntry.Cons(GameInfo, GameSummary));

        GameInfo.GameStatus = GameStatus.Stopped;
        StopTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(8));

        _ = FinalizeGame(GameSummary);
    }
    
    protected void OnStatusUpdate(GameStatus newStatus)
    {
        log.Info($"Game {GameInfo?.Name} {newStatus}");

        GameInfo.GameStatus = newStatus;

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

    private void OnGameMetricsUpdate(ServerGameMetrics gameMetrics)
    {
        GameMetrics = gameMetrics;
        log.Info($"Game {GameInfo?.Name} Turn {GameMetrics.CurrentTurn}, " +
                 $"{GameMetrics.TeamAPoints}-{GameMetrics.TeamBPoints}, " +
                 $"frame time: {GameMetrics.AverageFrameTime}");
    }

    protected void OnPlayerDisconnect(LobbyServerPlayerInfo playerInfo, LobbySessionInfo sessionInfo)
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

    protected void OnServerDisconnect()
    {
        QueuePenaltyManager.CapQueuePenalties(this);
    }
    
    protected void StartGame()
    {
        GameInfo.GameStatus = GameStatus.Assembling;
        Dictionary<int, LobbySessionInfo> sessionInfos = TeamInfo.TeamPlayerInfo
            .ToDictionary(
                playerInfo => playerInfo.PlayerId,
                playerInfo => SessionManager.GetSessionInfo(playerInfo.AccountId) ?? new LobbySessionInfo());  // fallback for bots TODO something smarter

        Server.SendJoinGameRequests(TeamInfo, sessionInfos, GameInfo.GameServerProcessCode);
        Server.LaunchGame(GameInfo, TeamInfo, sessionInfos);
    }

    protected void SetGameStatus(GameStatus status)
    {
        GameInfo.GameStatus = status;
    }

    public virtual void SetSecondaryCharacter(long accountId, int playerId, CharacterType characterType)
    {
        LobbyServerPlayerInfo lobbyServerPlayerInfo = TeamInfo.TeamPlayerInfo.Find(p => p.PlayerId == playerId);
        if (lobbyServerPlayerInfo is null)
        {
            log.Error($"Failed to set secondary character: {playerId} not found");
            return;
        }
        if (lobbyServerPlayerInfo.AccountId != accountId)
        {
            log.Error($"Failed to set secondary character: {playerId} does not belong to {LobbyServerUtils.GetHandle(accountId)}");
            return;
        }
        lobbyServerPlayerInfo.CharacterInfo = new LobbyCharacterInfo() { CharacterType = characterType };
    }
    
    // TODO there can be multiple
    public LobbyServerPlayerInfo GetPlayerInfo(long accountId)
    {
        return TeamInfo.TeamPlayerInfo.Find(p => p.AccountId == accountId && !p.IsRemoteControlled);
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

    // TODO distinct?
    public IEnumerable<long> GetPlayers()
    {
        return from p in TeamInfo.TeamPlayerInfo select p.AccountId;
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
            if (player.IsSpectator
                || player.IsNPCBot
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

    public void ForceReady()
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

    public void OnPlayerUsedGGPack(long accountId)
    {
        GameInfo.ggPackUsedAccountIDs.TryGetValue(accountId, out int ggPackUsedAccountIDs);
        GameInfo.ggPackUsedAccountIDs[accountId] = ggPackUsedAccountIDs + 1;
    }

    public void SendGameAssignmentNotification(LobbyServerProtocol client, bool reconnection = false)
    {
        LobbyServerPlayerInfo playerInfo = GetPlayerInfo(client.AccountId);
        GameAssignmentNotification notification = new GameAssignmentNotification
        {
            GameInfo = GameInfo,
            GameResult = GameInfo.GameResult,
            Observer = false,
            PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig()),
            Reconnection = reconnection,
            GameplayOverrides = GameConfig.GetGameplayOverrides()
        };

        client.Send(notification);
    }

    public void SendGameInfoNotifications()
    {
        GameInfo.ActivePlayers = TeamInfo.TeamPlayerInfo.Count;
        GameInfo.UpdateTimestamp = DateTime.UtcNow.Ticks;
        foreach (long player in GetPlayers())
        {
            LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
            if (playerConnection != null)
            {
                SendGameInfo(playerConnection);
            }
        }
    }

    public void SendGameInfo(LobbyServerProtocol playerConnection, GameStatus gamestatus = GameStatus.None)
    {
        // TODO do not mutate on send
        if (gamestatus != GameStatus.None)
        {
            GameInfo.GameStatus = gamestatus;
        }

        LobbyServerPlayerInfo playerInfo = GetPlayerInfo(playerConnection.AccountId);
        GameInfoNotification notification = new GameInfoNotification
        {
            GameInfo = GameInfo,
            TeamInfo = LobbyTeamInfo.FromServer(TeamInfo, 0, new MatchmakingQueueConfig()),
            PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig())
        };

        playerConnection.Send(notification);
    }

    protected void SendGameAssignmentNotification(long accountId, bool reconnection = false)
    {
        LobbyServerProtocol client = SessionManager.GetClientConnection(accountId);
        if (client is null)
        {
            log.Error($"Failed to send game assignment to {LobbyServerUtils.GetHandle(accountId)}");
            return;
        }
        SendGameAssignmentNotification(client, reconnection);
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
                log.Error($"Player {playerInfo.Handle}/{playerInfo.AccountId} who was to participate in game {GameInfo.Name} has disconnected");
                CancelMatch(playerInfo.Handle);
                return false;
            }
        }

        return true;
    }

    private void CancelMatch(string dodgerHandle = null)
    {
        foreach (LobbyServerProtocol client in GetClients())
        {
            client.LeaveGame(this);

            client.Send(new GameAssignmentNotification
            {
                GameInfo = null,
                GameResult = GameResult.NoResult,
                Reconnection = false
            });

            if (dodgerHandle != null)
            {
                client.SendSystemMessage(LocalizationPayload.Create(
                    "PlayerDisconnected", "Disconnect", LocalizationArg_Handle.Create(dodgerHandle)));
            }
            else
            {
                client.SendSystemMessage(LocalizationPayload.Create("FailedStartGameServer", "Frontend"));
            }

        }

        Server?.Shutdown();
    }

    public async Task FinalizeGame(LobbyGameSummary gameSummary)
    {
        //Wait 5 seconds for gg Usages
        await Task.Delay(5000);

        foreach (LobbyServerProtocol client in GetClients())
        {
            MatchResultsNotification response = new MatchResultsNotification
            {
                BadgeAndParticipantsInfo = gameSummary.BadgeAndParticipantsInfo,
                //Todo xp and stuff
                BaseXpGained = 0,
                CurrencyRewards = new List<MatchResultsNotification.CurrencyReward>()
            };
            client?.Send(response);
        }

        SendGameInfoNotifications();
        DiscordManager.Get().SendGameReport(GameInfo, Server?.Name, Server?.BuildVersion, gameSummary);
        DiscordManager.Get().SendAdminGameReport(GameInfo, Server?.Name, Server?.BuildVersion, gameSummary);
        
        //Wait a bit so people can look at stuff but we do have to send it so server can restart
        await Task.Delay(60000);
        Server?.Shutdown();
    }

    protected bool FillTeam(List<long> players, Team team)
    {
        foreach (long accountId in players)
        {
            LobbyServerProtocol client = SessionManager.GetClientConnection(accountId);
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (client == null)
            {
                log.Error($"Tried to add {account.Handle} to a game but they are not connected!");
                CancelMatch(account.Handle);
                return false;
            }
            int Playerid = TeamInfo.TeamPlayerInfo.Count + 1;
            LobbyServerPlayerInfo playerInfo = LobbyServerPlayerInfo.Of(account);
            playerInfo.ReadyState = ReadyState.Ready;
            playerInfo.TeamId = team;
            playerInfo.PlayerId = Playerid;
            log.Info($"adding player {client.UserName} ({playerInfo.CharacterType}), {client.AccountId} to {team}. readystate: {playerInfo.ReadyState}");
            TeamInfo.TeamPlayerInfo.Add(playerInfo);
        }

        return true;
    }

    public virtual bool UpdateCharacterInfo(long accountId, LobbyCharacterInfo characterInfo, LobbyPlayerInfoUpdate update)
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
            else if (update.CharacterType.HasValue)
            {
                SetSecondaryCharacter(accountId, update.PlayerId, update.CharacterType.Value);
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
                    .Where(players => !allowDuplicates && players.Count() > 1 && players.Key != CharacterType.PendingWillFill)
                    .SelectMany(players => players.Skip(1))
                    .Concat(
                        characters
                            .Where(players => players.Key == CharacterType.PendingWillFill)
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
                    LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
                    if (playerConnection == null)
                    {
                        continue;
                    }
                    playerConnection.Send(new EnterFreelancerResolutionPhaseNotification()
                    {
                        SubPhase = FreelancerResolutionPhaseSubType.DUPLICATE_FREELANCER
                    });
                    SendGameInfo(playerConnection);
                }
            }

            return didWeHadFillOrDuplicate;
        }
    }

    private ILookup<CharacterType, LobbyServerPlayerInfo> GetCharactersByTeam(Team team, long? excludeAccountId = null)
    {
        return TeamInfo.TeamPlayerInfo
            .Where(p => p.TeamId == team && p.AccountId != excludeAccountId)
            .ToLookup(p => p.CharacterInfo.CharacterType);
    }

    private IEnumerable<LobbyServerPlayerInfo> GetDuplicateCharacters(ILookup<CharacterType, LobbyServerPlayerInfo> characters)
    {
        return characters.Where(c => c.Count() > 1).SelectMany(c => c);
    }

    private bool IsCharacterUnavailable(LobbyServerPlayerInfo playerInfo, IEnumerable<LobbyServerPlayerInfo> duplicateCharsA, IEnumerable<LobbyServerPlayerInfo> duplicateCharsB)
    {
        IEnumerable<LobbyServerPlayerInfo> duplicateChars = playerInfo.TeamId == Team.TeamA ? duplicateCharsA : duplicateCharsB;
        CharacterConfigs.Characters.TryGetValue(playerInfo.CharacterInfo.CharacterType, out CharacterConfig characterConfig);
        return playerInfo.CharacterType == CharacterType.PendingWillFill
               || (!GameInfo.GameConfig.HasGameOption(GameOptionFlag.AllowDuplicateCharacters) && duplicateChars.Contains(playerInfo) && duplicateChars.First() != playerInfo)
               || characterConfig is null
               || !characterConfig.AllowForPlayers;
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

    private bool ValidateSelectedCharacter(long accountId, CharacterType character)
    {
        lock (characterSelectionLock)
        {
            LobbyServerPlayerInfo playerInfo = GetPlayerInfo(accountId);
            ILookup<CharacterType, LobbyServerPlayerInfo> teamCharacters = GetCharactersByTeam(playerInfo.TeamId, accountId);
            bool isValid = CharacterConfigs.Characters[character].AllowForPlayers
                           && (!teamCharacters.Contains(character) || GameInfo.GameConfig.HasGameOption(GameOptionFlag.AllowDuplicateCharacters));
            log.Info($"Character validation: {playerInfo.Handle} is {(isValid ? "" : "not ")}allowed to use {character}"
                     + $"(teammates are {string.Join(", ", teamCharacters.Select(x => x.Key))})");
            return isValid;
        }
    }

    private CharacterType AssignRandomCharacter(
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

    public bool ReconnectPlayer(LobbyServerProtocol conn)
    {
        LobbyServerPlayerInfo playerInfo = GetPlayerInfo(conn.AccountId);
        if (playerInfo == null)
        {
            log.Error($"Cannot reconnect player {LobbyServerUtils.GetHandle(conn.AccountId)} to {ProcessCode}");
            return false;
        }
            
        conn.JoinGame(this);
        playerInfo.ReplacedWithBots = false;
        SendGameAssignmentNotification(conn, true);
        conn.OnStartGame(this);
        SendGameInfo(conn);
        Server.StartGameForReconnection(conn.AccountId);

        return true;
    }
}