using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Assets
{
    public class AssetFile : IDisposable
    {
        public string Name { get; private set; }
        private AssetLoader _loader;
        private StreamReader _stream;
        public SerializedFileHeader Header { get; set; }
        public SerializedFileMetadata Metadata { get; set; }
        private List<AssetFile> _fileMap = new List<AssetFile>();

        public Dictionary<byte[], List<TypeEntry>> TypesByPropHash =
            new Dictionary<byte[], List<TypeEntry>>(new ByteArrayComparer());

        public ReadOnlyCollection<AssetFile> ExternalAssetRefs => _fileMap.Skip(1).ToList().AsReadOnly();

        private Dictionary<long, ISerializedItem> _referenceCache =
            new Dictionary<long, ISerializedItem>();

        public Dictionary<SerializedGameObject, GameObject> ObjectCache =
            new Dictionary<SerializedGameObject, GameObject>();

        private static Dictionary<int, Type> _unityTypeMap = new Dictionary<int, Type>
        {
            {(int) CommonTypeIds.GameObject, typeof(SerializedGameObject)},
            {(int) CommonTypeIds.Component, typeof(SerializedComponent)},
            {(int) CommonTypeIds.Transform, typeof(Transform)},
            {(int) CommonTypeIds.BoxCollider, typeof(BoxCollider)},
            {(int) CommonTypeIds.MonoBehaviour, typeof(SerializedMonoBehaviour)},
            {(int) CommonTypeIds.MonoScript, typeof(SerializedMonoScript)},
        };

        private static Dictionary<string, Type> _scriptTypeMap = new Dictionary<string, Type>
        {
        };

        static AssetFile()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                .Concat(Assembly.GetEntryAssembly().GetTypes()))
            {
                var attribute = type.GetCustomAttribute<SerializedMonoBehaviourAttribute>();
                if (attribute == null)
                    continue;

                _scriptTypeMap.Add(attribute.ClassName, type);
            }
        }

        public AssetFile(AssetLoader loader, string name, StreamReader stream)
        {
            Name = name;
            _loader = loader;
            _stream = stream;

            Initialize();

            LoadExternalReferences();
        }

        private void Initialize()
        {
            _fileMap.Add(this);
            Header = new SerializedFileHeader(_stream);
            Metadata = new SerializedFileMetadata(_stream);
            ProcessTypeTree();
        }

        /// <summary>
        /// Construct a lookup table of MonoScript property hashes to their
        /// BaseClass definitions in this AssetFile.
        /// </summary>
        private void ProcessTypeTree()
        {
            foreach (var baseClass in Metadata.TypeTree.BaseClasses)
            {
                if (baseClass.PropertiesHash == null) continue;

                if (!TypesByPropHash.TryGetValue(baseClass.PropertiesHash, out var typeList))
                {
                    typeList = new List<TypeEntry>();
                    TypesByPropHash.Add(baseClass.PropertiesHash, typeList);
                }

                typeList.Add(baseClass);
            }
        }

        public IEnumerable<T> GetObjectsByType<T>(TypeEntry type)
        {
            foreach (var objInfo in Metadata.ObjectInfoTable.Values)
            {
                if (objInfo.TypeId == type.Index)
                {
                    yield return (T) ReadObject(objInfo);
                }
            }
        }

        public IEnumerable<ObjectInfoEntry> GetObjectInfosByType(TypeEntry typeEntry)
        {
            foreach (var objInfo in Metadata.ObjectInfoTable.Entries)
            {
                if (objInfo.TypeId == typeEntry.Index)
                {
                    yield return objInfo;
                }
            }
        }

        private void LoadExternalReferences()
        {
            foreach (var externalReference in Metadata.ExternalReferencesTable)
            {
                var fileName = externalReference.FileName;
                if (fileName.StartsWith("library/"))
                {
                    fileName = "resources/" + fileName.Substring(8);
                }

                _fileMap.Add(_loader.LoadAsset(fileName));
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _loader?.Dispose(this);
            _loader = null;
        }

        public TypeEntry FindTypeById(CommonTypeIds typeId) => FindTypeById((int) typeId);

        public TypeEntry FindTypeById(int typeId)
        {
            foreach (var typeEntry in Metadata.TypeTree.BaseClasses)
            {
                if (typeEntry.TypeId == typeId)
                {
                    return typeEntry;
                }
            }

            return null;
        }

        public SerializedGameObject GetObjectByName(string name)
        {
            foreach (var gameObject in GetObjectsByType<SerializedGameObject>(FindTypeById(CommonTypeIds.GameObject)))
            {
                if (gameObject.Name == name)
                {
                    return gameObject;
                }
            }

            return null;
        }

        public IEnumerable<SerializedGameObject> GetObjectsByName(string name)
        {
            foreach (var gameObject in GetObjectsByType<SerializedGameObject>(FindTypeById(CommonTypeIds.GameObject)))
            {
                if (gameObject.Name == name)
                {
                    yield return gameObject;
                }
            }
        }

        public IEnumerable<SerializedGameObject> GetObjectsByComponent(SerializedMonoScript script)
        {
            if (!TypesByPropHash.TryGetValue(script.PropertiesHash.ToByteArray(), out var types))
            {
                yield break;
            }

            if (types.Count == 1)
            {
                foreach (var obj in GetObjectsByType<SerializedMonoBehaviour>(types[0]))
                {
                    yield return (SerializedGameObject) obj.GameObject.LoadValue();
                }
            }
            else if (types.Count > 1)
            {
                throw new NotImplementedException();
            }
        }

        public ISerializedItem ReadMonoScriptChild(SerializedMonoScript script)
        {
            // TODO Add namespace
            var key = '.' + script.ClassName;
            
            if (!_scriptTypeMap.ContainsKey(key))
            {
                return null;
            }

            var child = (ISerializedItem) Activator.CreateInstance(_scriptTypeMap[key]);
            child.DeserializeAsset(this, _stream);

            return child;
        }

        public ISerializedItem ReadObject(SerializedComponent objInfo)
        {
            return ReadObject(objInfo.PathId, objInfo.FileId);
        }

        public ISerializedItem ReadObject(ObjectInfoEntry objInfo, int fileId = 0)
        {
            return ReadObject(objInfo.PathId, fileId);
        }

        public ISerializedItem ReadObject(long pathId, int fileId = 0, bool restorePos = true)
        {
            if (pathId == 0)
            {
                if (fileId != 0)
                {
                    throw new ArgumentException($"pathId was 0, but fileId was {fileId}");
                }

                return null;
            }

            if (fileId != 0)
            {
                return _fileMap[fileId].ReadObject(pathId, 0, restorePos);
            }

            // Check the reference cache first

            _referenceCache.TryGetValue(pathId, out var cached);
            if (cached != null)
            {
                return cached;
            }

            var savedPos = _stream.Position;

            var objInfo = Metadata.ObjectInfoTable[pathId];

            var objType = Metadata.TypeTree.BaseClasses[objInfo.TypeId];

            _stream.Position = Header.DataOffset + objInfo.ByteStart;
            var endPos = Header.DataOffset + objInfo.ByteStart + objInfo.ByteSize;
            if (_unityTypeMap.ContainsKey(objType.TypeId))
            {
                var obj = (ISerializedItem) Activator.CreateInstance(_unityTypeMap[objType.TypeId]);
                _referenceCache[pathId] = obj;
                obj.DeserializeAsset(this, _stream);

                var currentPos = _stream.Position;
                if (currentPos < endPos)
                {
                    if (obj is SerializedMonoBehaviour smb)
                    {
//                        Log.Print(LogType.Warning,
//                            $"Didn't fully read MB {smb.Script.ClassName}, {currentPos}/{endPos}");
                    }
                    else
                    {
                        Log.Print(LogType.Warning, $"Didn't fully read {obj.GetType().Name}, {currentPos}/{endPos}");
                    }
                }
                else if (currentPos > endPos)
                {
                    throw new IndexOutOfRangeException(
                        $"Read past the end of {obj.GetType().Name}, {currentPos}/{endPos}");
                }

                if (restorePos)
                {
                    _stream.Position = savedPos;
                }

                return obj;
            }
            else
            {
//                Log.Print(LogType.Warning, $"No type mapping for {objType} {objInfo}");
            }

            if (restorePos)
            {
                _stream.Position = savedPos;
            }

            return null;
        }

        public void ClearCache()
        {
            _referenceCache.Clear();
            ObjectCache.Clear();
        }

        public override string ToString()
        {
            return $"{nameof(AssetFile)}(" +
                   $"{nameof(Name)}: {Name}, " +
                   $"{Metadata.ObjectInfoTable.Count} objects, " +
                   $"{ExternalAssetRefs.Count} external refs" +
                   ")";
        }
    }
}
