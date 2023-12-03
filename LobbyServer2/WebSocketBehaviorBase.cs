using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EvoS.Framework.Misc;
using log4net;
using WebSocketSharp;
using WebSocketSharp.Server;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

namespace CentralServer
{
    public abstract class WebSocketBehaviorBase<TMessage> : WebSocketBehavior
    {
        private static readonly ILog log = LogManager.GetLogger("WebSocketBehaviorBase");
        
        private readonly Dictionary<Type, Func<TMessage, int, Task>> messageHandlers = new Dictionary<Type, Func<TMessage, int, Task>>();
        private bool unregistered = false;
        public bool IsConnected { get; private set; } = true; // TODO default to false, set to true in OnOpen?
        
        protected void LogDebug(string msg)
        {
            Wrap(() => log.Debug(msg));
        }
        
        protected void LogInfo(string msg)
        {
            Wrap(() => log.Info(msg));
        }
        
        protected void LogWarn(string msg)
        {
            Wrap(() => log.Warn(msg));
        }
        
        protected void LogError(string msg)
        {
            Wrap(() => log.Error(msg));
        }

        protected void LogMessage(string prefix, object message)
        {
            try
            {
                LogDebug($"{prefix} {message.GetType().Name} {DefaultJsonSerializer.Serialize(message)}");
            }
            catch (Exception e)
            {
                LogDebug($"{prefix} {message.GetType().Name} <failed to serialize message>");
            }
        }
        
        protected sealed override void OnOpen()
        {
            IsConnected = true;
            Wrap((object x) => HandleOpen(), null);
        }

        protected virtual void HandleOpen()
        {
        }
        
        protected sealed override void OnClose(CloseEventArgs e)
        {
            IsConnected = false;
            LogInfo($"Disconnect: code {e.Code}, reason '{e.Reason}', clean {e.WasClean}");
            Wrap(HandleClose, e);
        }

        protected virtual void HandleClose(CloseEventArgs e)
        {
        }

        public void CloseConnection()
        {
            Close();
        }

        protected sealed override void OnError(ErrorEventArgs e)
        {
            if (!IsMinorError(e))
            {
                Wrap(x =>
                {
                    log.Error($"Websocket Error: {x.Message} {x.Exception}");
                }, e);
            }
            else
            {
                Wrap(x =>
                {
                    log.Warn($"Websocket Error: {x.Message} {x.Exception}");
                }, e);
            }
            Wrap(HandleError, e);
        }

        private static bool IsMinorError(ErrorEventArgs e)
        {
            return e.Exception is IOException
                   || "The stream has been closed".Equals(e.Exception?.Message);
        }

        protected virtual void HandleError(ErrorEventArgs e)
        {
        }

        protected sealed override void OnMessage(MessageEventArgs e)
        {
            Wrap(HandleMessage, e);
        }
        
        public Task Send(TMessage message, int callbackId = 0)
        {
            var t = new TaskCompletionSource<bool>();
            if (!IsConnected)
            {
                LogWarn($"Attempted to send {message.GetType()} to a disconnected socket");
                t.TrySetResult(false);
                return t.Task;
            }
            MemoryStream stream = new MemoryStream();
            if (SerializeMessage(stream, message, callbackId))
            {
                SendAsync(stream.ToArray(), completed => Wrap(c =>
                {
                    LogMessage(c ? ">" : ">!", message);
                    t.TrySetResult(c);
                }, completed));
            }
            else
            {
                log.Error($"No sender for {message.GetType().Name}");
                LogMessage(">X", message);
                t.TrySetResult(false);
            }
            
            return t.Task;
        }

        public Task Broadcast(TMessage message, int callbackId = 0)
        {
            var t = new TaskCompletionSource();
            MemoryStream stream = new MemoryStream();
            if (SerializeMessage(stream, message, callbackId))
            {
                Sessions.BroadcastAsync(stream.ToArray(), () =>
                {
                    LogMessage(">>", message);
                    t.TrySetResult();
                });
            }
            else
            {
                log.Error($"No sender for {message.GetType().Name}");
                LogMessage(">>X", message);
                t.TrySetResult();
            }
            return t.Task;
        }

        protected abstract TMessage DeserializeMessage(byte[] data, out int callbackId);
        
        protected abstract bool SerializeMessage(MemoryStream stream, TMessage message, int callbackId);

        protected async void HandleMessage(MessageEventArgs e)
        {
            TMessage deserialized = default(TMessage);
            int callbackId = 0;

            try
            {
                deserialized = DeserializeMessage(e.RawData, out callbackId);
            }
            catch (NullReferenceException nullEx)
            {
                log.Error("No message handler registered for data: " + BitConverter.ToString(e.RawData));
            }
            catch (Exception ex)
            {
                log.Error("Failed to deserialize data: " + BitConverter.ToString(e.RawData), ex);
            }

            if (deserialized != null)
            {
                Func<TMessage, int, Task> handler = GetHandler(deserialized.GetType());
                if (handler != null)
                {
                    LogMessage("<", deserialized);
                    try
                    {
                        await handler.Invoke(deserialized, callbackId);
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
        }

        // protected void RegisterHandler<T>(Action<T, int> handler) where T : TMessage
        // {
        //     messageHandlers.Add(typeof(T), (msg, callbackId) =>
        //     {
        //         handler((T)msg, callbackId);
        //         return Task.CompletedTask;
        //     });
        // }
        //
        // protected void RegisterHandler<T>(Action<T> handler) where T : TMessage
        // {
        //     messageHandlers.Add(typeof(T), (msg, callbackId) =>
        //     {
        //         handler((T)msg);
        //         return Task.CompletedTask;
        //     });
        // }

        protected void RegisterHandler<T>(Func<T, Task> handler) where T : TMessage
        {
            messageHandlers.Add(typeof(T), async (msg, callbackId) => { await handler((T)msg); });
        }
        
        protected void RegisterHandler<T>(Func<T, int, Task> handler) where T : TMessage
        {
            messageHandlers.Add(typeof(T), async (msg, callbackId) => { await handler((T)msg, callbackId); });
        }

        protected void UnregisterAllHandlers()
        {
            unregistered = true;
            messageHandlers.Clear();
        }

        private Func<TMessage, int, Task> GetHandler(Type type)
        {
            messageHandlers.TryGetValue(type, out var handler);
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
        
        protected void Wrap(Action handler)
        {
            LogContextPush();
            try
            {
                handler();
            }
            finally
            {
                LogContextPop();
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
        
        protected async Task Wrap<T>(Func<T, Task> handler, T param)
        {
            LogContextPush();
            try
            {
                await handler(param);
            }
            finally
            {
                LogContextPop();
            }
        }
        
        // plz forgive me
        protected void Wrap<T1, T2>(Action<T1, T2> handler, T1 param1, T2 param2)
        {
            LogContextPush();
            try
            {
                handler(param1, param2);
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