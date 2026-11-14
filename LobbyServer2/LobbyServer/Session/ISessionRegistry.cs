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

    /// <summary>
    /// A client registered on its websocket: checks it against the session <see cref="CreateSession"/> made,
    /// initializes the connection and registers it. Throws <c>RegisterGameException</c> if the session is
    /// missing or stale or the account is banned.
    /// </summary>
    void OnPlayerConnect(IClientConnection client, RegisterGameClientRequest registerRequest);

    /// <summary>
    /// A client's websocket closed. If the connection still holds the account's current session (a reconnect
    /// may already have replaced it), the session is kept as disconnected so that the player can reconnect.
    /// </summary>
    void OnPlayerDisconnect(IClientConnection client);

    LobbySessionInfo? GetDisconnectedSessionInfo(long accountId);
    LobbySessionInfo? KillSession(long accountId);
    void Broadcast(WebSocketMessage message);
}
