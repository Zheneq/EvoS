using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using EvoS.Framework.Misc;
using log4net;
using Microsoft.AspNetCore.Http;

namespace CentralServer
{
    // Mimicking old websocketsharp types
    public class WsMessageEventArgs(byte[] rawData)
    {
        public byte[] RawData { get; } = rawData;
    }

    public class WsCloseEventArgs(ushort code, string reason, bool wasClean)
    {
        public ushort Code { get; } = code;
        public string Reason { get; } = reason;
        public bool WasClean { get; } = wasClean;
    }

    public class WsErrorEventArgs(string message, Exception exception)
    {
        public string Message { get; } = message;
        public Exception Exception { get; } = exception;
    }

    public abstract class WebSocketBehaviorBase<TMessage>
    {
        private static readonly ILog log = LogManager.GetLogger("WebSocketBehaviorBase");

        // websocket-sharp fragmented outgoing messages at this length by default. The Atlas client and
        // game server have only ever received fragmented output, so we replicate it to keep the on-wire
        // frame layout equivalent (avoids tripping any max-frame-size limits on the far end).
        private const int FragmentLength = 1016;
        private const int ReceiveBufferSize = 8192;

        // One registry per closed generic type: LobbyServerProtocol uses TMessage=WebSocketMessage and
        // BridgeServerProtocol uses TMessage=AllianceMessageBase, so each endpoint gets its own set of
        // connections automatically. Replaces websocket-sharp's per-service Sessions.Broadcast.
        private static readonly ConcurrentDictionary<WebSocketBehaviorBase<TMessage>, byte> Connections = new();

        private readonly Dictionary<Type, Action<TMessage, int>> messageHandlers = new Dictionary<Type, Action<TMessage, int>>();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private bool unregistered = false;
        private int closeFired = 0;

        private WebSocket _socket;
        private HttpContext _context;

        protected HttpContext Context => _context;
        public bool IsConnected => _socket is not null && _socket.State == WebSocketState.Open;

        protected static void LogMessage(string prefix, object message)
        {
            try
            {
                log.Debug($"{prefix} {message.GetType().Name} {DefaultJsonSerializer.Serialize(message)}");
            }
            catch (Exception)
            {
                log.Debug($"{prefix} {message.GetType().Name} <failed to serialize message>");
            }
        }

        /// <summary>
        /// Drives a single accepted connection: fires HandleOpen, runs the receive loop (one message at a
        /// time, in order), and fires HandleClose exactly once when the socket ends. Returns when the
        /// connection is done so the hosting ASP.NET Core request can complete.
        /// </summary>
        public async Task RunConnection(WebSocket socket, HttpContext context)
        {
            _socket = socket;
            _context = context;
            Connections.TryAdd(this, 0);

            Wrap((object x) => HandleOpen(), null);

            WsCloseEventArgs closeArgs;
            try
            {
                closeArgs = await ReceiveLoop();
            }
            catch (Exception ex)
            {
                HandleReceiveError(ex);
                closeArgs = new WsCloseEventArgs(1006, ex.Message, false);
            }
            finally
            {
                Connections.TryRemove(this, out _);
            }

            FireClose(closeArgs);
        }

        private async Task<WsCloseEventArgs> ReceiveLoop()
        {
            byte[] buffer = new byte[ReceiveBufferSize];
            while (_socket.State == WebSocketState.Open)
            {
                using MemoryStream ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return new WsCloseEventArgs(
                            (ushort)(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure),
                            result.CloseStatusDescription ?? "",
                            true);
                    }
                    ms.Write(buffer, 0, result.Count);
                }
                // websocket-sharp reassembled fragments for us; System.Net.WebSockets does not, so keep
                // reading until EndOfMessage with no size cap (some messages are large).
                while (!result.EndOfMessage);

                Wrap(HandleMessage, new WsMessageEventArgs(ms.ToArray()));
            }

