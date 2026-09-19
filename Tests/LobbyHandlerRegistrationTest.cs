using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class LobbyHandlerRegistrationTest : EvosTest
{
    public LobbyHandlerRegistrationTest(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void RegisteredHandlersMatchWireContract()
    {
        // This is the wire contract. If this test fails, a handler registration was added,
        // removed, or lost in a refactor — update the list only for deliberate contract changes.
        var expected = new[]
        {
            "EvoS.Framework.Network.NetworkMessages.BalancedTeamRequest",
            "EvoS.Framework.Network.NetworkMessages.CalculateFreelancerStatsRequest",
            "EvoS.Framework.Network.NetworkMessages.ChatNotification",
            "EvoS.Framework.Network.NetworkMessages.CheckAccountStatusRequest",
            "EvoS.Framework.Network.NetworkMessages.CheckRAFStatusRequest",
            "EvoS.Framework.Network.NetworkMessages.ClientErrorSummary",
            "EvoS.Framework.Network.NetworkMessages.ClientFeedbackReport",
            "EvoS.Framework.Network.NetworkMessages.ClientStatusReport",
            "EvoS.Framework.Network.NetworkMessages.CrashReportArchiveNameRequest",
            "EvoS.Framework.Network.NetworkMessages.CreateGameRequest",
            "EvoS.Framework.Network.NetworkMessages.CustomKeyBindNotification",
            "EvoS.Framework.Network.NetworkMessages.EvosOptionsNotification",
            "EvoS.Framework.Network.NetworkMessages.EvosOptionsNotificationLegacy",
            "EvoS.Framework.Network.NetworkMessages.FriendUpdateRequest",
            "EvoS.Framework.Network.NetworkMessages.GameInfoUpdateRequest",
            "EvoS.Framework.Network.NetworkMessages.GameInvitationRequest",
            "EvoS.Framework.Network.NetworkMessages.GameInviteConfirmationResponse",
            "EvoS.Framework.Network.NetworkMessages.GroupChatRequest",
            "EvoS.Framework.Network.NetworkMessages.GroupConfirmationResponse",
            "EvoS.Framework.Network.NetworkMessages.GroupInviteRequest",
            "EvoS.Framework.Network.NetworkMessages.GroupJoinRequest",
            "EvoS.Framework.Network.NetworkMessages.GroupKickRequest",
            "EvoS.Framework.Network.NetworkMessages.GroupLeaveRequest",
            "EvoS.Framework.Network.NetworkMessages.GroupPromoteRequest",
            "EvoS.Framework.Network.NetworkMessages.GroupSuggestionResponse",
            "EvoS.Framework.Network.NetworkMessages.JoinGameRequest",
            "EvoS.Framework.Network.NetworkMessages.JoinMatchmakingQueueRequest",
            "EvoS.Framework.Network.NetworkMessages.LeaveGameRequest",
            "EvoS.Framework.Network.NetworkMessages.LeaveMatchmakingQueueRequest",
            "EvoS.Framework.Network.NetworkMessages.LoadingScreenToggleRequest",
            "EvoS.Framework.Network.NetworkMessages.OptionsNotification",
            "EvoS.Framework.Network.NetworkMessages.PaymentMethodsRequest",
            "EvoS.Framework.Network.NetworkMessages.PlayerGroupInfoUpdateRequest",
            "EvoS.Framework.Network.NetworkMessages.PlayerInfoUpdateRequest",
            "EvoS.Framework.Network.NetworkMessages.PlayerMatchDataRequest",
            "EvoS.Framework.Network.NetworkMessages.PlayerPanelUpdatedNotification",
            "EvoS.Framework.Network.NetworkMessages.PlayerUpdateStatusRequest",
            "EvoS.Framework.Network.NetworkMessages.PreviousGameInfoRequest",
            "EvoS.Framework.Network.NetworkMessages.PricesRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseAbilityVfxRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseBannerBackgroundRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseBannerForegroundRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseChatEmojiRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseInventoryItemRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseLoadoutSlotRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseModRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseTauntRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseTintRequest",
            "EvoS.Framework.Network.NetworkMessages.PurchaseTitleRequest",
            "EvoS.Framework.Network.NetworkMessages.RankedLeaderboardOverviewRequest",
            "EvoS.Framework.Network.NetworkMessages.RegisterGameClientRequest",
            "EvoS.Framework.Network.NetworkMessages.RejoinGameRequest",
            "EvoS.Framework.Network.NetworkMessages.SelectBannerRequest",
            "EvoS.Framework.Network.NetworkMessages.SelectRibbonRequest",
            "EvoS.Framework.Network.NetworkMessages.SelectTitleRequest",
            "EvoS.Framework.Network.NetworkMessages.SendRAFReferralEmailsRequest",
            "EvoS.Framework.Network.NetworkMessages.SetDevTagRequest",
            "EvoS.Framework.Network.NetworkMessages.SetGameSubTypeRequest",
            "EvoS.Framework.Network.NetworkMessages.SetRegionRequest",
            "EvoS.Framework.Network.NetworkMessages.StoreOpenedMessage",
            "EvoS.Framework.Network.NetworkMessages.SubscribeToCustomGamesRequest",
            "EvoS.Framework.Network.NetworkMessages.UIActionNotification",
            "EvoS.Framework.Network.NetworkMessages.UnsubscribeFromCustomGamesRequest",
            "EvoS.Framework.Network.NetworkMessages.UpdateRemoteCharacterRequest",
            "EvoS.Framework.Network.NetworkMessages.UpdateUIStateRequest",
            "EvoS.Framework.Network.NetworkMessages.UseGGPackRequest",
            "EvoS.Framework.Network.NetworkMessages.UseOverconRequest",
            "LobbyGameClientMessages.ClientErrorReport",
            "LobbyGameClientMessages.ClientPerformanceReport",
            "LobbyGameClientMessages.DEBUG_AdminSlashCommandNotification",
            "LobbyGameClientMessages.ErrorReportSummaryResponse",
            "LobbyGameClientMessages.RankedBanRequest",
            "LobbyGameClientMessages.RankedHoverClickRequest",
            "LobbyGameClientMessages.RankedSelectionRequest",
            "LobbyGameClientMessages.RankedTradeRequest",
        };

        var proto = new LobbyServerProtocol(SessionManager.Instance, GroupManager.Instance, GameManager.Instance);
        var actual = proto.RegisteredMessageTypes
            .Select(t => t.FullName)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
