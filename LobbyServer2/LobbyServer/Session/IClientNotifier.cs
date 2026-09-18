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
    /// <summary>
    /// Sends a plain-text system message (chat notification) to the account.
    /// Delegates to <see cref="LobbyServerProtocol.SendSystemMessage(string)"/>.
    /// </summary>
    void SendSystemMessage(long accountId, string text);
    void MarkFriendListForUpdate(long accountId);
    /// <summary>
    /// Immediately sends the friend status notification to the account.
    /// Delegates to <see cref="LobbyServerProtocol.RefreshFriendList"/>, which sends directly.
    /// Distinct from <see cref="MarkFriendListForUpdate"/>, which defers via
    /// <c>FriendManager.MarkForUpdate</c> and sends later on the periodic flush.
    /// </summary>
    void RefreshFriendList(long accountId);
    /// <summary>
    /// Delegates to <see cref="LobbyServerProtocol.BroadcastRefreshGroup"/>.
    /// <paramref name="resetReadyState"/> is required (no default) — intent must be explicit at call sites.
    /// </summary>
    void BroadcastRefreshGroup(long accountId, bool resetReadyState);
    void NotifyJoinedGroup(long accountId);
    void NotifyLeftGroup(long accountId);
    void NotifyGroupDisbanded(long accountId);
}
