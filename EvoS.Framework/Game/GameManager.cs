using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EvoS.Framework.Assets;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Game;
using EvoS.Framework.Network.Game.Messages;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using EvoS.Framework.Network.Unity.Messages;
using Newtonsoft.Json;

namespace EvoS.Framework.Game
{
    public class GameManager
    {
        public readonly NetworkServer NetworkServer = new NetworkServer();
        public readonly Dictionary<uint, GameObject> NetObjects = new Dictionary<uint, GameObject>();
        private readonly List<GameObject> _gameObjects = new List<GameObject>();
        public AbilityModManager AbilityModManager;
        public BarrierManager BarrierManager;
        public Board Board;
        public BrushCoordinator BrushCoordinator;
        public CollectTheCoins CollectTheCoins;
        public GameEventManager GameEventManager = new GameEventManager();
        public GameFlowData GameFlowData;
        public GameFlow GameFlow;
        public GameplayData GameplayData;
        public GameplayMutators GameplayMutators = new GameplayMutators();
        public InterfaceManager InterfaceManager;
        public MatchLogger MatchLogger;
        public MatchObjectiveKill MatchObjectiveKill;
        public ObjectivePoints ObjectivePoints;
        public ServerActionBuffer ServerActionBuffer;
        public ServerCombatManager ServerCombatManager;
        public ServerEffectManager ServerEffectManager;
        public SharedActionBuffer SharedActionBuffer;
        public SharedEffectBarrierManager SharedEffectBarrierManager;
        public SpawnPointManager SpawnPointManager;
        public SpoilsManager SpoilsManager;
        public TeamSelectData TeamSelectData;
        public TeamStatusDisplay TeamStatusDisplay;
        public TheatricsManager TheatricsManager;

        private bool s_quitting;
        private GameStatus m_gameStatus;
        private LobbyGameplayOverrides m_gameplayOverrides;
        private LobbyGameplayOverrides m_gameplayOverridesForCurrentGame;
        public Dictionary<int, ForbiddenDevKnowledge> ForbiddenDevKnowledge;
        private Dictionary<int, GamePlayer> _players = new Dictionary<int, GamePlayer>();

        public event Action OnGameAssembling = () => { };

        public event Action OnGameSelecting = () => { };

        public event Action OnGameLoadoutSelecting = () => { };

        public event Action<GameType> OnGameLaunched = delegate { };

        public event Action OnGameLoaded = () => { };

        public event Action OnGameStarted = () => { };

        public event Action<GameResult> OnGameStopped = delegate { };

        public event Action<GameStatus> OnGameStatusChanged = delegate { };

        public LobbyGameInfo GameInfo { get; private set; }

        public LobbyTeamInfo TeamInfo { get; private set; }

        [JsonIgnore] public LobbyGameConfig GameConfig => GameInfo.GameConfig;

        public LobbyGameplayOverrides GameplayOverrides
        {
            get
            {
                if (m_gameplayOverridesForCurrentGame != null)
                    return m_gameplayOverridesForCurrentGame;
                return m_gameplayOverrides;
            }
        }

        public LobbyMatchmakingQueueInfo QueueInfo { get; private set; }

        public List<LobbyPlayerInfo> TeamPlayerInfo { get; private set; }

        public LobbyGameSummary GameSummary { get; private set; }

        public LobbyGameSummaryOverrides GameSummaryOverrides { get; private set; }

        public bool EnableHiddenGameItems { get; set; }

        public GameStatus GameStatus => m_gameStatus;

        public float GameStatusTime { get; private set; }

        public static bool IsEditorAndNotGame() => false;
        public AssetLoader MapLoader;
        public AssetLoader AssetsLoader;
        public AssetLoader MiscLoader;

        public GameManager()
        {
            var dummyObject = new GameObject("Fake");
            dummyObject.transform = new Transform();
            dummyObject.AddComponent(GameplayMutators);
            RegisterObject(dummyObject);
        }

        public void Reset()
        {
            GameInfo = new LobbyGameInfo();
            TeamInfo = new LobbyTeamInfo();
            m_gameplayOverrides = new LobbyGameplayOverrides();
            m_gameStatus = GameStatus.Stopped;
            QueueInfo = null;
            ForbiddenDevKnowledge = null;
//            if (!((UnityEngine.Object) GameWideData.Get() != (UnityEngine.Object) null))
//                return;
//            this.GameplayOverrides.SetBaseCharacterConfigs(GameWideData.Get());
        }