            return new WsCloseEventArgs((ushort)WebSocketCloseStatus.NormalClosure, "", true);
        }

        private void FireClose(WsCloseEventArgs e)
        {
            if (Interlocked.Exchange(ref closeFired, 1) != 0)
            {
                return;
            }
            Wrap(x => log.Info($"Disconnect: code {x.Code}, reason '{x.Reason}', clean {x.WasClean}"), e);
            Wrap(HandleClose, e);
        }

        private void HandleReceiveError(Exception ex)
        {
            WsErrorEventArgs e = new WsErrorEventArgs(ex.Message, ex);
            if (!IsMinorError(e))
            {
                Wrap(x => log.Error($"Websocket Error: {x.Message} {x.Exception}"), e);
            }
            else
            {
                Wrap(x => log.Warn($"Websocket Error: {x.Message} {x.Exception}"), e);
            }
            Wrap(HandleError, e);
        }

        private static bool IsMinorError(WsErrorEventArgs e)
        {
            return e.Exception is IOException
                   || e.Exception is SocketException
                   || e.Exception is WebSocketException
                   || e.Exception is OperationCanceledException
                   || "The stream has been closed".Equals(e.Exception?.Message);
        }

        protected virtual void HandleOpen()
        {
        }

        protected virtual void HandleClose(WsCloseEventArgs e)
        {
        }

        protected virtual void HandleError(WsErrorEventArgs e)
        {
        }

        public void CloseConnection()
        {
            try
            {
                CloseConnectionAsync().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                log.Warn("Failed to close connection", e);
            }
        }

        private async Task CloseConnectionAsync()
        {
            WebSocket socket = _socket;
            if (socket is null)
            {
                return;
            }

            await _sendLock.WaitAsync();
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    // Only send the close frame (don't wait to receive one) so this can run concurrently
                    // with the pending ReceiveAsync in the receive loop, which will then observe the close.
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                log.Warn("Failed to close connection cleanly, aborting", e);
                try { socket.Abort(); } catch { /* ignored */ }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        protected abstract TMessage DeserializeMessage(byte[] data, out int callbackId);

        protected virtual void HandleMessage(WsMessageEventArgs e)
        {
            TMessage deserialized = default(TMessage);
            int callbackId = 0;

            try
            {
                deserialized = DeserializeMessage(e.RawData, out callbackId);
            }
            catch (NullReferenceException)
            {
                log.Error("No message handler registered for data: " + BitConverter.ToString(e.RawData));
            }
            catch (Exception ex)
            {
                log.Error("Failed to deserialize data: " + BitConverter.ToString(e.RawData), ex);
            }

            if (deserialized != null)
            {
                Action<TMessage, int> handler = GetHandler(deserialized.GetType());
                if (handler != null)
                {
                    LogMessage("<", deserialized);
                    try
                    {
                        handler.Invoke(deserialized, callbackId);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Handler for {deserialized.GetType()} failed", ex);
                    }
                }
                else
                {
                    log.Error("No handler for " + deserialized.GetType().Name + ": " + DefaultJsonSerializer.Serialize(deserialized));
                }
            }
            else if (e.RawData.Length > 0)
            {
                log.Error("Request deserialized to null, "
                          + $"size={e.RawData.Length} "
                          + $"data=\"{System.Text.Encoding.Default.GetString(e.RawData)}\"");
            }
        }

        /// <summary>
        /// Sends a single binary message, replicating websocket-sharp: sends are serialized (one at a time
        /// per connection) and payloads larger than FragmentLength go out as multiple continuation frames.
        /// </summary>
        protected void Send(byte[] data)
        {
            try
            {
                SendAsync(data).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                log.Warn("Failed to send message", e);
            }
        }

        private async Task SendAsync(byte[] data)
        {
            WebSocket socket = _socket;
            if (socket is null || socket.State != WebSocketState.Open)
            {
                log.Warn("Attempted to send to a disconnected socket");
                return;
            }

            await _sendLock.WaitAsync();
            try
            {
                int offset = 0;
                do
                {
                    int count = Math.Min(FragmentLength, data.Length - offset);
                    bool endOfMessage = offset + count >= data.Length;
                    await socket.SendAsync(
                        new ArraySegment<byte>(data, offset, count),
                        WebSocketMessageType.Binary,
                        endOfMessage,
                        CancellationToken.None);
                    offset += count;
                }
                while (offset < data.Length);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        protected void BroadcastRaw(byte[] data)
        {
            foreach (WebSocketBehaviorBase<TMessage> conn in Connections.Keys)
            {
                if (conn.IsConnected)
                {
                    conn.Send(data);
                }
            }
        }

        protected void RegisterHandler<T>(Action<T, int> handler) where T : TMessage
        {
            messageHandlers.Add(typeof(T), (msg, callbackId) => { handler((T)msg, callbackId); });
        }

        protected void RegisterHandler<T>(Action<T> handler) where T : TMessage
        {
            messageHandlers.Add(typeof(T), (msg, callbackId) => { handler((T)msg); });
        }

        protected void UnregisterAllHandlers()
        {
            unregistered = true;
            messageHandlers.Clear();
        }

        private Action<TMessage, int> GetHandler(Type type)
        {
            messageHandlers.TryGetValue(type, out Action<TMessage, int> handler);
            if (handler == null && !unregistered)
            {
                log.Error("No handler found for type " + type.Name);
            }
            return handler;
        }

        protected abstract string GetConnContext();

        private void LogContextPush()
        {
            string connContext = GetConnContext();
            LogicalThreadContext.Stacks["conns"].Push(connContext);
            LogicalThreadContext.Properties["conn"] = connContext;
        }

        private void LogContextPop()
        {
            LogicalThreadContext.Stacks["conns"].Pop();
            if (LogicalThreadContext.Stacks["conns"].Count > 0)
            {
                string connContext = LogicalThreadContext.Stacks["conns"].Pop();
                LogicalThreadContext.Stacks["conns"].Push(connContext);
                LogicalThreadContext.Properties["conn"] = connContext;
            }
            else
            {
                LogicalThreadContext.Properties["conn"] = null;
            }
        }

        protected void Wrap<T>(Action<T> handler, T param)
        {
            LogContextPush();
            try
            {
                handler(param);
            }
            finally
            {
                LogContextPop();
            }
        }

        // plz forgive me
        protected R Wrap<T1, T2, R>(Func<T1, T2, R> handler, T1 param1, T2 param2)
        {
            LogContextPush();
            try
            {
                return handler(param1, param2);
            }
            finally
            {
                LogContextPop();
            }
        }

        protected void Wrap<T1, T2, T3>(Action<T1, T2, T3> handler, T1 param1, T2 param2, T3 param3)
        {
            LogContextPush();
            try
            {
                handler(param1, param2, param3);
            }
            finally
            {
                LogContextPop();
            }
        }
    }
}
