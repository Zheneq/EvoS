using EvoS.Framework.Network;

namespace EvoS.Framework.Constants.Enums
{
    // Client does not distinguish PICK_BANS1/PICK_BANS2 and PICK_FREELANCER1/PICK_FREELANCER2
    [EvosMessage(184)]
    public enum FreelancerResolutionPhaseSubType
    {
        UNDEFINED,
        DUPLICATE_FREELANCER,
        WAITING_FOR_ALL_PLAYERS,
        PICK_BANS1,
        PICK_FREELANCER1,
        PICK_BANS2,
        PICK_FREELANCER2,
        FREELANCER_TRADE
    }

}
