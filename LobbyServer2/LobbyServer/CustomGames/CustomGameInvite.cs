namespace CentralServer.LobbyServer.CustomGames;

public class CustomGameInvite(
    int inviteId,
    long inviterAccountId,
    long inviteeAccountId,
    string processCode,
    int initialRequestId)
{
    public readonly int InviteId = inviteId; // sent to the invitee as GameInviteConfirmationRequest.RequestId
    public readonly long InviterAccountId = inviterAccountId;
    public readonly long InviteeAccountId = inviteeAccountId;
    public readonly string ProcessCode = processCode;
    public readonly int InitialRequestId = initialRequestId; // inviter's GameInvitationRequest.RequestId
}
