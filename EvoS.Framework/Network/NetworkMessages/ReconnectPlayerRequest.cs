// ROGUES
// SERVER
using EvoS.Framework.Network.Unity;
using System;

// server-only, missing in reactor
[Serializable]
public class ReconnectPlayerRequest : AllianceMessageBase
{
	public long AccountId;

	public long NewSessionId;

	public override void Serialize(NetworkWriter writer)
	{
		base.Serialize(writer);
		writer.Write(AccountId);
		writer.Write(NewSessionId);
	}

	public override void Deserialize(NetworkReader reader)
	{
		base.Deserialize(reader);
		AccountId = reader.ReadInt64();
		NewSessionId = reader.ReadInt64();
	}
}