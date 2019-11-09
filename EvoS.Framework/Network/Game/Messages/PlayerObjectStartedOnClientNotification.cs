using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(clientMsgIds: new short[] {65, 55})]
    public class PlayerObjectStartedOnClientNotification : MessageBase
    {
        public long AccountId;
        public int PlayerId;

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt64((ulong) AccountId);
            writer.WritePackedUInt32((uint) PlayerId);
        }

        public override void Deserialize(NetworkReader reader)
        {
            AccountId = (long) reader.ReadPackedUInt64();
            PlayerId = (int) reader.ReadPackedUInt32();
        }

        public override string ToString()
        {
            return $"{nameof(PlayerObjectStartedOnClientNotification)}(" +
                   $"{nameof(AccountId)}: {AccountId}, " +
                   $"{nameof(PlayerId)}: {PlayerId}" +
                   ")";
        }
    }
}
