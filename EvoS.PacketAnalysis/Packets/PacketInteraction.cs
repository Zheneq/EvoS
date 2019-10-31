using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Packets
{
    public class PacketInteraction : InstrumentCallbacks
    {
        public readonly List<PacketInteractionCall> Interactions = new List<PacketInteractionCall>();
        private Stack<PacketInteractionCall> _eventStack = new Stack<PacketInteractionCall>();
        private WeakReference<NetworkReader> _reader = new WeakReference<NetworkReader>(null);

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

        public override void OnEnter(NetworkReader reader, string className, string methodName)
        {
            base.OnEnter(reader, className, methodName);
            _eventStack.TryPeek(out var parent);
            
            if (reader != null)
                _reader.SetTarget(reader);
            else
                _reader.TryGetTarget(out reader);
            
            _eventStack.Push(new PacketInteractionCall(parent, className, methodName));
            if (parent == null)
                Interactions.Add(_eventStack.Peek());
            _eventStack.Peek().PositionOnEnter = (int) reader.Position;
        }

        public override void OnLeave(string className, string methodName)
        {
            base.OnLeave(className, methodName);
            _reader.TryGetTarget(out var reader);

            var top = _eventStack.Pop();
            top.PositionOnLeave = (int) reader.Position;
        }
    }
}
