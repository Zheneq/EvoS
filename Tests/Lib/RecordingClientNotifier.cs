using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace Tests.Lib;

/// <summary>
/// Hand-rolled recording fake for <see cref="IClientNotifier"/>. Accumulates all calls so
/// tests can assert on the messages sent without spinning up live connections.
/// </summary>
public class RecordingClientNotifier : IClientNotifier
{
    public readonly HashSet<long> OnlineAccounts = new();
    public readonly List<(long AccountId, WebSocketMessage Message)> Sent = new();
    public readonly List<(long AccountId, LocalizationPayload Message)> SystemMessages = new();
    public readonly List<(long AccountId, string Text)> SystemMessageTexts = new();
    public readonly List<long> FriendListUpdates = new();
    public readonly List<long> FriendListRefreshes = new();
    public readonly List<(long AccountId, bool ResetReadyState)> GroupRefreshes = new();
    public readonly List<long> JoinedGroup = new();
    public readonly List<long> LeftGroup = new();
    public readonly List<long> GroupDisbanded = new();

    public bool IsOnline(long accountId) => OnlineAccounts.Contains(accountId);
    public void Send(long accountId, WebSocketMessage message) => Sent.Add((accountId, message));
    public void SendSystemMessage(long accountId, LocalizationPayload message) => SystemMessages.Add((accountId, message));
    public void SendSystemMessage(long accountId, string text) => SystemMessageTexts.Add((accountId, text));
    public void MarkFriendListForUpdate(long accountId) => FriendListUpdates.Add(accountId);
    public void RefreshFriendList(long accountId) => FriendListRefreshes.Add(accountId);
    public void BroadcastRefreshGroup(long accountId, bool resetReadyState) => GroupRefreshes.Add((accountId, resetReadyState));
    public void NotifyJoinedGroup(long accountId) => JoinedGroup.Add(accountId);
    public void NotifyLeftGroup(long accountId) => LeftGroup.Add(accountId);
    public void NotifyGroupDisbanded(long accountId) => GroupDisbanded.Add(accountId);
}

/// <summary>
/// Swaps in a <see cref="RecordingClientNotifier"/> for the duration of a test, then resets
/// to the default implementation on dispose.
/// </summary>
public sealed class ClientNotifierScope : IDisposable
{
    public readonly RecordingClientNotifier Notifier = new();

    public ClientNotifierScope()
    {
        ClientNotifier.Set(Notifier);
    }

    public void Dispose()
    {
        ClientNotifier.Reset();
    }
}
