using System;
using EvoS.Framework.Constants.Enums;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(794)]
    public class QueueRequirement_Environement : QueueRequirement
    {
        private EnvironmentType Environment;

        public override bool AnyGroupMember => false;

        public override RequirementType Requirement => RequirementType.ProhibitEnvironment;

        public override void WriteToJson(JsonWriter writer)
        {
            writer.WritePropertyName("EnvironmentType");
            writer.WriteValue(Environment.ToString());
        }

        public static QueueRequirement Create(JsonReader reader)
        {
            QueueRequirement_Environement queueRequirement_Environement = new QueueRequirement_Environement();
            reader.Read();
            string value = reader.Value.ToString();
            queueRequirement_Environement.Environment = (EnvironmentType)Enum.Parse(typeof(EnvironmentType), value, true);
            reader.Read();
            return queueRequirement_Environement;
        }
    }
}
