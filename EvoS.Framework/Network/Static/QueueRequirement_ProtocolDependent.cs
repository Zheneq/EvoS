using System;
using System.Collections.Generic;

namespace EvoS.Framework.Network.Static;

/// <summary>
/// A requirement that only some client protocol versions can deserialize.
/// For any other client it is replaced with <see cref="QueueRequirement_Never"/> before sending
/// (see <see cref="RequirementCollection.ForProtocolVersion"/>).
/// </summary>
/// <remarks>
/// A concrete subclass must be added to <see cref="QueueRequirement.MessageTypes"/> without shifting the
/// NetSerializer type IDs vanilla clients rely on, and needs an <see cref="EvosMessageAttribute"/> ID.
/// </remarks>
[Serializable]
public abstract class QueueRequirement_ProtocolDependent : QueueRequirement
{
	// Defined per class, not serialized
	public abstract IReadOnlySet<string> SupportedProtocolVersions { get; }

	public bool IsSupportedBy(string protocolVersion)
	{
		return protocolVersion is not null && SupportedProtocolVersions.Contains(protocolVersion);
	}

	public QueueRequirement ForProtocolVersion(string protocolVersion)
	{
		return IsSupportedBy(protocolVersion) ? this : QueueRequirement_Never.CreateAdminDisabled();
	}
}
