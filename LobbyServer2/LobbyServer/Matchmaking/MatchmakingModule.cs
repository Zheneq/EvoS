using System;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

public class MatchmakingModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakingModule));
    private readonly IClientConnection _conn;

    public GameType SelectedGameType { get; set; }
    public ushort SelectedSubTypeMask { get; set; }
    public BotDifficulty AllyDifficulty { get; set; }
    public BotDifficulty EnemyDifficulty { get; set; }
    public bool IsReady { get; private set; }

    public void Ready() => IsReady = true;
    public void Unready() => IsReady = false;

    public MatchmakingModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<JoinMatchmakingQueueRequest>(HandleJoinMatchmakingQueueRequest);
        registry.Register<LeaveMatchmakingQueueRequest>(HandleLeaveMatchmakingQueueRequest);
        registry.Register<SetGameSubTypeRequest>(HandleSetGameSubTypeRequest);
    }

    private void HandleJoinMatchmakingQueueRequest(JoinMatchmakingQueueRequest request)
    {
        try
        {
            GroupInfo group = GroupManager.GetPlayerGroup(_conn.AccountId);
            if (!group.IsLeader(_conn.AccountId))
            {
                log.Warn($"{_conn.UserName} attempted to join {request.GameType} queue " +
                         $"while not being the leader of their group");
                _conn.Send(new JoinMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId });
                return;
            }

            foreach (long groupMember in group.Members)
            {
                LocalizationPayload failure = QueuePenaltyManager.CheckQueuePenalties(groupMember, request.GameType, _conn.AccountId);
                if (failure is not null)
                {
                    _conn.Send(new JoinMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId, LocalizedFailure = failure });
                    return;
                }
            }

            IsReady = true;
            MatchmakingManager.AddGroupToQueue(request.GameType, group);
            _conn.Send(new JoinMatchmakingQueueResponse { Success = true, ResponseId = request.RequestId });
        }
        catch (Exception e)
        {
            _conn.Send(new JoinMatchmakingQueueResponse
            {
                Success = false,
                ResponseId = request.RequestId,
                LocalizedFailure = LocalizationPayload.Create("ServerError@Global")
            });
            log.Error("Failed to process join queue request", e);
        }
    }

    private void HandleLeaveMatchmakingQueueRequest(LeaveMatchmakingQueueRequest request)
    {
        try
        {
            GroupInfo group = GroupManager.GetPlayerGroup(_conn.AccountId);
            if (!group.IsLeader(_conn.AccountId))
            {
                log.Warn($"{_conn.UserName} attempted to leave queue " +
                         $"while not being the leader of their group");
                _conn.Send(new LeaveMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId });
                return;
            }

            _conn.Send(new LeaveMatchmakingQueueResponse { Success = true, ResponseId = request.RequestId });
            IsReady = false;
            MatchmakingManager.RemoveGroupFromQueue(group);
        }
        catch (Exception e)
        {
            _conn.Send(new LeaveMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId });
            log.Error("Failed to process leave queue request", e);
        }
    }

    private void HandleSetGameSubTypeRequest(SetGameSubTypeRequest request)
    {
        // SubType update comes before GameType update in PlayerInfoUpdateRequest
        SelectedSubTypeMask = request.SubTypeMask;
        _conn.Send(new SetGameSubTypeResponse { ResponseId = request.RequestId }); // we need to confirm success before sending a group update
        GroupManager.UpdateSelectedSubTypesForAccount(_conn.AccountId);
    }

    public ushort GetSubTypeMask()
    {
        return Math.Max((ushort)1, SelectedSubTypeMask);
    }

    public void SetGameType(GameType gameType)
    {
        SelectedGameType = gameType;
    }

    public void SetAllyDifficulty(BotDifficulty difficulty)
    {
        AllyDifficulty = difficulty;
    }

    public void SetEnemyDifficulty(BotDifficulty difficulty)
    {
        EnemyDifficulty = difficulty;
    }

    public void SetContextualReadyState(ContextualReadyState contextualReadyState)
    {
        log.Info($"SetContextualReadyState {contextualReadyState.ReadyState} {contextualReadyState.GameProcessCode}");

        LocalizationPayload failure = QueuePenaltyManager.CheckQueuePenalties(_conn.AccountId, SelectedGameType);
        if (failure is not null)
        {
            ResetReadyState();
            _conn.SendSystemMessage(failure);
            return;
        }

        GroupInfo group = GroupManager.GetPlayerGroup(_conn.AccountId);
        IsReady = contextualReadyState.ReadyState == ReadyState.Ready;  // TODO can be Accepted and others
        if (group == null)
        {
            log.Error($"{LobbyServerUtils.GetHandle(_conn.AccountId)} is not in a group when setting contextual ready state");
            return;
        }
        if (_conn.CurrentGame != null)
        {
            if (_conn.CurrentGame.ProcessCode != contextualReadyState.GameProcessCode)
            {
                log.Error($"Received ready state {contextualReadyState.ReadyState} " +
                          $"from {LobbyServerUtils.GetHandle(_conn.AccountId)} " +
                          $"for game {contextualReadyState.GameProcessCode} " +
                          $"while they are in game {_conn.CurrentGame.ProcessCode}");
                return;
            }

            if (contextualReadyState.ReadyState == ReadyState.Ready) // TODO can be Accepted and others
            {
                _conn.CurrentGame.SetPlayerReady(_conn.AccountId);
            }
            else
            {
                _conn.CurrentGame.SetPlayerUnReady(_conn.AccountId);
            }
        }
        else
        {
            UpdateGroupReadyState();
            _conn.BroadcastRefreshGroup();
        }
    }

    public void ResetReadyState()
    {
        IsReady = false;
        UpdateGroupReadyState();
    }

    public void UpdateGroupReadyState()
    {
        GroupInfo group = GroupManager.GetPlayerGroup(_conn.AccountId);
        if (group == null)
        {
            log.Error($"Attempted to update group ready state of {_conn.AccountId} who is not in a group");
            return;
        }
        LobbyServerProtocol leader = null;
        bool allAreReady = true;
        foreach (long groupMember in group.Members)
        {
            LobbyServerProtocol conn = SessionManager.GetClientConnection(groupMember);
            allAreReady &= conn?.IsReady ?? false;
            if (group.IsLeader(groupMember))
            {
                leader = conn;
            }
        }

        bool isGroupQueued = MatchmakingManager.IsQueued(group);

        if (allAreReady && !isGroupQueued)
        {
            if (leader == null)
            {
                log.Error($"Attempted to update group {group.GroupId} ready state with not connected leader {group.Leader}");
                return;
            }
            MatchmakingManager.AddGroupToQueue(leader.SelectedGameType, group);
        }
        else if (!allAreReady && isGroupQueued)
        {
            MatchmakingManager.RemoveGroupFromQueue(group, true);
        }
    }
}
