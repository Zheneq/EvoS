using System;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(795)]
    public class QueueRequirement_DateTime : QueueRequirement
    {
        private RequirementType m_requirementType;

        private DateTime m_dateTime;

        public override bool AnyGroupMember => false;

        public override RequirementType Requirement => m_requirementType;

        public override void WriteToJson(JsonWriter writer)
        {
            writer.WritePropertyName("Value");
            writer.WriteValue(m_dateTime);
        }

        public static QueueRequirement Create(RequirementType reqType, JsonReader reader)
        {
            QueueRequirement_DateTime queueRequirement_DateTime = new QueueRequirement_DateTime();
            queueRequirement_DateTime.m_requirementType = reqType;
            reader.Read();
            if (reader.TokenType == JsonToken.Date)
            {
                queueRequirement_DateTime.m_dateTime = (DateTime)reader.Value;
                reader.Read();
            }
            else
            {
                string s = reader.Value as string;
                queueRequirement_DateTime.m_dateTime = DateTime.Parse(s);
                reader.Read();
            }
            return queueRequirement_DateTime;
        }
    }
}
