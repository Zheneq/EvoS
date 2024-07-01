using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Unity
{
    public class GameObject
    {
        public string Name { get; set; }
        private readonly List<Component> _components = new List<Component>();
        // private GameManager _gameManager;

        [JsonIgnore] public Transform transform;

        // [JsonIgnore]
        // public GameManager GameManager
        // {
        //     get => _gameManager;
        //     set
        //     {
        //         if (_gameManager != null)
        //         {
        //             throw new ApplicationException($"GameObject {this} already registered!");
        //         }
        //
        //         _gameManager = value;
        //     }
        // }

        public GameObject() : this(null)
        {
        }

        public GameObject(string name, params Type[] components)
        {
            Name = name;
            foreach (Type componentType in components)
            {
                AddComponent(componentType);
            }
        }

        public IEnumerable<T> GetComponents<T>() where T : Component
        {
            foreach (var component in _components)
            {
                if (component.GetType().IsSubclassOf(typeof(T)))
                {
                    yield return (T) component;
                }
            }
        }

        public ReadOnlyCollection<Component> GetComponents()
        {
            return _components.AsReadOnly();
        }

        private Component AddComponent(Type type)
        {
            return AddComponent((Component) Activator.CreateInstance(type));
        }

        public T AddComponent<T>() where T : Component
        {
            return (T) AddComponent(typeof(T));
        }

        public Component AddComponent(Component component)
        {
            component.gameObject = this;

            _components.Add(component);

            // if (GameManager != null && component is MonoBehaviour behaviour)
            // {
            //     behaviour.Awake();
            // }

            return component;
        }

        public Component GetComponent(Type type)
        {
            foreach (var component in _components)
            {
                if (component.GetType().IsAssignableFrom(type))
                {
                    return component;
                }
            }

            return null;
        }

        public T GetComponent<T>() where T : Component
        {
            return (T) GetComponent(typeof(T));
        }

        public override string ToString()
        {
            var pos = GetComponent<Transform>();

            return $"{nameof(GameObject)}(" +
                   $"{nameof(Name)}: {Name}, " +
                   (pos != null
                       ? $"Position: {pos.localPosition}, " +
                         (pos.children.Count != 0 ? $"{pos.children.Count} children" : "")
                       : "") +
                   $"{_components.Count} components" +
                   ")";
        }
    }
}
