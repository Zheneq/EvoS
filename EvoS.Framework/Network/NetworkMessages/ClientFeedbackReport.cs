using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;


namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(687, typeof(ClientFeedbackReport))]
    public class ClientFeedbackReport : WebSocketMessage
    {
        public string Message;
        public ClientFeedbackReport.FeedbackReason Reason;
        public long ReportedPlayerAccountId;
        public string ReportedPlayerHandle;

        [Serializable]
        [EvosMessage(688, typeof(ClientFeedbackReport.FeedbackReason))]
        public enum FeedbackReason
        {
            _,
            Suggestion,
            Bug,
            UnsportsmanlikeConduct,
            Harassment,
            AFKing,
            HateSpeech,
            Feeding,
            Spam,
            OffensiveName,
            Other,
            Botting
        }
    }
}
