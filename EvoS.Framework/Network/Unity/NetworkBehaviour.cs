using System;
using System.Collections.Generic;
using System.ComponentModel;
using EvoS.Framework.Assets;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Game;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Unity
{
    public class NetworkBehaviour : MonoBehaviour
    {
        public NetworkInstanceId netId => myView.netId;
        [JsonIgnore] public ClientConnection connectionToServer => myView.connectionToServer;
        [JsonIgnore] public ClientConnection connectionToClient => myView.connectionToClient;
        public short playerControllerId => myView.playerControllerId;
        protected uint syncVarDirtyBits => m_SyncVarDirtyBits;

        [JsonIgnore]
        protected bool syncVarHookGuard
        {
            get => m_SyncVarGuard;
            set => m_SyncVarGuard = value;
        }

        [JsonIgnore]
        private NetworkIdentity myView
        {
            get
            {
                NetworkIdentity myView;
                if (m_MyView == null)
                {
                    m_MyView = GetComponent<NetworkIdentity>();
                    if (m_MyView == null)
                    {
                        Log.Print(LogType.Error, "There is no NetworkIdentity on this object. Please add one.");
                    }

                    myView = m_MyView;
                }
                else
                {
                    myView = m_MyView;
                }

                return myView;
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeCommand(int cmdHash, NetworkReader reader)
        {
            return InvokeCommandDelegate(cmdHash, reader);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeRPC(int cmdHash, NetworkReader reader)
        {
            return InvokeRpcDelegate(cmdHash, reader);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncEvent(int cmdHash, NetworkReader reader)
        {
            return InvokeSyncEventDelegate(cmdHash, reader);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncList(int cmdHash, NetworkReader reader)
        {
            return InvokeSyncListDelegate(cmdHash, reader);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterCommandDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                Invoker invoker = new Invoker();
                invoker.invokeType = UNetInvokeType.Command;
                invoker.invokeClass = invokeClass;
                invoker.invokeFunction = func;
                s_CmdHandlerDelegates[cmdHash] = invoker;
                if (EvoSGameConfig.DebugNetworkBehaviour)
                    Log.Print(LogType.Debug,
                        string.Concat("RegisterCommandDelegate hash:", cmdHash, " ", func.GetMethodName()));
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterRpcDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                Invoker invoker = new Invoker();
                invoker.invokeType = UNetInvokeType.ClientRpc;
                invoker.invokeClass = invokeClass;
                invoker.invokeFunction = func;
                s_CmdHandlerDelegates[cmdHash] = invoker;
                if (EvoSGameConfig.DebugNetworkBehaviour)
                    Log.Print(LogType.Debug,
                        string.Concat("RegisterRpcDelegate hash:", cmdHash, " ", func.GetMethodName()));
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterEventDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                Invoker invoker = new Invoker();
                invoker.invokeType = UNetInvokeType.SyncEvent;
                invoker.invokeClass = invokeClass;
                invoker.invokeFunction = func;
                s_CmdHandlerDelegates[cmdHash] = invoker;
                if (EvoSGameConfig.DebugNetworkBehaviour)
                    Log.Print(LogType.Debug,
                        string.Concat("RegisterEventDelegate hash:", cmdHash, " ", func.GetMethodName()));
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected static void RegisterSyncListDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                Invoker invoker = new Invoker();
                invoker.invokeType = UNetInvokeType.SyncList;
                invoker.invokeClass = invokeClass;
                invoker.invokeFunction = func;
                s_CmdHandlerDelegates[cmdHash] = invoker;
                if (EvoSGameConfig.DebugNetworkBehaviour)
                    Log.Print(LogType.Debug,
                        string.Concat("RegisterSyncListDelegate hash:", cmdHash, " ", func.GetMethodName()));
            }
        }

        internal static string GetInvoker(int cmdHash)
        {
            string result;
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                result = null;
            }
            else
            {
                Invoker invoker = s_CmdHandlerDelegates[cmdHash];
                result = invoker.DebugString();
            }

            return result;
        }

        internal static bool GetInvokerForHashCommand(int cmdHash, out Type invokeClass,
            out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.Command, out invokeClass,
                out invokeFunction);
        }

        internal static bool GetInvokerForHashClientRpc(int cmdHash, out Type invokeClass,
            out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.ClientRpc,
                out invokeClass, out invokeFunction);
        }

        internal static bool GetInvokerForHashSyncList(int cmdHash, out Type invokeClass,
            out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.SyncList,
                out invokeClass, out invokeFunction);
        }

        internal static bool GetInvokerForHashSyncEvent(int cmdHash, out Type invokeClass,
            out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.SyncEvent,
                out invokeClass, out invokeFunction);
        }

        private static bool GetInvokerForHash(int cmdHash, UNetInvokeType invokeType,
            out Type invokeClass, out CmdDelegate invokeFunction)
        {
            Invoker invoker = null;
            bool result;
            if (!s_CmdHandlerDelegates.TryGetValue(cmdHash, out invoker))
            {
                Log.Print(LogType.Debug, "GetInvokerForHash hash:" + cmdHash + " not found");

                invokeClass = null;
                invokeFunction = null;
                result = false;
            }
            else if (invoker == null)
            {
                Log.Print(LogType.Debug, "GetInvokerForHash hash:" + cmdHash + " invoker null");

                invokeClass = null;
                invokeFunction = null;
                result = false;
            }
            else if (invoker.invokeType != invokeType)
            {
                Log.Print(LogType.Error, "GetInvokerForHash hash:" + cmdHash + " mismatched invokeType");

                invokeClass = null;
                invokeFunction = null;
                result = false;
            }
            else
            {
                invokeClass = invoker.invokeClass;
                invokeFunction = invoker.invokeFunction;
                result = true;
            }

            return result;
        }

        internal static void DumpInvokers()
        {
            Log.Print(LogType.Debug, "DumpInvokers size:" + s_CmdHandlerDelegates.Count);
            foreach (KeyValuePair<int, Invoker> keyValuePair in s_CmdHandlerDelegates)
            {
                Log.Print(LogType.Debug,
                    string.Concat("  Invoker:", keyValuePair.Value.invokeClass, ":",
                        keyValuePair.Value.invokeFunction.GetMethodName(), " ", keyValuePair.Value.invokeType, " ",
                        keyValuePair.Key));
            }
        }

        internal bool ContainsCommandDelegate(int cmdHash)
        {
            return s_CmdHandlerDelegates.ContainsKey(cmdHash);
        }

        internal bool InvokeCommandDelegate(int cmdHash, NetworkReader reader)
        {
            bool result;
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                result = false;
            }
            else
            {
                Invoker invoker = s_CmdHandlerDelegates[cmdHash];
                if (invoker.invokeType != UNetInvokeType.Command)
                {
                    result = false;
                }
                else
                {
                    if (GetType() != invoker.invokeClass)
                    {
                        if (!GetType().IsSubclassOf(invoker.invokeClass))
                        {
                            return false;
                        }
                    }

                    invoker.invokeFunction(this, reader);
                    result = true;
                }
            }

            return result;
        }

        internal bool InvokeRpcDelegate(int cmdHash, NetworkReader reader)
        {
            bool result;
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                result = false;
            }
            else
            {
                Invoker invoker = s_CmdHandlerDelegates[cmdHash];
                if (invoker.invokeType != UNetInvokeType.ClientRpc)
                {
                    result = false;
                }
                else
                {
                    if (GetType() != invoker.invokeClass)
                    {
                        if (!GetType().IsSubclassOf(invoker.invokeClass))
                        {
                            return false;
                        }
                    }

                    invoker.invokeFunction(this, reader);
                    result = true;
                }
            }

            return result;
        }

        internal bool InvokeSyncEventDelegate(int cmdHash, NetworkReader reader)
        {
            bool result;
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                result = false;
            }
            else
            {
                Invoker invoker = s_CmdHandlerDelegates[cmdHash];
                if (invoker.invokeType != UNetInvokeType.SyncEvent)
                {
                    result = false;
                }
                else
                {
                    invoker.invokeFunction(this, reader);
                    result = true;
                }
            }

            return result;
        }

        internal bool InvokeSyncListDelegate(int cmdHash, NetworkReader reader)
        {
            bool result;
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                result = false;
            }
            else
            {
                Invoker invoker = s_CmdHandlerDelegates[cmdHash];
                if (invoker.invokeType != UNetInvokeType.SyncList)
                {
                    result = false;
                }
                else if (GetType() != invoker.invokeClass)
                {
                    result = false;
                }
                else
                {
                    invoker.invokeFunction(this, reader);
                    result = true;
                }
            }

            return result;
        }

        internal static string GetCmdHashHandlerName(int cmdHash)
        {
            string result;
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                result = cmdHash.ToString();
            }
            else
            {
                Invoker invoker = s_CmdHandlerDelegates[cmdHash];
                result = invoker.invokeType + ":" + invoker.invokeFunction.GetMethodName();
            }

            return result;
        }

        private static string GetCmdHashPrefixName(int cmdHash, string prefix)
        {
            string result;
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                result = cmdHash.ToString();
            }
            else
            {
                Invoker invoker = s_CmdHandlerDelegates[cmdHash];
                string text = invoker.invokeFunction.GetMethodName();
                int num = text.IndexOf(prefix);
                if (num > -1)
                {
                    text = text.Substring(prefix.Length);
                }

                result = text;
            }

            return result;
        }

        internal static string GetCmdHashCmdName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeCmd");
        }

        internal static string GetCmdHashRpcName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeRpc");
        }

        internal static string GetCmdHashEventName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeSyncEvent");
        }

        internal static string GetCmdHashListName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeSyncList");
        }

        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, uint dirtyBit,
            ref NetworkInstanceId netIdField)
        {
            if (!m_SyncVarGuard)
            {
                NetworkInstanceId networkInstanceId = default;
                if (newGameObject != null)
                {
                    NetworkIdentity component = newGameObject.GetComponent<NetworkIdentity>();
                    if (component != null)
                    {
                        networkInstanceId = component.netId;
                        if (networkInstanceId.IsEmpty())
                        {
                            Log.Print(LogType.Warning, "SetSyncVarGameObject GameObject " + newGameObject +
                                                       " has a zero netId. Maybe it is not spawned yet?");
                        }
                    }
                }

                NetworkInstanceId networkInstanceId2 = default;
                if (gameObjectField != null)
                {
                    networkInstanceId2 = gameObjectField.GetComponent<NetworkIdentity>().netId;
                }

                if (networkInstanceId != networkInstanceId2)
                {
                    if (EvoSGameConfig.DebugSyncVars)
                        Log.Print(LogType.Debug,
                            string.Concat("SetSyncVar GameObject ", GetType().Name, " bit [", dirtyBit, "] netfieldId:",
                                networkInstanceId2, "->", networkInstanceId));

                    SetDirtyBit(dirtyBit);
                    gameObjectField = newGameObject;
                    netIdField = networkInstanceId;
                }
            }
        }

        protected void SetSyncVar<T>(T value, ref T fieldValue, uint dirtyBit)
        {
            bool flag = false;
            if (value == null)
            {
                if (fieldValue != null)
                {
                    flag = true;
                }
            }
            else
            {
                flag = !value.Equals(fieldValue);
            }

            if (flag)
            {
                if (EvoSGameConfig.DebugSyncVars)
                    Log.Print(LogType.Debug,
                        string.Concat("SetSyncVar ", GetType().Name, " bit [", dirtyBit, "] ", fieldValue, "->",
                            value));

                SetDirtyBit(dirtyBit);
                fieldValue = value;
            }
        }

        public void SetDirtyBit(uint dirtyBit)
        {
            m_SyncVarDirtyBits |= dirtyBit;
        }

        public void ClearAllDirtyBits()
        {
            m_LastSendTime = Time.time;
            m_SyncVarDirtyBits = 0u;
        }

        internal int GetDirtyChannel()
        {
            if (Time.time - m_LastSendTime > GetNetworkSendInterval())
            {
                if (m_SyncVarDirtyBits != 0u)
                {
                    return GetNetworkChannel();
                }
            }

            return -1;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendRPCInternal(NetworkWriter writer, int channelId, string rpcName)
        {
//            if (!this.isServer)
//            {
//                Log.Print(LogType.Warning, "ClientRpc call on un-spawned object");
//            }
//            else
//            {
            writer.FinishMessage();
            NetworkServer.SendWriterToReady(gameObject, writer, channelId);
//            }
        }

        public virtual bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public virtual void OnDeserialize(NetworkReader reader, bool initialState)
        {
            throw new NotImplementedException();
        }

        public virtual void PreStartClient()
        {
        }

        public virtual void OnNetworkDestroy()
        {
        }

        public virtual void OnStartServer()
        {
        }

        public virtual void OnStartClient()
        {
        }

        public virtual void OnStartAuthority()
        {
        }

        public virtual void OnStopAuthority()
        {
        }

        public virtual bool OnRebuildObservers(HashSet<ClientConnection> observers, bool initialize)
        {
            return false;
        }

        public virtual bool OnCheckObserver(ClientConnection conn)
        {
            return true;
        }

        public virtual int GetNetworkChannel()
        {
            return 0;
        }

        public virtual float GetNetworkSendInterval()
        {
//            return 0.1f; // TODO - default
            return -1f;
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            throw new System.NotImplementedException();
        }

        private uint m_SyncVarDirtyBits;
        private float m_LastSendTime;
        private bool m_SyncVarGuard;
        private const float k_DefaultSendInterval = 0.1f;
        private NetworkIdentity m_MyView;

        private static Dictionary<int, Invoker> s_CmdHandlerDelegates =
            new Dictionary<int, Invoker>();

        public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader);

        protected delegate void EventDelegate(List<Delegate> targets, NetworkReader reader);

        protected enum UNetInvokeType
        {
            Command,
            ClientRpc,
            SyncEvent,
            SyncList
        }

        protected class Invoker
        {
            public string DebugString()
            {
                return $"{invokeType}:{invokeClass}:{invokeFunction.GetMethodName()}";
            }

            public UNetInvokeType invokeType;
            public Type invokeClass;
            public CmdDelegate invokeFunction;
        }
    }
}
