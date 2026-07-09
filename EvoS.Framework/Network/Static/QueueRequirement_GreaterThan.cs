using System;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(793)]
    public class QueueRequirement_GreaterThan : QueueRequirement
    {
        private RequirementType m_requirementType;
        private bool m_anyGroupMember;

        public int MinValue { get; set; }

        public override bool AnyGroupMember => m_anyGroupMember;

        public override RequirementType Requirement => m_requirementType;
        
        public override void WriteToJson(JsonWriter writer)
        {
            writer.WritePropertyName("MinValue");
            writer.WriteValue(MinValue);
            writer.WritePropertyName("AnyGroupMember");
            writer.WriteValue(AnyGroupMember.ToString());
        }

        public static QueueRequirement Create(RequirementType reqType, JsonReader reader)
        {
            QueueRequirement_GreaterThan queueRequirement_GreaterThan = new QueueRequirement_GreaterThan();
            queueRequirement_GreaterThan.m_requirementType = reqType;
            reader.Read();
            queueRequirement_GreaterThan.MinValue = int.Parse(reader.Value.ToString());
            reader.Read();
            if (reader.TokenType == JsonToken.PropertyName && reader.Value != null && reader.Value.ToString() == "AnyGroupMember")
            {
                reader.Read();
                queueRequirement_GreaterThan.m_anyGroupMember = bool.Parse(reader.Value.ToString());
                reader.Read();
            }
            else
            {
                queueRequirement_GreaterThan.m_anyGroupMember = false;
            }
            return queueRequirement_GreaterThan;
        }
    }
}
