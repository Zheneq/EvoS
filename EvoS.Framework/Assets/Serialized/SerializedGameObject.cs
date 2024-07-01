using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Assets.Serialized
{
    public class SerializedGameObject : ISerializedItem
    {
        public SerializedVector<SerializedComponent> Components { get; set; }
        public uint Layer { get; set; }
        public string Name { get; set; }
        public ushort Tag { get; set; }
        public bool IsActive { get; set; }
        private readonly WeakReference<AssetFile> _assetFile = new WeakReference<AssetFile>(null);

        public void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            _assetFile.SetTarget(assetFile);

            Components = new SerializedVector<SerializedComponent>();
            Components.DeserializeAsset(assetFile, stream);
            Layer = stream.ReadUInt32();
            Name = stream.ReadString32();
            Tag = stream.ReadUInt16();
            IsActive = stream.ReadBoolean();
        }

        public IEnumerable<ISerializedItem> ComponentChildren()
        {
            foreach (var component in Components)
            {
                var compValue = component.LoadValue();
                if (compValue is SerializedMonoBehaviour smb)
                {
                    yield return smb.Child;
                }
                else
                {
                    yield return compValue;
                }
            }
        }

        public List<string> ComponentNames()
        {
            var list = new List<string>();
            foreach (var component in Components)
            {
                var compValue = component.LoadValue();
                if (compValue is SerializedMonoBehaviour smb)
                {
                    list.Add(smb.Script?.ClassName);
                }
                else
                {
                    list.Add(compValue != null ? compValue.GetType().FullName : "null");
                }
            }

            return list;
        }

        public T GetComponent<T>() where T : class, ISerializedItem
        {
            foreach (var component in ComponentChildren())
            {
                if (component is T childBase)
                {
                    return childBase;
                }
            }

            return null;
        }

        public override string ToString()
        {
            return $"{nameof(SerializedGameObject)}(" +
                   $"{nameof(Name)}: {Name}, " +
//                   $"{nameof(Components)}: {Components}, " +
                   $"{nameof(Components)}: {Components.Count} components, " +
//                   $"{nameof(Layer)}: {Layer}, " +
//                   $"{nameof(Tag)}: {Tag}, " +
//                   $"{nameof(IsActive)}: {IsActive}" +
                   ")";
        }

        public GameObject Instantiate()
        {
            if (_assetFile.TryGetTarget(out var assetFile) &&
                assetFile.ObjectCache.TryGetValue(this, out var child))
            {
                return child;
            }
        
            var gameObj = new GameObject(Name);
            assetFile?.ObjectCache?.Add(this, gameObj);
            var names = ComponentNames();
            var index = 0;
        
            Transform transform = null;
            foreach (var component in ComponentChildren())
            {
                var name = names[index++];
                switch (component)
                {
                    case Transform trans:
                        transform = trans;
                        gameObj.AddComponent(trans);
                        gameObj.transform = trans;
                        break;
                    case Component comp:
                        comp.transform = transform;
                        gameObj.AddComponent(comp);
                        break;
                    case null:
                        // ignore
                        break;
                    default:
                        Console.WriteLine($"  Unhandled component child {name} - {component}");
                        break;
                }
            }
        
            return gameObj;
        }
    }
}
