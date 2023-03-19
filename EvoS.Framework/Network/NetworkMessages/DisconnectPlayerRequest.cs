
// server-only, missing in reactor
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using System;

[Serializable]
public class DisconnectPlayerRequest : AllianceMessageBase
{
	public LobbySessionInfo SessionInfo;
	public LobbyServerPlayerInfo PlayerInfo;
	public GameResult GameResult;

	public override void Deserialize(NetworkReader reader)
	{
		base.Deserialize(reader);
		AllianceMessageBase.DeserializeObject<LobbySessionInfo>(out this.SessionInfo, reader);
		AllianceMessageBase.DeserializeObject<LobbyServerPlayerInfo>(out this.PlayerInfo, reader);
		this.GameResult = (GameResult)reader.ReadByte();
	}

	public override void Serialize(NetworkWriter writer)
	{
		base.Serialize(writer);
		SerializeObject(SessionInfo, writer);
		SerializeObject(PlayerInfo, writer);
		SerializeObject(GameResult, writer);
	}
}