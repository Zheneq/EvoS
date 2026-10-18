using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EvoS.Framework.Misc;
using log4net;
using NetSerializer;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static;

[JsonConverter(typeof(JsonConverter))]
[Serializable]
[EvosMessage(147)]
public class RequirementCollection : IEnumerable<QueueRequirement>, IEnumerable
{
	private static readonly ILog log = LogManager.GetLogger(typeof(RequirementCollection));

	[EvosMessage(150)]
	public List<byte[]> RequirementsAsBinaryData;

	[NonSerialized]
	private bool m_dirty = true;
	[EvosMessage(148)]
	private List<QueueRequirement> m_queueRequirementAsList = new List<QueueRequirement>();
	
	// custom: Lazy so that concurrent first uses can't get the serializer before its types are added
	private static readonly Lazy<Serializer> s_serializer = new(() =>
	{
		Serializer serializer = new Serializer(Enumerable.Empty<Type>());
		serializer.AddTypes(QueueRequirement.MessageTypes);
		return serializer;
	});

	private IEnumerator<QueueRequirement> InternalEnumerator()
	{
		// bool flag = false;
		if (RequirementsAsBinaryData.IsNullOrEmpty())
		{
			yield break;
		}
		
		foreach (byte[] data in RequirementsAsBinaryData)
		{
			MemoryStream stream = new MemoryStream(data);
			InternalSerializer.Deserialize(stream, out object arg);
			if (arg != null && arg is QueueRequirement req)
			{
				yield return req;
				// flag = true;
			}
		}
	}

	public void Add(QueueRequirement item)
	{
		if (item != null)
		{
			MemoryStream memoryStream = new MemoryStream();
			InternalSerializer.Serialize(memoryStream, item);
			if (RequirementsAsBinaryData == null)
			{
				RequirementsAsBinaryData = new List<byte[]>();
			}
			RequirementsAsBinaryData.Add(memoryStream.ToArray());
		}
		m_dirty = true;
	}

	public void AddRange(IEnumerable<QueueRequirement> collection)
	{
		foreach (QueueRequirement item in collection)
		{
			Add(item);
		}
		m_dirty = true;
	}

	public bool Exists(Predicate<QueueRequirement> match)
	{
		foreach (QueueRequirement obj in this)
		{
			if (match(obj))
			{
				return true;
			}
		}
		return false;
	}

	public RequirementCollection Where(Predicate<QueueRequirement> match)
	{
		RequirementCollection requirementCollection = Create();
		foreach (QueueRequirement queueRequirement in this)
		{
			if (match(queueRequirement))
			{
				requirementCollection.Add(queueRequirement);
			}
		}
		return requirementCollection;
	}

	/// <summary>
	/// Returns this collection, or a copy in which requirements the client cannot deserialize
	/// are replaced with <see cref="QueueRequirement_Never"/>.
	/// </summary>
	/// custom
	public RequirementCollection ForProtocolVersion(string protocolVersion)
	{
		if (protocolVersion is null)
		{
			log.Error($"Checking queue requirement against null protocol version\n{new StackTrace(true)}");
		}
		List<QueueRequirement> requirements = new List<QueueRequirement>();
		bool replaced = false;
		foreach (QueueRequirement queueRequirement in this)
		{
			QueueRequirement requirement = queueRequirement is QueueRequirement_ProtocolDependent protocolDependent
				? protocolDependent.ForProtocolVersion(protocolVersion)
				: queueRequirement;
			replaced |= requirement != queueRequirement;
			requirements.Add(requirement);
		}
		return replaced ? Create(requirements) : this;
	}

	// TODO check protocol version on server
	// public bool DoesApplicantPass(
	// 	IQueueRequirementSystemInfo systemInfo,
	// 	IQueueRequirementApplicant applicant,
	// 	GameType gameType,
	// 	GameSubType gameSubType)
	// {
	// 	return false == Exists(p => !p.DoesApplicantPass(systemInfo, applicant, gameType, gameSubType));
	// }
	//
	// public LocalizationPayload GenerateFailure(
	// 	IQueueRequirementSystemInfo systemInfo,
	// 	IQueueRequirementApplicant applicant,
	// 	GameType gameType,
	// 	GameSubType gameSubType,
	// 	RequirementMessageContext context)
	// {
	// 	foreach (QueueRequirement queueRequirement in this)
	// 	{
	// 		if (!queueRequirement.DoesApplicantPass(systemInfo, applicant, gameType, gameSubType))
	// 		{
	// 			return queueRequirement.GenerateFailure(systemInfo, applicant, context);
	// 		}
	// 	}
	// 	return null;
	// }

	public IEnumerator<QueueRequirement> GetEnumerator()
	{
		return InternalEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return InternalEnumerator();
	}

	public static RequirementCollection Create()
	{
		return new RequirementCollection();
	}

	public static RequirementCollection Create(IEnumerable<QueueRequirement> requirements)
	{
		List<byte[]> list = null;
		if (!requirements.IsNullOrEmpty())
		{
			list = new List<byte[]>();
			foreach (QueueRequirement ob in requirements)
			{
				MemoryStream memoryStream = new MemoryStream();
				InternalSerializer.Serialize(memoryStream, ob);
				list.Add(memoryStream.ToArray());
			}
		}
		return new RequirementCollection
		{
			RequirementsAsBinaryData = list
		};
	}

	public List<QueueRequirement> ToList()
	{
		if (m_dirty)
		{
			m_dirty = false;
			m_queueRequirementAsList.Clear();
			foreach (QueueRequirement item in this)
			{
				m_queueRequirementAsList.Add(item);
			}
		}
		return m_queueRequirementAsList;
	}

	private static Serializer InternalSerializer => s_serializer.Value;

	private class JsonConverter : Newtonsoft.Json.JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(RequirementCollection);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
			{
				return null;
			}
			if (reader.TokenType != JsonToken.StartArray)
			{
				throw new Exception($"Bad JSON definition of RequirementCollection, expected '[' not {reader.TokenType}='{reader.Value}'");
			}
			reader.Read();
			RequirementCollection requirementCollection = Create();
			while (reader.TokenType != JsonToken.EndArray)
			{
				QueueRequirement item = QueueRequirement.ExtractRequirementFromReader(reader);
				requirementCollection.Add(item);
				reader.Read();
			}
			if (reader.TokenType != JsonToken.EndArray)
			{
				throw new Exception($"Bad JSON definition of RequirementCollection, expected ']' not {reader.TokenType}='{reader.Value}'");
			}
			return requirementCollection;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			RequirementCollection requirementCollection = value as RequirementCollection;
			writer.WriteStartArray();
			foreach (QueueRequirement queueRequirement in requirementCollection)
			{
				writer.WriteStartObject();
				writer.WritePropertyName(queueRequirement.Requirement.ToString());
				writer.WriteStartObject();
				queueRequirement.WriteToJson(writer);
				writer.WriteEndObject();
				writer.WriteEndObject();
			}
			writer.WriteEndArray();
		}
	}
}
