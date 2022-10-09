using EvoS.Framework.Network.Unity;

public class AllianceResponseBase : AllianceMessageBase
{
	public bool Success;
	public string ErrorMessage;

	public override void Serialize(NetworkWriter writer)
	{
		base.Serialize(writer);
		writer.Write(Success);
		writer.Write(ErrorMessage);
	}

	public override void Deserialize(NetworkReader reader)
	{
		base.Deserialize(reader);
		Success = reader.ReadBoolean();
		ErrorMessage = reader.ReadString();
	}
}
