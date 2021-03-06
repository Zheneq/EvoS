﻿using EvoS.Framework.Network.NetworkMessages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace EvoS.LobbyServer.NetworkMessageHandlers
{
    class GroupInviteRequestHandler : IEvosNetworkMessageHandler
    {
        public async Task OnMessage(LobbyServerConnection connection, object requestData)
        {
            var request = (GroupInviteRequest)requestData;
            var response = new GroupInviteResponse() { ResponseId = request.RequestId, FriendHandle = request.FriendHandle };
            connection.SendMessage(response);

            LobbyServerConnection user = LobbyServer.GetPlayerByHandle(request.FriendHandle);
            await user.SendMessage(new GroupConfirmationRequest()
            {
                //LeaderFullHandle = connection.PlayerInfo.GetHandle(),
                //LeaderName = connection.PlayerInfo.GetHandle(),
                ConfirmationNumber = 1234,
                ExpirationTime = TimeSpan.FromMinutes(1),
                GroupId = 508, // TODO
                //JoinerAccountId = connection.PlayerInfo.GetAccountId(),
                //JoinerName = connection.PlayerInfo.GetHandle(),
                RequestId = 0,
                ResponseId = 0,
                Type = GroupConfirmationRequest.JoinType.Unicode000E
            });
        }
    }
}