        public void SetGameStatus(GameStatus gameStatus, GameResult gameResult = GameResult.NoResult,
            bool notify = true)
        {
            if (gameStatus == m_gameStatus)
                return;
            m_gameStatus = gameStatus;
//            this.GameStatusTime = Time.unscaledTime; // TODO
            if (s_quitting || !notify)
                return;
            if (!GameInfo.GameServerProcessCode.IsNullOrEmpty() && GameInfo.GameConfig != null)
            {
                Log.Print(LogType.Game,
                    gameResult == GameResult.NoResult
                        ? $"Game {GameInfo.Name} is {gameStatus.ToString().ToLower()}"
                        : $"Game {GameInfo.Name} is {gameStatus.ToString().ToLower()} with result {gameResult.ToString()}");
            }

            switch (gameStatus)
            {
                case GameStatus.Assembling:
                    OnGameAssembling();
                    break;
                case GameStatus.FreelancerSelecting:
                    OnGameSelecting();
                    break;
                case GameStatus.LoadoutSelecting:
                    OnGameLoadoutSelecting();
                    break;
                case GameStatus.Launched:
                    OnGameLaunched(GameInfo.GameConfig.GameType);
                    break;
                case GameStatus.Loaded:
                    OnGameLoaded();
                    break;
                case GameStatus.Started:
                    OnGameStarted();
                    break;
                case GameStatus.Stopped:
                    OnGameStopped(gameResult);
                    break;
            }

            OnGameStatusChanged(gameStatus);
        }

        public void SetGameInfo(LobbyGameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }

        public void SetQueueInfo(LobbyMatchmakingQueueInfo queueInfo)
        {
            QueueInfo = queueInfo;
        }

        public void SetTeamInfo(LobbyTeamInfo teamInfo)
        {
            TeamInfo = teamInfo;
        }

        public void SetTeamPlayerInfo(List<LobbyPlayerInfo> teamPlayerInfo)
        {
            TeamPlayerInfo = teamPlayerInfo;
        }

        public void SetGameSummary(LobbyGameSummary gameSummary)
        {
            GameSummary = gameSummary;
        }

        public void SetGameSummaryOverrides(LobbyGameSummaryOverrides gameSummaryOverrides)
        {
            GameSummaryOverrides = gameSummaryOverrides;
        }

        public void StopGame(GameResult gameResult = GameResult.NoResult)
        {
            SetGameStatus(GameStatus.Stopped, gameResult);
//            GameTime.scale = 1f;
        }

        public bool IsGameLoading()
        {
            bool flag = false;
            if (GameInfo != null && GameInfo.GameConfig != null &&
                GameInfo.GameStatus != GameStatus.Stopped)
            {
                if (GameInfo.GameConfig.GameType != GameType.Custom)
                {
                    if (GameInfo.GameStatus >= GameStatus.Assembling)
                        flag = true;
                }
                else if (GameInfo.GameStatus.IsPostLaunchStatus())
                    flag = true;
            }

            return flag;
        }

        public static bool IsGameTypeValidForGGPack(GameType gameType)
        {
            if (gameType != GameType.Tutorial && gameType != GameType.Practice)
                return gameType != GameType.Custom;
            return false;
        }

