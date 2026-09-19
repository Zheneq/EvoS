using System.Collections.Generic;
using System.Net;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Session;

public interface ISessionRegistry
{
    IClientConnection? GetClientConnection(long accountId);
    LobbySessionInfo? GetSessionInfo(long accountId);
    IEnumerable<long> GetOnlinePlayers();
    long? GetOnlinePlayerByHandle(string handle);
    long? GetOnlinePlayerByHandleOrUsername(string handleOrUsername);
    LobbySessionInfo CreateSession(long accountId, LobbySessionInfo connectingSessionInfo,
        IPAddress ipAddress, bool rejectIfActive = false);
    LobbySessionInfo? GetDisconnectedSessionInfo(long accountId);
    LobbySessionInfo? KillSession(long accountId);
    void Broadcast(WebSocketMessage message);
}
