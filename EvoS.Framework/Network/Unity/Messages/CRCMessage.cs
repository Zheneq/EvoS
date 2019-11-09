namespace EvoS.Framework.Network.Unity.Messages
{
    [UNetMessage(serverMsgIds: new short[] {14})]
    public class CRCMessage : MessageBase
    {
        public CRCMessageEntry[] scripts;

        public override void Deserialize(NetworkReader reader)
        {
            scripts = new CRCMessageEntry[reader.ReadUInt16()];
            for (int index = 0; index < scripts.Length; ++index)
                scripts[index] = new CRCMessageEntry
                {
                    name = reader.ReadString(),
                    channel = reader.ReadByte()
                };
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((ushort) scripts.Length);
            for (int index = 0; index < scripts.Length; ++index)
            {
                writer.Write(scripts[index].name);
                writer.Write(scripts[index].channel);
            }
        }

        public override string ToString()
        {
            return $"{nameof(CRCMessage)}(" +
                   $"{nameof(scripts)}: {scripts.Length} entries" +
                   ")";
        }
    }

    public struct CRCMessageEntry
    {
        public string name;
        public byte channel;

        public CRCMessageEntry(string name, byte channel)
        {
            this.name = name;
            this.channel = channel;
        }
    }
}
