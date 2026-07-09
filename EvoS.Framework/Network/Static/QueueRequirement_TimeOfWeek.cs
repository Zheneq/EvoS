using System;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(789)]
    public class QueueRequirement_TimeOfWeek : QueueRequirement
    {
        public TimeSpan Start
        {
            get;
            set;
        }

        public TimeSpan End
        {
            get;
            set;
        }

        public override RequirementType Requirement => RequirementType.TimeOfWeek;

        public override bool AnyGroupMember => false;

        public override void WriteToJson(JsonWriter writer)
        {
            writer.WritePropertyName("Start");
            writer.WriteValue(Start);
            writer.WritePropertyName("End");
            writer.WriteValue(End);
        }

        public static QueueRequirement Create(JsonReader reader)
        {
            reader.Read();
            string s = reader.Value as string;
            reader.Read();
            reader.Read();
            string s2 = reader.Value as string;
            reader.Read();
            QueueRequirement_TimeOfWeek queueRequirement_TimeOfWeek = new QueueRequirement_TimeOfWeek();
            queueRequirement_TimeOfWeek.Start = TimeSpan.Parse(s);
            queueRequirement_TimeOfWeek.End = TimeSpan.Parse(s2);
            return queueRequirement_TimeOfWeek;
        }
    }
}
