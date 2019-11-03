using System;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Cmd
{
    public class UnknownCmd : BaseCmd
    {
        public int Hash;
        public string Name;
        public byte[] Payload { get; set; }

        public override void Deserialize(NetworkReader reader, GameObject context)
        {
            Payload = reader.ReadBytes((int) (reader.Length - reader.Position));
        }

        public override string ToString()
        {
            return $"{nameof(UnknownCmd)}(" +
                   $"{nameof(NetId)}: {NetId.Value}, " +
                   (Name != null
                       ? $"{nameof(Name)}: {Name}, "
                       : $"{nameof(Hash)}: {HashResolver.LookupCmd(Hash)}, ") +
                   $"{nameof(Payload)}: {Convert.ToBase64String(Payload)}" +
                   ")";
        }
    }
}
