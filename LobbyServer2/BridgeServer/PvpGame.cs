using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.BridgeServer;

public class PvpGame: Game
{
    private static readonly ILog log = LogManager.GetLogger(typeof(PvpGame));

    public PvpGame(BridgeServerProtocol server)
    {
        AssignServer(server);
    }
    
    public async Task StartGameAsync(List<long> teamA, List<long> teamB, GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex)
    {
        GameSubType = gameSubTypes[subTypeIndex];
        // Fill Teams
        if (!FillTeam(teamA, Team.TeamA, GameSubType) || !FillTeam(teamB, Team.TeamB, GameSubType))
        {
            return;
        }

        BuildGameInfo(gameType, gameSubTypes, subTypeIndex);

        // Assign Current Server
        GetClients().ForEach(c => c.JoinGame(this));

        // Assign players to game
        SetGameStatus(GameStatus.FreelancerSelecting);
        GetClients().ForEach(client => SendGameAssignmentNotification(client));
        GetClients().ForEach(client => client.OnGameAssigned(this));

        await HandleRankedResolutionPhase();

        // Check for duplicated and WillFill characters
        if (CheckDuplicatedAndFill())
        {
            // Wait for Freelancer selection time
            TimeSpan timeout = GameSubType.Mods.Contains(GameSubType.SubTypeMods.RankedFreelancerSelection) ? TimeSpan.Zero : GameInfo.SelectTimeout;
            TimeSpan timePassed = TimeSpan.Zero;
            bool allReady = false;

            log.Info($"Waiting for {timeout} to let players pick new characters");

            while (!allReady && timePassed <= timeout)
            {
                allReady = GetPlayers().All(player => GetPlayerInfo(player).ReadyState == ReadyState.Ready);

                if (!allReady)
                {
                    timePassed += TimeSpan.FromSeconds(1);
                    await Task.Delay(1000);
                }
            }
        }

        // Enter loadout selection
        SetGameStatus(GameStatus.LoadoutSelecting);

        // Check if all characters have selected a new freelancer; if not, force them to change
        CheckIfAllSelected();

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

        // If game Server failed to start, we go back to the character select screen
        if (!CheckIfAllParticipantsAreConnected())
        {
            return;
        }
        
        foreach (LobbyServerProtocol client in GetClients())
        {
            if (client is not null && client.IsConnected)
            {
                client.BroadcastRefreshFriendList();
                break;
            }
        }

        StartGame();

        foreach (LobbyServerProtocol client in GetClients())
        {
            SendGameInfo(client);
        }

        SetGameStatus(GameStatus.Launched);
        // see AppState_CharacterSelect#Update (AppState_GroupCharacterSelect has HandleGameLaunched, it's much simpler)
        ForceReady();

        SendGameInfoNotifications();

        GetClients().ForEach(c => c.OnStartGame(this));

        //send this to or stats break 11hour debuging later lol
        SetGameStatus(GameStatus.Started);
        SendGameInfoNotifications();

        log.Info($"Game {gameType} started");
    }
    
    public void BuildGameInfo(GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex)
    {
        GameSubType gameMode = gameSubTypes[subTypeIndex];
        GameInfo = new LobbyGameInfo
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
                Map = MatchmakingQueue.SelectMap(gameMode),
                ResolveTimeoutLimit = 1600, // TODO ?
                RoomName = "",
                Spectators = 0,
                SubTypes = gameSubTypes,
                TeamABots = gameMode.TeamABots, // TODO update with actual values (for antisocial)?
                TeamAPlayers = gameMode.TeamAPlayers,
                TeamBBots = gameMode.TeamBBots,
                TeamBPlayers = gameMode.TeamBPlayers,
            },
            GameResult = GameResult.NoResult,
            GameServerAddress = Server.URI,
            GameServerProcessCode = Server.ProcessCode
        };
    }
}