using System;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(790)]
    public class QueueRequirement_Never : QueueRequirement
    {
        private RequirementType m_requirementType = RequirementType.AdminDisabled;

        public override bool AnyGroupMember => false;

        public override RequirementType Requirement => m_requirementType;

        public static QueueRequirement CreateAdminDisabled()
        {
            QueueRequirement_Never queueRequirement_Never = new QueueRequirement_Never();
            queueRequirement_Never.m_requirementType = RequirementType.AdminDisabled;
            return queueRequirement_Never;
        }

        public override void WriteToJson(JsonWriter writer)
        {
        }

        public static QueueRequirement Create(RequirementType reqType, JsonReader reader)
        {
            QueueRequirement_Never queueRequirement_Never = new QueueRequirement_Never();
            queueRequirement_Never.m_requirementType = reqType;
            return queueRequirement_Never;
        }
    }
}
