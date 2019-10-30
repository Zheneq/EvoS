using System.Collections.Generic;

namespace EvoS.PacketAnalysis.Packets
{
    public enum PacketInteractionEventType
    {
        CallMethod,
        CallSetterLike,
        SetField
    }

    public abstract class PacketInteractionEvent
    {
        public PacketInteractionEventType Type;
        public PacketInteractionCall Context;

        public PacketInteractionEvent(PacketInteractionEventType type, PacketInteractionCall context)
        {
            Type = type;
            Context = context;

            context?.Events.Add(this);
        }
    }

    public class PacketInteractionCall : PacketInteractionEvent
    {
        public readonly string ClassName;
        public readonly string MethodName;
        public readonly List<PacketInteractionEvent> Events = new List<PacketInteractionEvent>();

        public PacketInteractionCall(PacketInteractionCall context, string className, string methodName)
            : base(PacketInteractionEventType.CallMethod, context)
        {
            ClassName = className;
            MethodName = methodName;
        }
    }

    public class PacketInteractionSetFieldEvent : PacketInteractionEvent
    {
        public string FieldName;
        public object Value;

        public PacketInteractionSetFieldEvent(PacketInteractionCall context, string fieldName, object value)
            : base(PacketInteractionEventType.SetField, context)
        {
            FieldName = fieldName;
            Value = value;
        }
    }

    public class PacketInteractionCallSetterLikeEvent : PacketInteractionEvent
    {
        public string MethodCalled;
        public object[] Args;

        public PacketInteractionCallSetterLikeEvent(PacketInteractionCall context, string methodCalled, object[] args)
            : base(PacketInteractionEventType.CallSetterLike, context)
        {
            MethodCalled = methodCalled;
            Args = args;
        }
    }
}
