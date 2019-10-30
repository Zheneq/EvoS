using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Game.Messages;
using EvoS.Framework.Network.Unity;
using EvoS.Framework.Network.Unity.Messages;
using vtortola.WebSockets;

namespace EvoS.Framework.Network.Game
{
    public delegate void UNetGameMessageDelegate<T>(GamePlayer player, T msg) where T : MessageBase;

    public class ClientConnection
    {
        protected vtortola.WebSockets.WebSocket Socket;
        public GameManager ActiveGame { get; set; }

        public virtual string address => Socket.RemoteEndpoint.ToString();

        protected UNetSerializer Serializer = new UNetSerializer();
        protected HashSet<NetworkIdentity> _visList = new HashSet<NetworkIdentity>();
        protected HashSet<NetworkInstanceId> _clientOwnedObjects;
        public bool isReady;
        protected static int _connectionIdCounter;
        protected uint lastMessageOutgoingSeqNum;
        public readonly int connectionId = Interlocked.Increment(ref _connectionIdCounter);
        protected NetworkWriter m_Writer = new NetworkWriter();
        
        protected ClientConnection() {}

        public ClientConnection(vtortola.WebSockets.WebSocket socket)
        {
            Socket = socket;
            isReady = true;

            SetupLoginHandler();
        }

        public void RegisterHandler<T>(short msgId, GamePlayer player, UNetGameMessageDelegate<T> handler)
            where T : MessageBase
        {
            Serializer.RegisterHandler(msgId, msg => handler(player, (T) msg));
        }

        protected void SetupLoginHandler()
        {
            // The client sends AddPlayer and then LoginRequest, instead we'll use the session token to determine
            // which Game the connection is fore

            AddPlayerMessage addPlayerMessage = null;
            Serializer.RegisterHandler(37, msg => { addPlayerMessage = (AddPlayerMessage) msg; });
            Serializer.RegisterHandler(51, msg =>
            {
                var loginReq = (LoginRequest) msg;
                if (addPlayerMessage == null)
                {
                    Log.Print(LogType.Error, "Received LoginRequest before AddPlayerMessage!");
                    Disconnect();
                    return;
                }

                var gameManager = GameManagerHolder.FindGameManager(loginReq);
                if (gameManager == null)
                {
                    Log.Print(LogType.Error, $"Didn't find a GameManager for {loginReq}'");
                    Disconnect();
                    return;
                }

                Send(52, new LoginResponse
                {
                    Reconnecting = false,
                    Success = true
                });

                gameManager.AddPlayer(this, loginReq, addPlayerMessage);
            });
        }

        internal void AddToVisList(NetworkIdentity uv)
        {
            _visList.Add(uv);
            ActiveGame.NetworkServer.ShowForConnection(uv, this);
        }

        internal void RemoveFromVisList(NetworkIdentity uv, bool isDestroyed)
        {
            _visList.Remove(uv);
            if (!isDestroyed)
            {
//                NetworkServer.HideForConnection(uv, this);
                Send(13, new ObjectDestroyMessage
                {
                    netId = uv.netId
                });
            }
        }

        internal void RemoveObservers()
        {
            foreach (NetworkIdentity networkIdentity in _visList)
            {
                networkIdentity.RemoveObserverInternal(this);
            }

            _visList.Clear();
        }

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            if (_clientOwnedObjects == null)
            {
                _clientOwnedObjects = new HashSet<NetworkInstanceId>();
            }

            _clientOwnedObjects.Add(obj.netId);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            _clientOwnedObjects?.Remove(obj.netId);
        }


        public void Disconnect()
        {
            Log.Print(LogType.Game, "Client disconnected.");
            if (Socket.IsConnected)
                Socket.Close();

            isReady = false;

            Socket.Dispose();
        }

        public virtual void SendByChannel(short msgType, MessageBase msg, int channelId)
        {
            m_Writer.StartMessage(msgType);
            msg.Serialize(m_Writer);
            m_Writer.FinishMessage();
            SendWriter(m_Writer, channelId);
        }

        public virtual void SendWriter(NetworkWriter writer, int channelId)
        {
            writer.WriteSeqNum(++lastMessageOutgoingSeqNum);
            channelId = 0;
            SendWSMessage(writer);
        }

        public virtual bool SendBytes(byte[] bytes, int numBytes, int channelId)
        {
            uint num = ++lastMessageOutgoingSeqNum;
            bytes[0] = (byte) (num & byte.MaxValue);
            bytes[1] = (byte) (num >> 8 & byte.MaxValue);
            bytes[2] = (byte) (num >> 16 & byte.MaxValue);
            bytes[3] = (byte) (num >> 24 & byte.MaxValue);
            channelId = 0;
            SendWSMessage(bytes, numBytes);
            return true;
        }

        public void Send(short msgType, MessageBase msg)
        {
            SendByChannel(msgType, msg, 0);
        }

        public async Task SendWSMessage(NetworkWriter msg)
        {
            var responseStream = new MemoryStream();
            responseStream.Write(msg.AsArraySegment().Array, 0, msg.AsArraySegment().Count);

            using (var writer = Socket.CreateMessageWriter(WebSocketMessageType.Binary))
            {
                await writer.WriteAsync(UNetMessage.Serialize(responseStream.ToArray()));
                await writer.FlushAsync();
            }
        }

        public async Task SendWSMessage(byte[] bytes, int count)
        {
            var responseStream = new MemoryStream();
            responseStream.Write(bytes, 0, count);

            using (var writer = Socket.CreateMessageWriter(WebSocketMessageType.Binary))
            {
                await writer.WriteAsync(UNetMessage.Serialize(responseStream.ToArray()));
                await writer.FlushAsync();
            }
        }

        public async Task SendAsync(short msgId, object message)
        {
            throw new NotImplementedException();
        }

        public async void HandleConnection()
        {
            while (true)
            {
                var message = await Socket.ReadMessageAsync(CancellationToken.None);

                if (message == null)
                {
                    Log.Print(LogType.Warning, "Message is null");
                    Disconnect();
                    return;
                }

                if (message.MessageType != WebSocketMessageType.Binary)
                {
                    throw new NotImplementedException();
                }

                using (var ms = new MemoryStream())
                {
                    await message.CopyToAsync(ms);
                    await ms.FlushAsync();

                    var messageBytes = ms.ToArray();

                    Serializer.ProcessUNetMessages(messageBytes);
                }

                message.Dispose();
            }
        }
    }
}
