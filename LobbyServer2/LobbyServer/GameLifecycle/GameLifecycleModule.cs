using System.Collections.Generic;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;

namespace CentralServer.LobbyServer.GameLifecycle;

public class GameLifecycleModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(GameLifecycleModule));
    private readonly IClientConnection _conn;
    private readonly IGameRegistry _gameRegistry;

    private Game _currentGame;

    public Game CurrentGame
    {
        get => _currentGame;
        private set
        {
            if (_currentGame != value)
            {
                _currentGame = value;
                _conn.BroadcastRefreshFriendList();
                _conn.BroadcastRefreshGroup();
            }
        }
    }

    public bool IsInGame() => CurrentGame != null;

    public bool IsInCharacterSelect() => CurrentGame != null && CurrentGame.GameStatus <= GameStatus.FreelancerSelecting;

    public LobbyServerPlayerInfo PlayerInfo => CurrentGame?.GetPlayerInfo(_conn.AccountId);

    public GameLifecycleModule(IClientConnection conn, IGameRegistry gameRegistry)
    {
        _conn = conn;
        _gameRegistry = gameRegistry;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<JoinGameRequest>(HandleJoinGameRequest);
        registry.Register<CreateGameRequest>(HandleCreateGameRequest);
        registry.Register<GameInfoUpdateRequest>(HandleGameInfoUpdateRequest);
        registry.Register<BalancedTeamRequest>(HandleBalancedTeamRequest);
        registry.Register<LeaveGameRequest>(HandleLeaveGameRequest);
        registry.Register<PreviousGameInfoRequest>(HandlePreviousGameInfoRequest);
        registry.Register<GameInvitationRequest>(HandleGameInvitationRequest);
        registry.Register<GameInviteConfirmationResponse>(HandleGameInviteConfirmationResponse);
        registry.Register<RankedLeaderboardOverviewRequest>(HandleRankedLeaderboardOverviewRequest);
        registry.Register<CalculateFreelancerStatsRequest>(HandleCalculateFreelancerStatsRequest);
        registry.Register<PlayerPanelUpdatedNotification>(HandlePlayerPanelUpdatedNotification);
        registry.Register<UseOverconRequest>(HandleUseOverconRequest);
        registry.Register<UseGGPackRequest>(HandleUseGGPackRequest);
        registry.Register<RejoinGameRequest>(HandleRejoinGameRequest);
    }

    public void JoinGame(Game game)
    {
        Game prevServer = CurrentGame;
        CurrentGame = game;
        log.Info($"{LobbyServerUtils.GetHandle(_conn.AccountId)} joined {game?.ProcessCode} (was in {prevServer?.ProcessCode ?? "lobby"})");
    }

    public bool LeaveGame(Game game)
    {
        if (game == null)
        {
            log.Error($"{_conn.AccountId} is asked to leave null server (current server = {CurrentGame?.ProcessCode ?? "null"})");
            return false;
        }
        if (CurrentGame == null)
        {
            log.Debug($"{_conn.AccountId} is asked to leave {game.ProcessCode} while they are not on any server");
            return false;
        }
        if (CurrentGame != game)
        {
            log.Debug($"{_conn.AccountId} is asked to leave {game.ProcessCode} while they are on {CurrentGame.ProcessCode}. Ignoring.");
            return false;
        }

        CurrentGame = null;
        log.Info($"{LobbyServerUtils.GetHandle(_conn.AccountId)} leaves {game.ProcessCode}");

        // forcing catalyst panel update -- otherwise it would show catas for the character from the last game
        _conn.Send(new ForcedCharacterChangeFromServerNotification
        {
            ChararacterInfo = DB.Get().AccountDao.GetAccount(_conn.AccountId).GetCharacterInfo(),
        });

        return true;
    }

    private void HandleJoinGameRequest(JoinGameRequest joinGameRequest)
    {
        _conn.ResetReadyState();
        Game game = CustomGameManager.JoinGame(
            _conn.AccountId,
            joinGameRequest.GameServerProcessCode,
            joinGameRequest.AsSpectator,
            out LocalizationPayload failure);
        if (game == null)
        {
            _conn.Send(new JoinGameResponse
            {
                ResponseId = joinGameRequest.RequestId,
                LocalizedFailure = failure,
                Success = false
            });
            return;
        }

        JoinGame(game);
        _conn.Send(new JoinGameResponse
        {
            ResponseId = joinGameRequest.RequestId
        });
    }

    private void HandleCreateGameRequest(CreateGameRequest createGameRequest)
    {
        _conn.ResetReadyState();
        Game game = CustomGameManager.CreateGame(_conn.AccountId, createGameRequest.GameConfig, out LocalizationPayload error);
        if (game == null)
        {
            _conn.Send(new CreateGameResponse
            {
                ResponseId = createGameRequest.RequestId,
                LocalizedFailure = error,
                Success = false,
                AllowRetry = true,
            });
            return;
        }
        GroupManager.GetPlayerGroup(_conn.AccountId).Members
            .ForEach(groupMember => SessionManager.GetClientConnection(groupMember)?.JoinGame(game));
        _conn.Send(new CreateGameResponse
        {
            ResponseId = createGameRequest.RequestId,
            AllowRetry = true,
        });
    }

    private void HandleGameInfoUpdateRequest(GameInfoUpdateRequest gameInfoUpdateRequest)
    {
        Game game = CustomGameManager.GetMyGame(_conn.AccountId);

        if (game.GameSubType.Mods.Contains(GameSubType.SubTypeMods.RankedFreelancerSelection)) {
            List<LobbyPlayerInfo> hasControllingPlayerId = gameInfoUpdateRequest.TeamInfo.TeamPlayerInfo.FindAll(p => p.ControllingPlayerId != 0);
            if (hasControllingPlayerId.Count > 0) {
                bool success1 = CustomGameManager.BalanceTeams(_conn.AccountId, new List<BalanceTeamSlot>());
                _conn.Send(new BalancedTeamResponse
                {
                    Success = success1,
                    ResponseId = gameInfoUpdateRequest.RequestId,
                    Slots = new List<BalanceTeamSlot>()
                });
                _conn.Send(new ChatNotification
                {
                    ConsoleMessageType = ConsoleMessageType.SystemMessage,
                    Text = "Controlling multiple characters is not allowed in this mode. "
                           + "If you want to control multiple characters, please select Deathmatch mode. "
                           + "Normal bots are allowed, however."
                });
                return;
            }
        }

        bool success = CustomGameManager.UpdateGameInfo(_conn.AccountId, gameInfoUpdateRequest.GameInfo, gameInfoUpdateRequest.TeamInfo);

        _conn.Send(new GameInfoUpdateResponse
        {
            Success = success,
            ResponseId = gameInfoUpdateRequest.RequestId,
            GameInfo = game?.GameInfo,
            TeamInfo = LobbyTeamInfo.FromServer(game?.TeamInfo, 0, new MatchmakingQueueConfig()),
        });
    }

    private void HandleBalancedTeamRequest(BalancedTeamRequest request)
    {
        bool success = CustomGameManager.BalanceTeams(_conn.AccountId, request.Slots);
        _conn.Send(new BalancedTeamResponse
        {
            Success = success,
            ResponseId = request.RequestId,
            Slots = request.Slots
        });
    }

    private void HandleLeaveGameRequest(LeaveGameRequest request)
    {
        Game game = CurrentGame;
        log.Info($"{_conn.AccountId} leaves game {game?.ProcessCode}");
        if (game != null)
        {
            LeaveGame(game);
            game.DisconnectPlayer(_conn.AccountId);
        }
        _conn.Send(new LeaveGameResponse
        {
            Success = true,
            ResponseId = request.RequestId
        });
        _conn.Send(new GameStatusNotification
        {
            GameServerProcessCode = game?.ProcessCode,
            GameStatus = GameStatus.Stopped
        });
        _conn.SendGameUnassignmentNotification();
    }
    
    private void HandlePreviousGameInfoRequest(PreviousGameInfoRequest request)
    {
        Game game = _gameRegistry.GetGameWithPlayer(_conn.AccountId);
        LobbyGameInfo lobbyGameInfo = null;

        if (game != null && game.Server != null && game.Server.IsConnected)
        {
            if (game.GameStatus != GameStatus.Stopped && !game.GetPlayerInfo(_conn.AccountId).ReplacedWithBots)
            {
                game.DisconnectPlayer(_conn.AccountId);
                log.Info($"{LobbyServerUtils.GetHandle(_conn.AccountId)} was in game {game.ProcessCode}, requesting disconnect");
            }
            else
            {
                log.Info($"{LobbyServerUtils.GetHandle(_conn.AccountId)} was in game {game.ProcessCode}");
            }
            lobbyGameInfo = game.GameInfo;
        }
        else
        {
            log.Info($"{LobbyServerUtils.GetHandle(_conn.AccountId)} wasn't in any game");
        }

        PreviousGameInfoResponse response = new PreviousGameInfoResponse
        {
            PreviousGameInfo = lobbyGameInfo,
            ResponseId = request.RequestId
        };
        _conn.Send(response);
    }

    private void HandleGameInvitationRequest(GameInvitationRequest request)
    {
        _conn.Send(new GameInvitationResponse
        {
            Success = false,
            InviteeHandle = request.InviteeHandle,
            ResponseId = request.RequestId
        });
    }

    private void HandleGameInviteConfirmationResponse(GameInviteConfirmationResponse response)
    {
    }

    private void HandleRankedLeaderboardOverviewRequest(RankedLeaderboardOverviewRequest request)
    {
        _conn.Send(new RankedLeaderboardOverviewResponse
        {
            GameType = GameType.PvP,
            TierInfoPerGroupSize = new Dictionary<int, PerGroupSizeTierInfo>(),
            Success = false,
            ResponseId = request.RequestId
        });
    }

    private void HandleCalculateFreelancerStatsRequest(CalculateFreelancerStatsRequest request)
    {
        _conn.Send(new CalculateFreelancerStatsResponse
        {
            GlobalPercentiles = new Dictionary<StatDisplaySettings.StatType, PercentileInfo>(),
            FreelancerSpecificPercentiles = new Dictionary<int, PercentileInfo>(),
            Success = false,
            ResponseId = request.RequestId
        });
    }

    private void HandlePlayerPanelUpdatedNotification(PlayerPanelUpdatedNotification msg)
    {
    }

    private void HandleUseOverconRequest(UseOverconRequest request)
    {
        UseOverconResponse response = new UseOverconResponse()
        {
            ActorId = request.ActorId,
            OverconId = request.OverconId,
            ResponseId = request.RequestId
        };
        _conn.Send(response);
        if (CurrentGame != null)
        {
            response.ResponseId = 0;
            foreach (IClientConnection client in CurrentGame.GetClients())
            {
                if (client.AccountId != _conn.AccountId)
                {
                    client.Send(response);
                }
            }
        }
    }

    private void HandleUseGGPackRequest(UseGGPackRequest request)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        UseGGPackResponse response = new UseGGPackResponse()
        {
            GGPackUserName = account.Handle,
            GGPackUserBannerBackground = account.AccountComponent.SelectedBackgroundBannerID,
            GGPackUserBannerForeground = account.AccountComponent.SelectedForegroundBannerID,
            GGPackUserRibbon = account.AccountComponent.SelectedRibbonID,
            GGPackUserTitle = account.AccountComponent.SelectedTitleID,
            GGPackUserTitleLevel = 1,
            ResponseId = request.RequestId
        };
        _conn.Send(response);
        if (CurrentGame != null)
        {
            CurrentGame.OnPlayerUsedGGPack(_conn.AccountId);
            foreach (IClientConnection client in CurrentGame.GetClients())
            {
                if (client.AccountId != _conn.AccountId)
                {
                    UseGGPackNotification useGGPackNotification = new UseGGPackNotification()
                    {
                        GGPackUserName = account.Handle,
                        GGPackUserBannerBackground = account.AccountComponent.SelectedBackgroundBannerID,
                        GGPackUserBannerForeground = account.AccountComponent.SelectedForegroundBannerID,
                        GGPackUserRibbon = account.AccountComponent.SelectedRibbonID,
                        GGPackUserTitle = account.AccountComponent.SelectedTitleID,
                        GGPackUserTitleLevel = 1,
                        NumGGPacksUsed = CurrentGame.GameInfo.ggPackUsedAccountIDs[_conn.AccountId]
                    };
                    client.Send(useGGPackNotification);
                }
            }
        }
    }

    private void HandleRejoinGameRequest(RejoinGameRequest request)
    {
        if (request.PreviousGameInfo == null || request.Accept == false)
        {
            _conn.Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
            return;
        }

        log.Info($"{_conn.UserName} wants to reconnect to game {request.PreviousGameInfo.GameServerProcessCode}");

        Game game = GameManager.GetGameWithPlayer(_conn.AccountId);

        if (game == null || game.Server == null || !game.Server.IsConnected)
        {
            _conn.Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
            log.Info($"Game {request.PreviousGameInfo.GameServerProcessCode} not found");
            return;
        }

        LobbyServerPlayerInfo playerInfo = game.GetPlayerInfo(_conn.AccountId);
        if (playerInfo == null)
        {
            _conn.Send(new RejoinGameResponse { ResponseId = request.RequestId, Success = false });
            log.Info($"{_conn.UserName} was not in game {request.PreviousGameInfo.GameServerProcessCode}");
            return;
        }

        _conn.Send(new RejoinGameResponse { ResponseId = request.RequestId, Success = true });
        log.Info($"Reconnecting {_conn.UserName} to game {game.GameInfo.GameServerProcessCode} ({game.ProcessCode})");
        _conn.ResetReadyState();
        game.ReconnectPlayer(_conn);
    }
}
