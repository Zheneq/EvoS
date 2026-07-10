using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using log4net;
using MoreLinq.Extensions;

namespace CentralServer.BridgeServer;

public class PvpGame: Game
{
    private static readonly ILog log = LogManager.GetLogger(typeof(PvpGame));

    public PvpGame(BridgeServerProtocol server)
    {
        AssignServer(server);
    }
    
    public async Task StartGameAsync(
        List<MatchPlayerData> teamA,
        List<MatchPlayerData> teamB,
        GameType gameType,
        List<GameSubType> gameSubTypes,
        int subTypeIndex)
    {
        GameSubType = gameSubTypes[subTypeIndex];

        // if (asymmetricSlots is { Count: > 0 })
        // {
        //     // TODO does it really matter?
        //     // Clone the shared GameSubType and set per-team proxy counts for this specific match
        //     GameSubType = GameSubType.Clone();
        //     GameSubType.TeamABots = teamA.Where(asymmetricSlots.ContainsKey).Sum(id => asymmetricSlots[id] - 1);
        //     GameSubType.TeamBBots = teamB.Where(asymmetricSlots.ContainsKey).Sum(id => asymmetricSlots[id] - 1);
        // }

        // Fill Teams
        if (!FillTeam(teamA, Team.TeamA, GameSubType) || !FillTeam(teamB, Team.TeamB, GameSubType))
        {
            return;
        }

        GameInfo = BuildGameInfo(gameType, gameSubTypes, subTypeIndex);

        // Assign Current Server
        GetClients().ForEach(c => c.JoinGame(this));

        // Assign players to game
        SetGameStatus(GameStatus.FreelancerSelecting);
        Players.ForEach(player => SendGameAssignmentNotification(player));
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
}