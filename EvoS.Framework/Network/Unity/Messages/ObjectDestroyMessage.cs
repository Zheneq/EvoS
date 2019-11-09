namespace EvoS.Framework.Network.Unity.Messages
{
    [UNetMessage(serverMsgIds: new short[] {13})]
    public class ObjectDestroyMessage : MessageBase
    {
        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
        }

        public NetworkInstanceId netId;

        public override string ToString()
        {
            return $"{nameof(ObjectDestroyMessage)}(" +
                   $"{nameof(netId)}: {netId}" +
                   ")";
        }
    }
}
