using System;
using System.Reflection;
using EvoS.Framework.Network.Unity;
using log4net;
using Newtonsoft.Json;

public class AllianceMessageBase : MessageBase
{
	private static readonly ILog log = LogManager.GetLogger(typeof(AllianceMessageBase));
	
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
		log.Error("Nested messages must implement DeserializeNested to avoid calling into the base Deserialize");
	}

	// rogues + custom json fallback
	public static void SerializeObject(object o, NetworkWriter writer)
	{
		writer.Write(o != null);
		if (o != null)
		{
			MethodInfo methodInfo = o.GetType().GetMethod("Serialize");
			if (methodInfo != null)
			{
				methodInfo.Invoke(o, new object[]
				{
					writer
				});
			}
			else
			{
				writer.Write(JsonConvert.SerializeObject(o));
			}
		}
	}

	// rogues + custom json fallback
	public static void DeserializeObject<T>(out T o, NetworkReader reader)
	{
		if (!reader.ReadBoolean())
		{
			o = default(T);
			return;
		}
		ConstructorInfo constructor = typeof(T).GetConstructor(Type.EmptyTypes);
		o = (T)((object)constructor.Invoke(Array.Empty<object>()));
		string name = "Deserialize";
		if (typeof(T).IsSubclassOf(typeof(AllianceMessageBase)))
		{
			name = "DeserializeNested";
		}
		MethodInfo methodInfo = o.GetType().GetMethod(name);
		if (methodInfo != null)
		{
			methodInfo.Invoke(o, new object[]
			{
				reader
			});
		}
		else
		{
			String jsonStr;
			try
			{
				jsonStr = reader.ReadString();
				o = JsonConvert.DeserializeObject<T>(jsonStr);
			}
			catch
			{
				log.Error($"Deserialize Error: Couldn't find method {name} of {o.GetType().Name}");
			}
			
		}
	}
}
