using System;
using System.IO;
using Newtonsoft.Json;

namespace EvoS.Framework.Assets.Serialized
{
    public class SerializedComponent : ISerializedItem
    {
        public int FileId { get; set; }
        public long PathId { get; set; }
        [JsonIgnore] private WeakReference<AssetFile> _assetFile;

        public SerializedComponent()
        {
            
        }
        public SerializedComponent(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            _assetFile = new WeakReference<AssetFile>(assetFile);

            FileId = stream.ReadInt32();
            PathId = stream.ReadInt64();
        }

        public ISerializedItem LoadValue()
        {
            if (!_assetFile.TryGetTarget(out var assetFile))
            {
                return null;
            }

            return assetFile.ReadObject(PathId, FileId);
        }

        public ISerializedItem LoadMonoBehaviourChild()
        {
            var smb = (SerializedMonoBehaviour) LoadValue();
            
            return smb?.Child;
        }

        public override string ToString()
        {
            return "Component(" +
                   (FileId != 0 ? $"{nameof(FileId)}: {FileId}, " : "") +
                   (PathId != 0 ? $"{nameof(PathId)}: {PathId}" : "null") +
                   ")";
        }
    }
}
