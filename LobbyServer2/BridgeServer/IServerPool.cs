using System.Collections.Generic;

namespace CentralServer.BridgeServer;

public interface IServerPool
{
    void AddServer(BridgeServerProtocol gameServer);
    void RemoveServer(string processCode);
    BridgeServerProtocol GetServer(bool custom = false);
    bool IsAnyServerAvailable();
    List<BridgeServerProtocol> GetServers();
    BridgeServerProtocol FindServerByAddress(string address);
    bool HasOtherServerWithFingerprint(string fingerprint, string processCode);
    void DisconnectByFingerprint(string fingerprint);
}
