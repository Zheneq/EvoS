using System;
using System.Numerics;

namespace EvoS.Framework.Network.Unity.Messages
{
    [UNetMessage(serverMsgIds: new short[] {3})]
    public class ObjectSpawnMessage : MessageBase
    {
        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            assetId = reader.ReadNetworkHash128();
            position = reader.ReadVector3();
            payload = reader.ReadBytesAndSize();
            uint num = 16u;
            if (reader.Length - reader.Position >= num)
            {
                rotation = reader.ReadQuaternion();
            }
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.Write(assetId);
            writer.Write(position);
            writer.WriteBytesFull(payload);
            writer.Write(rotation);
        }

        public NetworkInstanceId netId;
        public NetworkHash128 assetId;
        public Vector3 position;
        public byte[] payload;
        public Quaternion rotation;

        public override string ToString()
        {
            return $"{nameof(ObjectSpawnMessage)}(" +
                   $"{nameof(netId)}: {netId}, " +
                   $"{nameof(assetId)}: {assetId}, " +
                   $"{nameof(position)}: {position}, " +
                   $"{nameof(rotation)}: {rotation}, " +
                   $"{nameof(payload)}: {(payload != null ? Convert.ToBase64String(payload) : null)}" +
                   ")";
        }
    }
}
