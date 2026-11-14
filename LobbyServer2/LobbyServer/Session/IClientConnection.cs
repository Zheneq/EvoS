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
    long SessionToken { get; }
    void Send(WebSocketMessage message);
    /// <summary>Sends the message to every connected client.</summary>
    void Broadcast(WebSocketMessage message);
    void SendSystemMessage(LocalizationPayload message);
    void SendSystemMessage(string text);
    void BroadcastRefreshFriendList();
    void RefreshFriendList();
    void BroadcastRefreshGroup(bool resetReadyState = false);
    void RefreshGroup(bool resetReadyState = false);
    bool IsInGroup();
    void OnJoinGroup();
    void OnLeaveGroup();
    void OnGroupDisbanded();
    Game CurrentGame { get; }
    bool IsInGame();
    bool IsInCharacterSelect();
    bool IsInQueue();
    void OnAccountVisualsUpdated();
    void ResetReadyState();
    void SendGameUnassignmentNotification();
    PlayerOnlineStatus Status { get; set; }
    void OnGameAssigned(Game game);
    void JoinGame(Game game);
    bool LeaveGame(Game game);
    void OnStartGame(Game game);
    bool IsConnected { get; }
    void Initialize(long accountId, string userName, long sessionToken);
    void CloseConnection();
    string? ProxyName { get; }
    CharacterType? ActiveCharacterType { get; }

    // Temporary: queue selection and ready state move from the connection to the group (plan 11)
    bool IsReady { get; }
    GameType SelectedGameType { get; }
    ushort GetSubTypeMask();
    void SetGameType(GameType gameType);
}
