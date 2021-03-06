﻿using EvoS.Framework.Network.NetworkMessages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace EvoS.LobbyServer.NetworkMessageHandlers
{
    class LeaveMatchmakingQueueRequestHandler : IEvosNetworkMessageHandler
    {
        public async Task OnMessage(LobbyServerConnection connection, object requestData)
        {
            var request = (LeaveMatchmakingQueueRequest)requestData;
            LobbyQueueManager.RemovePlayerFromQueue(connection);
            await connection.SendMessage(new LeaveMatchmakingQueueResponse() { ResponseId = request.RequestId });
        }
    }
}
