using CentralServer.BridgeServer;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Session;

public interface IClientConnection
{
    long AccountId { get; }
    string Handle { get; }
    string UserName { get; }
    void Send(WebSocketMessage message);
    void SendSystemMessage(LocalizationPayload message);
    void BroadcastRefreshFriendList();
    void BroadcastRefreshGroup(bool resetReadyState = false);
    Game CurrentGame { get; }
    void OnAccountVisualsUpdated();
    void ResetReadyState();
    void SendGameUnassignmentNotification();
}
