using EvoS.Framework;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using System.IO;
using EvoS.Framework.Network;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;

namespace EvoS.LobbyServer
{
    public class LobbyServer
    {
        private static List<LobbyServerConnection> ConnectedClients = new List<LobbyServerConnection>();

        public static void Main(string[] args = null)
        {
            Task server = Task.Run(StartServer);
            server.Wait();
            Log.Print(LogType.Lobby, "Server Stopped");
        }

        private static async Task StartServer()
        {
            Log.Print(LogType.Lobby, "Starting LobbyServer");
            WebSocketListener server = new WebSocketListener(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 6060));
            server.Standards.RegisterStandard(new WebSocketFactoryRfc6455());

            // Server doesnt start if i await StartAsync...
#pragma warning disable CS4014
            server.StartAsync();
#pragma warning restore CS4014

            Log.Print(LogType.Lobby, "Started LobbyServer on '0.0.0.0:6060'");
            LobbyQueueManager.GetInstance();
            while (true)
            {
                Log.Print(LogType.Lobby, "Waiting for clients to connect...");
                WebSocket socket = await server.AcceptWebSocketAsync(CancellationToken.None);
                Log.Print(LogType.Lobby, "Client connected");
                LobbyServerConnection newClient = new LobbyServerConnection(socket);
                newClient.OnDisconnect += NewClient_OnDisconnect;
                ConnectedClients.Add(newClient);
                new Thread(newClient.HandleConnection).Start();
            }
        }

        private static void NewClient_OnDisconnect(object sender, EventArgs e)
        {
            ConnectedClients.Remove((LobbyServerConnection)sender);
        }

        private static async void HandleClient(object arg)
        {
            WebSocket client = (WebSocket)arg;
            while (true)
            {
                WebSocketMessageReadStream message = await client.ReadMessageAsync(CancellationToken.None);
                if (!client.IsConnected || message == null)
                {
                    Log.Print(LogType.Lobby, "A client disconnected");
                    client.Dispose();
                    return;
                }
                Log.Print(LogType.Debug, $"RECV {message.MessageType} {(message.MessageType == WebSocketMessageType.Text ? message.ReadText() : message.ReadBinary())}");
                message.Dispose();
            }

        }

        public static async Task sendChatAsync(ChatNotification chat, LobbyServerConnection sender)
        {
            switch (chat.ConsoleMessageType)
            {
                case ConsoleMessageType.GlobalChat:
                    sendChatToAll(chat);
                    break;
                
                case ConsoleMessageType.WhisperChat:
                    await sender.SendMessage(chat);
                    LobbyServerConnection recipient = GetPlayerByHandle(chat.RecipientHandle);
                    if (recipient != null)
                        await recipient.SendMessage(chat);
                    break;
                
                default:
                    Log.Print(LogType.Warning, $"Unhandled chat type: {chat.ConsoleMessageType.ToString()}");
                    break;
            }
            
        }

        public static async Task sendChatToAll(ChatNotification chat)
        {
            foreach (LobbyServerConnection con in ConnectedClients)
                await con.SendMessage(chat);
        }

        public static LobbyServerConnection GetPlayerByHandle(string handle)
        {
            string DEV_TAG = "";
            if (handle.StartsWith(DEV_TAG)) {
                handle = handle.Substring(DEV_TAG.Length);
            }

            foreach (LobbyServerConnection con in ConnectedClients)
            {
                /*if (con.PlayerInfo.GetHandle() == handle)
                {
                    return con;
                }*/
            }
            return null;
        }

        public static LobbyServerConnection GetPlayerByAccountId(long accountId)
        {
            foreach (LobbyServerConnection con in ConnectedClients)
            {
                //if (con.PlayerInfo.GetAccountId() == accountId)
                //{
                //    return con;
                //}
            }
            return null;
        }
    }
}
