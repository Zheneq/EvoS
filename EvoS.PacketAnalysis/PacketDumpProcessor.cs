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
        private GameManager _game = new GameManager();
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
                    _rpcTypes.Add(cmd.Hash, type);
            }
        }

        public PacketDumpProcessor(PacketProvider packetProvider)
        {
            _packetProvider = packetProvider;

            InitFakeGame();
        }

        public void InitFakeGame()
        {
            _game.SetGameInfo(new LobbyGameInfo
            {
                GameConfig = new LobbyGameConfig
                {
//                    Map = "VR_Practice"
                    Map = "Skyway_Deathmatch"
                }
            });
            _game.SetTeamPlayerInfo(new List<LobbyPlayerInfo>
            {
                new LobbyPlayerInfo()
            });
            _game.LaunchGame(false);

            _game.SpawnObject<Board, Board>(_game.MapLoader, out _game.Board);
        }

        public void Process()
        {
            var i = 0;
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
                if (!_game.AssetsLoader.NetObjsByAssetId.TryGetValue(objSpawn.assetId, out serializedGameObject) &&
                    !_game.MapLoader.NetObjsByAssetId.TryGetValue(objSpawn.assetId, out serializedGameObject) &&
                    !_game.MiscLoader.NetObjsByAssetId.TryGetValue(objSpawn.assetId, out serializedGameObject))
                {
                    Log.Print(LogType.Error, $"Unknown asset in {objSpawn}");
                    return;
                }

                var gameObject = serializedGameObject.Instantiate();
                var netIdent = gameObject.GetComponent<NetworkIdentity>();
                netIdent.SetNetworkInstanceId(objSpawn.netId);
//                if (_game.Board != null)
                _game.RegisterObject(gameObject); // must register after setting network inst id

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
                if (!_game.AssetsLoader.NetworkScenes.TryGetValue(spawnScene.sceneId.Value, out serializedGameObject) &&
                    !_game.MapLoader.NetworkScenes.TryGetValue(spawnScene.sceneId.Value, out serializedGameObject) &&
                    !_game.MiscLoader.NetworkScenes.TryGetValue(spawnScene.sceneId.Value, out serializedGameObject))
                {
                    Log.Print(LogType.Error, $"Unknown scene in {spawnScene}");
                    return;
                }

                var gameObject = serializedGameObject.Instantiate();
                var netIdent = gameObject.GetComponent<NetworkIdentity>();
                netIdent.SetNetworkInstanceId(spawnScene.netId);
//                if (_game.Board != null)
                _game.RegisterObject(gameObject); // must register after setting network inst id

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
                    var gameSceneSingletons = _game.NetObjects[2];
                    _game.TheatricsManager = gameSceneSingletons.GetComponent<TheatricsManager>();
                    _game.AbilityModManager = gameSceneSingletons.GetComponent<AbilityModManager>();
                    _game.SharedEffectBarrierManager = _game.NetObjects[3].GetComponent<SharedEffectBarrierManager>();
                    _game.SharedActionBuffer = _game.NetObjects[4].GetComponent<SharedActionBuffer>();

                    var commonGameLogic = _game.NetObjects[5];
                    _game.InterfaceManager = commonGameLogic.GetComponent<InterfaceManager>();
                    _game.GameFlow = commonGameLogic.GetComponent<GameFlow>();
                    _game.ServerCombatManager = commonGameLogic.GetComponent<ServerCombatManager>();
                    _game.ServerEffectManager = commonGameLogic.GetComponent<ServerEffectManager>();
                    _game.TeamStatusDisplay = commonGameLogic.GetComponent<TeamStatusDisplay>();
                    _game.ServerActionBuffer = commonGameLogic.GetComponent<ServerActionBuffer>();
                    _game.TeamSelectData = commonGameLogic.GetComponent<TeamSelectData>();
                    _game.BarrierManager = commonGameLogic.GetComponent<BarrierManager>();

                    _game.BrushCoordinator = _game.NetObjects[6].GetComponent<BrushCoordinator>();
                    var sceneGameLogic = _game.NetObjects[7];
                    _game.GameFlowData = sceneGameLogic.GetComponent<GameFlowData>();
                    _game.GameplayData = sceneGameLogic.GetComponent<GameplayData>();
                    _game.SpoilsManager = sceneGameLogic.GetComponent<SpoilsManager>();
                    _game.ObjectivePoints = sceneGameLogic.GetComponent<ObjectivePoints>();
                    _game.SpawnPointManager = sceneGameLogic.GetComponent<SpawnPointManager>();
                    _game.MatchObjectiveKill = sceneGameLogic.GetComponent<MatchObjectiveKill>();

                    // wake up all objects
//                    foreach (var gameObj in _game.NetObjects.Values)
//                    {
//                        _game.RegisterObject(gameObj);
//                    }
                }
            }
            else if (packet.Message is ObjectCmdMessage cmdMessage)
            {
                _cmdTypes.TryGetValue(cmdMessage.Hash, out var cmdType);
                var cmd = (BaseCmd) Activator.CreateInstance(cmdType ?? typeof(UnknownCmd));
                if (cmd is UnknownCmd unknownCmd) unknownCmd.Hash = cmdMessage.Hash;
                cmd.NetId = cmdMessage.NetId;
                cmd.Deserialize(new NetworkReader(cmdMessage.Payload));
                packet.Message = cmd;
            }
            else if (packet.Message is ObjectRpcMessage rpcMessage)
            {
                _rpcTypes.TryGetValue(rpcMessage.Hash, out var rpcType);
                var rpc = (BaseRpc) Activator.CreateInstance(rpcType ?? typeof(UnknownRpc));
                if (rpc is UnknownRpc unknownRpc) unknownRpc.Hash = rpcMessage.Hash;
                rpc.NetId = rpcMessage.NetId;
                rpc.Deserialize(new NetworkReader(rpcMessage.Payload));
                packet.Message = rpc;
            }
            else if (packet.Message is ObjectUpdateMessage update)
            {
                if (!_game.NetObjects.TryGetValue(update.NetId.Value, out var gameObject))
                {
                    Log.Print(LogType.Error, $"Unknown net ident {update.NetId} referenced in update {update}!");
                    return;
                }

                if (packet.PacketNum == 777)
                {
                    Console.WriteLine(Utils.ToHex(update.Payload));
                    Console.WriteLine();
                }

                Patcher.Callbacks = packet.PacketInteraction = new PacketInteraction();
                var netIdent = gameObject.GetComponent<NetworkIdentity>();
                netIdent.OnUpdateVars(new NetworkReader(update.Payload), false);
                Patcher.Callbacks = new PacketInteraction();
            }
        }
    }
}
