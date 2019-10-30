using System;
using System.Collections.Generic;
using System.Reflection;
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
    }
}
