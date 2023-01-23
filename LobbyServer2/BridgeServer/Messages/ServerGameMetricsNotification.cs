using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;
using System;
using System.Collections.Generic;
using System.Text;


[Serializable]
public class ServerGameMetricsNotification : AllianceMessageBase
{
    public ServerGameMetrics GameMetrics;

    public override void Serialize(NetworkWriter writer)
    {
        base.Serialize(writer);
        SerializeObject(GameMetrics, writer);
    }


    public override void Deserialize(NetworkReader reader)
    {
        base.Deserialize(reader);
        DeserializeObject(out GameMetrics, reader);
    }
}

