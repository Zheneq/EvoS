using System.Collections.Generic;

namespace EvoS.Framework.Network;

public static class ProtocolVersion
{
    public const string VANILLA = "b486c83d8a8950340936d040e1953493";
    
    public static readonly ISet<string> SUPPORTED_PROTO_VERSIONS = new HashSet<string>
    {
        VANILLA,
        "15e77d6ee51844cc02507b3e73c5aa3c", // 1.4
        "89945cd84ac637ac678c60dd840acbda", // 1.5
    };
}