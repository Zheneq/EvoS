using System;
using System.Collections.Generic;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Unity
{
    public class Component : ISerializedItem
    {
        [JsonIgnore] public GameObject gameObject;
        public Transform transform;
        public bool enabled = true;

        public Component GetComponent(Type type)
        {
            return gameObject.GetComponent(type);
        }

        public T GetComponent<T>() where T : Component
        {
            return gameObject.GetComponent<T>();
        }

        public IEnumerable<T> GetComponents<T>() where T : Component
        {
            return gameObject.GetComponents<T>();
        }

        public virtual void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            throw new NotImplementedException();
        }

        // [JsonIgnore] public GameManager GameManager => gameObject.GameManager;
        // [JsonIgnore] public Board Board => gameObject.GameManager.Board;
        // [JsonIgnore] public BrushCoordinator BrushCoordinator => gameObject.GameManager.BrushCoordinator;
        // [JsonIgnore] public NetworkServer NetworkServer => gameObject.GameManager.NetworkServer;
        // [JsonIgnore] public GameFlow GameFlow => gameObject.GameManager.GameFlow;
        // [JsonIgnore] public GameFlowData GameFlowData => gameObject.GameManager.GameFlowData;
        // [JsonIgnore] public GameEventManager GameEventManager => gameObject.GameManager.GameEventManager;
        // [JsonIgnore] public MatchLogger MatchLogger => gameObject.GameManager.MatchLogger;
        // [JsonIgnore] public CollectTheCoins CollectTheCoins => gameObject.GameManager.CollectTheCoins;
        // [JsonIgnore] public GameplayData GameplayData => gameObject.GameManager.GameplayData;
        // [JsonIgnore] public GameplayMutators GameplayMutators => gameObject.GameManager.GameplayMutators;

        public string name
        {
            get => gameObject.Name;
            set => gameObject.Name = value;
        }
    }
}
