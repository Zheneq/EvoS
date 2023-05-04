using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using log4net;
using WebSocketSharp;

namespace CentralServer.BridgeServer
{
    public class BridgeServerProtocol : WebSocketBehaviorBase<AllianceMessageBase>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BridgeServerProtocol));
        
        public static readonly object characterSelectionLock = new object();

        public string Address;
        public int Port;
        private LobbySessionInfo SessionInfo;
        public LobbyGameInfo GameInfo { private set; get; }
        public LobbyServerTeamInfo TeamInfo { private set; get; } = new LobbyServerTeamInfo() { TeamPlayerInfo = new List<LobbyServerPlayerInfo>() };

        public string URI => "ws://" + Address + ":" + Port;
        
        // TODO sync with GameInfo.GameStatus or get rid of it (GameInfo can be null)
        public GameStatus ServerGameStatus { get; private set; } = GameStatus.None;
        public string ProcessCode { get; } = "Artemis" + DateTime.Now.Ticks;
        public string Name => SessionInfo?.UserName ?? "ATLAS";
        public string BuildVersion => SessionInfo?.BuildVersion ?? "";
        public bool IsPrivate { get; private set; }
        public bool IsConnected { get; private set; } = true;

        public LobbyServerPlayerInfo GetPlayerInfo(long accountId)
        {
            return TeamInfo.TeamPlayerInfo.Find(p => p.AccountId == accountId);
        }

        public IEnumerable<long> GetPlayers(Team team)
        {
            return from p in TeamInfo.TeamInfo(team) select p.AccountId;
        }

        public IEnumerable<long> GetPlayers()
        {
            return from p in TeamInfo.TeamPlayerInfo select p.AccountId;
        }

        public List<LobbyServerProtocol> GetClients()
        {
            List<LobbyServerProtocol> clients = new List<LobbyServerProtocol>();

            // If we don't have any player in teams, return an empty list
            if (TeamInfo == null || TeamInfo.TeamPlayerInfo == null) return clients;

            foreach (LobbyServerPlayerInfo player in TeamInfo.TeamPlayerInfo)
            {
                if (player.IsSpectator || player.IsNPCBot || player.ReplacedWithBots) continue;
                LobbyServerProtocol client = SessionManager.GetClientConnection(player.AccountId);
                if (client != null) clients.Add(client);
            }

            return clients;
        }

        protected override AllianceMessageBase DeserializeMessage(byte[] data, out int callbackId)
        {
            NetworkReader networkReader = new NetworkReader(data);
            short messageType = networkReader.ReadInt16();
            callbackId = networkReader.ReadInt32();
            List<Type> messageTypes = GetMessageTypes();
            if (messageType >= messageTypes.Count)
            {
                log.Error($"Unknown bridge message type {messageType}");
                throw new NullReferenceException();
            }

            Type type = messageTypes[messageType];
            return Deserialize(type, networkReader);
        }

        public static readonly List<Type> BridgeMessageTypes = new List<Type>
        {
            typeof(RegisterGameServerRequest),
            typeof(RegisterGameServerResponse),
            typeof(LaunchGameRequest),
            typeof(JoinGameServerRequest),
            null, // typeof(JoinGameAsObserverRequest),
            typeof(ShutdownGameRequest),
            typeof(DisconnectPlayerRequest),
            typeof(ReconnectPlayerRequest),
            null, // typeof(MonitorHeartbeatResponse),
            typeof(ServerGameSummaryNotification),
            typeof(PlayerDisconnectedNotification),
            typeof(ServerGameMetricsNotification),
            typeof(ServerGameStatusNotification),
            typeof(MonitorHeartbeatNotification),
            typeof(LaunchGameResponse),
            typeof(JoinGameServerResponse),
            null, // typeof(JoinGameAsObserverResponse)
        };

        protected List<Type> GetMessageTypes()
        {
            return BridgeMessageTypes;
        }

        protected override string GetConnContext()
        {
            return $"S {Address}:{Port}";
        }


        public BridgeServerProtocol()
        {
            RegisterHandler<RegisterGameServerRequest>(HandleRegisterGameServerRequest);
            RegisterHandler<ServerGameSummaryNotification>(HandleServerGameSummaryNotification);
            RegisterHandler<PlayerDisconnectedNotification>(HandlePlayerDisconnectedNotification);
            RegisterHandler<DisconnectPlayerRequest>(HandleDisconnectPlayerRequest);
            RegisterHandler<ReconnectPlayerRequest>(HandleReconnectPlayerRequest);
            RegisterHandler<ServerGameMetricsNotification>(HandleServerGameMetricsNotification);
            RegisterHandler<ServerGameStatusNotification>(HandleServerGameStatusNotification);
            RegisterHandler<MonitorHeartbeatNotification>(HandleMonitorHeartbeatNotification);
            RegisterHandler<LaunchGameResponse>(HandleLaunchGameResponse);
            RegisterHandler<JoinGameServerResponse>(HandleJoinGameServerResponse);
        }

        private void HandleRegisterGameServerRequest(RegisterGameServerRequest request, int callbackId)
        {
            string data = request.SessionInfo.ConnectionAddress;
            Address = data.Split(":")[0];
            Port = Convert.ToInt32(data.Split(":")[1]);
            SessionInfo = request.SessionInfo;
            IsPrivate = request.isPrivate;
            ServerManager.AddServer(this);

            Send(new RegisterGameServerResponse
                {
                    Success = true
                },
                callbackId);
        }

        private void HandleServerGameSummaryNotification(ServerGameSummaryNotification notify)
        {
            LobbyGameSummary gameSummary = notify.GameSummary;
            if (gameSummary == null)
            {
                GameInfo.GameResult = GameResult.TieGame;
                gameSummary = new LobbyGameSummary();
            }
            else
            {
                GameInfo.GameResult = gameSummary.GameResult;
            }

            log.Info($"Game {GameInfo?.Name} at {gameSummary?.GameServerAddress} finished " +
                     $"({gameSummary.NumOfTurns} turns), " +
                     $"{gameSummary.GameResult} {gameSummary.TeamAPoints}-{gameSummary.TeamBPoints}");

            try
            {
                gameSummary.BadgeAndParticipantsInfo = AccoladeUtils.ProcessGameSummary(gameSummary);
            }
            catch (Exception ex)
            {
                log.Error("Failed to process game summary", ex);
            }

            _ = FinalizeGame(gameSummary);
        }

        private async Task FinalizeGame(LobbyGameSummary gameSummary)
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
            DiscordManager.Get().SendGameReport(GameInfo, Name, BuildVersion, gameSummary);

            ServerGameStatus = GameStatus.Stopped;
            //Wait a bit so people can look at stuff but we do have to send it so server can restart
            await Task.Delay(60000);
            Send(new ShutdownGameRequest());
        }

        private void HandlePlayerDisconnectedNotification(PlayerDisconnectedNotification request)
        {
            log.Info($"Player {request.PlayerInfo.AccountId} left game {GameInfo?.GameServerProcessCode}");

            foreach (LobbyServerProtocol client in GetClients())
            {
                if (client.AccountId == request.PlayerInfo.AccountId)
                {
                    if (client.CurrentServer == this)
                    {
                        client.CurrentServer = null;
                    }

                    break;
                }
            }

            GetPlayerInfo(request.PlayerInfo.AccountId).ReplacedWithBots = true;
        }

        private void HandleDisconnectPlayerRequest(DisconnectPlayerRequest request)
        {
            log.Info($"Sending Disconnect player Request for accountId {request.PlayerInfo.AccountId}");
        }

        private void HandleReconnectPlayerRequest(ReconnectPlayerRequest request)
        {
            log.Info($"Sending reconnect player Request for accountId {request.AccountId} with reconectionsession id {request.NewSessionId}");
        }

        private void HandleServerGameMetricsNotification(ServerGameMetricsNotification request)
        {
            log.Info($"Game {GameInfo?.Name} Turn {request.GameMetrics?.CurrentTurn}, " +
                     $"{request.GameMetrics?.TeamAPoints}-{request.GameMetrics?.TeamBPoints}, " +
                     $"frame time: {request.GameMetrics?.AverageFrameTime}");
        }

        private void HandleServerGameStatusNotification(ServerGameStatusNotification request)
        {
            log.Info($"Game {GameInfo?.Name} {request.GameStatus}");

            ServerGameStatus = request.GameStatus;

            if (ServerGameStatus == GameStatus.Stopped)
            {
                foreach (LobbyServerProtocol client in GetClients())
                {
                    client.CurrentServer = null;

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

                    // Set client back to previus CharacterType
                    if (GetPlayerInfo(client.AccountId).CharacterType != client.OldCharacter)
                    {
                        ResetCharacterToOriginal(client);
                    }

                    client.OldCharacter = CharacterType.None;
                }
            }
        }

        private void HandleMonitorHeartbeatNotification(MonitorHeartbeatNotification notify)
        {
            
        }

        private void HandleLaunchGameResponse(LaunchGameResponse response)
        {
            log.Info(
                $"Game {GameInfo?.Name} launched ({response.GameServerAddress}, {response.GameInfo?.GameStatus}) with {response.GameInfo?.ActiveHumanPlayers} players");
        }

        private void HandleJoinGameServerResponse(JoinGameServerResponse response)
        {
            log.Info(
                $"Player {response.PlayerInfo?.Handle} {response.PlayerInfo?.AccountId} {response.PlayerInfo?.CharacterType} " +
                $"joined {GameInfo?.Name}  ({response.GameServerProcessCode})");
        }

        private AllianceMessageBase Deserialize(Type type, NetworkReader reader)
        {
            if (!typeof(AllianceMessageBase).IsAssignableFrom(type))
            {
                throw new Exception("Class " + type + " is not AllianceMessageBase");
            }
            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new Exception("No default constructor in class " + type);
            }
            AllianceMessageBase o = (AllianceMessageBase)constructor.Invoke(Array.Empty<object>());
            o.Deserialize(reader);
            return o;
        }

        protected override void HandleClose(CloseEventArgs e)
        {
            UnregisterAllHandlers();
            ServerManager.RemoveServer(ProcessCode);
            IsConnected = false;
        }

        public void OnPlayerUsedGGPack(long accountId)
        {
            GameInfo.ggPackUsedAccountIDs.TryGetValue(accountId, out int ggPackUsedAccountIDs);
            GameInfo.ggPackUsedAccountIDs[accountId] = ggPackUsedAccountIDs + 1;
        }

        public bool IsAvailable()
        {
            return ServerGameStatus == GameStatus.None && !IsPrivate && IsConnected;
        }

        public void ReserveForGame()
        {
            ServerGameStatus = GameStatus.Assembling;
            // TODO release if game did not start?
        }

        public void StartGameForReconection(long accountId)
        {
            LobbyServerPlayerInfo playerInfo = GetPlayerInfo(accountId);
            LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(accountId);

            //Can we modify ReconnectPlayerRequest and send the a new SessionToken to?
            ReconnectPlayerRequest reconnectPlayerRequest = new ReconnectPlayerRequest()
            {
                AccountId = accountId,
                NewSessionId = sessionInfo.ReconnectSessionToken
            };

            Send(reconnectPlayerRequest);

            JoinGameServerRequest request = new JoinGameServerRequest
            {
                OrigRequestId = 0,
                GameServerProcessCode = GameInfo.GameServerProcessCode,
                PlayerInfo = playerInfo,
                SessionInfo = sessionInfo
            };
            Send(request);
        }

        public void StartGame()
        {
            ServerGameStatus = GameStatus.Assembling;
            Dictionary<int, LobbySessionInfo> sessionInfos = TeamInfo.TeamPlayerInfo
                .ToDictionary(
                    playerInfo => playerInfo.PlayerId,
                    playerInfo => SessionManager.GetSessionInfo(playerInfo.AccountId) ?? new LobbySessionInfo());  // fallback for bots TODO something smarter

            foreach (LobbyServerPlayerInfo playerInfo in TeamInfo.TeamPlayerInfo)
            {
                LobbySessionInfo sessionInfo = sessionInfos[playerInfo.PlayerId];
                JoinGameServerRequest request = new JoinGameServerRequest
                {
                    OrigRequestId = 0,
                    GameServerProcessCode = GameInfo.GameServerProcessCode,
                    PlayerInfo = playerInfo,
                    SessionInfo = sessionInfo
                };
                Send(request);
            }

            Send(new LaunchGameRequest()
            {
                GameInfo = GameInfo,
                TeamInfo = TeamInfo,
                SessionInfo = sessionInfos,
                GameplayOverrides = new LobbyGameplayOverrides()
            });
        }

        public bool Send(AllianceMessageBase msg, int originalCallbackId = 0)
        {
            short messageType = GetMessageType(msg);
            if (messageType >= 0)
            {
                Send(messageType, msg, originalCallbackId);
                LogMessage(">", msg);
                return true;
            }
            log.Error($"No sender for {msg.GetType().Name}");
            LogMessage(">X", msg);

            return false;
        }

        private bool Send(short msgType, AllianceMessageBase msg, int originalCallbackId = 0)
        {
            NetworkWriter networkWriter = new NetworkWriter();
            networkWriter.Write(msgType);
            networkWriter.Write(originalCallbackId);
            msg.Serialize(networkWriter);
            Send(networkWriter.ToArray());
            return true;
        }

        public short GetMessageType(AllianceMessageBase msg)
        {
            short num = (short)GetMessageTypes().IndexOf(msg.GetType());
            if (num < 0)
            {
                log.Error($"Message type {msg.GetType().Name} is not in the MonitorGameServerInsightMessages MessageTypes list and doesnt have a type");
            }

            return num;
        }

        public void FillTeam(List<long> players, Team team)
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
                playerInfo.PlayerId = TeamInfo.TeamPlayerInfo.Count + 1;
                log.Info($"adding player {client.UserName} ({playerInfo.CharacterType}), {client.AccountId} to {team}. readystate: {playerInfo.ReadyState}");
                TeamInfo.TeamPlayerInfo.Add(playerInfo);
            }
        }

        public void BuildGameInfo(GameType gameType, GameSubType gameMode)
        {
            int playerCount = GetClients().Count;
            GameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = playerCount,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                SelectTimeout = TimeSpan.FromSeconds(30),
                LoadoutSelectTimeout = TimeSpan.FromSeconds(30),
                ActiveHumanPlayers = playerCount,
                ActivePlayers = playerCount,
                CreateTimestamp = DateTime.Now.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect & GameOptionFlag.NoInputIdleDisconnect,
                    GameServerShutdownTime = -1,
                    GameType = gameType,
                    InstanceSubTypeBit = 1,
                    IsActive = true,
                    Map = MatchmakingQueue.SelectMap(gameMode),
                    ResolveTimeoutLimit = 1600, // TODO ?
                    RoomName = "",
                    Spectators = 0,
                    SubTypes = GameModeManager.GetGameTypeAvailabilities()[gameType].SubTypes,
                    TeamABots = 0,
                    TeamAPlayers = TeamInfo.TeamAPlayerInfo.Count(),
                    TeamBBots = 0,
                    TeamBPlayers = TeamInfo.TeamBPlayerInfo.Count(),
                },
                GameResult = GameResult.NoResult,
                GameServerAddress = this.URI,
                GameServerProcessCode = this.ProcessCode
            };
        }

        public void SetGameStatus(GameStatus status)
        {
            GameInfo.GameStatus = status;
        }

        public void SendGameInfoNotifications(bool keepOldData = false)
        {
            foreach (long player in GetPlayers())
            {
                LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
                if (playerConnection != null)
                {
                    SendGameInfo(playerConnection, GameStatus.None, keepOldData);
                }
            }
        }

        public void SendGameInfo(LobbyServerProtocol playerConnection, GameStatus gamestatus = GameStatus.None, bool keepOldData = false)
        {

            if (gamestatus != GameStatus.None)
            {
                GameInfo.GameStatus = gamestatus;
            }

            LobbyServerPlayerInfo playerInfo = GetPlayerInfo(playerConnection.AccountId);
            GameInfoNotification notification = new GameInfoNotification()
            {
                GameInfo = GameInfo,
                TeamInfo = LobbyTeamInfo.FromServer(TeamInfo, 0, new MatchmakingQueueConfig(), keepOldData),
                PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig())
            };

            playerConnection.Send(notification);
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
                GameplayOverrides = client.GetGameplayOverrides()
            };

            client.Send(notification);
        }

        public void UpdateModsAndCatalysts()
        {
            MatchmakingQueueConfig queueConfig = new MatchmakingQueueConfig();

            for (int i = 0; i < GetClients().Count; i++)
            {
                TeamInfo.TeamPlayerInfo[i].CharacterInfo = LobbyPlayerInfo.FromServer(TeamInfo.TeamPlayerInfo[i], 0, queueConfig).CharacterInfo;
            }
        }

        public bool CheckDuplicatedAndFill()
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
            return playerInfo.CharacterType == CharacterType.PendingWillFill
                   || (playerInfo.TeamId == Team.TeamA && duplicateChars.Contains(playerInfo) && duplicateChars.First() != playerInfo);
        }

        private Dictionary<CharacterType, string> GetThiefNames(ILookup<CharacterType, LobbyServerPlayerInfo> characters)
        {
            return characters
                .Where(players => players.Count() > 1 && players.Key != CharacterType.PendingWillFill)
                .ToDictionary(
                    players => players.Key,
                    players => players.First().Handle);
        }

        public void CheckIfAllSelected()
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
                    LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
                    PersistedAccountData account = DB.Get().AccountDao.GetAccount(playerInfo.AccountId);
                    
                    if (IsCharacterUnavailable(playerInfo, duplicateCharsA, duplicateCharsB)
                        && playerInfo.ReadyState != ReadyState.Ready)
                    {
                        CharacterType randomType = account.AccountComponent.LastCharacter;
                        if (account.AccountComponent.LastCharacter == playerInfo.CharacterType)  // TODO it does not automatically mean that currently selected character is allowed
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

        public bool ValidateSelectedCharacter(long accountId, CharacterType character)
        {
            lock (characterSelectionLock)
            {
                LobbyServerPlayerInfo playerInfo = GetPlayerInfo(accountId);
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
            SendGameInfo(playerConnection);
        }

        public void ResetCharacterToOriginal(LobbyServerProtocol playerConnection, bool isDisconnected = false) 
        {
            if (playerConnection.OldCharacter != CharacterType.None)
            {
                UpdateAccountCharacter(GetPlayerInfo(playerConnection.AccountId), playerConnection.OldCharacter);
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