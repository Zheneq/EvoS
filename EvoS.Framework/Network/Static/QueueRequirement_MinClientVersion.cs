using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static;

/// <summary>
/// Passes if the client version is the given one or newer. The client checks it against its own build version.
/// </summary>
[Serializable]
[EvosMessage(800)]
public class QueueRequirement_MinClientVersion(int major, int minor, int patch) : QueueRequirement_ProtocolDependent
{
	private static readonly IReadOnlySet<string> s_supportedProtocolVersions = new HashSet<string>
	{
		ProtocolVersion.V1_8,
	};

	// Field names are part of the protocol, must match the client
	private int m_major = major;
	private int m_minor = minor;
	private int m_patch = patch;

	public override IReadOnlySet<string> SupportedProtocolVersions => s_supportedProtocolVersions;

	public override bool AnyGroupMember => false;

	public override RequirementType Requirement => RequirementType.MinClientVersion;

	public override void WriteToJson(JsonWriter writer)
	{
		writer.WritePropertyName("Version");
		writer.WriteValue($"{m_major}.{m_minor}.{m_patch}");
	}

	public static QueueRequirement Create(JsonReader reader)
	{
		reader.Read();
		Version version = Version.Parse(reader.Value?.ToString() ?? "999.999.999");
		reader.Read();
		return new QueueRequirement_MinClientVersion(version.Major, version.Minor, Math.Max(version.Build, 0));
	}
}
