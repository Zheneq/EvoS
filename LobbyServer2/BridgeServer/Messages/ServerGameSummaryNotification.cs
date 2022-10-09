using System;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

[Serializable]
public class ServerGameSummaryNotification : AllianceMessageBase
{
    internal LobbyGameSummary GameSummary;
    internal LobbyGameSummaryOverrides GameSummaryOverrides;

    public override void Serialize(NetworkWriter writer)
    {
        base.Serialize(writer);
        SerializeObject(GameSummary, writer);
        SerializeObject(GameSummaryOverrides, writer);
    }
    
    public override void Deserialize(NetworkReader reader)
    {
        base.Deserialize(reader);
        DeserializeObject(out GameSummary, reader);
        DeserializeObject(out GameSummaryOverrides, reader);
    }
}