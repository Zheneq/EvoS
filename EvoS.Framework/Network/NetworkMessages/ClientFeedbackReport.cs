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
            None,
            Suggestion,
            Bug,
            UnsportsmanlikeConduct,
            VerbalHarassment,
            LeavingTheGameAFK,
            HateSpeech,
            IntentionallyFeeding,
            SpammingAdvertising,
            OffensiveName,
            Other,
            Botting
        }
    }
}