        public void AddPlayer(ClientConnection connection, LoginRequest loginReq, AddPlayerMessage msg)
        {
            _players.Add(loginReq.PlayerId, new GamePlayer(connection, loginReq, msg));
            connection.ActiveGame = this;
            
            var gfPlayer = GameFlow.GetPlayerFromConnectionId(connection.connectionId);
            gfPlayer.m_id = (byte) loginReq.PlayerId;
            gfPlayer.m_valid = true;
            gfPlayer.m_accountId = long.Parse(loginReq.AccountId);
            gfPlayer.m_connectionId = connection.connectionId;
            GameFlow.playerDetails[gfPlayer] = new PlayerDetails(PlayerGameAccountType.Human)
            {
                m_team =  Team.TeamA,
                m_handle = "test handle",
                m_accountId = gfPlayer.m_accountId,
                m_lobbyPlayerInfoId = 0
            };

            // This isn't actually correct, but the client logs a warning with what it expected and continues
            connection.Send(14, new CRCMessage
            {
                scripts = new[]
                {
                    new CRCMessageEntry("ActorData", 0),
                    new CRCMessageEntry("BrushCoordinator", 0),
                    new CRCMessageEntry("ActorController", 0),
                    new CRCMessageEntry("AbilityData", 0),
                    new CRCMessageEntry("ActorStats", 0),
                    new CRCMessageEntry("ActorStatus", 0),
                    new CRCMessageEntry("ActorBehavior", 0),
                    new CRCMessageEntry("PlayerData", 0),
                    new CRCMessageEntry("PowerUp", 0),
                    new CRCMessageEntry("GameFlow", 0),
                    new CRCMessageEntry("TeamStatusDisplay", 0),
                    new CRCMessageEntry("BarrierManager", 0),
                    new CRCMessageEntry("GameFlowData", 0),
                    new CRCMessageEntry("ObjectivePoints", 0),
                    new CRCMessageEntry("CoinCarnageManager", 0),
                    new CRCMessageEntry("ActorTeamSensitiveData", 0),
                    new CRCMessageEntry("ActorAdditionalVisionProviders", 0),
                    new CRCMessageEntry("ActorCinematicRequests", 0),
                    new CRCMessageEntry("FreelancerStats", 0),
                    new CRCMessageEntry("Manta_SyncComponent", 0),
                    new CRCMessageEntry("Rampart_SyncComponent", 0),
                    new CRCMessageEntry("SinglePlayerManager", 0)
                }
            });

            connection.RegisterHandler<AssetsLoadingProgress>(61, _players[loginReq.PlayerId], OnAssetLoadingProgress);
            connection.RegisterHandler<AssetsLoadedNotification>(53, _players[loginReq.PlayerId],
                OnAssetsLoadedNotification);
        }

        private void OnAssetLoadingProgress(GamePlayer player, AssetsLoadingProgress msg)
        {
            // TODO should send to all
            player.Connection.Send(62, msg);
        }

        private void OnAssetsLoadedNotification(GamePlayer player, AssetsLoadedNotification msg)
        {   
            player.Connection.Send(56, new ReconnectReplayStatus {WithinReconnectReplay = true});
            player.Connection.Send(54, new SpawningObjectsNotification
            {
                PlayerId = player.LoginRequest.PlayerId,
                SpawnableObjectCount = NetObjects.Count
            });
            
            // ObjectSpawnFinishedMessage 0 instructs the client to hold off on calling OnStartClient
            // until we send ObjectSpawnFinishedMessage 1
            player.Connection.Send(12, new ObjectSpawnFinishedMessage {state = 0});

            foreach (var netObj in NetObjects.Values)
            {
                var netIdent = netObj.GetComponent<NetworkIdentity>();
                netIdent.AddObserver(player.Connection);
            }

            player.Connection.Send(56, new ReconnectReplayStatus {WithinReconnectReplay = false});
            player.Connection.Send(12, new ObjectSpawnFinishedMessage {state = 1});
            
            // Should wait for all players to have reached this point

            GameFlowData.gameState = GameState.SpawningPlayers;
            foreach (var netObj in NetObjects.Values)
            {
                var netIdent = netObj.GetComponent<NetworkIdentity>();
                netIdent.UNetUpdate();
            }
            foreach (var playerInfo in TeamPlayerInfo)
            {
                SpawnPlayerCharacter(playerInfo);
                // actors get synclist updates for currentCardIds and modifiedStats
            }

            // check for owning player
            foreach (var actor in GameFlowData.GetAllActorsForPlayer(0))
            {
                player.Connection.Send(4, new OwnerMessage
                {
                    netId = actor.netId,
                    playerControllerId = 0 // ?
                });
            }

            // The following should be sent after all players have loaded
            foreach (var netObj in NetObjects.Values)
            {
                var atsd = netObj.GetComponent<ActorTeamSensitiveData>();
                if (atsd == null) continue;

                // Just send the play to an arbitrary location for now
                atsd.CallRpcMovement(GameEventManager.EventType.Invalid,
                    new GridPosProp(5, 5, 6), new GridPosProp(5, 5, 5),
                    null, ActorData.MovementType.Teleport, false, false);
            }

            GameFlowData.gameState = GameState.StartingGame;
            UpdateAllNetObjs();
            
            GameFlowData.gameState = GameState.Deployment;
            UpdateAllNetObjs();
            
            GameFlowData.gameState = GameState.BothTeams_Decision;
            GameFlowData.Networkm_currentTurn = 1;
            GameFlowData.Networkm_willEnterTimebankMode = true;
            GameFlowData.Networkm_timeRemainingInDecisionOverflow = 5;
            UpdateAllNetObjs();

            GameFlow.CallRpcSetMatchTime(0);
            // kRpcRpcApplyAbilityModById
            foreach (var actor in GameFlowData.GetActors())
            {
                var turnSm = actor.gameObject.GetComponent<ActorTurnSM>();
                turnSm.CallRpcTurnMessage(TurnMessage.TURN_START, 0);
            }
            BarrierManager.CallRpcUpdateBarriers();
            GameFlowData.CallRpcUpdateTimeRemaining(21);
        }

