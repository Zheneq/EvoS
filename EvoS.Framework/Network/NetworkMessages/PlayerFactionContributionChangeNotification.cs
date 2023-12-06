using EvoS.Framework.Network.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(426)]
    public class PlayerFactionContributionChangeNotification : WebSocketMessage
    {
        public int CompetitionId;

        public int FactionId;

        public int AmountChanged;

        public int TotalXP;

        public long AccountID;
    }
}
