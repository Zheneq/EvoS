using System;
using System.Reflection;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Unity;

public class AllianceMessageBase : MessageBase
{
	public const int INVALID_ID = 0;

	public int RequestId;
	public int ResponseId;

	public override void Serialize(NetworkWriter writer)
	{
		writer.Write(this.ResponseId);
	}

	public override void Deserialize(NetworkReader reader)
	{
		this.ResponseId = reader.ReadInt32();
	}

	public virtual void DeserializeNested(NetworkReader reader)
	{
		Log.Print(LogType.Error, "Nested messages must implement DeserializeNested to avoid calling into the base Deserialize");
	}

	public static void SerializeObject(object o, NetworkWriter writer)
	{
		writer.Write(o != null);
		if (o != null)
		{
			o.GetType().GetMethod("Serialize").Invoke(o, new object[]
			{
				writer
			});
		}
	}

	public static void DeserializeObject<T>(out T o, NetworkReader reader)
	{
		if (reader.ReadBoolean())
		{
			ConstructorInfo constructor = typeof(T).GetConstructor(new Type[0]);
			o = (T)((object)constructor.Invoke(new object[0]));
			string name = "Deserialize";
			if (typeof(T).IsSubclassOf(typeof(AllianceMessageBase)))
			{
				name = "DeserializeNested";
			}
			o.GetType().GetMethod(name).Invoke(o, new object[]
			{
				reader
			});
			return;
		}
		o = default(T);
	}
}
