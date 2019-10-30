using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EvoS.Framework.Logging;
using HarmonyLib;

namespace EvoS.PacketAnalysis
{
    public class MethodPatcher
    {
        private static readonly MethodInfo CbOnCallMethod =
            AccessTools.Method(typeof(InstrumentCallbacks), nameof(InstrumentCallbacks.OnCallMethod));

        private static readonly MethodInfo CbOnEnterMethod =
            AccessTools.Method(typeof(InstrumentCallbacks), nameof(InstrumentCallbacks.OnEnter));

        private static readonly MethodInfo CbOnLeaveMethod =
            AccessTools.Method(typeof(InstrumentCallbacks), nameof(InstrumentCallbacks.OnLeave));

        private static readonly MethodInfo CbOnSetFld =
            AccessTools.Method(typeof(InstrumentCallbacks), nameof(InstrumentCallbacks.OnSetFld));

        private static readonly MethodInfo _logMethod =
            AccessTools.FirstMethod(typeof(Log), x => x.Name == "Print" && x.GetParameters().Length == 2);

        public MethodBase OriginalMethod;
        public ILGenerator IlGen;
        public IEnumerable<CodeInstruction> Instructions;
        public List<CodeInstruction> EmittedInstructions = new List<CodeInstruction>();
        public Dictionary<Type, LocalBuilder> LocsByType = new Dictionary<Type, LocalBuilder>();
        public StackRefTypeInfo[] ParamIsRef;
        public List<StackRefTypeInfo> Stack = new List<StackRefTypeInfo>();
        public IList<LocalVariableInfo> LocalVariables;

        private MethodPatcher(MethodBase originalMethod, ILGenerator ilGen, IEnumerable<CodeInstruction> instructions)
        {
            OriginalMethod = originalMethod;
            IlGen = ilGen;
            Instructions = instructions;
            LocalVariables = OriginalMethod.GetMethodBody().LocalVariables;

            ParamIsRef = new StackRefTypeInfo[OriginalMethod.GetParameters().Length];
            for (var j = 0; j < ParamIsRef.Length; j++)
            {
                var pType = OriginalMethod.GetParameters()[j].ParameterType;
                if (pType.IsByRef)
                {
                    ParamIsRef[j] = new StackRefTypeInfo(null, pType, OriginalMethod.GetParameters()[j].Name);
                }
            }
        }

        private int PositionOf(CodeInstruction instruction) => EmittedInstructions.IndexOf(instruction);

        private void Emit(OpCode opCode, object operand = null) => Emit(new CodeInstruction(opCode, operand));

        private void Emit(CodeInstruction instruction)
        {
            EmittedInstructions.Add(instruction);
        }

        private void EmitAt(CodeInstruction instruction, int pos)
        {
            EmittedInstructions.Insert(pos, instruction);
        }

        private void EmitSimulate(CodeInstruction instruction)
        {
            if (instruction.opcode.FlowControl != FlowControl.Next &&
                instruction.opcode.FlowControl != FlowControl.Call)
            {
                // Don't bother with any flow control
                Stack.Clear();
            }

            var sz = Stack.Count;
            SimulatePop(Stack, instruction);
            SimulatePush(Stack, instruction, ParamIsRef, !OriginalMethod.IsStatic,
                OriginalMethod.DeclaringType.IsValueType ? OriginalMethod.DeclaringType : null);
//            Console.WriteLine($"Net {Stack.Count - sz} | {instruction}");

            Emit(instruction);
        }

        private LocalBuilder GetLocal(object o)
        {
            if (o is FieldInfo fi) o = fi.FieldType;
            if (!(o is Type type)) throw new NotImplementedException();

            if (!LocsByType.TryGetValue(type, out var loc))
            {
                loc = IlGen.DeclareLocal(type);
                LocsByType.Add(type, loc);
            }

            return loc;
        }

