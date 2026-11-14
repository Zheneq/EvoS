using System;
using System.Collections.Generic;
using System.Net;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Session;

public interface ISessionRegistry
{
    /// <summary>
    /// A player's session is about to start: their connection is initialized but not registered yet,
    /// so <see cref="GetClientConnection"/> does not return it. Raised under the session lock.
    /// </summary>
    event Action<long> OnSessionStarting;

    IClientConnection? GetClientConnection(long accountId);
    LobbySessionInfo? GetSessionInfo(long accountId);
    IEnumerable<long> GetOnlinePlayers();
    long? GetOnlinePlayerByHandle(string handle);
    long? GetOnlinePlayerByHandleOrUsername(string handleOrUsername);
    LobbySessionInfo CreateSession(long accountId, LobbySessionInfo connectingSessionInfo,
        IPAddress ipAddress, bool hasLiveGame, bool rejectIfActive = false);
    LobbySessionInfo? GetDisconnectedSessionInfo(long accountId);
    LobbySessionInfo? KillSession(long accountId);
    void Broadcast(WebSocketMessage message);
}
