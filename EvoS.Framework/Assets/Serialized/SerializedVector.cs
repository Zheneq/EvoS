using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Assets.Serialized
{
    public class SerializedVector<T> : List<T>, ISerializedItem
    {
        public bool InlineRead { get; set; }

        public SerializedVector(bool inlineRead = false)
        {
            InlineRead = inlineRead;
        }


        public SerializedVector(AssetFile assetFile, StreamReader stream, bool inlineRead = false) : this(inlineRead)
        {
            DeserializeAsset(assetFile, stream);
        }

        public void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            Clear();

            var length = stream.ReadInt32();
            if (typeof(T) == typeof(string))
            {
                for (var i = 0; i < length; i++)
                {
                    Add((T) (object) stream.ReadString32());
                }
            }
            else if (typeof(T) == typeof(int))
            {
                for (var i = 0; i < length; i++)
                {
                    Add((T) (object) stream.ReadInt32());
                }
            }
            else if (typeof(T) == typeof(Vector3))
            {
                for (var i = 0; i < length; i++)
                {
                    Add((T) (object) stream.ReadVector3());
                }
            }
            else if (typeof(T).IsEnum)
            {
                for (var i = 0; i < length; i++)
                {
                    Add((T) (object) stream.ReadInt32());
                }
            }
            else if (typeof(T).IsValueType ||
                     InlineRead && typeof(ISerializedItem).IsAssignableFrom(typeof(T)) ||
                     typeof(T) == typeof(SerializedComponent))
            {
                for (var i = 0; i < length; i++)
                {
                    var item = (ISerializedItem) Activator.CreateInstance(typeof(T));
                    item.DeserializeAsset(assetFile, stream);
                    Add((T) item);
                }
            }
            else if (typeof(ISerializedItem).IsAssignableFrom(typeof(T)))
            {
                for (var i = 0; i < length; i++)
                {
                    var ptr = (SerializedComponent) Activator.CreateInstance(typeof(SerializedComponent));
                    ptr.DeserializeAsset(assetFile, stream);
                    Add((T) ptr.LoadValue());
                }
            }
            else
            {
                throw new NotImplementedException($"Unhandled vector child type {typeof(T)}");
            }
        }

        public override string ToString()
        {
            return $"Vector<{typeof(T).Name}>(" +
                   $"{Count} entries" +
                   ")";
        }
    }

    public static class SerializedVectorExtensions
    {
        public static T[] ToChildArray<T>(this SerializedVector<SerializedMonoBehaviour> vector)
            where T : MonoBehaviour
        {
            return ToChildList<T>(vector).ToArray();
        }

        public static IList<T> ToChildList<T>(this SerializedVector<SerializedMonoBehaviour> vector)
            where T : MonoBehaviour
        {
            return vector.Select(child => (T) child.Child).ToList();
        }
    }
}
