using CentralServer.BridgeServer;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Session;

public interface IClientConnection
{
    long AccountId { get; }
    void Send(WebSocketMessage message);
    Game CurrentGame { get; }
    void OnAccountVisualsUpdated();
}
