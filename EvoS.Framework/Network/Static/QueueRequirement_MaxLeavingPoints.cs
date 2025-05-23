using System;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(792)]
    public class QueueRequirement_MaxLeavingPoints : QueueRequirement
    {
        public float MaxValue { get; set; }

        public override bool AnyGroupMember => m_anyGroupMember;

        public override RequirementType Requirement =>
            RequirementType.MaxLeavingPoints;

        private bool m_anyGroupMember;

        public QueueRequirement_MaxLeavingPoints(bool mAnyGroupMember)
        {
            m_anyGroupMember = mAnyGroupMember;
        }
    }
}
