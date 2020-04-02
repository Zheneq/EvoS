using EvoS.Framework.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.LobbyServer.Utils;
using EvoS.Framework.DataAccess;

namespace EvoS.LobbyServer.NetworkMessageHandlers
{
    class RegisterGameClientRequestHandler : IEvosNetworkMessageHandler
    {
        public async Task OnMessage(ClientConnection connection, object requestData)
        {
            RegisterGameClientRequest request = (RegisterGameClientRequest) requestData;
            PlayerData.Player p = PlayerData.GetPlayer(request.SessionInfo.Handle);
            connection.AccountId = p.AccountId;
            connection.UserName = p.UserName;
            connection.SelectedTitleID = p.SelectedTitleID;
            connection.SelectedBackgroundBannerID = p.SelectedBackgroundBannerID;
            connection.SelectedForegroundBannerID = p.SelectedForegroundBannerID;
            connection.SelectedRibbonID = p.SelectedTitleID;
            connection.SelectedCharacter = p.LastSelectedCharacter;

            // Send RegisterGameClientResponse
            await Send_RegisterGameClientResponse(connection, request);

            // Sent LobbyServerReadyNotification
            await Send_LobbyServerReadyNotification(connection);

            // Send "{username} has connected" to global chat
            await Send_ChatConnectedNotification(connection);
        }


        private async Task Send_RegisterGameClientResponse(ClientConnection connection, RegisterGameClientRequest request)
        {
            var response = RegisterGameClient(request);
            await connection.SendMessage(response);
        }

        private async Task Send_LobbyServerReadyNotification(ClientConnection connection)
        {
            var lobbyServerReady = LobbyServerReady(connection);
            await connection.SendMessage(lobbyServerReady);
        }

        private async Task Send_ChatConnectedNotification(ClientConnection connection)
        {
            ChatNotification connectedMessage = new ChatNotification() { Text = $"{connection.UserName} has connected", ConsoleMessageType = ConsoleMessageType.SystemMessage };
            await Program.sendChatToAll(connectedMessage);
        }


        private LobbyServerReadyNotification LobbyServerReady(ClientConnection connection)
        {
            return new LobbyServerReadyNotification
            {
                AccountData = PlayerUtils.GetAccountData(connection),
                AlertMissionData = new LobbyAlertMissionDataNotification(),
                CharacterDataList = DummyLobbyData.CreateCharacterDataList(),
                CommerceURL = "http://127.0.0.1/AtlasCommerce",
                EnvironmentType = EnvironmentType.External,
                FactionCompetitionStatus = new FactionCompetitionNotification(),
                FriendStatus = new FriendStatusNotification {FriendList = FriendListUtils.GetFriendList(connection.AccountId)},
                GroupInfo = new LobbyPlayerGroupInfo
                {
                    SelectedQueueType = GameType.Practice,
                    MemberDisplayName = connection.UserName,
                    // ChararacterInfo = DummyLobbyData.CreateLobbyCharacterInfo(CharacterType.Archer),
                    Members = new List<UpdateGroupMemberData>()
                },
                SeasonChapterQuests =
                    new LobbySeasonQuestDataNotification
                    {
                        SeasonChapterQuests = new Dictionary<int, SeasonChapterQuests>()
                    },
                ServerQueueConfiguration = DummyLobbyData.CreateServerQueueConfiguration(),
                Status = new LobbyStatusNotification
                {
                    AllowRelogin = false,
                    ServerLockState = ServerLockState.Unlocked,
                    ServerMessageOverrides = new ServerMessageOverrides(),
                    ClientAccessLevel = ClientAccessLevel.Full,
                    HasPurchasedGame = true,
                    GameplayOverrides = DummyLobbyData.CreateLobbyGameplayOverrides(),
                    UtcNow = DateTime.UtcNow,
                    PacificNow = DateTime.Now, // TODO: Should be pacific time
                    ErrorReportRate = TimeSpan.FromMinutes(3)
                }
            };
        }

        private static RegisterGameClientResponse RegisterGameClient(RegisterGameClientRequest request)
        {
            RegisterGameClientResponse response = new RegisterGameClientResponse();

            response.SessionInfo = request.SessionInfo;
            response.SessionInfo.ConnectionAddress = "127.0.0.1";
            response.SessionInfo.LanguageCode = "EN";
            response.AuthInfo = request.AuthInfo;
            response.AuthInfo.AccountId = request.SessionInfo.AccountId; // Override AuthInfo.AccountId with SessionInfo.AccountID, The account id from SessionInfo is set in the DirectoryServer and has the accountid value from database for the client username
            response.DevServerConnectionUrl = "127.0.0.1"; // What is this?
            response.AuthInfo.AccountStatus = null;
            response.Status = new LobbyStatusNotification
            {
                AllowRelogin = false,
                ClientAccessLevel = ClientAccessLevel.Full,
                ConnectionQueueInfo = null,
                ErrorReportRate = TimeSpan.FromMinutes(3),
                GameplayOverrides = null,
                HasPurchasedGame = true,
                HighestPurchasedGamePack = 0,
                LocalizedFailure = null,
                PacificNow = DateTime.UtcNow, // TODO: Originally had "0001-01-01T00:00:00"
                ServerLockState = ServerLockState.Unlocked,
                ServerMessageOverrides = new ServerMessageOverrides().FillDummyData(),
                TimeOffset = TimeSpan.Zero,
                UtcNow = DateTime.UtcNow, // TODO: Originally had "0001-01-01T00:00:00"
                RequestId = 0,
                ResponseId = 0,
            };
            response.LocalizedFailure = null;
            response.RequestId = 0;
            response.ResponseId = request.RequestId;
            return response;
        }
    }
}