        private void Patch()
        {
            var fldCallbacks = AccessTools.Field(typeof(Patcher), nameof(Patcher.Callbacks));
            Console.WriteLine($"Patching {OriginalMethod.DeclaringType.FullName}.{OriginalMethod.Name}");

            var objRef = IlGen.DeclareLocal(typeof(object));

            Emit(OpCodes.Ldsfld, fldCallbacks);
            Emit(OpCodes.Ldstr, OriginalMethod.DeclaringType.Name);
            Emit(OpCodes.Ldstr, OriginalMethod.Name);
            Emit(OpCodes.Callvirt, CbOnEnterMethod);

//            EmitLog($"> {OriginalMethod.DeclaringType.FullName}.{OriginalMethod.Name}");

            foreach (var instruction in Instructions)
            {
                if (instruction.opcode == OpCodes.Stfld)
                {
                    var destField = (FieldInfo) instruction.operand;
                    var valLocal = GetLocal(destField.FieldType);

                    if (destField.Name == "m_accountId")
                        Console.WriteLine();

                    Console.WriteLine($"Patch set of {destField} | {destField.FieldType}");

                    var nop = new CodeInstruction(OpCodes.Nop);
                    SwapLabels(instruction, nop);
                    Emit(nop);

//                    EmitLog($"  > About to set {destField}");

                    Emit(OpCodes.Stloc, valLocal);
                    Emit(OpCodes.Stloc, objRef);

                    Emit(OpCodes.Ldsfld, fldCallbacks);
                    Emit(OpCodes.Ldloc, objRef);
                    if (Stack.Count >= 2 && Stack[1] != null)
                    {
                        Emit(OpCodes.Ldobj, Stack[1].ElementOrType);
                        Emit(OpCodes.Box, Stack[1].ElementOrType);
                    }

                    Emit(OpCodes.Ldstr, destField.Name);
                    Emit(OpCodes.Ldloc, valLocal);
                    if (destField.FieldType.IsPrimitive || destField.FieldType.IsEnum ||
                        destField.FieldType.IsValueType)
                    {
                        Emit(OpCodes.Box, destField.FieldType);
                    }

                    Emit(OpCodes.Callvirt, CbOnSetFld);

                    Emit(OpCodes.Ldloc, objRef);
                    Emit(OpCodes.Ldloc, valLocal);
                }
                else if (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                {
                    var callee = (MethodInfo) instruction.operand;
                    if (callee.Name.StartsWith("Hook") || callee.Name.StartsWith("Set") ||
                        callee.Name.StartsWith("set_") || callee.Name == "AddInternal" || callee.Name == "Clear")
                    {
                        Console.WriteLine($"Patch call to {callee}");

                        var paramLocs = callee.GetParameters().Select(param => GetLocal(param.ParameterType)).ToList();
                        var paramTypes = callee.GetParameters().Select(param => param.ParameterType).ToList();

//                        EmitLog($"  > About to call {callee}");

                        var nop = new CodeInstruction(OpCodes.Nop);
                        SwapLabels(instruction, nop);
                        Emit(nop);

                        for (var j = paramLocs.Count - 1; j >= 0; j--)
                        {
                            Emit(OpCodes.Stloc, paramLocs[j]);
                        }

                        Emit(OpCodes.Stloc, objRef);

                        Emit(OpCodes.Ldsfld, fldCallbacks);
                        Emit(OpCodes.Ldloc, objRef);
                        Emit(OpCodes.Ldstr, callee.Name);

                        Emit(OpCodes.Ldc_I4, paramLocs.Count);
                        Emit(OpCodes.Newarr, typeof(object));
                        for (var j = 0; j < paramLocs.Count; j++)
                        {
                            Emit(OpCodes.Dup);
                            Emit(OpCodes.Ldc_I4, j);
                            Emit(OpCodes.Ldloc, paramLocs[j]);
                            if (paramTypes[j].IsPrimitive || paramTypes[j].IsEnum || paramTypes[j].IsValueType)
                            {
                                Emit(OpCodes.Box, paramTypes[j]);
                            }

                            Emit(OpCodes.Stelem_Ref);
                        }

                        Emit(OpCodes.Callvirt, CbOnCallMethod);

                        Emit(OpCodes.Ldloc, objRef);
                        for (var j = 0; j < paramLocs.Count; j++)
                        {
                            Emit(OpCodes.Ldloc, paramLocs[j]);
                        }
                    }
                    else if (callee.Name == "Serialize")
                    {
                        if (callee.GetParameters().Length != 1)
                            throw new NotImplementedException();

                        var arg = Stack[0];
                        if (arg != null && arg.Instruction.operand is FieldInfo destField)
                        {
                            Console.WriteLine($"Patch ref set of {destField} | {destField.FieldType}");
//                            EmitLog($"  > About to ref set {destField}");

                            // store the reference to the object holding the field
                            var beforeLdflda = PositionOf(arg.Instruction);
                            EmitAt(new CodeInstruction(OpCodes.Dup), beforeLdflda);
                            EmitAt(new CodeInstruction(OpCodes.Stloc, objRef), beforeLdflda + 1);
                            EmitSimulate(instruction);

                            Emit(OpCodes.Ldsfld, fldCallbacks);
                            Emit(OpCodes.Ldloc, objRef);
                            // we hope that it's never a struct :)
//                            if (Stack.Count >= 2 && Stack[1] != null)
//                            {
//                                Emit(OpCodes.Ldobj, Stack[1].ElementOrType);
//                                Emit(OpCodes.Box, Stack[1].ElementOrType);
//                            }

                            Emit(OpCodes.Ldstr, arg.EntryName);
                            Emit(OpCodes.Ldloc, objRef);
                            Emit(OpCodes.Ldfld, destField); // load the same field as in Ldflda
                            if (destField.FieldType.IsPrimitive || destField.FieldType.IsEnum)
                            {
                                Emit(OpCodes.Box, destField.FieldType);
                            }

                            Emit(OpCodes.Callvirt, CbOnSetFld);

                            continue;
                        }
                    }
                    else if (instruction.opcode == OpCodes.Call &&
                             callee.DeclaringType.Namespace.StartsWith("EvoS") &&
                             !callee.Name.StartsWith("get_") &&
                             !callee.Name.Contains("Bitf"))
                    {
//                        Patcher.PatchMethod(callee);
                    }
                }
                else if (instruction.opcode == OpCodes.Ret)
                {
                    var newRet = new CodeInstruction(OpCodes.Ldsfld, fldCallbacks);
                    SwapLabels(instruction, newRet);
                    Emit(newRet);
                    Emit(OpCodes.Ldstr, OriginalMethod.DeclaringType.FullName);
                    Emit(OpCodes.Ldstr, OriginalMethod.Name);
                    Emit(OpCodes.Callvirt, CbOnLeaveMethod);
                }

                EmitSimulate(instruction);
            }
        }

        private void EmitLog(string msg)
        {
            Emit(OpCodes.Ldc_I4_1);
            Emit(OpCodes.Ldstr, msg);
            Emit(OpCodes.Call, _logMethod);
        }

        private void SimulatePop(List<StackRefTypeInfo> stack, CodeInstruction instruction)
        {
            var count = 0;

            if (instruction.opcode.FlowControl == FlowControl.Call)
            {
                var target = (MethodBase) instruction.operand;
                if (!target.IsStatic && instruction.opcode != OpCodes.Newobj) // ?
                    count++;
                count += target.GetParameters().Length;
                if (instruction.opcode == OpCodes.Calli)
                    count++;
            }
            else
            {
                switch (instruction.opcode.StackBehaviourPop)
                {
                    case StackBehaviour.Pop0:
                        count = 0;
                        break;
                    case StackBehaviour.Pop1:
                    case StackBehaviour.Popi:
                    case StackBehaviour.Popref:
                    case StackBehaviour.Varpop:
                        count = 1;
                        break;
                    case StackBehaviour.Pop1_pop1:
                    case StackBehaviour.Popi_pop1:
                    case StackBehaviour.Popi_popi:
                    case StackBehaviour.Popi_popi8:
                    case StackBehaviour.Popi_popr4:
                    case StackBehaviour.Popi_popr8:
                    case StackBehaviour.Popref_pop1:
                    case StackBehaviour.Popref_popi:
                        count = 2;
                        break;
                    case StackBehaviour.Popi_popi_popi:
                    case StackBehaviour.Popref_popi_popi:
                    case StackBehaviour.Popref_popi_popi8:
                    case StackBehaviour.Popref_popi_popr4:
                    case StackBehaviour.Popref_popi_popr8:
                    case StackBehaviour.Popref_popi_popref:
                    case StackBehaviour.Popref_popi_pop1:
                        count = 3;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            for (var i = count; i > 0 && stack.Count != 0; i--)
            {
                stack.RemoveAt(0);
            }
        }

        public class StackRefTypeInfo
        {
            public Type EntryType;
            public String EntryName;
            public CodeInstruction Instruction;
            public Type ElementOrType => EntryType.GetElementType() ?? EntryType;

            public StackRefTypeInfo(CodeInstruction instruction, Type entryType, string entryName)
            {
                EntryType = entryType;
                EntryName = entryName;
                Instruction = instruction;
            }

            public StackRefTypeInfo With(CodeInstruction instruction) =>
                new StackRefTypeInfo(instruction, EntryType, EntryName);
        }

        private void SimulatePush(List<StackRefTypeInfo> stack, CodeInstruction instruction,
            StackRefTypeInfo[] paramIsRef,
            bool isInstance, Type structType)
        {
            if (instruction.opcode == OpCodes.Ldarg_0)
            {
                stack.Insert(0, isInstance
                    ? (structType != null
                        ? new StackRefTypeInfo(instruction, structType, "this")
                        : null)
                    : paramIsRef[0]?.With(instruction));
                return;
            }

            if (instruction.opcode == OpCodes.Ldarg_1)
            {
                stack.Insert(0, paramIsRef[isInstance ? 0 : 1]?.With(instruction));
                return;
            }

            if (instruction.opcode == OpCodes.Ldarg_2)
            {
                stack.Insert(0, paramIsRef[isInstance ? 1 : 2]?.With(instruction));
                return;
            }

            if (instruction.opcode == OpCodes.Ldarg_3)
            {
                stack.Insert(0, paramIsRef[isInstance ? 2 : 3]?.With(instruction));
                return;
            }

            if (instruction.opcode == OpCodes.Ldarg)
            {
                stack.Insert(0, paramIsRef[(int) instruction.operand - (isInstance ? 1 : 0)]?.With(instruction));
                return;
            }

            if (instruction.opcode == OpCodes.Ldarg_S)
            {
                stack.Insert(0, paramIsRef[(int) instruction.operand - (isInstance ? 1 : 0)]?.With(instruction));
                return;
            }

            if (instruction.opcode == OpCodes.Ldarga)
            {
                stack.Insert(0, null); // TODO take type from operand
                return;
            }

            if (instruction.opcode == OpCodes.Ldarga_S)
            {
                stack.Insert(0, null); // TODO take type from operand
                return;
            }

            if (instruction.opcode == OpCodes.Ldloc ||
                instruction.opcode == OpCodes.Ldloc_0 ||
                instruction.opcode == OpCodes.Ldloc_1 ||
                instruction.opcode == OpCodes.Ldloc_2 ||
                instruction.opcode == OpCodes.Ldloc_3 ||
                instruction.opcode == OpCodes.Ldloc_S)
            {
                var index = 0;
                if (instruction.opcode == OpCodes.Ldloc) index = ((LocalBuilder) instruction.operand).LocalIndex;
                if (instruction.opcode == OpCodes.Ldloc_0) index = 0;
                if (instruction.opcode == OpCodes.Ldloc_1) index = 1;
                if (instruction.opcode == OpCodes.Ldloc_2) index = 2;
                if (instruction.opcode == OpCodes.Ldloc_3) index = 3;
                if (instruction.opcode == OpCodes.Ldloc_S) index = ((LocalBuilder) instruction.operand).LocalIndex;

                var local = LocalVariables[index];

                stack.Insert(0, new StackRefTypeInfo(
                    instruction, local.LocalType, $"local_{local.LocalIndex}"
                ));
                return;
            }

            if (instruction.opcode == OpCodes.Ldloca ||
                instruction.opcode == OpCodes.Ldloca_S)
            {
                var index = ((LocalBuilder) instruction.operand).LocalIndex;
                var local = LocalVariables[index];
                var locType = local.LocalType;
                if (!locType.IsValueType)
                {
                    locType = locType.MakeByRefType();
                }

                stack.Insert(0, new StackRefTypeInfo(
                    instruction, locType, $"local_{local.LocalIndex}"
                ));
                return;
            }

            if (instruction.opcode == OpCodes.Ldflda)
            {
                var field = (FieldInfo) instruction.operand;
                stack.Insert(0, new StackRefTypeInfo(
                    instruction,
                    field.FieldType,
                    field.Name
                ));
                return;
            }

            if (instruction.opcode.FlowControl == FlowControl.Call)
            {
                if (instruction.operand is MethodInfo target)
                {
                    if (target.ReturnType.FullName == "System.Void" && instruction.opcode != OpCodes.Newobj)
                        return;
                    stack.Insert(0, new StackRefTypeInfo(
                        instruction,
                        target.ReturnType,
                        "unknown"
                    ));
                }

                return;
            }

            switch (instruction.opcode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    break;
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Varpush: // ?
                    stack.Insert(0, null);
                    break;
                case StackBehaviour.Pushref: // ?
                    stack.Insert(0, null);
                    break;
                case StackBehaviour.Push1_push1:
                    stack.Insert(0, null);
                    stack.Insert(0, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void SwapLabels(CodeInstruction a, CodeInstruction b)
        {
            var tmp = a.labels;
            a.labels = b.labels;
            b.labels = tmp;
        }

        internal static IEnumerable<CodeInstruction> Transpile(MethodBase orig, ILGenerator ilGen,
            IEnumerable<CodeInstruction> instructions)
        {
            var patcher = new MethodPatcher(orig, ilGen, instructions);
            patcher.Patch();
            return patcher.EmittedInstructions;
        }
    }
}
