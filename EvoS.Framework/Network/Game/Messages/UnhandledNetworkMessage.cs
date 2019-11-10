using System;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    public class UnhandledNetworkMessage : MessageBase
    {
        public byte[] Payload;

        public override void Serialize(NetworkWriter writer)
        {
            writer.WriteBytesFull(Payload);
        }

        public override void Deserialize(NetworkReader reader)
        {
            Payload = reader.ReadBytes((int) (reader.Length - reader.Position));
        }

        public override string ToString()
        {
            return $"{GetType().Name}(" +
                   $"{nameof(Payload)}: {Convert.ToBase64String(Payload)}" +
                   ")";
        }
    }
}
