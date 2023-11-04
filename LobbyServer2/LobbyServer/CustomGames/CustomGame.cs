using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.CustomGames;

public class CustomGame : Game
{
    private static readonly ILog log = LogManager.GetLogger(typeof(CustomGame));

    private const bool AllowBots = false;
    
    public readonly long Owner;
    
    private static readonly object characterSelectionLock = new object();
    
    public CustomGame(long accountId, LobbyGameConfig gameConfig)
    {
        Owner = accountId;
        // Name = $"CG-{LobbyServerUtils.GetHandle(accountId)}";
        List<GroupInfo> teamA = new List<GroupInfo>
        {
            GroupManager.GetPlayerGroup(accountId)
        };

        LobbyGameInfo lobbyGameInfo = new LobbyGameInfo
        {
            AcceptedPlayers = GroupManager.GetPlayerGroup(accountId).Members.Count,
            AcceptTimeout = new TimeSpan(0, 0, 0),
            SelectTimeout = TimeSpan.FromSeconds(30),
            LoadoutSelectTimeout = TimeSpan.FromSeconds(30),
            ActiveHumanPlayers = GroupManager.GetPlayerGroup(accountId).Members.Count,
            ActivePlayers = GroupManager.GetPlayerGroup(accountId).Members.Count,
            CreateTimestamp = DateTime.UtcNow.Ticks,
            GameConfig = gameConfig,
            GameResult = GameResult.NoResult,
            GameStatus = GameStatus.Assembling,
            GameServerProcessCode = $"CustomGame-{Guid.NewGuid()}",
        };
        lobbyGameInfo.GameConfig.GameType = GameType.Custom;
        // ProcessCode = lobbyGameInfo.GameServerProcessCode;

        LobbyServerTeamInfo teamInfo = new LobbyServerTeamInfo()
        {
            TeamPlayerInfo = new List<LobbyServerPlayerInfo>()
        };

        int playerId = 0;
        foreach (long groupAccountId in teamA.SelectMany(group => group.Members).ToList())
        {
            PersistedAccountData playerAccount = DB.Get().AccountDao.GetAccount(groupAccountId);
            LobbyServerPlayerInfo teamPlayerInfo = LobbyServerPlayerInfo.Of(playerAccount);
            teamPlayerInfo.ReadyState = ReadyState.Unknown;
            teamPlayerInfo.TeamId = Team.TeamA;
            teamPlayerInfo.PlayerId = playerId++;
            teamPlayerInfo.Handle = playerAccount.Handle;
            teamPlayerInfo.IsGameOwner = groupAccountId == accountId;
            teamInfo.TeamPlayerInfo.Add(teamPlayerInfo);
        }

        TeamInfo = teamInfo;
        GameInfo = lobbyGameInfo;

        foreach (long groupAccountId in teamA.SelectMany(group => group.Members).ToList())
        {
            LobbyServerProtocol client = SessionManager.GetClientConnection(groupAccountId);
            if (client != null)
            {
                SendGameAssignmentNotification(groupAccountId);
            }
        }
        SendGameInfoNotifications();
    }

    public override void Terminate()
    {
        base.Terminate();
        foreach (LobbyServerPlayerInfo playerCheck in TeamInfo.TeamPlayerInfo)
        {
            if (playerCheck.AccountId == 0) continue;
            
            LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(playerCheck.AccountId);
            if (playerConnection?.CurrentGame == this)
            {
                playerConnection.LeaveGame(this);
            }
        }
    }

