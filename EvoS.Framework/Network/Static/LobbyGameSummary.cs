using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    public class LobbyGameSummary
    {
        public string GameServerAddress;
        public GameResult GameResult;
        public float GameResultFraction = 0.5f;
        public string TimeText = string.Empty;
        public int NumOfTurns;
        public int TeamAPoints;
        public int TeamBPoints;
        public TimeSpan MatchTime;
        public List<PlayerGameSummary> PlayerGameSummaryList = new List<PlayerGameSummary>();
        public List<BadgeAndParticipantInfo> BadgeAndParticipantsInfo;

	    public void Deserialize(NetworkReader reader)
	    {
		    GameResult = (GameResult)reader.ReadInt16();
		    GameResultFraction = reader.ReadSingle();
		    TimeText = reader.ReadString();
		    NumOfTurns = reader.ReadInt32();
		    TeamAPoints = reader.ReadInt32();
		    TeamBPoints = reader.ReadInt32();
		    MatchTime = new TimeSpan(reader.ReadInt64());
		    int num = reader.ReadInt32();
		    PlayerGameSummaryList = new List<PlayerGameSummary>(num);
		    for (int i = 0; i < num; i++)
		    {
			    PlayerGameSummary playerGameSummary = new PlayerGameSummary();
			    playerGameSummary.Deserialize(reader);
			    PlayerGameSummaryList.Add(playerGameSummary);
		    }
	    }

	    // added in rogues
	    public void Serialize(NetworkWriter writer)
	    {
		    writer.Write((short)GameResult);
		    writer.Write(GameResultFraction);
		    writer.Write(TimeText);
		    writer.Write(NumOfTurns);
		    writer.Write(TeamAPoints);
		    writer.Write(TeamBPoints);
		    writer.Write(MatchTime.Ticks);
		    int count = PlayerGameSummaryList.Count;
		    writer.Write(count);
		    foreach (PlayerGameSummary playerGameSummary in PlayerGameSummaryList)
		    {
			    playerGameSummary.Serialize(writer);
		    }
	    }
    }
}
