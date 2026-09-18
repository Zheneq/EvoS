using System.Collections.Generic;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Network.WebSocket;

namespace Tests.Lib;

/// <summary>
/// Hand-rolled recording fake for <see cref="IClientConnection"/>. Accumulates all sent
/// messages so tests can assert on protocol output without spinning up live connections.
/// </summary>
public class RecordingClientConnection : IClientConnection
{
    public long AccountId { get; set; }
    public Game CurrentGame { get; set; } = null!;
    public readonly List<WebSocketMessage> Sent = new();

    public void Send(WebSocketMessage message) => Sent.Add(message);
}
