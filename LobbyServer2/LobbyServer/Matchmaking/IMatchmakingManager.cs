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
    Task StartGameAsync(List<MatchPlayerData> teamA, List<MatchPlayerData> teamB,
        GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex,
        Dictionary<long, DateTime> queueEntryTimes = null);
    void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary,
        GameSubType gameSubType, List<MatchPlayerData> players);

    /// <summary>
    /// Filters the group leader's selected sub types down to what the group can queue for and stores the result in
    /// <see cref="GroupInfo.SubTypeMask"/>. If it changed, refreshes the group, resetting the ready state if asked to.
    /// </summary>
    void UpdateSelectedSubTypes(GroupInfo groupInfo, bool resetReadyStateIfUpdated = true);
    void UpdateSelectedSubTypesForAccount(long accountId);
}