        public void UpdateAllNetObjs()
        {
            foreach (var netObj in NetObjects.Values)
            {
                var netIdent = netObj.GetComponent<NetworkIdentity>();
                netIdent.UNetUpdate();
            }
        }

//        public class ObserverMessage : MessageBase
//        {
//            public Replay.Message Message;
//
//            public override void Serialize(NetworkWriter writer)
//            {
//                GeneratedNetworkCode._WriteMessage_Replay(writer, this.Message);
//            }
//
//            public override void Deserialize(NetworkReader reader)
//            {
//                this.Message = GeneratedNetworkCode._ReadMessage_Replay(reader);
//            }
//        }

        public void LaunchGame()
        {
            MapLoader = new AssetLoader();
            MapLoader.LoadAssetBundle("Bundles/scenes/maps.bundle");
            MapLoader.LoadAsset(
                $"archive:/buildplayer-robotfactory_opu_gamemode/buildplayer-{GameConfig.Map.ToLower()}");
            MapLoader.ConstructCaches();

            AssetsLoader = new AssetLoader();
            AssetsLoader.LoadAsset("resources.assets");
            AssetsLoader.ConstructCaches();

            MiscLoader = new AssetLoader();
            MiscLoader.LoadAssetBundle("Bundles/scenes/frontend.bundle");
            MiscLoader.LoadAsset("archive:/buildplayer-options_ui/buildplayer-clientenvironmentsingletons");
            MiscLoader.ConstructCaches();

            SpawnObject(MiscLoader, "ApplicationSingletonsNetId", out _);
            SpawnObject(MiscLoader, "GameSceneSingletons", out var gameSceneSingletons);
            TheatricsManager = gameSceneSingletons.GetComponent<TheatricsManager>();
            AbilityModManager = gameSceneSingletons.GetComponent<AbilityModManager>();
            SpawnObject(MiscLoader, "SharedEffectBarrierManager", out SharedEffectBarrierManager);
            SpawnObject(MiscLoader, "SharedActionBuffer", out SharedActionBuffer);
            SharedActionBuffer.Networkm_actionPhase = ActionBufferPhase.Done;

            SpawnScene(MapLoader, 1, out var commonGameLogic);
            InterfaceManager = commonGameLogic.GetComponent<InterfaceManager>();
            GameFlow = commonGameLogic.GetComponent<GameFlow>();
            MatchLogger = commonGameLogic.GetComponent<MatchLogger>();
            ServerCombatManager = commonGameLogic.GetComponent<ServerCombatManager>();
            ServerEffectManager = commonGameLogic.GetComponent<ServerEffectManager>();
            TeamStatusDisplay = commonGameLogic.GetComponent<TeamStatusDisplay>();
            ServerActionBuffer = commonGameLogic.GetComponent<ServerActionBuffer>();
            TeamSelectData = commonGameLogic.GetComponent<TeamSelectData>();
            BarrierManager = commonGameLogic.GetComponent<BarrierManager>();

            SpawnObject<Board, Board>(MapLoader, out Board);

            SpawnScene(MapLoader, 2, out BrushCoordinator);
            SpawnScene(MapLoader, 3, out var sceneGameLogic);
            GameFlowData = sceneGameLogic.GetComponent<GameFlowData>();
            GameplayData = sceneGameLogic.GetComponent<GameplayData>();
            SpoilsManager = sceneGameLogic.GetComponent<SpoilsManager>();
            ObjectivePoints = sceneGameLogic.GetComponent<ObjectivePoints>();
            SpawnPointManager = sceneGameLogic.GetComponent<SpawnPointManager>();
            MatchObjectiveKill = sceneGameLogic.GetComponent<MatchObjectiveKill>();

            DumpNetObjects();
        }

        public void DumpNetObjects()
        {
            foreach (var (k, v) in NetObjects)
            {
                Console.WriteLine($"{k}: {v}");
            }
        }

