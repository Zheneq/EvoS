using System;
using System.Collections.Generic;
using System.IO;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.WebSocket;
using log4net;
using WebSocketSharp;
using WebSocketSharp.Server;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

namespace CentralServer
{
    public abstract class WebSocketBehaviorBase<TMessage> : WebSocketBehavior
    {
        private static readonly ILog log = LogManager.GetLogger("WebSocketBehaviorBase");
        
        private readonly Dictionary<Type, Action<TMessage, int>> messageHandlers = new Dictionary<Type, Action<TMessage, int>>();
        private bool unregistered = false;

        protected static void LogMessage(string prefix, object message)
        {
            try
            {
                log.Debug($"{prefix} {message.GetType().Name} {DefaultJsonSerializer.Serialize(message)}");
            }
            catch (Exception e)
            {
                log.Debug($"{prefix} {message.GetType().Name} <failed to serialize message>");
            }
        }
        
        protected sealed override void OnOpen()
        {
            Wrap((object x) => HandleOpen(), null);
        }

        protected virtual void HandleOpen()
        {
        }
        
        protected sealed override void OnClose(CloseEventArgs e)
        {
            Wrap(x => log.Info($"Disconnect: code {x.Code}, reason '{x.Reason}', clean {x.WasClean}"), e);
            Wrap(HandleClose, e);
        }

        protected virtual void HandleClose(CloseEventArgs e)
        {
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

        protected abstract TMessage DeserializeMessage(byte[] data, out int callbackId);

        protected virtual void HandleMessage(MessageEventArgs e)
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