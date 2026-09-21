using CentralServer.LobbyServer.Session;
using EvoS.Framework.Network.NetworkMessages;
using log4net;

namespace CentralServer.LobbyServer.Chat;

public class ChatModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ChatModule));
    private readonly IClientConnection _conn;

    public ChatModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<ChatNotification>(HandleChatNotification);
        registry.Register<GroupChatRequest>(HandleGroupChatRequest);
    }

    private void HandleChatNotification(ChatNotification notification)
    {
        ChatManager.Get().HandleChatNotification(_conn, notification);
    }

    private void HandleGroupChatRequest(GroupChatRequest request)
    {
        ChatManager.Get().HandleGroupChatRequest(_conn, request);
    }
}
