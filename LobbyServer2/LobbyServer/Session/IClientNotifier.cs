using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Session;

/// <summary>
/// Outbound notification port over <see cref="SessionManager"/>. Every method is a no-op for
/// offline accounts, mirroring the old <c>?.Send(...)</c> call sites. Implemented by
/// <see cref="ClientNotifier.SessionManagerClientNotifier"/>; testable via a recording fake.
/// </summary>
public interface IClientNotifier
{
    bool IsOnline(long accountId);
    void Send(long accountId, WebSocketMessage message);
    void SendSystemMessage(long accountId, LocalizationPayload message);
    void MarkFriendListForUpdate(long accountId);
    /// <summary>
    /// Delegates to <see cref="LobbyServerProtocol.BroadcastRefreshGroup"/>.
    /// <paramref name="resetReadyState"/> is required (no default) — intent must be explicit at call sites.
    /// </summary>
    void BroadcastRefreshGroup(long accountId, bool resetReadyState);
    void NotifyJoinedGroup(long accountId);
    void NotifyLeftGroup(long accountId);
    void NotifyGroupDisbanded(long accountId);
}
