using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(serverMsgIds: new short[] {52})]
    public class LoginResponse : MessageBase
    {
        public bool Success;
        public bool Reconnecting;
        public string ErrorMessage;
        public uint LastReceivedMsgSeqNum;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(Success);
            writer.Write(Reconnecting);
            writer.Write(ErrorMessage);
            writer.WritePackedUInt32(LastReceivedMsgSeqNum);
        }

        public override void Deserialize(NetworkReader reader)
        {
            Success = reader.ReadBoolean();
            Reconnecting = reader.ReadBoolean();
            ErrorMessage = reader.ReadString();
            LastReceivedMsgSeqNum = reader.ReadPackedUInt32();
        }

        public override string ToString()
        {
            return $"{nameof(LoginResponse)}(" +
                   $"{nameof(Success)}: {Success}, " +
                   $"{nameof(Reconnecting)}: {Reconnecting}, " +
                   $"{nameof(ErrorMessage)}: {ErrorMessage}, " +
                   $"{nameof(LastReceivedMsgSeqNum)}: {LastReceivedMsgSeqNum}" +
                   ")";
        }
    }
}
