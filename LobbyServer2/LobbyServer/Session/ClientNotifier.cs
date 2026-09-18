using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Session;

/// <summary>
/// Static access point for <see cref="IClientNotifier"/>. Follows the same incremental-DI
/// pattern as <c>MatchmakerRanked</c>: production code calls <see cref="Get"/>, tests swap the
/// implementation via <see cref="Set"/> / <see cref="Reset"/> inside a <c>ClientNotifierScope</c>.
/// </summary>
public static class ClientNotifier
{
    private static volatile IClientNotifier _instance = new SessionManagerClientNotifier();

    public static IClientNotifier Get() => _instance;

    internal static void Set(IClientNotifier notifier) => _instance = notifier;

    internal static void Reset() => _instance = new SessionManagerClientNotifier();
}

/// <summary>
/// Default implementation: thin delegation to <see cref="SessionManager.GetClientConnection"/>.
/// Every method is a no-op when the account is offline.
/// </summary>
internal sealed class SessionManagerClientNotifier : IClientNotifier
{
    public bool IsOnline(long accountId) =>
        SessionManager.GetClientConnection(accountId) is not null;

    public void Send(long accountId, WebSocketMessage message) =>
        SessionManager.GetClientConnection(accountId)?.Send(message);

    public void SendSystemMessage(long accountId, LocalizationPayload message) =>
        SessionManager.GetClientConnection(accountId)?.SendSystemMessage(message);

    public void SendSystemMessage(long accountId, string text) =>
        SessionManager.GetClientConnection(accountId)?.SendSystemMessage(text);

    public void MarkFriendListForUpdate(long accountId) =>
        SessionManager.GetClientConnection(accountId)?.BroadcastRefreshFriendList();

    public void BroadcastRefreshGroup(long accountId, bool resetReadyState) =>
        SessionManager.GetClientConnection(accountId)?.BroadcastRefreshGroup(resetReadyState);

    public void RefreshFriendList(long accountId) =>
        SessionManager.GetClientConnection(accountId)?.RefreshFriendList();

    public void NotifyJoinedGroup(long accountId) =>
        SessionManager.GetClientConnection(accountId)?.OnJoinGroup();

    public void NotifyLeftGroup(long accountId) =>
        SessionManager.GetClientConnection(accountId)?.OnLeaveGroup();

    public void NotifyGroupDisbanded(long accountId) =>
        SessionManager.GetClientConnection(accountId)?.OnGroupDisbanded();
}
