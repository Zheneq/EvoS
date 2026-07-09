using System;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(792)]
    public class QueueRequirement_MaxLeavingPoints : QueueRequirement
    {
        private bool m_anyGroupMember;

        public float MaxValue { get; set; }

        public override bool AnyGroupMember => m_anyGroupMember;

        public override RequirementType Requirement => RequirementType.MaxLeavingPoints;


        public override void WriteToJson(JsonWriter writer)
        {
            writer.WritePropertyName("MaxValue");
            writer.WriteValue(MaxValue);
            writer.WritePropertyName("AnyGroupMember");
            writer.WriteValue(AnyGroupMember.ToString());
        }

        public static QueueRequirement Create(JsonReader reader)
        {
            QueueRequirement_MaxLeavingPoints queueRequirement_MaxLeavingPoints = new QueueRequirement_MaxLeavingPoints();
            reader.Read();
            queueRequirement_MaxLeavingPoints.MaxValue = float.Parse(reader.Value.ToString());
            reader.Read();
            if (reader.TokenType == JsonToken.PropertyName && reader.Value != null && reader.Value.ToString() == "AnyGroupMember")
            {
                reader.Read();
                queueRequirement_MaxLeavingPoints.m_anyGroupMember = bool.Parse(reader.Value.ToString());
                reader.Read();
            }
            else
            {
                queueRequirement_MaxLeavingPoints.m_anyGroupMember = false;
            }
            return queueRequirement_MaxLeavingPoints;
        }
    }
}
