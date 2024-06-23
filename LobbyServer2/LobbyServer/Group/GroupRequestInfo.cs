using System;
using EvoS.Framework.Network.NetworkMessages;

namespace CentralServer.LobbyServer.Group
{
    public class GroupRequestInfo
    {
        public readonly long RequestId;
        public readonly long RequesterAccountId;
        public readonly long RequesteeAccountId;
        public readonly long GroupId;
        public readonly GroupConfirmationRequest.JoinType JoinType;
        public readonly DateTime Expiration;

        public GroupRequestInfo(
            long requestId,
            long requesterAccountId,
            long requesteeAccountId,
            long groupId,
            GroupConfirmationRequest.JoinType joinType,
            DateTime expiration)
        {
            RequestId = requestId;
            RequesterAccountId = requesterAccountId;
            RequesteeAccountId = requesteeAccountId;
            GroupId = groupId;
            JoinType = joinType;
            Expiration = expiration;
        }

        public bool IsInvitation => JoinType == GroupConfirmationRequest.JoinType.InviteToFormGroup;

        public bool HasExpiredPadded => Expiration.AddSeconds(10) < DateTime.UtcNow;
    }
}