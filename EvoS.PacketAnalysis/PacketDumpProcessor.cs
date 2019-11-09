using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network;
using EvoS.Framework.Network.Game.Messages;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using EvoS.Framework.Network.Unity.Messages;
using EvoS.PacketAnalysis.Cmd;
using EvoS.PacketAnalysis.Packets;
using EvoS.PacketAnalysis.Rpc;

namespace EvoS.PacketAnalysis
{
    public class PacketDumpProcessor
    {
        private PacketProvider _packetProvider;
        private UNetSerializer _serializer = new UNetSerializer();
        public GameManager Game = new GameManager();
        public ReadOnlyCollection<PacketInfo> Packets => _packetProvider.Packets;
        private static Dictionary<int, Type> _rpcTypes = new Dictionary<int, Type>();
        private static Dictionary<int, Type> _cmdTypes = new Dictionary<int, Type>();

        static PacketDumpProcessor()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                .Concat(Assembly.GetEntryAssembly().GetTypes()))
            {
                var rpc = type.GetCustomAttribute<RpcAttribute>();
                if (rpc != null)
                    _rpcTypes.Add(rpc.Hash, type);

                var cmd = type.GetCustomAttribute<CmdAttribute>();
                if (cmd != null)
                    _cmdTypes.Add(cmd.Hash, type);
            }
        }

        public PacketDumpProcessor(PacketProvider packetProvider)
        {
            _packetProvider = packetProvider;

            InitFakeGame();
        }

        public void InitFakeGame()
        {
            Game.SetGameInfo(new LobbyGameInfo
            {
                GameConfig = new LobbyGameConfig
                {
//                    Map = "VR_Practice"
                    Map = "Skyway_Deathmatch"
                }
            });
            Game.SetTeamPlayerInfo(new List<LobbyPlayerInfo>
            {
                new LobbyPlayerInfo()
            });
            Game.LaunchGame(false);

            Game.SpawnObject<Board, Board>(Game.MapLoader, out Game.Board);
        }

        public void Process()
        {
            foreach (var packet in _packetProvider.Packets)
            {
                packet.Deserialize(_serializer);

                if (packet.Message != null)
                {
                    try
                    {
                        SimulatePacket(packet);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        packet.Error = e;
                    }
                }
            }
        }

        private void SimulatePacket(PacketInfo packet)
        {
            EvoSGameConfig.NetworkIsServer = packet.Direction == PacketDirection.FromClient;
            EvoSGameConfig.NetworkIsClient = packet.Direction != PacketDirection.FromClient;

            if (!(packet.Message is AssetsLoadingProgress))
                Log.Print(LogType.Packet,
                    $"{packet.PacketNum} {packet.msgSeqNum} {packet.Direction}: {packet.Message}");

            if (packet.Message is ObjectSpawnMessage objSpawn)
            {
                SerializedGameObject serializedGameObject;
                if (!Game.AssetsLoader.NetObjsByAssetId.TryGetValue(objSpawn.assetId, out serializedGameObject) &&
                    !Game.MapLoader.NetObjsByAssetId.TryGetValue(objSpawn.assetId, out serializedGameObject) &&
                    !Game.MiscLoader.NetObjsByAssetId.TryGetValue(objSpawn.assetId, out serializedGameObject))
                {
                    Log.Print(LogType.Error, $"Unknown asset in {objSpawn}");
                    return;
                }

                Game.AssetsLoader.ClearCache();
                Game.MiscLoader.ClearCache();
                Game.MapLoader.ClearCache();

                var gameObject = serializedGameObject.Instantiate();
                var netIdent = gameObject.GetComponent<NetworkIdentity>();
                netIdent.SetNetworkInstanceId(objSpawn.netId);
                packet.NetId = netIdent.netId.Value;
                Game.RegisterObject(gameObject); // must register after setting network inst id

                if (objSpawn.payload != null)
                {
                    Patcher.Callbacks = packet.PacketInteraction = new PacketInteraction();
                    netIdent.OnUpdateVars(new NetworkReader(objSpawn.payload), true);

                    Patcher.Callbacks = new PacketInteraction();
                }
            }
            else if (packet.Message is ObjectSpawnSceneMessage spawnScene)
            {
                SerializedGameObject serializedGameObject;
                if (!Game.AssetsLoader.NetworkScenes.TryGetValue(spawnScene.sceneId.Value, out serializedGameObject) &&
                    !Game.MapLoader.NetworkScenes.TryGetValue(spawnScene.sceneId.Value, out serializedGameObject) &&
                    !Game.MiscLoader.NetworkScenes.TryGetValue(spawnScene.sceneId.Value, out serializedGameObject))
                {
                    Log.Print(LogType.Error, $"Unknown scene in {spawnScene}");
                    return;
                }

                Game.AssetsLoader.ClearCache();
                Game.MiscLoader.ClearCache();
                Game.MapLoader.ClearCache();

                var gameObject = serializedGameObject.Instantiate();
                var netIdent = gameObject.GetComponent<NetworkIdentity>();
                netIdent.SetNetworkInstanceId(spawnScene.netId);
                packet.NetId = netIdent.netId.Value;
                Game.RegisterObject(gameObject); // must register after setting network inst id

                if (spawnScene.payload != null)
                {
                    Patcher.Callbacks = packet.PacketInteraction = new PacketInteraction();
                    netIdent.OnUpdateVars(new NetworkReader(spawnScene.payload), true);

                    Patcher.Callbacks = new PacketInteraction();
                }
            }
            else if (packet.Message is ObjectSpawnFinishedMessage spawnFinishedMessage)
            {
                if (spawnFinishedMessage.state == 1)
                {
                    var gameSceneSingletons = Game.NetObjects[2];
                    Game.TheatricsManager = gameSceneSingletons.GetComponent<TheatricsManager>();
                    Game.AbilityModManager = gameSceneSingletons.GetComponent<AbilityModManager>();
                    Game.SharedEffectBarrierManager = Game.NetObjects[3].GetComponent<SharedEffectBarrierManager>();
                    Game.SharedActionBuffer = Game.NetObjects[4].GetComponent<SharedActionBuffer>();

                    var commonGameLogic = Game.NetObjects[5];
                    Game.InterfaceManager = commonGameLogic.GetComponent<InterfaceManager>();
                    Game.GameFlow = commonGameLogic.GetComponent<GameFlow>();
                    Game.ServerCombatManager = commonGameLogic.GetComponent<ServerCombatManager>();
                    Game.ServerEffectManager = commonGameLogic.GetComponent<ServerEffectManager>();
                    Game.TeamStatusDisplay = commonGameLogic.GetComponent<TeamStatusDisplay>();
                    Game.ServerActionBuffer = commonGameLogic.GetComponent<ServerActionBuffer>();
                    Game.TeamSelectData = commonGameLogic.GetComponent<TeamSelectData>();
                    Game.BarrierManager = commonGameLogic.GetComponent<BarrierManager>();

                    Game.BrushCoordinator = Game.NetObjects[6].GetComponent<BrushCoordinator>();
                    var sceneGameLogic = Game.NetObjects[7];
                    Game.GameFlowData = sceneGameLogic.GetComponent<GameFlowData>();
                    Game.GameplayData = sceneGameLogic.GetComponent<GameplayData>();
                    Game.SpoilsManager = sceneGameLogic.GetComponent<SpoilsManager>();
                    Game.ObjectivePoints = sceneGameLogic.GetComponent<ObjectivePoints>();
                    Game.SpawnPointManager = sceneGameLogic.GetComponent<SpawnPointManager>();
                    Game.MatchObjectiveKill = sceneGameLogic.GetComponent<MatchObjectiveKill>();
                }
            }
            else if (packet.Message is ObjectCmdMessage cmdMessage)
            {
                packet.NetId = cmdMessage.NetId.Value;

                _cmdTypes.TryGetValue(cmdMessage.Hash, out var cmdType);
                var cmd = (BaseCmd) Activator.CreateInstance(cmdType ?? typeof(UnknownCmd));
                if (cmd is UnknownCmd unknownCmd) unknownCmd.Hash = cmdMessage.Hash;
                cmd.NetId = cmdMessage.NetId;
                Game.NetObjects.TryGetValue(cmdMessage.NetId.Value, out var gameObject);
                cmd.Deserialize(new NetworkReader(cmdMessage.Payload), gameObject);
                packet.Message = cmd;
            }
            else if (packet.Message is ObjectRpcMessage rpcMessage)
            {
                packet.NetId = rpcMessage.NetId.Value;

                _rpcTypes.TryGetValue(rpcMessage.Hash, out var rpcType);
                var rpc = (BaseRpc) Activator.CreateInstance(rpcType ?? typeof(UnknownRpc));
                if (rpc is UnknownRpc unknownRpc) unknownRpc.Hash = rpcMessage.Hash;
                rpc.NetId = rpcMessage.NetId;
                Game.NetObjects.TryGetValue(rpcMessage.NetId.Value, out var gameObject);
                rpc.Deserialize(new NetworkReader(rpcMessage.Payload), gameObject);
                packet.Message = rpc;
            }
            else if (packet.Message is ObjectUpdateMessage update)
            {
                packet.NetId = update.NetId.Value;

                if (!Game.NetObjects.TryGetValue(update.NetId.Value, out var gameObject))
                {
                    Log.Print(LogType.Error, $"Unknown net ident {update.NetId} referenced in update {update}!");
                    return;
                }

                Patcher.Callbacks = packet.PacketInteraction = new PacketInteraction();
                var netIdent = gameObject.GetComponent<NetworkIdentity>();
                netIdent.OnUpdateVars(new NetworkReader(update.Payload), false);
                Patcher.Callbacks = new PacketInteraction();
            }
            else if (packet.Message is SyncListMessage syncList)
            {
                packet.NetId = syncList.NetId.Value;

                var reader = new NetworkReader(syncList.Payload);

                var opMessage = new SyncListOperation();
                opMessage.Hash = syncList.Hash;
                opMessage.NetId = syncList.NetId.Value;
                opMessage.Operation = (SyncList<int>.Operation) reader.ReadByte();
                opMessage.Index = (int) reader.ReadPackedUInt32();

                if (!Patcher.SyncListLookup.TryGetValue(syncList.Hash, out var syncListField)) return;
                opMessage.SyncListField = syncListField;

                // if there are any SyncList implementations with DeserializeItem methods that make use
                // of meaningful instance state, this won't work
                var syncListInstance = Activator.CreateInstance(syncListField.FieldType);
                var deserializeMethod = syncListField.FieldType.GetMethod("DeserializeItem");

                var obj = deserializeMethod.Invoke(syncListInstance, new object[] {reader});
                opMessage.Value = obj;
                packet.Message = opMessage;
            }
        }
    }
}
