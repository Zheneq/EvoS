using System;

namespace EvoS.PacketAnalysis.Cmd
{
    public class CmdAttribute : Attribute
    {
        public int Hash;

        public CmdAttribute(int hash)
        {
            Hash = hash;
        }
    }
}
