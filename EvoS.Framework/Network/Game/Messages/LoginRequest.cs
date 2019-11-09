using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(clientMsgIds: new short[]{51})]
    public class LoginRequest : MessageBase
    {
        public string AccountId;
        public string SessionToken;
        public int PlayerId;
        public uint LastReceivedMsgSeqNum;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(AccountId);
            writer.Write(SessionToken);
            writer.WritePackedUInt32((uint) PlayerId);
            writer.WritePackedUInt32(LastReceivedMsgSeqNum);
        }

        public override void Deserialize(NetworkReader reader)
        {
            AccountId = reader.ReadString();
            SessionToken = reader.ReadString();
            PlayerId = (int) reader.ReadPackedUInt32();
            LastReceivedMsgSeqNum = reader.ReadPackedUInt32();
        }

        public override string ToString()
        {
            return $"{nameof(LoginRequest)}(" +
                   $"{nameof(AccountId)}: {AccountId}, " +
                   $"{nameof(SessionToken)}: {SessionToken}, " +
                   $"{nameof(PlayerId)}: {PlayerId}, " +
                   $"{nameof(LastReceivedMsgSeqNum)}: {LastReceivedMsgSeqNum}" +
                   ")";
        }
    }
}
