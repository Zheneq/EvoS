using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Group;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Matchmaking;

public interface IMatchmakingManager
{
    bool Enabled { get; set; }
    List<MatchmakingQueue> GetQueues();
    MatchmakingQueue GetQueue(GameType gameType);
    void Update();
    bool AddGroupToQueue(GameType gameType, GroupInfo group);
    bool RemoveGroupFromQueue(GroupInfo group, bool suppressWarnings = false);
    bool IsQueued(GroupInfo group);
    void StartPractice(LobbyServerProtocol client);
    Task StartGameAsync(List<MatchPlayerData> teamA, List<MatchPlayerData> teamB,
        GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex,
        Dictionary<long, DateTime> queueEntryTimes = null);
    void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary,
        GameSubType gameSubType, List<MatchPlayerData> players);
}
