using System;
using System.Collections.Generic;

namespace CentralServer.BridgeServer;

public interface IServerPool
{
    /// <summary>
    /// A game server connected that is already running a game: the pool had a server with its
    /// process code, or the server reports itself as private (mid-game, e.g. after a lobby restart).
    /// </summary>
    event Action<BridgeServerProtocol> OnServerReconnected;

    void AddServer(BridgeServerProtocol gameServer);
    void RemoveServer(string processCode);
    BridgeServerProtocol GetServer(bool custom = false);
    bool IsAnyServerAvailable();
    List<BridgeServerProtocol> GetServers();
    BridgeServerProtocol FindServerByAddress(string address);
    bool HasOtherServerWithFingerprint(string fingerprint, string processCode);
    void DisconnectByFingerprint(string fingerprint);
}
