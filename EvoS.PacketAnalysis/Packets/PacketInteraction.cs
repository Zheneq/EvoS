using System;
using System.Collections.Generic;

namespace EvoS.PacketAnalysis.Packets
{
    public class PacketInteraction : InstrumentCallbacks
    {
        public readonly List<PacketInteractionCall> Interactions = new List<PacketInteractionCall>();
        private Stack<PacketInteractionCall> _eventStack = new Stack<PacketInteractionCall>();

        public override void OnSetFld(object instance, string field, object value)
        {
            base.OnSetFld(instance, field, value);

            var top = _eventStack.Peek();
            new PacketInteractionSetFieldEvent(top, field, value);
        }

        public override void OnCallMethod(object instance, string method, object[] value)
        {
            base.OnCallMethod(instance, method, value);

            var top = _eventStack.Peek();
            new PacketInteractionCallSetterLikeEvent(top, method, value);
        }

        public override void OnEnter(string className, string methodName)
        {
            base.OnEnter(className, methodName);

            _eventStack.TryPeek(out var parent);
            _eventStack.Push(new PacketInteractionCall(parent, className, methodName));
            if (parent == null)
                Interactions.Add(_eventStack.Peek());
        }

        public override void OnLeave(string className, string methodName)
        {
            base.OnLeave(className, methodName);

            _eventStack.Pop();
        }
    }
}
