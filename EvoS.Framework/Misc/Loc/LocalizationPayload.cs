using System;
using System.Collections.Generic;
using System.IO;
using EvoS.Framework.Misc;
using EvoS.Framework.Network;
using log4net;
using NetSerializer;

[EvosMessage(25)]
[Serializable]
public class LocalizationPayload
{
	private static readonly ILog log = LogManager.GetLogger(typeof(LocalizationPayload));
	
	public string Term = "unset";
	public string Context;
	public byte[][] ArgumentsAsBinaryData;

	[NonSerialized]
	private static Serializer s_serializer;

	public static Serializer Serializer
	{
		get
		{
			if (s_serializer == null)
			{
				s_serializer = new Serializer(new[] {
					typeof(LocalizationArg_AccessLevel),
					typeof(LocalizationArg_BroadcastMessage),
					typeof(LocalizationArg_ChatChannel),
					typeof(LocalizationArg_Faction),
					typeof(LocalizationArg_FirstRank),
					typeof(LocalizationArg_FirstType),
					typeof(LocalizationArg_Freelancer),
					typeof(LocalizationArg_GameType),
					typeof(LocalizationArg_Handle),
					typeof(LocalizationArg_Int32),
					typeof(LocalizationArg_LocalizationPayload),
					typeof(LocalizationArg_TimeSpan)
				});
			}
			return s_serializer;
		}
	}

	private List<LocalizationArg> ExtractArguments()
	{
		List<LocalizationArg> list = null;
		try
		{
			if (ArgumentsAsBinaryData.IsNullOrEmpty())
			{
				return null;
			}
			list = new List<LocalizationArg>();
			byte[][] argumentsAsBinaryData = ArgumentsAsBinaryData;
			foreach (byte[] buffer in argumentsAsBinaryData)
			{
				MemoryStream stream = new MemoryStream(buffer);
				Serializer.Deserialize(stream, out object ob);
				if (ob is LocalizationArg arg)
				{
					list.Add(arg);
				}
			}
			return list;
		}
		catch (Exception exception)
		{
			log.Error(exception);
			return list;
		}
	}

	public override string ToString()
	{
		return $"{Term}@{Context}";
		// string text = StringUtil.TR(Term, Context);
		// List<LocalizationArg> list = ExtractArguments();
		// if (list.IsNullOrEmpty())
		// {
		// 	return text;
		// }
		// if (list.Count == 1)
		// {
		// 	return string.Format(text, list[0].TR());
		// }
		// if (list.Count == 2)
		// {
		// 	return string.Format(text, list[0].TR(), list[1].TR());
		// }
		// if (list.Count == 3)
		// {
		// 	return string.Format(text, list[0].TR(), list[1].TR(), list[2].TR());
		// }
		// if (list.Count > 4)
		// {
		// 	log.Error($"We do not support more than four arguments to localized payloads: {Term}@{Context}");
		// }
		// return string.Format(text, list[0].TR(), list[1].TR(), list[2].TR(), list[3].TR());
	}

	public string ToDebugString()
	{
		return ToString();
	}

	public static LocalizationPayload Create(string attedLocIdentifier)
	{
		string[] array = attedLocIdentifier.Split("@".ToCharArray(), 2);
		if (array.Length != 2)
		{ 
			throw new Exception($"Bad argument ({attedLocIdentifier}) to LocalizationPayload, expected string with an @ in it.");
		}
		return new LocalizationPayload
		{
			Term = array[0],
			Context = array[1]
		};
	}

	public static LocalizationPayload Create(string term, string context)
	{
		return new LocalizationPayload
		{
			Term = term,
			Context = context
		};
	}

	public static LocalizationPayload Create(string term, string context, params LocalizationArg[] arguments)
	{
		List<byte[]> list = null;
		if (!arguments.IsNullOrEmpty())
		{
			list = new List<byte[]>();
			foreach (LocalizationArg ob in arguments)
			{
				MemoryStream memoryStream = new MemoryStream();
				Serializer.Serialize(memoryStream, ob);
				list.Add(memoryStream.ToArray());
			}
		}

		return new LocalizationPayload
		{
			Term = term,
			Context = context,
			ArgumentsAsBinaryData = list.ToArray()
		};
	}
}
