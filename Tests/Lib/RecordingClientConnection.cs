using System.Collections.Generic;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace Tests.Lib;

/// <summary>
/// Hand-rolled recording fake for <see cref="IClientConnection"/>. Accumulates all sent
/// messages so tests can assert on protocol output without spinning up live connections.
/// </summary>
public class RecordingClientConnection : IClientConnection
{
    public long AccountId { get; set; }
    public string Handle { get; set; } = "Test#1";
    public string UserName { get; set; } = "testuser";
    public long SessionToken { get; set; }
    public int CloseConnectionCalls;
    public Game CurrentGame { get; set; } = null!;
    public bool InQueue { get; set; }
    public bool IsInQueue() => InQueue;
    public bool InGroup { get; set; }
    public bool IsInGroup() => InGroup;
    public bool InGame { get; set; }
    public bool IsInGame() => InGame;
    public bool InCharacterSelect { get; set; }
    public bool IsInCharacterSelect() => InCharacterSelect;
    public readonly List<WebSocketMessage> Sent = new();
    public readonly List<WebSocketMessage> Broadcasts = new();
    public readonly List<LocalizationPayload> SystemMessages = new();
    public readonly List<string> TextSystemMessages = new();
    public int FriendListRefreshes;
    public int FriendListSends;
    public readonly List<bool> GroupRefreshes = new();
    public readonly List<bool> GroupSends = new();
    public int JoinGroupCalls;
    public int LeaveGroupCalls;
    public int GroupDisbandedCalls;
    public int VisualsUpdates;
    public int ResetReadyStateCalls;
    public int GameUnassignmentCalls;
    public readonly List<Game> AssignedGames = new();
    public readonly List<Game> LeftGames = new();
    public bool LeaveGameResult { get; set; } = true;

    public bool IsConnected { get; set; } = true;
    public PlayerOnlineStatus Status { get; set; } = PlayerOnlineStatus.Online;
    public void Send(WebSocketMessage message) => Sent.Add(message);
    public void Broadcast(WebSocketMessage message) => Broadcasts.Add(message);
    public void SendSystemMessage(LocalizationPayload message) => SystemMessages.Add(message);
    public void SendSystemMessage(string text) => TextSystemMessages.Add(text);
    public void BroadcastRefreshFriendList() => FriendListRefreshes++;
    public void RefreshFriendList() => FriendListSends++;
    public void BroadcastRefreshGroup(bool resetReadyState = false) => GroupRefreshes.Add(resetReadyState);
    public void RefreshGroup(bool resetReadyState = false) => GroupSends.Add(resetReadyState);
    public void OnJoinGroup() => JoinGroupCalls++;
    public void OnLeaveGroup() => LeaveGroupCalls++;
    public void OnGroupDisbanded() => GroupDisbandedCalls++;
    public void OnAccountVisualsUpdated() => VisualsUpdates++;
    public void ResetReadyState() => ResetReadyStateCalls++;
    public void SendGameUnassignmentNotification() => GameUnassignmentCalls++;
    public void OnGameAssigned(Game game) => AssignedGames.Add(game);
    public void JoinGame(Game game) { }
    public bool LeaveGame(Game game)
    {
        LeftGames.Add(game);
        return LeaveGameResult;
    }
    public void OnStartGame(Game game) { }
    public void Initialize(long accountId, string userName, long sessionToken)
    {
        AccountId = accountId;
        UserName = userName;
        SessionToken = sessionToken;
    }
    public void CloseConnection() => CloseConnectionCalls++;
    public string? ProxyName => null;
    public CharacterType? ActiveCharacterType { get; set; }

    public bool IsReady { get; set; }
    public GameType SelectedGameType { get; set; } = GameType.PvP;
    public ushort SubTypeMask { get; set; } = 1;
    public ushort GetSubTypeMask() => SubTypeMask;
    public void SetGameType(GameType gameType) => SelectedGameType = gameType;
}
