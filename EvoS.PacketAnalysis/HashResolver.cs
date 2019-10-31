using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace EvoS.PacketAnalysis
{
    public enum HashType
    {
        Cmd,
        SyncList,
        Rpc,
    }

    internal struct HashInfo
    {
        public string Name;
        public string ClassName;
        public int Hash;
        public HashType Type;

        public HashInfo(string name, string className, int hash, HashType type)
        {
            Name = name;
            ClassName = className;
            Hash = hash;
            Type = type;
        }
    }

    public class HashResolver
    {
        private static readonly Dictionary<int, HashInfo> _hashInfos = new Dictionary<int, HashInfo>();

        public static void Init(string basePath)
        {
            var assemblyPath = Path.Join(basePath, "Managed", "Assembly-CSharp.dll");
            if (!File.Exists(assemblyPath)) return;
            
            var module = ModuleDefinition.ReadModule(assemblyPath);

            foreach (var instruction in from type in module.Types
                from method in type.Methods
                where method.HasBody &&
                      method.Name == ".cctor"
                from instruction in method.Body.Instructions
                where instruction.OpCode == OpCodes.Stsfld
                select instruction)
            {
                var fieldRef = (FieldReference) instruction.Operand;
                var fieldName = fieldRef.Name;
                var className = fieldRef.DeclaringType.Name;

                HashType type;
                if (fieldName.StartsWith("kCmd"))
                    type = HashType.Cmd;
                else if (fieldName.StartsWith("kRpc"))
                    type = HashType.Rpc;
                else if (fieldName.StartsWith("kList"))
                    type = HashType.SyncList;
                else
                    continue;

                if (instruction.Previous.OpCode != OpCodes.Ldc_I4)
                {
                    throw new NotImplementedException();
                    continue;
                }

                var value = (int) instruction.Previous.Operand;

                _hashInfos.Add(value, new HashInfo(
                    fieldName,
                    className,
                    value,
                    type
                ));
            }
        }

        public static string LookupSyncList(int hash) => LookupInternal(hash, HashType.SyncList);
        public static string LookupCmd(int hash) => LookupInternal(hash, HashType.Cmd);
        public static string LookupRpc(int hash) => LookupInternal(hash, HashType.Rpc);

        private static string LookupInternal(int hash, HashType expectedType)
        {
            if (!_hashInfos.TryGetValue(hash, out var info))
                return $"Unknown{expectedType}_{hash}";

            if (info.Type != expectedType)
                throw new NotImplementedException();

            return info.Name;
        }
    }
}
