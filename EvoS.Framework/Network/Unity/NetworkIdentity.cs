using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Game;

namespace EvoS.Framework.Network.Unity
{
    [SerializedMonoBehaviour("NetworkIdentity")]
    public class NetworkIdentity : MonoBehaviour
    {
        public bool LocalPlayerAuthority { get; set; }
        public NetworkInstanceId netId => m_NetId;
        public NetworkSceneId sceneId => m_SceneId;
        public NetworkServer Server { set; private get; }

        public bool serverOnly
        {
            get => m_ServerOnly;
            set => m_ServerOnly = value;
        }

        public NetworkHash128 assetId => m_AssetId;

        public void SetDynamicAssetId(NetworkHash128 newAssetId)
        {
            if (!m_AssetId.IsValid() || m_AssetId.Equals(newAssetId))
            {
                m_AssetId = newAssetId;
            }
            else
            {
                Log.Print(LogType.Warning, "SetDynamicAssetId object already has an assetId <" + m_AssetId + ">");
            }
        }

        public short playerControllerId => m_PlayerId;

        public ClientConnection connectionToServer => m_ConnectionToServer;

        public ClientConnection connectionToClient => m_ConnectionToClient;

        public ReadOnlyCollection<ClientConnection> observers
        {
            get
            {
                ReadOnlyCollection<ClientConnection> result;
                result = m_Observers == null ? null : new ReadOnlyCollection<ClientConnection>(m_Observers);
                return result;
            }
        }

        private void CacheBehaviours()
        {
            if (m_NetworkBehaviours == null)
            {
                m_NetworkBehaviours = GetComponents<NetworkBehaviour>().ToArray();
            }
        }

        public void SetNetworkInstanceId(NetworkInstanceId newNetId)
        {
            m_NetId = newNetId;
            if (newNetId.Value == 0u)
            {
//                m_IsServer = false;
            }
        }

        public void ForceSceneId(int newSceneId)
        {
            m_SceneId = new NetworkSceneId((uint) newSceneId);
        }

        public void RemoveObserverInternal(ClientConnection conn)
        {
            if (m_Observers != null)
            {
                m_Observers.Remove(conn);
                m_ObserverConnections.Remove(conn.connectionId);
            }
        }

        public bool OnCheckObserver(ClientConnection conn)
        {
            for (int i = 0; i < m_NetworkBehaviours.Length; i++)
            {
                NetworkBehaviour networkBehaviour = m_NetworkBehaviours[i];
                try
                {
                    if (!networkBehaviour.OnCheckObserver(conn))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Print(LogType.Error, "Exception in OnCheckObserver:" + ex.Message + " " + ex.StackTrace);
                }
            }

            return true;
        }

        public void UNetSerializeAllVars(NetworkWriter writer)
        {
            CacheBehaviours();

            for (int i = 0; i < m_NetworkBehaviours.Length; i++)
            {
                NetworkBehaviour networkBehaviour = m_NetworkBehaviours[i];
                if (EvoSGameConfig.DebugNetSerialize)
                    Log.Print(LogType.Debug,
                        $"Will serialize {GetType().Name}.{networkBehaviour.GetType().Name} at {writer.Position}");
                networkBehaviour.OnSerialize(writer, true);
            }

            if (EvoSGameConfig.DebugNetSerialize)
                Log.Print(LogType.Debug, $"Finished UNetSerializeAllVars at {writer.Position}");
        }

        private bool GetInvokeComponent(int cmdHash, Type invokeClass, out NetworkBehaviour invokeComponent)
        {
            NetworkBehaviour networkBehaviour = null;
            for (int i = 0; i < m_NetworkBehaviours.Length; i++)
            {
                NetworkBehaviour networkBehaviour2 = m_NetworkBehaviours[i];
                if (networkBehaviour2.GetType() == invokeClass || networkBehaviour2.GetType().IsSubclassOf(invokeClass))
                {
                    networkBehaviour = networkBehaviour2;
                    break;
                }
            }

            bool result;
            if (networkBehaviour == null)
            {
                string cmdHashHandlerName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Error,
                    string.Concat("Found no behaviour for incoming [", cmdHashHandlerName, "] on ", gameObject,
                        ",  the server and client should have the same NetworkBehaviour instances [netId=", netId,
                        "]."));
                invokeComponent = null;
                result = false;
            }
            else
            {
                invokeComponent = networkBehaviour;
                result = true;
            }

            return result;
        }

