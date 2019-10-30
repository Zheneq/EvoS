using System;

namespace EvoS.PacketAnalysis.Rpc
{
    public class RpcAttribute : Attribute
    {
        public int Hash;

        public RpcAttribute(int hash)
        {
            Hash = hash;
        }
    }
}