    // TODO merge with MatchOrchestrator
    public override bool UpdateCharacterInfo(long accountId, LobbyCharacterInfo characterInfo, LobbyPlayerInfoUpdate update)
    {
        LobbyServerPlayerInfo serverPlayerInfo = update.PlayerId == 0 ? GetPlayerInfo(accountId) : GetPlayerById(update.PlayerId);
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
                // && ServerGameStatus == GameStatus.FreelancerSelecting
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

            CheckIfAllSelected();
            SendGameInfoNotifications();
            return true;
        }
    }

    // TODO merge with MatchOrchestrator
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

    // TODO merge with MatchOrchestrator
    private ILookup<CharacterType, LobbyServerPlayerInfo> GetCharactersByTeam(Team team, long? excludeAccountId = null)
    {
        return TeamInfo.TeamPlayerInfo
            .Where(p => p.TeamId == team && p.AccountId != excludeAccountId)
            .ToLookup(p => p.CharacterInfo.CharacterType);
    }

    public void BalanceTeams(List<BalanceTeamSlot> slots)
    {
        TeamInfo = CreateBalancedTeamInfo(slots);
        SendGameInfoNotifications();
        CustomGameManager.NotifyUpdate();
    }

    // TODO slots are not used
    // TODO bots are removed
    private LobbyServerTeamInfo CreateBalancedTeamInfo(List<BalanceTeamSlot> slots)
    {
        LobbyServerTeamInfo teamInfo = new LobbyServerTeamInfo()
        {
            TeamPlayerInfo = new List<LobbyServerPlayerInfo>()
        };

        int counter = 0;

        foreach (LobbyServerPlayerInfo player in TeamInfo.TeamPlayerInfo)
        {
            if (player.AccountId != 0 && player.ControllingPlayerId == 0)
            {
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(player.AccountId);
                LobbyServerPlayerInfo playerInfo = LobbyServerPlayerInfo.Of(account);

                playerInfo.TeamId = player.TeamId == Team.Spectator ? player.TeamId : counter % 2 == 0 ? Team.TeamA : Team.TeamB;

                playerInfo.IsNPCBot = false;
                playerInfo.IsGameOwner = player.IsGameOwner;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count + 1;
                playerInfo.ReadyState = ReadyState.Unknown;
                playerInfo.CharacterInfo = player.CharacterInfo;
                teamInfo.TeamPlayerInfo.Add(playerInfo);

                counter++;
            }
        }
        return teamInfo;
    }

    private LobbyServerTeamInfo CreateTeamInfo(LobbyTeamInfo originalTeamInfo)
    {
        LobbyServerTeamInfo teamInfo = new LobbyServerTeamInfo()
        {
            TeamPlayerInfo = new List<LobbyServerPlayerInfo>()
        };

        foreach (LobbyPlayerInfo player in originalTeamInfo.TeamPlayerInfo)
        {
            if (player.AccountId != 0 && player.ControllingPlayerId == 0)
            {
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(player.AccountId);
                LobbyServerPlayerInfo playerInfo = LobbyServerPlayerInfo.Of(account);
                playerInfo.TeamId = player.TeamId;
                playerInfo.IsNPCBot = false;
                playerInfo.IsGameOwner = player.IsGameOwner;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count + 1;
                playerInfo.ReadyState = player.ReadyState;
                playerInfo.CharacterInfo = player.CharacterInfo;
                playerInfo.CustomGameVisualSlot = player.CustomGameVisualSlot;
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }
        }

        foreach (LobbyPlayerInfo player in originalTeamInfo.TeamPlayerInfo)
        {
            bool isProxy = player.AccountId != 0 && player.ControllingPlayerId != 0;
            bool isBot = player.AccountId == 0 && player.ControllingPlayerId == 0;
            bool isRealPlayer = player.AccountId != 0 && player.ControllingPlayerId == 0;
            if (isProxy || isBot && !AllowBots)
            {
                LobbyServerPlayerInfo controllingPlayer;

                if (isProxy)
                {
                    // use specified controller player
                    LobbyPlayerInfo lobbyPlayerInfo = originalTeamInfo.TeamPlayerInfo
                        .Find(p => p.PlayerId == player.ControllingPlayerId);
                    if (lobbyPlayerInfo is null)
                    {
                        log.Error($"Failed to find controlling player for {player.PlayerId} ({player.CharacterType})");
                        continue;
                    }
                    long controllingPlayerAccountId = lobbyPlayerInfo.AccountId;
                    controllingPlayer = teamInfo.TeamPlayerInfo.Find(p => p.AccountId == controllingPlayerAccountId);
                }
                else
                {
                    // get any real player on the same team or just any real player
                    controllingPlayer =
                        teamInfo.TeamPlayerInfo.FirstOrDefault(p => p.TeamId == player.TeamId)
                        ?? teamInfo.TeamPlayerInfo.FirstOrDefault();
                }
                
                if (controllingPlayer == null)
                {
                    log.Error($"Failed to find controlling player for {player.PlayerId} ({player.CharacterType}) (isProxy={isProxy})");
                    continue;
                }
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(controllingPlayer.AccountId);

                LobbyServerPlayerInfo playerInfo = LobbyServerPlayerInfo.Of(account);
                playerInfo.ReadyState = controllingPlayer.ReadyState;
                playerInfo.IsGameOwner = false;
                playerInfo.TeamId = player.TeamId;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count + 1;
                playerInfo.IsNPCBot = false;
                playerInfo.Handle = controllingPlayer.Handle;
                playerInfo.CharacterInfo = LobbyCharacterInfo.Of(account.CharacterData[player.CharacterType]);
                playerInfo.ControllingPlayerId = controllingPlayer.PlayerId;
                playerInfo.ControllingPlayerInfo = controllingPlayer;
                playerInfo.CustomGameVisualSlot = player.CustomGameVisualSlot;
                playerInfo.Difficulty = player.Difficulty;
                controllingPlayer.ProxyPlayerIds.Add(playerInfo.PlayerId);
                teamInfo.TeamPlayerInfo.Add(playerInfo);

            }
            else if (isBot)
            {
                LobbyServerPlayerInfo playerInfo = new LobbyServerPlayerInfo
                {
                    ReadyState = ReadyState.Ready,
                    IsGameOwner = false,
                    TeamId = player.TeamId,
                    PlayerId = teamInfo.TeamPlayerInfo.Count + 1,
                    IsNPCBot = true,
                    Handle = player.Handle,
                    CharacterInfo = player.CharacterInfo,
                    ControllingPlayerId = 0,
                    ControllingPlayerInfo = null,
                    CustomGameVisualSlot = player.CustomGameVisualSlot,
                    Difficulty = player.Difficulty,
                    BotCanTaunt = player.BotCanTaunt,
                };
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }
            else if (!isRealPlayer)
            {
                log.Error($"Player {player.PlayerId} ({player.CharacterType}) is neither a real player, nor a bot, nor a proxy");
            }
        }
        return teamInfo;
    }
    
    public override void SetSecondaryCharacter(long accountId, int playerId, CharacterType characterType)
    {
        LobbyServerPlayerInfo lobbyServerPlayerInfo = TeamInfo.TeamPlayerInfo.Find(p => p.PlayerId == playerId);
        if (lobbyServerPlayerInfo is null)
        {
            log.Error($"Failed to set secondary character: {playerId} not found");
            return;
        }
        if (Owner != accountId)
        {
            log.Error($"Failed to set secondary character: {playerId} does not belong to {LobbyServerUtils.GetHandle(accountId)}");
            return;
        }
        // TODO validate there is no duplicates/duplicates are allowed
        lobbyServerPlayerInfo.CharacterInfo = new LobbyCharacterInfo() { CharacterType = characterType };
    }

    public bool Join(long accountId, bool asSpectator)
    {
        Team teamToJoin = asSpectator
            ? Team.Spectator
            : TeamInfo.TeamAPlayerInfo.Count() == GameInfo.GameConfig.TeamAPlayers
                ? Team.TeamB
                : Team.TeamA;

        PersistedAccountData newAccount = DB.Get().AccountDao.GetAccount(accountId);
        LobbyServerPlayerInfo newPlayerInfo = LobbyServerPlayerInfo.Of(newAccount);
        newPlayerInfo.TeamId = teamToJoin;
        newPlayerInfo.PlayerId = TeamInfo.TeamPlayerInfo.Count + 1;
        newPlayerInfo.IsNPCBot = false;
        newPlayerInfo.IsGameOwner = false;
        TeamInfo.TeamPlayerInfo.Add(newPlayerInfo);

        SendGameAssignmentNotification(accountId);
        SendGameInfoNotifications();

        return true;
    }

    public void UpdateGameInfo(LobbyGameInfo gameInfo, LobbyTeamInfo teamInfo)
    {
        // Check if player is kicked
        foreach (LobbyServerPlayerInfo player in TeamInfo.TeamPlayerInfo)
        {
            if (player.AccountId != 0 && teamInfo.TeamPlayerInfo.All(u => u.AccountId != player.AccountId))
            {
                log.Info($"Player {player.AccountId} was kicked from the game {ProcessCode}");
                LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player.AccountId);
                playerConnection?.Send(new GameAssignmentNotification
                {
                    GameInfo = null,
                    GameResult = GameResult.ClientKicked,
                    Reconnection = false
                });
                playerConnection?.LeaveGame(this);
            }
        }
        
        TeamInfo = CreateTeamInfo(teamInfo);
        CheckIfAllSelected();
        GameInfo.GameConfig = gameInfo.GameConfig;
        ForceUnReady();

        SendGameInfoNotifications();
        CustomGameManager.NotifyUpdate();
    }

    public override void DisconnectPlayer(long accountId)
    {
        if (accountId == Owner)
        {
            log.Info($"Owner Left Game kicking all players");
            DisperseCustomGame(GameResult.OwnerLeft, accountId);

            CustomGameManager.DeleteGame(Owner);

            return;
        }
        
        TeamInfo.TeamPlayerInfo.RemoveAll(u => u.AccountId == accountId);

        if (TeamInfo.TeamPlayerInfo.Count > 0)
        {
            SendGameInfoNotifications();
        }
        else
        {
            // No players left remove server
            CustomGameManager.DeleteGame(Owner);
        }
        CustomGameManager.NotifyUpdate();
    }

    private void DisperseCustomGame(GameResult gameResult, long accountIdToIgnore = 0)
    {
        foreach (LobbyServerPlayerInfo playerCheck in TeamInfo.TeamPlayerInfo)
        {
            LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(playerCheck.AccountId);
            if (accountIdToIgnore > 0 && playerCheck.AccountId != accountIdToIgnore)
            {
                playerConnection.Send(new GameAssignmentNotification
                {
                    GameInfo = null,
                    GameResult = gameResult,
                    Reconnection = false
                });
            }
            playerConnection.LeaveGame(this);
        }
    }

    public override void SetPlayerReady(long accountId)
    {
        base.SetPlayerReady(accountId);
        SendGameInfoNotifications();
        CheckCustomGameStart();
    }

    public override void SetPlayerUnReady(long accountId)
    {
        base.SetPlayerUnReady(accountId);
        SendGameInfoNotifications();
    }

    private void CheckCustomGameStart()
    {
        LobbyServerTeamInfo teamInfo = TeamInfo;
        bool allAreReady = !teamInfo.TeamPlayerInfo.Any(u => !u.IsReady && !u.IsSpectator);

        if (allAreReady && GameInfo.GameConfig.TotalHumanPlayers == (teamInfo.TeamAPlayerInfo.Count() + teamInfo.TeamBPlayerInfo.Count()))
        {
            StartCustomGame();
        }
    }

    private void StartCustomGame()
    {
        log.Info($"Starting Custom game...");

        // Get a server
        BridgeServerProtocol server = ServerManager.GetServer(true);
        if (server == null)
        {
            log.Info($"No available server for Custom game mode");
            LocalizationPayload msg = LocalizationPayload.Create("FailedStartGameServer@Frontend");
            GetClients().ForEach(c => c?.SendSystemMessage(msg));
            ForceUnReady();
            SendGameInfoNotifications();
            return;
        }
        
        // Remove custom game server
        CustomGameManager.DeleteGame(Owner);

        AssignServer(server);
        BuildGameInfoCustomGame(GameInfo);
        if (!GameManager.RegisterGame(ProcessCode, this))
        {
            log.Info($"Failed to register custom game");
            CancelMatch();
            return;
        }
        _ = StartCustomGameAsync();
    }

    public async Task StartCustomGameAsync()
    {
        // this all loads me into a custom game
        TeamInfo.TeamPlayerInfo.ForEach(p => log.Info($"Player {p.AccountId} is on team {p.TeamId}"));

        // Assign all users to the new Current Server
        GetClients().ForEach(c => c.JoinGame(this));

        // Assign players to game
        SetGameStatus(GameStatus.FreelancerSelecting);
        GetClients().ForEach(client => SendGameAssignmentNotification(client));

        SetGameStatus(GameStatus.LoadoutSelecting);

        if (!CheckIfAllParticipantsAreConnected())
        {
            return;
        }

        SendGameInfoNotifications();

        // Wait Loadout Selection time
        log.Info($"Waiting for {GameInfo.LoadoutSelectTimeout} to let players update their loadouts");

        await Task.Delay(GameInfo.LoadoutSelectTimeout);

        log.Info("Launching...");
        SetGameStatus(GameStatus.Launching);
        SendGameInfoNotifications();

        // If game server failed to start, we go back to the character select screen
        if (!CheckIfAllParticipantsAreConnected())
        {
            return;
        }

        StartGame();

        SetGameStatus(GameStatus.Launched);
        SendGameInfoNotifications();

        GetClients().ForEach(c => c.OnStartGame(this));

        //send this to or stats break 11hour debuging later lol
        SetGameStatus(GameStatus.Started);
        SendGameInfoNotifications();

        log.Info($"Game Custom started");
    }

    // TODO merge with match orchestrator
    private void CheckIfAllSelected()
    {
        bool changed = false;
        lock (characterSelectionLock)
        {
            ILookup<CharacterType, LobbyServerPlayerInfo> teamACharacters = GetCharactersByTeam(Team.TeamA);
            ILookup<CharacterType, LobbyServerPlayerInfo> teamBCharacters = GetCharactersByTeam(Team.TeamB);

            IEnumerable<LobbyServerPlayerInfo> duplicateCharsA = GetDuplicateCharacters(teamACharacters);
            IEnumerable<LobbyServerPlayerInfo> duplicateCharsB = GetDuplicateCharacters(teamBCharacters);

            HashSet<CharacterType> usedFillCharacters = new HashSet<CharacterType>();

            foreach (LobbyServerPlayerInfo playerInfo in TeamInfo.TeamPlayerInfo) // custom game - bots don't have accounts
            {
                if (IsCharacterUnavailable(playerInfo, duplicateCharsA, duplicateCharsB)
                    && playerInfo.ReadyState != ReadyState.Ready)
                {
                    CharacterType randomType = AssignRandomCharacter(
                        playerInfo,
                        playerInfo.TeamId == Team.TeamA ? teamACharacters : teamBCharacters,
                        usedFillCharacters);
                    log.Info($"{playerInfo.Handle} switched from {playerInfo.CharacterType} to {randomType}");

                    usedFillCharacters.Add(randomType);
                        
                    // LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
                    // if (playerConnection != null)
                    // {
                    //     NotifyCharacterChange(playerConnection, playerInfo, randomType);
                    //     SetPlayerReady(playerConnection, playerInfo, randomType);
                    // }

                    // custom game only
                    changed = true;
                    playerInfo.CharacterInfo.CharacterType = randomType; 
                }
            }
            
            // custom game only
            if (changed)
            {
                CreateTeamInfo(LobbyTeamInfo.FromServer(TeamInfo, 0, new MatchmakingQueueConfig()));
            }
        }
    }

    // TODO merge with match orchestrator
    private IEnumerable<LobbyServerPlayerInfo> GetDuplicateCharacters(ILookup<CharacterType, LobbyServerPlayerInfo> characters)
    {
        return characters.Where(c => c.Count() > 1).SelectMany(c => c);
    }

    // TODO merge with match orchestrator
    private bool IsCharacterUnavailable(LobbyServerPlayerInfo playerInfo, IEnumerable<LobbyServerPlayerInfo> duplicateCharsA, IEnumerable<LobbyServerPlayerInfo> duplicateCharsB)
    {
        if (!playerInfo.IsRemoteControlled && !playerInfo.IsAIControlled) // for custom games only
        {
            return false;
        }
        
        IEnumerable<LobbyServerPlayerInfo> duplicateChars = playerInfo.TeamId == Team.TeamA ? duplicateCharsA : duplicateCharsB;
        CharacterConfigs.Characters.TryGetValue(playerInfo.CharacterInfo.CharacterType, out CharacterConfig characterConfig);
        return playerInfo.CharacterType == CharacterType.PendingWillFill
               || (!GameInfo.GameConfig.HasGameOption(GameOptionFlag.AllowDuplicateCharacters) && duplicateChars.Contains(playerInfo)) // && duplicateChars.First() != playerInfo) not in custom games
               || !characterConfig.AllowForPlayers;
    }

    // TODO merge with match orchestrator
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

    public void BuildGameInfoCustomGame(LobbyGameInfo gameinfo)
    {
        GameInfo = gameinfo.Clone();
        GameInfo.ActivePlayers = TeamInfo.TeamPlayerInfo.Count;
        GameInfo.LoadoutSelectTimeout = TimeSpan.FromSeconds(30);
        GameInfo.AcceptedPlayers = TeamInfo.TeamPlayerInfo.Count;
        GameInfo.ActiveHumanPlayers = TeamInfo.TeamPlayerInfo.Count(p => p.IsHumanControlled);
        GameInfo.GameConfig.TeamAPlayers = TeamInfo.TeamAPlayerInfo.Count();
        GameInfo.GameConfig.TeamAPlayers = TeamInfo.TeamAPlayerInfo.Count();
        GameInfo.GameConfig.Spectators = TeamInfo.SpectatorInfo.Count();
        GameInfo.GameServerAddress = Server?.URI;
        GameInfo.GameServerProcessCode = Server?.ProcessCode;
        // GameInfo.GameServerProcessCode = this.ProcessCode; // TODO CUSTOM GAMES Actual server process code doesn't match the one on the client and in ServerManager, might cause issues
    }
}