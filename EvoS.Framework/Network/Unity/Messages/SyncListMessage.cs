namespace EvoS.Framework.Network.Unity.Messages
{
    [UNetMessage(serverMsgIds: new short[] {9}, clientMsgIds:new short[]{9})]
    public class SyncListMessage : MessageBase
    {
        public NetworkInstanceId NetId;
        public int Hash;
        public byte[] Payload { get; set; }

        public override void Deserialize(NetworkReader reader)
        {
            NetId = reader.ReadNetworkId();
            Hash = (int) reader.ReadPackedUInt32();
            Payload = reader.ReadBytes((int) (reader.Length - reader.Position));
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(NetId);
            writer.WritePackedUInt32((uint) Hash);
            writer.WriteBytesFull(Payload);
        }
    }
}
