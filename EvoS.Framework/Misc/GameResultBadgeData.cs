using System;

namespace EvoS.Framework.Misc;

public static class GameResultBadgeData
{
	[Serializable]
	public class ConsolidatedBadgeGroup
	{
		public string BadgeGroupDisplayName;
		public string BadgeGroupDescription;
		public GameBalanceVars.GameResultBadge.BadgeRole DisplayCategory;
		public int[] BadgeIDs;
	}
}