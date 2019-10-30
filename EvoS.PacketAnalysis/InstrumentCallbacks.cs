using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Logging;

namespace EvoS.PacketAnalysis
{
    public class InstrumentCallbacks
    {
        public Stack<string> MethodStack = new Stack<string>();
        private string DepthIndent => new string(' ', MethodStack.Count * 2);

        public virtual void OnSetFld(object instance, string field, object value)
        {
            Log.Print(LogType.Debug, $"{DepthIndent}Field set: {instance?.GetType().Name}.{field} = {value}");
        }

        public virtual void OnCallMethod(object instance, string method, object[] value)
        {
            var args = string.Join(", ", value.Select(v => v?.ToString() ?? "null").ToList());
            Log.Print(LogType.Debug, $"{DepthIndent}Method called: {instance?.GetType().Name}.{method}({args})");
        }

        public virtual void OnEnter(string className, string methodName)
        {
            Log.Print(LogType.Debug, $"{DepthIndent}> {className}.{methodName}");
            MethodStack.Push($"{DepthIndent}> {className}.{methodName}");
        }

        public virtual void OnLeave(string className, string methodName)
        {
            MethodStack.Pop();
        }
    }
}