        private void SpawnPlayerCharacter(LobbyPlayerInfo playerInfo)
        {
            // TODO would normally check playerInfo.CharacterInfo.CharacterType

            SpawnObject<ActorTeamSensitiveData>(MiscLoader, "ActorTeamSensitiveData_Friendly",
                out var scoundrelFriendly);
            SpawnObject(AssetsLoader, "Scoundrel", out var scoundrel);
            var scoundrelActor = scoundrel.GetComponent<ActorData>();
            var scoundrelPlayerData = scoundrel.GetComponent<PlayerData>();
            scoundrelActor.SetClientFriendlyTeamSensitiveData(scoundrelFriendly);
            scoundrelPlayerData.m_player = GameFlow.GetPlayerFromConnectionId(1); // TODO hardcoded connection id
            scoundrelPlayerData.PlayerIndex = 0;

            scoundrelActor.ServerLastKnownPosSquare = Board.GetBoardSquare(5, 5);
            scoundrelActor.InitialMoveStartSquare = Board.GetBoardSquare(5, 5);
            scoundrelActor.UpdateDisplayName("Foo bar player");
            scoundrelActor.ActorIndex = 0;
            scoundrelActor.PlayerIndex = 0;
            scoundrelFriendly.SetActorIndex(scoundrelActor.ActorIndex);
            scoundrelActor.SetTeam(Team.TeamA);

            GameFlowData.AddPlayer(scoundrel);

            var netChar = scoundrel.GetComponent<NetworkIdentity>();
            var netAtsd = scoundrelFriendly.GetComponent<NetworkIdentity>();
            foreach (var player in _players.Values)
            {
                netChar.AddObserver(player.Connection);
                netAtsd.AddObserver(player.Connection);
            }
        }

        public void RegisterObject(GameObject gameObj)
        {
            if (gameObj.GameManager == this)
                return;
            if (gameObj.GameManager != null)
                throw new InvalidOperationException($"Object registered with another GameManager! {gameObj}");

            gameObj.GameManager = this;
            _gameObjects.Add(gameObj);

            foreach (var component in gameObj.GetComponents<MonoBehaviour>().ToList())
            {
                component.Awake();
            }

            // recursively register children and parent
            foreach (var child in gameObj.transform.children)
            {
                RegisterObject(child.gameObject);
            }

            if (gameObj.transform.father?.gameObject != null)
                RegisterObject(gameObj.transform.father.gameObject);

            var netIdent = gameObj.GetComponent<NetworkIdentity>();
            if (netIdent != null)
            {
                netIdent.OnStartServer();
                NetObjects.Add(netIdent.netId.Value, gameObj);
            }
        }

        public void SpawnObject<T, TR>(AssetLoader loader, out TR component)
            where T : MonoBehaviour where TR : Component
        {
            SpawnObject<T>(loader, out var obj, false);
            component = obj.GetComponent<TR>();
            RegisterObject(obj);
        }

        public void SpawnObject<T>(AssetLoader loader, out GameObject obj, bool register = true) where T : MonoBehaviour
        {
            loader.ClearCache();
            obj = loader.GetObjectByComponent<T>().Instantiate();
            if (register) RegisterObject(obj);
        }

        public void SpawnObject<T>(AssetLoader loader, string name, out T component) where T : Component
        {
            SpawnObject(loader, name, out var obj, false);
            component = obj.GetComponent<T>();
            RegisterObject(obj);
        }

        public void SpawnObject(AssetLoader loader, string name, out GameObject obj, bool register = true)
        {
            loader.ClearCache();
            obj = loader.NetObjsByName[name].Instantiate();
            if (register) RegisterObject(obj);
        }

        public void SpawnScene(AssetLoader loader, uint sceneId, out GameObject scene, bool register = true)
        {
            foreach (var o in NetObjects.Where(o => o.Value.GetComponent<NetworkIdentity>().sceneId.Value == sceneId))
            {
                scene = o.Value;
                return;
            }

            loader.ClearCache();
            scene = loader.NetworkScenes[sceneId].Instantiate();
            if (register) RegisterObject(scene);
        }

        public void SpawnScene<T>(AssetLoader loader, uint sceneId, out T component) where T : Component
        {
            foreach (var o in NetObjects.Where(o => o.Value.GetComponent<NetworkIdentity>().sceneId.Value == sceneId))
            {
                component = o.Value.GetComponent<T>();
                return;
            }

            SpawnScene(loader, sceneId, out var scene, false);
            component = scene.GetComponent<T>();
            RegisterObject(scene);
        }
    }
}
