using System;
using EvoS.Framework.Misc;

[Serializable]
public class LocalizationArg_AbilityPing : LocalizationArg
{
	public CharacterType m_characterType;
	public string m_abilityType;
	public string m_abilityName;
	public bool m_isSelectable;
	public int m_remainingCooldown;
	public bool m_isUlt;
	public int m_currentTechPoints;
	public int m_maxTechPoints;

	// public static LocalizationArg_AbilityPing Create(
	// 	CharacterType characterType,
	// 	Ability ability,
	// 	bool isSelectable,
	// 	int remainingCooldown,
	// 	bool isUlt,
	// 	int currentTechPoints,
	// 	int maxTechPoints)
	// {
	// 	return new LocalizationArg_AbilityPing
	// 	{
	// 		m_characterType = characterType,
	// 		m_abilityType = ability.GetType().ToString(),
	// 		m_abilityName = ability.m_abilityName,
	// 		m_isSelectable = isSelectable,
	// 		m_remainingCooldown = remainingCooldown,
	// 		m_isUlt = isUlt,
	// 		m_currentTechPoints = currentTechPoints,
	// 		m_maxTechPoints = maxTechPoints
	// 	};
	// }

	// public override string TR()
	// {
	// 	string arg;
	// 	if (m_isSelectable)
	// 	{
	// 		arg = StringUtil.TR("Ready!", "Global");
	// 	}
	// 	else if (m_remainingCooldown <= 0)
	// 	{
	// 		arg = m_isUlt
	// 			? string.Format(StringUtil.TR("EnergySoFar", "Global"), m_currentTechPoints, m_maxTechPoints)
	// 			: StringUtil.TR("Ready!", "Global");
	// 	}
	// 	else
	// 	{
	// 		arg = m_remainingCooldown == 1
	// 			? StringUtil.TR("TurnLeft", "GameModes")
	// 			: string.Format(StringUtil.TR("TurnsLeft", "GameModes"), m_remainingCooldown);
	// 	}
	// 	return string.Format(
	// 		StringUtil.TR("AbilityPingMessage", "Global"),
	// 		StringUtil.TR_CharacterName(m_characterType.ToString()),
	// 		StringUtil.TR_AbilityName(m_abilityType, m_abilityName),
	// 		arg);
	// }
}
