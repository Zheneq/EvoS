using System;
using System.Collections.Generic;
using EvoS.Framework.Logging;

namespace EvoS.Framework.Assets
{
    public class TypeTree
    {
        public int Attributes { get; set; }
        public byte Embeded { get; set; }
        public List<TypeEntry> BaseClasses { get; set; } = new List<TypeEntry>();
        public Dictionary<int, TypeTreeInfo> TypeInfos { get; set; } = new Dictionary<int, TypeTreeInfo>();
        public Dictionary<int, TypeTreeInfo> ScriptTypeInfos { get; set; } = new Dictionary<int, TypeTreeInfo>();

        public TypeTree(StreamReader stream)
        {
            Attributes = stream.ReadInt32();
            Embeded = stream.ReadByte();

            var baseClassCount = stream.ReadInt32();
            for (var i = 0; i < baseClassCount; i++)
            {
                var entry = new TypeEntry(stream) {Index = i};
                BaseClasses.Add(entry);

                if (Embeded == 0) continue;

                if (entry.TypeId == (int) CommonTypeIds.MonoBehaviour)
                {
                    var info = new TypeTreeInfo(stream);
                    if (!ScriptTypeInfos.TryAdd(entry.Unknown2, info))
                    {
                        Log.Print(LogType.Warning, $"Multiple scripts with id={entry.Unknown2}!");
                    }
                }
                else
                {
                    TypeInfos.Add(entry.TypeId, new TypeTreeInfo(stream));
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(TypeTree)}(" +
                   $"{nameof(Attributes)}: {Attributes}, " +
                   $"{nameof(Embeded)}: {Embeded}, " +
                   $"{nameof(BaseClasses)}: {String.Join(", ", BaseClasses)}" +
                   ")";
        }
    }

    public class TypeTreeInfo
    {
        public TypeTreeInfo(StreamReader stream)
        {
            var nodeCount = stream.ReadInt32();
            var stringBufSize = stream.ReadInt32();

            // TODO
            stream.Position += nodeCount * 24 + stringBufSize;
        }
    }

    public class TypeEntry
    {
        public int Index { get; set; }
        public int TypeId { get; set; }
        public byte Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public byte[] Foo2 { get; set; }
        public byte[] PropertiesHash { get; set; }

        public TypeEntry(StreamReader stream)
        {
            TypeId = stream.ReadInt32();
            Unknown1 = stream.ReadByte();
            Unknown2 = stream.ReadInt16();
            if (TypeId == 114) // MonoBehaviour
            {
                Foo2 = stream.ReadBytes(16);
            }

            PropertiesHash = stream.ReadBytes(16);
        }

        public override string ToString()
        {
            return $"{nameof(TypeEntry)}(" +
                   $"{nameof(TypeId)}: {TypeId}, " +
                   $"{nameof(Unknown1)}: {Unknown1}, " +
                   $"{nameof(Unknown2)}: {Unknown2}, " +
                   $"{nameof(Foo2)}: {(Foo2 != null ? BitConverter.ToString(Foo2).Replace("-", "") : null)}, " +
                   $"{nameof(PropertiesHash)}: {BitConverter.ToString(PropertiesHash).Replace("-", "")}" +
                   ")";
        }
    }
}
