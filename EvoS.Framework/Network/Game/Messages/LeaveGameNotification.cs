using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(clientMsgIds: new short[]{67})]
    public class LeaveGameNotification : MessageBase
    {
        public int PlayerId;
        public bool IsPermanent;
        public GameResult GameResult;

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint) PlayerId);
            writer.Write(IsPermanent);
            writer.Write((int) GameResult);
        }

        public override void Deserialize(NetworkReader reader)
        {
            PlayerId = (int) reader.ReadPackedUInt32();
            IsPermanent = reader.ReadBoolean();
            GameResult = (GameResult) reader.ReadInt32();
        }

        public override string ToString()
        {
            return $"{nameof(LeaveGameNotification)}(" +
                   $"{nameof(PlayerId)}: {PlayerId}, " +
                   $"{nameof(IsPermanent)}: {IsPermanent}, " +
                   $"{nameof(GameResult)}: {GameResult}" +
                   ")";
        }
    }
}
