using System.Collections.Generic;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Session;
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
    public Game CurrentGame { get; set; } = null!;
    public readonly List<WebSocketMessage> Sent = new();
    public readonly List<LocalizationPayload> SystemMessages = new();
    public int FriendListRefreshes;
    public readonly List<bool> GroupRefreshes = new();
    public int VisualsUpdates;
    public int ResetReadyStateCalls;
    public int GameUnassignmentCalls;

    public bool IsConnected { get; set; } = true;
    public PlayerOnlineStatus Status { get; set; } = PlayerOnlineStatus.Online;
    public void Send(WebSocketMessage message) => Sent.Add(message);
    public void SendSystemMessage(LocalizationPayload message) => SystemMessages.Add(message);
    public void BroadcastRefreshFriendList() => FriendListRefreshes++;
    public void BroadcastRefreshGroup(bool resetReadyState = false) => GroupRefreshes.Add(resetReadyState);
    public void OnAccountVisualsUpdated() => VisualsUpdates++;
    public void ResetReadyState() => ResetReadyStateCalls++;
    public void SendGameUnassignmentNotification() => GameUnassignmentCalls++;
    public void JoinGame(Game game) { }
    public void OnStartGame(Game game) { }
}
