using System;
using System.Diagnostics;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkBehaviours;

namespace EvoS.Framework.Misc
{
    public class CaptureTheFlag
    {
        public void ExecuteClientGameModeEvent(ClientGameModeEvent gameModeEvent)
        {
            throw new NotImplementedException();
        }

        public enum CTF_VictoryCondition
        {
            TeamMustBeHoldingFlag,
            TeamMustNotBeHoldingFlag,
            OtherTeamMustBeHoldingFlag,
            OtherTeamMustNotBeHoldingFlag,
            TeamMustHaveCapturedFlag,
            TeamMustNotHaveCapturedFlag,
            OtherTeamMustHaveCapturedFlag,
            OtherTeamMustNotHaveCapturedFlag,
        }

        public enum TurninRegionState
        {
            Active,
            Locked,
            Disabled,
        }

        public enum TurninType
        {
            FlagHolderMovingIntoCaptureRegion,
            FlagHolderEndingTurnInCaptureRegion,
            FlagHolderSpendingWholeTurnInCaptureRegion,
            CaptureRegionActivatingUnderFlagHolder,
        }

        public enum RelationshipToClient
        {
            Neutral,
            Friendly,
            Hostile,
        }
    }
}
