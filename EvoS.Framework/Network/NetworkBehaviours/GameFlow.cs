using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [Serializable]
    [SerializedMonoBehaviour("GameFlow")]
    public class GameFlow : NetworkBehaviour
    {
        private static int kRpcRpcDisplayConsoleText = -789469928;
        private static int kRpcRpcSetMatchTime = -559523706;

        private Dictionary<Player, PlayerDetails> m_playerDetails =
            new Dictionary<Player, PlayerDetails>(new PlayerComparer());

        private Timer gameTickTimer;
        private AutoResetEvent gameTickTimerEvent;

        internal Dictionary<Player, PlayerDetails> playerDetails => m_playerDetails;

        static GameFlow()
        {
            RegisterRpcDelegate(typeof (GameFlow), kRpcRpcDisplayConsoleText, InvokeRpcRpcDisplayConsoleText);
            RegisterRpcDelegate(typeof (GameFlow), kRpcRpcSetMatchTime, InvokeRpcRpcSetMatchTime);
        }

        public GameFlow()
        {
        }

        public GameFlow(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public void StartGame()
        {
            gameTickTimerEvent = new AutoResetEvent(false);
            gameTickTimer = new Timer(this.TickGame, gameTickTimerEvent, 0, 1000);
            CallRpcSetMatchTime(0);
        }

        private void TickGame(Object autoEvent)
        {
            GameFlowData.UpdateTimeRemaining();
        }

        public void StopGame()
        {
            gameTickTimer.Dispose();
            gameTickTimer = null;
            gameTickTimerEvent = null;
        }

        public Player GetPlayerFromConnectionId(int connectionId)
        {
            foreach (var (key, _) in m_playerDetails)
            {
                if (key.m_connectionId == connectionId)
                    return key;
            }

            return new Player();
        }

        public string GetPlayerHandleFromConnectionId(int connectionId)
        {
            foreach (var (key, value) in m_playerDetails)
            {
                if (key.m_connectionId == connectionId)
                {
                    return value.m_handle;
                }
            }

            return string.Empty;
        }

        public string GetPlayerHandleFromAccountId(long accountId)
        {
            foreach (var (key, value) in m_playerDetails)
            {
                if (key.m_accountId == accountId)
                {
                    return value.m_handle;
                }
            }

            return string.Empty;
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (!initialState)
                writer.WritePackedUInt32(syncVarDirtyBits);
            if (!initialState && syncVarDirtyBits == 0U)
                return false;
            var networkWriterAdapter = new NetworkWriterAdapter(writer);
            var count = m_playerDetails.Count;
            networkWriterAdapter.Serialize(ref count);
            if (count < 0 || count > 20)
            {
                Log.Print(LogType.Error, "Invalid number of players: " + count);
                Mathf.Clamp(count, 0, 20);
            }

            foreach (var playerDetail in m_playerDetails)
            {
                var key = playerDetail.Key;
                var playerDetails = playerDetail.Value;
                if (playerDetails != null)
                {
                    key.OnSerializeHelper(networkWriterAdapter);
                    playerDetails.OnSerializeHelper(networkWriterAdapter);
                }
            }

            return true;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            var num1 = uint.MaxValue;
            if (!initialState)
                num1 = reader.ReadPackedUInt32();
            if (num1 == 0U)
                return;
            var networkReaderAdapter = new NetworkReaderAdapter(reader);
            int num2 = m_playerDetails.Count;
            networkReaderAdapter.Serialize(ref num2);
            if (num2 < 0 || num2 > 20)
            {
                Log.Print(LogType.Error, "Invalid number of players: " + num2);
                num2 = Mathf.Clamp(num2, 0, 20);
            }

            m_playerDetails.Clear();
            for (int index1 = 0; index1 < num2; ++index1)
            {
                var index2 = new Player();
                var playerDetails = new PlayerDetails(PlayerGameAccountType.None);
                index2.OnSerializeHelper(networkReaderAdapter);
                playerDetails.OnSerializeHelper(networkReaderAdapter);
                index2.m_accountId = playerDetails.m_accountId;
                m_playerDetails[index2] = playerDetails;
//                if ((bool) (GameFlowData.Get()) && GameFlowData.Get().LocalPlayerData == null)
//                    GameFlowData.Get().SetLocalPlayerData();
            }
        }

        protected static void InvokeRpcRpcDisplayConsoleText(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcDisplayConsoleText called on server.");
//            else
//                ((GameFlow) obj).RpcDisplayConsoleText(GeneratedNetworkCode._ReadDisplayConsoleTextMessage_None(reader));
        }

        protected static void InvokeRpcRpcSetMatchTime(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcSetMatchTime called on server.");
//            else
//                ((GameFlow) obj).RpcSetMatchTime(reader.ReadSingle());
        }

        public void CallRpcDisplayConsoleText(DisplayConsoleTextMessage message)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcDisplayConsoleText called on client.");
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcDisplayConsoleText);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                GeneratedNetworkCode._WriteDisplayConsoleTextMessage_None(writer, message);
                SendRPCInternal(writer, 0, "RpcDisplayConsoleText");
            }
        }

        public void CallRpcSetMatchTime(float timeSinceMatchStart)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcSetMatchTime called on client.");
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcSetMatchTime);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(timeSinceMatchStart);
                SendRPCInternal(writer, 0, "RpcSetMatchTime");
            }
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
        }

        public override string ToString()
        {
            return $"{nameof(GameFlow)}(" +
                   ")";
        }

        public class SetHumanInfoMessage : MessageBase
        {
            public string m_userName;
            public string m_buildVersion;
            public string m_accountIdString;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(m_userName);
                writer.Write(m_buildVersion);
                writer.Write(m_accountIdString);
            }

            public override void Deserialize(NetworkReader reader)
            {
                m_userName = reader.ReadString();
                m_buildVersion = reader.ReadString();
                m_accountIdString = reader.ReadString();
            }
        }

        public class SelectCharacterMessage : MessageBase
        {
            public string m_characterName;
            public int m_skinIndex;
            public int m_patternIndex;
            public int m_colorIndex;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(m_characterName);
                writer.WritePackedUInt32((uint) m_skinIndex);
                writer.WritePackedUInt32((uint) m_patternIndex);
                writer.WritePackedUInt32((uint) m_colorIndex);
            }

            public override void Deserialize(NetworkReader reader)
            {
                m_characterName = reader.ReadString();
                m_skinIndex = (int) reader.ReadPackedUInt32();
                m_patternIndex = (int) reader.ReadPackedUInt32();
                m_colorIndex = (int) reader.ReadPackedUInt32();
            }
        }

        public class SetTeamFinalizedMessage : MessageBase
        {
            public int m_team;

            public override void Serialize(NetworkWriter writer)
            {
                writer.WritePackedUInt32((uint) m_team);
            }

            public override void Deserialize(NetworkReader reader)
            {
                m_team = (int) reader.ReadPackedUInt32();
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 1)]
        public struct PlayerComparer : IEqualityComparer<Player>
        {
            public bool Equals(Player x, Player y)
            {
                return x == y;
            }

            public int GetHashCode(Player obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
