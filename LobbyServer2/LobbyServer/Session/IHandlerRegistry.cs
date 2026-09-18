using System;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Session;

public interface IHandlerRegistry
{
    void Register<T>(Action<T> handler) where T : WebSocketMessage;
}
