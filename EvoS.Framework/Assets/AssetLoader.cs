using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using EvoS.Framework.Assets.Bundles;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Assets
{
    public class AssetLoader
    {
        public static string BasePath;
        private Dictionary<string, AssetFile> _assetFiles = new Dictionary<string, AssetFile>();

        private Dictionary<string, UnityFs> _assetBundles =
            new Dictionary<string, UnityFs>();

        private List<AssetFile> _strongRefs = new List<AssetFile>();

        private Dictionary<byte[], List<SerializedMonoScript>> _scriptsByHash =
            new Dictionary<byte[], List<SerializedMonoScript>>(new ByteArrayComparer());

        public readonly Dictionary<string, SerializedMonoScript> ScriptsByName =
            new Dictionary<string, SerializedMonoScript>();

        public Dictionary<string, SerializedGameObject> NetObjsByName = new Dictionary<string, SerializedGameObject>();
        public Dictionary<uint, SerializedGameObject> NetworkScenes = new Dictionary<uint, SerializedGameObject>();

        public Dictionary<NetworkHash128, SerializedGameObject> NetObjsByAssetId =
            new Dictionary<NetworkHash128, SerializedGameObject>();

        public Dictionary<string, SerializedGameObject>.ValueCollection NetworkedObjects => NetObjsByName.Values;
        public Dictionary<string, UnityFs> AssetBundles => _assetBundles;

        public AssetLoader()
        {
            if (BasePath == null && !FindAssetRoot())
            {
                throw new FileNotFoundException("Failed to find resources.assets");
            }
        }

        // public SerializedGameObject GetNetObj(string assetId)
        // {
        //     return GetNetObj(new NetworkHash128(Utils.ToByteArray(assetId)));
        // }

        public SerializedGameObject GetNetObj(NetworkHash128 assetId)
        {
            return NetObjsByAssetId[assetId];
        }

        public AssetFile LoadAsset(string name, bool strongRef = false)
        {
            if (_assetFiles.TryGetValue(name, out var assetFile))
            {
                return assetFile;
            }

            // check if the asset is part of a bundle
            if (name.StartsWith("archive:/"))
            {
                var nameInfo = name.Split("/");
                var bundleName = nameInfo[1];
                var assetName = nameInfo[2];

                // check if the bundled asset is already loaded
                if (_assetFiles.TryGetValue(assetName, out assetFile))
                {
                    return assetFile;
                }

                // ensure the bundle is loaded
                if (!_assetBundles.TryGetValue(bundleName, out var unityFs))
                {
                    throw new ArgumentException($"Asset bundle containing asset file {name} not loaded!");
                }

                foreach (var node in unityFs.Nodes)
                {
                    if (node.Name.ToLower() != assetName) continue;

                    assetFile = unityFs.OpenAssetFile(node);
                    break;
                }

                if (assetFile == null)
                {
                    throw new ArgumentException($"Asset {assetName} not found in bundle {bundleName}!");
                }
            }
            else
            {
                assetFile = new AssetFile(this, name, new StreamReader(Path.Join(BasePath, name)));
            }

            _assetFiles[assetFile.Name] = assetFile;
            _assetFiles[name] = assetFile;

            if (strongRef) _strongRefs.Add(assetFile);

            return assetFile;
        }

        public void LoadAssetBundle(string fileName)
        {
            var unityFs = new UnityFs(this, Path.Join(BasePath, fileName));

            _assetBundles.Add(unityFs.Name, unityFs);
        }

//         public void ConstructCaches()
//         {
//             ConstructMonoScriptMap();
//
//             LoadNetworkedObjects();
// //            DumpNetworkObjectComponents();
//         }

        public SerializedGameObject GetObjectByComponent<T>() where T : MonoBehaviour
        {
            return GetObjectsByComponent<T>().FirstOrDefault();
        }

        public IEnumerable<SerializedGameObject> GetObjectsByComponent<T>() where T : MonoBehaviour
        {
            var attribute = typeof(T).GetCustomAttribute<SerializedMonoBehaviourAttribute>();
            if (attribute == null)
            {
                throw new InvalidDataException($"{typeof(T)} has no {nameof(SerializedMonoBehaviourAttribute)}!");
            }

            if (!ScriptsByName.TryGetValue(attribute.ClassName, out var script))
            {
                yield break;
            }

            foreach (var assetFile in AllAssetFiles())
            {
                foreach (var gameObject in assetFile.GetObjectsByComponent(script))
                {
                    yield return gameObject;
                }
            }
        }

        public SerializedGameObject GetObjectByComponent(SerializedMonoScript script)
        {
            return GetObjectsByComponent(script).FirstOrDefault();
        }

        public IEnumerable<SerializedGameObject> GetObjectsByComponent(SerializedMonoScript script)
        {
            foreach (var assetFile in AllAssetFiles())
            {
                foreach (var gameObject in assetFile.GetObjectsByComponent(script))
                {
                    yield return gameObject;
                }
            }
        }

        private IEnumerable<AssetFile> AllAssetFiles()
        {
            var stack = new Stack<AssetFile>();
            var seen = new HashSet<string>();
            foreach (var asset in _assetFiles.Values)
            {
                stack.Push(asset);
            }

            while (!stack.IsNullOrEmpty())
            {
                var assetFile = stack.Pop();
                if (seen.Contains(assetFile.Name))
                {
                    continue;
                }

                yield return assetFile;
                seen.Add(assetFile.Name);

                foreach (var extRef in assetFile.ExternalAssetRefs)
                {
                    stack.Push(extRef);
                }
            }
        }

        private void ConstructMonoScriptMap()
        {
            foreach (var assetFile in AllAssetFiles())
            {
                var monoScriptType = assetFile.FindTypeById(CommonTypeIds.MonoScript);
                if (monoScriptType == null)
                {
                    continue;
                }

                foreach (var gameObject in assetFile.GetObjectInfosByType(monoScriptType))
                {
                    var obj = (SerializedMonoScript) assetFile.ReadObject(gameObject);
                    if (obj.PropertiesHash.IsZero())
                    {
                        continue;
                    }

                    // multiple scripts can share the same properties hash
                    if (!_scriptsByHash.TryGetValue(obj.PropertiesHash.ToByteArray(), out var scriptList))
                    {
                        scriptList = new List<SerializedMonoScript>();
                        _scriptsByHash.Add(obj.PropertiesHash.ToByteArray(), scriptList);
                    }

                    scriptList.Add(obj);
                    if (!ScriptsByName.TryAdd(obj.QualifiedName, obj))
                    {
                        var existing = ScriptsByName[obj.QualifiedName];
                        if (existing.QualifiedName == obj.QualifiedName && existing.AssemblyName == obj.AssemblyName)
                        {
                            continue;
                        }

                        Log.Print(LogType.Warning, $"    trying:  {obj}");
                        Log.Print(LogType.Warning, $"    already: {ScriptsByName[obj.QualifiedName]}");
                    }
                }
            }

            Log.Print(LogType.Misc, $"Loaded {ScriptsByName.Count} MonoScript type mappings");
        }

        // private void LoadNetworkedObjects()
        // {
        //     var count = 0;
        //     foreach (var assetFile in AllAssetFiles())
        //     {
        //         InternalLoadNetworkedObjects(assetFile);
        //         count++;
        //     }
        //
        //     Log.Print(LogType.Misc,
        //         $"Loaded {NetworkScenes.Count} networked scenes and " +
        //         $"{NetworkedObjects.Count} networked game objects from {count} asset files.");
        // }
        //
        // private void InternalLoadNetworkedObjects(AssetFile assetFile)
        // {
        //     if (!ScriptsByName.TryGetValue(CommonTypes.NetworkIdentity, out var netIdentScript))
        //     {
        //         return;
        //     }
        //
        //     foreach (SerializedGameObject obj in assetFile.GetObjectsByComponent(netIdentScript))
        //     {
        //         var netIdent = obj.GetComponent<NetworkIdentity>();
        //         if (netIdent != null)
        //         {
        //             var netHash = netIdent.assetId;
        //             if (!netHash.IsZero())
        //             {
        //                 if (!NetObjsByName.TryAdd(obj.Name, obj))
        //                 {
        //                     Log.Print(LogType.Warning,
        //                         $"Multiple objects with name {obj.Name}, latest in {assetFile.Name}");
        //                 }
        //
        //                 if (!NetObjsByAssetId.TryAdd(netHash, obj))
        //                 {
        //                     Log.Print(LogType.Warning,
        //                         $"Multiple objects with netId {netHash}, latest in {assetFile.Name}");
        //                 }
        //             }
        //             else
        //             {
        //                 NetworkScenes.Add(netIdent.sceneId.Value, obj);
        //             }
        //         }
        //     }
        // }

        // public void DumpNetworkObjectComponents()
        // {
        //     foreach (var obj in NetworkedObjects)
        //     {
        //         var netIdent = obj.GetComponent<NetworkIdentity>();
        //
        //         Log.Print(LogType.Misc, $"{netIdent.assetId} {obj.Name}");
        //         Log.Print(LogType.Misc, $"  {string.Join(", ", obj.ComponentNames())}");
        //     }
        // }
        //
        // public void ClearCache()
        // {
        //     foreach (var assetFile in AllAssetFiles())
        //     {
        //         assetFile.ClearCache();
        //     }
        // }

        public void Dispose(AssetFile assetFile)
        {
            _strongRefs.Remove(assetFile);
            _assetFiles.Remove(assetFile.Name);
        }

        public void DumpAssetFileDepTree()
        {
            var removed = new List<AssetFile>();
            var roots = AllAssetFiles().ToList();

            foreach (var file in AllAssetFiles())
            {
                if (removed.Contains(file)) continue;

                var toRemove = new Stack<AssetFile>(file.ExternalAssetRefs);
                while (toRemove.Count != 0)
                {
                    var item = toRemove.Pop();
                    if (removed.Contains(item)) continue;

                    removed.Add(item);
                    roots.Remove(item);

                    foreach (var child in item.ExternalAssetRefs)
                    {
                        toRemove.Push(child);
                    }
                }
            }

            void DumpTree(AssetFile child, int depth = 0)
            {
                Console.WriteLine($"{new string(' ', 2 * depth)}{child.Name}");
                foreach (var assetRef in child.ExternalAssetRefs)
                {
                    DumpTree(assetRef, depth + 1);
                }
            }

            foreach (var assetFile in roots)
            {
                DumpTree(assetFile);
            }
        }

        public static bool FindAssetRoot(string hint = null)
        {
            bool CheckValid(string path)
            {
                if (!File.Exists(Path.Join(path, "resources.assets"))) return false;

                Log.Print(LogType.Misc, $"Assets root = {path}");
                BasePath = path;
                return true;
            }

            return hint != null && CheckValid(hint) ||
                   CheckValid("Win64\\AtlasReactor_Data") ||
                   CheckValid("AtlasReactor_Data");
        }
    }
}
