using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.BridgeServer
{
    public class MatchOrchestrator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MatchOrchestrator));
        public static readonly object characterSelectionLock = new object();
        
        private readonly BridgeServerProtocol server;

        public MatchOrchestrator(BridgeServerProtocol server)
        {
            this.server = server;
        }
        
        public async Task StartGameAsync(List<long> teamA, List<long> teamB, GameType gameType, GameSubType gameSubType)
        {
            // Fill Teams
            FillTeam(teamA, Team.TeamA);
            FillTeam(teamB, Team.TeamB);
            server.BuildGameInfo(gameType, gameSubType);

            // Assign Current Server
            server.GetClients().ForEach(c => c.JoinServer(server));

            // Assign players to game
            server.SetGameStatus(GameStatus.FreelancerSelecting);
            server.GetClients().ForEach(client => server.SendGameAssignmentNotification(client));

            // Check for duplicated and WillFill characters
            if (CheckDuplicatedAndFill())
            {
                // Wait for Freelancer selection time
                TimeSpan timeout = server.GameInfo.SelectTimeout;
                TimeSpan timePassed = TimeSpan.Zero;
                bool allReady = false;
                
                log.Info($"Waiting for {timeout} to let players pick new characters");

                while (!allReady && timePassed <= timeout)
                {
                    allReady = server.GetPlayers().All(player => server.GetPlayerInfo(player).ReadyState == ReadyState.Ready);

                    if (!allReady)
                    {
                        timePassed += TimeSpan.FromSeconds(1);
                        await Task.Delay(1000);
                    }
                }
            }
            
            // Enter loadout selection
            server.SetGameStatus(GameStatus.LoadoutSelecting);

            // Check if all characters have selected a new freelancer; if not, force them to change
            CheckIfAllSelected();
            
            server.SendGameInfoNotifications();

            // Wait Loadout Selection time
            log.Info($"Waiting for {server.GameInfo.LoadoutSelectTimeout} to let players update their loadouts");
            await Task.Delay(server.GameInfo.LoadoutSelectTimeout);

            log.Info("Launching...");
            server.SetGameStatus(GameStatus.Launching);
            server.SendGameInfoNotifications();

            // If game server failed to start, we go back to the character select screen
            if (!server.IsConnected)
            {
                log.Error($"Server {server.URI} reserved for game {server.GameInfo.Name} has disconnected");
                foreach (LobbyServerProtocol client in server.GetClients())
                {

                    // Set client back to previus CharacterType
                    if (server.GetPlayerInfo(client.AccountId).CharacterType != client.OldCharacter) {
                        ResetCharacterToOriginal(client);
                    }
                    client.OldCharacter = CharacterType.None;

                    // Clear CurrentServer
                    client.LeaveServer(server);

                    client.Send(new GameAssignmentNotification
                    {
                        GameInfo = null,
                        GameResult = GameResult.NoResult,
                        Reconnection = false
                    });
                }
                return;
            }

            server.StartGame();

            foreach (LobbyServerProtocol client in server.GetClients())
            {
                server.SendGameInfo(client);
            }

            server.SetGameStatus(GameStatus.Launched);
            // see AppState_CharacterSelect#Update (AppState_GroupCharacterSelect has HandleGameLaunched, it's much simpler)
            server.ForceReady();
            
            server.SendGameInfoNotifications();

            server.GetClients().ForEach(c => c.OnStartGame(server));

            //send this to or stats break 11hour debuging later lol
            server.SetGameStatus(GameStatus.Started);
            server.SendGameInfoNotifications();

            log.Info($"Game {gameType} started");
        }

        public async Task FinalizeGame(LobbyGameSummary gameSummary)
        {
            //Wait 5 seconds for gg Usages
            await Task.Delay(5000);

            foreach (LobbyServerProtocol client in server.GetClients())
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

            server.SendGameInfoNotifications();
            DiscordManager.Get().SendGameReport(server.GameInfo, server.Name, server.BuildVersion, gameSummary);

            //Wait a bit so people can look at stuff but we do have to send it so server can restart
            await Task.Delay(60000);
            server.Send(new ShutdownGameRequest());
        }

        private void FillTeam(List<long> players, Team team)
        {
            foreach (long accountId in players)
            {
                LobbyServerProtocol client = SessionManager.GetClientConnection(accountId);
                if (client == null)
                {
                    log.Error($"Tried to add {accountId} to a game but they are not connected!");
                    continue;
                }

                LobbyServerPlayerInfo playerInfo = SessionManager.GetPlayerInfo(client.AccountId);
                playerInfo.ReadyState = ReadyState.Ready;
                playerInfo.TeamId = team;
                playerInfo.PlayerId = server.TeamInfo.TeamPlayerInfo.Count + 1;
                log.Info($"adding player {client.UserName} ({playerInfo.CharacterType}), {client.AccountId} to {team}. readystate: {playerInfo.ReadyState}");
                server.TeamInfo.TeamPlayerInfo.Add(playerInfo);
            }
        }

        public bool UpdateCharacterInfo(long accountId, LobbyCharacterInfo characterInfo, LobbyPlayerInfoUpdate update)
        {
            LobbyServerPlayerInfo serverPlayerInfo = server.GetPlayerInfo(accountId);
            LobbyCharacterInfo serverCharacterInfo = serverPlayerInfo.CharacterInfo;

            if (server.GameInfo.GameStatus == GameStatus.LoadoutSelecting
                && update.CharacterType != null
                && update.CharacterType.HasValue
                && update.CharacterType != serverCharacterInfo.CharacterType)
            {
                log.Warn($"{accountId} attempted to switch from {serverCharacterInfo.CharacterType} " +
                         $"to {update.CharacterType} during LoadoutSelecting status");
                return false;
            }
            
            lock (characterSelectionLock)
            {
                CharacterType characterType = update.CharacterType ?? serverCharacterInfo.CharacterType;
                if (update.ContextualReadyState != null
                    && update.ContextualReadyState.HasValue
                    && server.ServerGameStatus == GameStatus.FreelancerSelecting 
                    && !ValidateSelectedCharacter(accountId, characterType))
                {
                    log.Warn($"{accountId} attempted to ready up while in game using illegal character {characterType}");
                    return false;
                }

                serverPlayerInfo.CharacterInfo = characterInfo;

                server.SendGameInfoNotifications();
                // TODO remove?
                if (server.GameInfo.GameStatus == GameStatus.FreelancerSelecting && update.CharacterType.HasValue)
                {
                    SessionManager.GetClientConnection(accountId).Send(new ForcedCharacterChangeFromServerNotification
                    {
                        ChararacterInfo = characterInfo,
                    });
                }

                return true;
            }
        }

        public void UpdateAccountVisuals(long accountId)
        {
            LobbyServerPlayerInfo serverPlayerInfo = server.GetPlayerInfo(accountId);
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

        private bool CheckDuplicatedAndFill()
        {
            lock (characterSelectionLock)
            {
                bool didWeHadFillOrDuplicate = false;
                for (Team team = Team.TeamA; team <= Team.TeamB; ++team)
                {
                    ILookup<CharacterType, LobbyServerPlayerInfo> characters = GetCharactersByTeam(team);
                    log.Info($"{team}: {string.Join(", ", characters.Select(e => e.Key + ": [" + string.Join(", ", e.Select(x => x.Handle)) + "]"))}");

                    List<LobbyServerPlayerInfo> playersRequiredToSwitch = characters
                        .Where(players => players.Count() > 1 && players.Key != CharacterType.PendingWillFill)
                        .SelectMany(players => players.Skip(1))
                        .Concat(
                            characters
                                .Where(players => players.Key == CharacterType.PendingWillFill)
                                .SelectMany(players => players))
                        .ToList();

                    foreach (LobbyServerPlayerInfo character in characters.SelectMany(x => x)) 
                    {
                        CharacterConfigs.Characters.TryGetValue(character.CharacterInfo.CharacterType, out CharacterConfig characterConfig);
                        if (!characterConfig.AllowForPlayers)
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
                    foreach (long player in server.GetPlayers())
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
                        server.SendGameInfo(playerConnection);
                    }
                }

                return didWeHadFillOrDuplicate;
            }
        }

        private ILookup<CharacterType, LobbyServerPlayerInfo> GetCharactersByTeam(Team team, long? excludeAccountId = null)
        {
            return server.TeamInfo.TeamPlayerInfo
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
                   || (playerInfo.TeamId == Team.TeamA && duplicateChars.Contains(playerInfo) && duplicateChars.First() != playerInfo)
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

        private void CheckIfAllSelected()
        {
            lock (characterSelectionLock)
            {
                ILookup<CharacterType, LobbyServerPlayerInfo> teamACharacters = GetCharactersByTeam(Team.TeamA);
                ILookup<CharacterType, LobbyServerPlayerInfo> teamBCharacters = GetCharactersByTeam(Team.TeamB);

                IEnumerable<LobbyServerPlayerInfo> duplicateCharsA = GetDuplicateCharacters(teamACharacters);
                IEnumerable<LobbyServerPlayerInfo> duplicateCharsB = GetDuplicateCharacters(teamBCharacters);

                HashSet<CharacterType> usedFillCharacters = new HashSet<CharacterType>();

                foreach (long player in server.GetPlayers())
                {
                    LobbyServerPlayerInfo playerInfo = server.GetPlayerInfo(player);
                    LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
                    PersistedAccountData account = DB.Get().AccountDao.GetAccount(playerInfo.AccountId);
                    
                    if (IsCharacterUnavailable(playerInfo, duplicateCharsA, duplicateCharsB)
                        && playerInfo.ReadyState != ReadyState.Ready)
                    {
                        CharacterType randomType = account.AccountComponent.LastCharacter;
                        if (account.AccountComponent.LastCharacter == playerInfo.CharacterType)
                        {
                            // If they do not press ready and do not select a new character
                            // force them a random character else use the one they selected
                            randomType = AssignRandomCharacter(
                                playerInfo, 
                                playerInfo.TeamId == Team.TeamA ? teamACharacters : teamBCharacters, 
                                usedFillCharacters);
                        }
                        log.Info($"{playerInfo.Handle} switched from {playerInfo.CharacterType} to {randomType}");

                        usedFillCharacters.Add(randomType);

                        UpdateAccountCharacter(playerInfo, randomType);
                        SessionManager.UpdateLobbyServerPlayerInfo(player);
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
                LobbyServerPlayerInfo playerInfo = server.GetPlayerInfo(accountId);
                ILookup<CharacterType, LobbyServerPlayerInfo> teamCharacters = GetCharactersByTeam(playerInfo.TeamId, accountId);
                bool isValid = !teamCharacters.Contains(character);
                log.Info($"Character validation: {playerInfo.Handle} is {(isValid ? "" : "not ")}allowed to use {character}"
                         +  $"(teammates are {string.Join(", ", teamCharacters.Select(x => x.Key))})");
                return isValid;
            }
        }

        private CharacterType AssignRandomCharacter(
            LobbyServerPlayerInfo playerInfo,
            ILookup<CharacterType,LobbyServerPlayerInfo> teammates,
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

        // TODO remove
        private void UpdateAccountCharacter(LobbyServerPlayerInfo playerInfo, CharacterType randomType)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(playerInfo.AccountId);
            account.AccountComponent.LastCharacter = randomType;
            DB.Get().AccountDao.UpdateAccount(account);
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
            server.SendGameInfo(playerConnection);
        }

        // TODO remove
        public void ResetCharacterToOriginal(LobbyServerProtocol playerConnection, bool isDisconnected = false) 
        {
            if (playerConnection.OldCharacter != CharacterType.None)
            {
                UpdateAccountCharacter(server.GetPlayerInfo(playerConnection.AccountId), playerConnection.OldCharacter);
                if (!isDisconnected)
                {
                    SessionManager.UpdateLobbyServerPlayerInfo(playerConnection.AccountId);
                    PersistedAccountData account = DB.Get().AccountDao.GetAccount(playerConnection.AccountId);
                    playerConnection.Send(new PlayerAccountDataUpdateNotification()
                    {
                        AccountData = account,
                    });
                }
            }
        }
    }
}