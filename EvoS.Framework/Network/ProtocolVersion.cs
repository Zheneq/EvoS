using System.Collections.Generic;

namespace EvoS.Framework.Network;

public static class ProtocolVersion
{
    public const string VANILLA = "b486c83d8a8950340936d040e1953493";
    public const string V1_5 = "89945cd84ac637ac678c60dd840acbda";
    public const string V1_8 = "9621c1ae243255a865773baf96edc036";

    public static readonly ISet<string> SUPPORTED_PROTO_VERSIONS = new HashSet<string>
    {
        VANILLA,
        V1_5,
        V1_8,
    };
}