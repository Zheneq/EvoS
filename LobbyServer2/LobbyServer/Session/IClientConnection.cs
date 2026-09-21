using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Friend;
using EvoS.Framework.Constants.Enums;
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
    PlayerOnlineStatus Status { get; set; }
    void JoinGame(Game game);
    void OnStartGame(Game game);
    bool IsConnected { get; }
    void Initialize(long accountId, string userName, long sessionToken);
    void CloseConnection();
    string? ProxyName { get; }
    CharacterType? ActiveCharacterType { get; }
}
