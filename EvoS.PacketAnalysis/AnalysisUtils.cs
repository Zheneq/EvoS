using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace EvoS.PacketAnalysis
{
    public class AnalysisUtils
    {
        private static readonly Type PatchFunctions = AccessTools.TypeByName("HarmonyLib.PatchFunctions");
        private static readonly MethodInfo GetInstructions = AccessTools.Method(PatchFunctions, "GetInstructions");
        private static readonly Type IlInstruction = AccessTools.TypeByName("HarmonyLib.ILInstruction");
        private static readonly MethodInfo GetCodeInstruction = AccessTools.Method(IlInstruction, "GetCodeInstruction");
        private static readonly ILGenerator DummyIlGen;

        static AnalysisUtils()
        {
            var dm = new DynamicMethod("dummy", typeof(void), new Type[0]);
            DummyIlGen = dm.GetILGenerator();
        }

        public static IEnumerable<CodeInstruction> GetMethodInstructions(Type type, string name)
        {
            MethodBase invokeSyncList = AccessTools.Method(type, name);

            return GetMethodInstructions(invokeSyncList);
        }

        public static IEnumerable<CodeInstruction> GetMethodInstructions(MethodBase method)
        {
            var instructions = (IList) GetInstructions.Invoke(null, new object[] {DummyIlGen, method});

            foreach (var instruction in instructions)
            {
                yield return (CodeInstruction) GetCodeInstruction.Invoke(instruction, new object[0]);
            }
        }
    }
}
