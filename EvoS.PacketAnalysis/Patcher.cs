using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;
using HarmonyLib;

namespace EvoS.PacketAnalysis
{
    public class Patcher
    {
        public static InstrumentCallbacks Callbacks;
        private static readonly Harmony harmony = new Harmony("evos.patcher");
        private static HashSet<MethodInfo> _patchedMethods = new HashSet<MethodInfo>();
        public static readonly Dictionary<int, FieldInfo> SyncListLookup = new Dictionary<int, FieldInfo>();

        public static void PatchAll()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(NetworkBehaviour).IsAssignableFrom(type))
                        PatchMethod(AccessTools.Method(type, "OnDeserialize"));
//                    else if (typeof(BaseCmd).IsAssignableFrom(type))
//                        PatchMethod(AccessTools.Method(type, "Deserialize"));
//                    else if (typeof(BaseRpc).IsAssignableFrom(type))
//                        PatchMethod(AccessTools.Method(type, "Deserialize"));
                }
            }
        }

        private static HashSet<Type> _avoidTypes = new HashSet<Type>
        {
            typeof(Log),
            typeof(Mathf),
        };

        public static void PatchMethod(MethodInfo root)
        {
            if (!_patchedMethods.Add(root) || _avoidTypes.Contains(root.DeclaringType) ||
                root.GetMethodBody() == null) return;

            var patchTransformer =
                new HarmonyMethod(AccessTools.Method(typeof(MethodPatcher), nameof(MethodPatcher.Transpile)));

            harmony.Patch(root, transpiler: patchTransformer);
        }

        public static void ResolveSyncListFields()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(NetworkBehaviour).IsAssignableFrom(type)) continue;

                    // execute the static constructor instead of parsing its instructions
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

                    foreach (var method in AccessTools.GetDeclaredMethods(type))
                    {
                        if (!method.Name.StartsWith("InvokeSyncList")) continue;

                        var methodParams = method.GetParameters();
                        if (methodParams.Length != 2 ||
                            methodParams[0].ParameterType.FullName != "EvoS.Framework.Network.Unity.NetworkBehaviour" ||
                            methodParams[1].ParameterType.FullName != "EvoS.Framework.Network.Unity.NetworkReader")
                            continue;

                        var methodDelegate = (NetworkBehaviour.CmdDelegate) Delegate.CreateDelegate(
                            typeof(NetworkBehaviour.CmdDelegate), method);

                        var instructions = AnalysisUtils.GetMethodInstructions(method);
                        var hash = 0;
                        foreach (var instruction in instructions)
                        {
                            if (instruction.opcode != OpCodes.Ldfld) continue;

                            hash = NetworkBehaviour.GetHashByDelegate(methodDelegate);
                            SyncListLookup.Add(hash, (FieldInfo) instruction.operand);
                            break;
                        }

                        if (hash != 0) continue;

                        Log.Print(LogType.Warning, $"No SyncList found in {method.FullDescription()}");
                    }
                }
            }
        }
    }
}
