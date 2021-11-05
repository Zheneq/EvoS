using CentralServer.LobbyServer.Character;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using System;
using System.Collections.Generic;
using System.Text;

namespace CentralServer.LobbyServer.Session
{
    public static class SessionManager
    {
        private static Dictionary<long, LobbyPlayerInfo> ActivePlayers = new Dictionary<long, LobbyPlayerInfo>();// key: AccountID
        private static Dictionary<long, long> SessionTokenAccountIDCache = new Dictionary<long, long>(); // key: SessionToken, value: AccountID
        private static Dictionary<long, LobbyServerProtocol> ActiveConnections = new Dictionary<long, LobbyServerProtocol>();
        private static long GeneratedSessionToken = 0;


        public static LobbyPlayerInfo OnPlayerConnect(LobbyServerProtocol client, RegisterGameClientRequest clientRequest)
        {
            long sessionToken = GeneratedSessionToken++;
            Database.Account user = Database.Account.GetByUserName(clientRequest.AuthInfo.Handle);
            user.AccountId = clientRequest.AuthInfo.AccountId;

            client.AccountId = user.AccountId;
            client.SessionToken = sessionToken;
            client.UserName = user.UserName;
            client.SelectedGameType = user.LastSelectedGameType;
            client.SelectedSubTypeMask = 0;

            LobbyPlayerInfo playerInfo = new LobbyPlayerInfo
            {
                AccountId = user.AccountId,
                BannerID = user.BannerID,
                BotCanTaunt = false,
                BotsMasqueradeAsHumans = false,
                CharacterInfo = CharacterManager.GetCharacterInfo(user.AccountId, user.LastCharacter),
                ControllingPlayerId = 0,
                EffectiveClientAccessLevel = ClientAccessLevel.Full,
                EmblemID = user.EmblemID,
                Handle = user.UserName,
                IsGameOwner = true,
                IsLoadTestBot = false,
                IsNPCBot = false,
                PlayerId = 0,
                ReadyState = ReadyState.Unknown,
                ReplacedWithBots = false,
                RibbonID = user.RibbonID,
                TitleID = user.TitleID,
                TitleLevel = 1
            };

            ActivePlayers.Add(user.AccountId, playerInfo);
            SessionTokenAccountIDCache.Add(sessionToken, playerInfo.AccountId);
            ActiveConnections.Add(user.AccountId, client);

            return playerInfo;
        }

        public static void OnPlayerDisconnect(LobbyServerProtocol client)
        {
            ActivePlayers.Remove(client.AccountId);
            SessionTokenAccountIDCache.Remove(client.SessionToken);
            ActiveConnections.Remove(client.AccountId);
        }

        public static LobbyPlayerInfo GetPlayerInfo(long accountId)
        {
            LobbyPlayerInfo playerInfo = null;
            ActivePlayers.TryGetValue(accountId, out playerInfo);

            return playerInfo;
        }

        public static long GetAccountIdOf(long sessionToken)
        {
            if (SessionTokenAccountIDCache.TryGetValue(sessionToken, out long accountId))
            {
                return accountId;
            }

            return 0;
        }

        public static LobbyServerProtocol GetClientConnection(long accountId)
        {
            LobbyServerProtocol clientConnection = null;
            ActiveConnections.TryGetValue(accountId, out clientConnection);
            return clientConnection;
        }
    }
}