        public void HandleSyncEvent(int cmdHash, NetworkReader reader)
        {
            Type invokeClass;
            NetworkBehaviour.CmdDelegate cmdDelegate;
            NetworkBehaviour obj;
            if (gameObject == null)
            {
                string cmdHashHandlerName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Warning, string.Concat("SyncEvent [", cmdHashHandlerName,
                    "] received for deleted object [netId=", netId, "]"));
            }
            else if (!NetworkBehaviour.GetInvokerForHashSyncEvent(cmdHash, out invokeClass, out cmdDelegate))
            {
                string cmdHashHandlerName2 = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Error,
                    string.Concat("Found no receiver for incoming [", cmdHashHandlerName2, "] on ", gameObject,
                        ",  the server and client should have the same NetworkBehaviour instances [netId=", netId,
                        "]."));
            }
            else if (!GetInvokeComponent(cmdHash, invokeClass, out obj))
            {
                string cmdHashHandlerName3 = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Warning, string.Concat("SyncEvent [", cmdHashHandlerName3,
                    "] handler not found [netId=",
                    netId, "]"));
            }
            else
            {
                cmdDelegate(obj, reader);
            }
        }

        public void HandleSyncList(int cmdHash, NetworkReader reader)
        {
            Type invokeClass;
            NetworkBehaviour.CmdDelegate cmdDelegate;
            NetworkBehaviour obj;
            if (gameObject == null)
            {
                string cmdHashHandlerName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Warning, string.Concat("SyncList [", cmdHashHandlerName,
                    "] received for deleted object [netId=", netId, "]"));
            }
            else if (!NetworkBehaviour.GetInvokerForHashSyncList(cmdHash, out invokeClass, out cmdDelegate))
            {
                string cmdHashHandlerName2 = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Error,
                    string.Concat("Found no receiver for incoming [", cmdHashHandlerName2, "] on ", gameObject,
                        ",  the server and client should have the same NetworkBehaviour instances [netId=", netId,
                        "]."));
            }
            else if (!GetInvokeComponent(cmdHash, invokeClass, out obj))
            {
                string cmdHashHandlerName3 = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Warning, string.Concat("SyncList [", cmdHashHandlerName3,
                    "] handler not found [netId=",
                    netId, "]"));
            }
            else
            {
                cmdDelegate(obj, reader);
            }
        }

        public void HandleCommand(int cmdHash, NetworkReader reader)
        {
            Type invokeClass;
            NetworkBehaviour.CmdDelegate cmdDelegate;
            NetworkBehaviour obj;
            if (gameObject == null)
            {
                string cmdHashHandlerName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Warning, string.Concat("Command [", cmdHashHandlerName,
                    "] received for deleted object [netId=", netId, "]"));
            }
            else if (!NetworkBehaviour.GetInvokerForHashCommand(cmdHash, out invokeClass, out cmdDelegate))
            {
                string cmdHashHandlerName2 = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Error,
                    string.Concat("Found no receiver for incoming [", cmdHashHandlerName2, "] on ", gameObject,
                        ",  the server and client should have the same NetworkBehaviour instances [netId=", netId,
                        "]."));
            }
            else if (!GetInvokeComponent(cmdHash, invokeClass, out obj))
            {
                string cmdHashHandlerName3 = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Warning, string.Concat("Command [", cmdHashHandlerName3,
                    "] handler not found [netId=",
                    netId, "]"));
            }
            else
            {
                cmdDelegate(obj, reader);
            }
        }

        public void HandleRPC(int cmdHash, NetworkReader reader)
        {
            Type invokeClass;
            NetworkBehaviour.CmdDelegate cmdDelegate;
            NetworkBehaviour obj;
            if (gameObject == null)
            {
                string cmdHashHandlerName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Warning, string.Concat("ClientRpc [", cmdHashHandlerName,
                    "] received for deleted object [netId=", netId, "]"));
            }
            else if (!NetworkBehaviour.GetInvokerForHashClientRpc(cmdHash, out invokeClass, out cmdDelegate))
            {
                string cmdHashHandlerName2 = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Error,
                    string.Concat("Found no receiver for incoming [", cmdHashHandlerName2, "] on ", gameObject,
                        ",  the server and client should have the same NetworkBehaviour instances [netId=", netId,
                        "]."));
            }
            else if (!GetInvokeComponent(cmdHash, invokeClass, out obj))
            {
                string cmdHashHandlerName3 = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                Log.Print(LogType.Warning, string.Concat("ClientRpc [", cmdHashHandlerName3,
                    "] handler not found [netId=",
                    netId, "]"));
            }
            else
            {
                cmdDelegate(obj, reader);
            }
        }
        
        public void UNetUpdate()
        {
            uint num = 0;
            foreach (var netBehav in m_NetworkBehaviours)
            {
                var dirtyChannel = netBehav.GetDirtyChannel();
                if (dirtyChannel != -1)
                    num |= (uint) (1 << dirtyChannel);
            }
            if (num == 0U)
                return;
            for (var channelId = 0; channelId < NetworkServer.numChannels; ++channelId)
            {
                if (((int) num & 1 << channelId) == 0) continue;
                
                s_UpdateWriter.StartMessage(8);
                s_UpdateWriter.Write(netId);
                var flag = false;
                foreach (var netBehav in m_NetworkBehaviours)
                {
                    var position = s_UpdateWriter.Position;
                    var networkBehaviour = netBehav;
                    if (networkBehaviour.GetDirtyChannel() != channelId)
                    {
                        networkBehaviour.OnSerialize(s_UpdateWriter, false);
                    }
                    else
                    {
                        if (networkBehaviour.OnSerialize(s_UpdateWriter, false))
                        {
                            networkBehaviour.ClearAllDirtyBits();
                            flag = true;
                        }
                        if (s_UpdateWriter.Position - position > NetworkServer.maxPacketSize)
                            Log.Print(LogType.Warning,
                                $"Large state update of {(s_UpdateWriter.Position - position)} bytes for netId:{netId} from script:{networkBehaviour}");
                    }
                }
                if (flag)
                {
                    s_UpdateWriter.FinishMessage();
                    NetworkServer.SendWriterToReady(gameObject, s_UpdateWriter, channelId);
                }
            }
        }

        public void OnUpdateVars(NetworkReader reader, bool initialState)
        {
            if (initialState && m_NetworkBehaviours == null)
            {
                m_NetworkBehaviours = GetComponents<NetworkBehaviour>().ToArray();
            }
            
            foreach (var networkBehaviour in m_NetworkBehaviours)
            {
                if (EvoSGameConfig.DebugNetSerialize)
                    Log.Print(LogType.Debug,
                        $"Will deserialize {GetType().Name}.{networkBehaviour.GetType().Name} at {reader.Position}/{reader.Length}");
                networkBehaviour.OnDeserialize(reader, initialState);
            }

            if (EvoSGameConfig.DebugNetSerialize)
                Log.Print(LogType.Debug, $"Finished OnUpdateVars at {reader.Position}/{reader.Length}");
            if (reader.Position != reader.Length)
            {
                var remain = reader.ReadBytes((int) (reader.Length - reader.Position));

                Log.Print(LogType.Debug, $"    remain={BitConverter.ToString(remain).Replace("-", "")}");
            }
        }

        public void SetConnectionToServer(ClientConnection conn)
        {
            m_ConnectionToServer = conn;
        }

        public void SetConnectionToClient(ClientConnection conn, short newPlayerControllerId)
        {
            m_PlayerId = newPlayerControllerId;
            m_ConnectionToClient = conn;
        }

        public void OnNetworkDestroy()
        {
            var num = 0;
            while (m_NetworkBehaviours != null && num < m_NetworkBehaviours.Length)
            {
                NetworkBehaviour networkBehaviour = m_NetworkBehaviours[num];
                networkBehaviour.OnNetworkDestroy();
                num++;
            }
        }

        public void ClearObservers()
        {
            if (m_Observers == null)
            {
                return;
            }

            var count = m_Observers.Count;
            for (var i = 0; i < count; i++)
            {
                var clientConnection = m_Observers[i];
                clientConnection.RemoveFromVisList(this, true);
            }

            m_Observers.Clear();
            m_ObserverConnections.Clear();
        }

        public void AddObserver(ClientConnection conn)
        {
            if (m_Observers == null)
            {
                Log.Print(LogType.Error, "AddObserver for " + gameObject + " observer list is null");
            }
            else if (m_ObserverConnections.Contains(conn.connectionId))
            {
                Log.Print(LogType.Debug, string.Concat("Duplicate observer ", conn.address, " added for ", gameObject));
            }
            else
            {
                Log.Print(LogType.Debug, string.Concat("Added observer ", conn.address, " added for ", gameObject));

                m_Observers.Add(conn);
                m_ObserverConnections.Add(conn.connectionId);
                conn.AddToVisList(this);
            }
        }

        public void RemoveObserver(ClientConnection conn)
        {
            if (m_Observers != null)
            {
                m_Observers.Remove(conn);
                m_ObserverConnections.Remove(conn.connectionId);
                conn.RemoveFromVisList(this, false);
            }
        }

        public void RebuildObservers(bool initialize)
        {
            if (m_Observers != null)
            {
                var flag = false;
                var flag2 = false;
                var hashSet = new HashSet<ClientConnection>();
                var hashSet2 = new HashSet<ClientConnection>(m_Observers);
                foreach (var networkBehaviour in m_NetworkBehaviours)
                {
                    flag2 |= networkBehaviour.OnRebuildObservers(hashSet, initialize);
                }

                if (!flag2)
                {
                    if (!initialize)
                    {
                        return;
                    }

                    foreach (var conn in Server.connections)
                    {
                        if (conn == null)
                        {
                            continue;
                        }

                        if (conn.isReady)
                        {
                            AddObserver(conn);
                        }
                    }
                }
                else
                {
                    foreach (var conn in hashSet)
                    {
                        if (conn == null)
                        {
                            continue;
                        }

                        if (!conn.isReady)
                        {
                            Log.Print(LogType.Warning,
                                string.Concat("Observer is not ready for ", gameObject, " ", conn));
                        }
                        else if (initialize || !hashSet2.Contains(conn))
                        {
                            conn.AddToVisList(this);
                            Log.Print(LogType.Debug, string.Concat("New Observer for ", gameObject, " ", conn));

                            flag = true;
                        }
                    }

                    foreach (var conn in hashSet2)
                    {
                        if (hashSet.Contains(conn))
                        {
                            continue;
                        }

                        conn.RemoveFromVisList(this, false);
                        Log.Print(LogType.Debug, string.Concat("Removed Observer for ", gameObject, " ", conn));

                        flag = true;
                    }

                    if (flag)
                    {
                        m_Observers = new List<ClientConnection>(hashSet);
                        m_ObserverConnections.Clear();
                        foreach (var observer in m_Observers)
                        {
                            m_ObserverConnections.Add(observer.connectionId);
                        }
                    }
                }
            }
        }

        public void MarkForReset()
        {
            m_Reset = true;
        }

        public void Reset()
        {
            if (m_Reset)
            {
                m_Reset = false;
                m_NetId = NetworkInstanceId.Zero;
                m_ConnectionToServer = null;
                m_ConnectionToClient = null;
                m_PlayerId = -1;
                m_NetworkBehaviours = null;
                ClearObservers();
            }
        }

        [SerializeField] private NetworkSceneId m_SceneId;

        [SerializeField] private NetworkHash128 m_AssetId;

        [SerializeField] private bool m_ServerOnly;

        private NetworkInstanceId m_NetId;

        private ClientConnection m_ConnectionToServer;

        private ClientConnection m_ConnectionToClient;

        private short m_PlayerId = -1;

        private NetworkBehaviour[] m_NetworkBehaviours;

        private HashSet<int> m_ObserverConnections = new HashSet<int>();

        private List<ClientConnection> m_Observers = new List<ClientConnection>();

        private bool m_Reset;

        private NetworkWriter s_UpdateWriter = new NetworkWriter(); // TODO could be shared per NetworkServer

        public void OnStartServer()
        {
            CacheBehaviours();
            if (netId.IsEmpty())
                m_NetId = gameObject.GameManager.NetworkServer.GetNextNetworkId();

            foreach (var networkBehaviour in m_NetworkBehaviours)
            {
                try
                {
                    networkBehaviour.OnStartServer();
                }
                catch (Exception ex)
                {
                    Log.Print(LogType.Error, $"Exception in OnStartServer:{ex.Message} {ex.StackTrace}");
                }
            }
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            stream.AlignTo();
            m_SceneId = new NetworkSceneId(stream.ReadUInt32());
            m_AssetId = new NetworkHash128(new Hash128(assetFile, stream).Bytes);
            m_ServerOnly = stream.ReadBoolean();
            stream.AlignTo();
            LocalPlayerAuthority = stream.ReadBoolean();
            stream.AlignTo();
        }

        public override string ToString()
        {
            return $"{nameof(NetworkIdentity)}(" +
                   $"{nameof(netId)}: {netId}, " +
                   $"{nameof(m_SceneId)}: {m_SceneId}, " +
                   $"{nameof(m_AssetId)}: {m_AssetId}" +
                   ")";
        }
    }
}
