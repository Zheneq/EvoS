using System;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Rpc
{
    public class UnknownRpc : BaseRpc
    {
        public int Hash;
        public string Name;
        public byte[] Payload { get; set; }

        public override void Deserialize(NetworkReader reader)
        {
            Payload = reader.ReadBytes((int) (reader.Length - reader.Position));
        }

        public override string ToString()
        {
            return $"{nameof(UnknownRpc)}(" +
                   (Name != null
                       ? $"{nameof(Name)}: {Name}, "
                       : $"{nameof(Hash)}: {HashResolver.LookupRpc(Hash)}, ") +
                   $"{nameof(Payload)}: {Convert.ToBase64String(Payload)}" +
                   ")";
        }
    }
}
