// ROGUES
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Unity;
using System;

// Custom AdminShutdownGame
[Serializable]
public class AdminShutdownGameRequest : AllianceMessageBase
{
    public GameResult GameResult;

    public override void Serialize(NetworkWriter writer)
    {
        base.Serialize(writer);
        writer.Write((int)GameResult);
    }
}
