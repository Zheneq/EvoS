using System;

namespace EvoS.Framework.Network.Unity.Messages
{
    [UNetMessage(serverMsgIds: new short[] {8}, clientMsgIds:new short[]{8})]
    public class ObjectUpdateMessage : MessageBase
    {
        public NetworkInstanceId NetId;
        public byte[] Payload { get; set; }

        public override void Deserialize(NetworkReader reader)
        {
            NetId = reader.ReadNetworkId();
            Payload = reader.ReadBytes((int) (reader.Length - reader.Position));
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(NetId);
            writer.WriteBytesFull(Payload);
        }

        public override string ToString()
        {
            return $"{nameof(ObjectUpdateMessage)}(" +
                   $"{nameof(NetId)}: {NetId}, " +
                   $"{nameof(Payload)}: {Convert.ToBase64String(Payload)}" +
                   ")";
        }
    }
}
