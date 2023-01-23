using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(707)]
    public class LobbyTeamInfo
    {
        [JsonIgnore]
        public IEnumerable<LobbyPlayerInfo> TeamAPlayerInfo
        {
            get { return TeamInfo(Team.TeamA); }
        }

        [JsonIgnore] public IEnumerable<LobbyPlayerInfo> TeamBPlayerInfo => TeamInfo(Team.TeamB);

        [JsonIgnore] public IEnumerable<LobbyPlayerInfo> SpectatorInfo => TeamInfo(Team.Spectator);

        [JsonIgnore] public int TotalPlayerCount => (TeamPlayerInfo == null) ? 0 : TeamPlayerInfo.Count;

        public IEnumerable<LobbyPlayerInfo> TeamInfo(Team team)
        {
            if (TeamPlayerInfo == null)
            {
                return Enumerable.Empty<LobbyPlayerInfo>();
            }

            return from p in TeamPlayerInfo
                where p.TeamId == team
                select p;
        }

        public static LobbyTeamInfo FromServer(LobbyServerTeamInfo serverInfo,
            MatchmakingQueueConfig queueConfig)
        {
            LobbyTeamInfo lobbyTeamInfo = null;
            if (serverInfo != null)
            {
                lobbyTeamInfo = new LobbyTeamInfo();
                if (serverInfo.TeamPlayerInfo != null)
                {
                    lobbyTeamInfo.TeamPlayerInfo = new List<LobbyPlayerInfo>();
                    foreach (LobbyServerPlayerInfo serverInfo2 in serverInfo.TeamPlayerInfo)
                    {
                        lobbyTeamInfo.TeamPlayerInfo.Add(LobbyPlayerInfo.FromServer(serverInfo2, queueConfig));
                    }
                }
            }

            return lobbyTeamInfo;
        }

        public LobbyPlayerInfo GetPlayer(long account)
        {
            if (TeamPlayerInfo == null)
            {
                return null;
            }

            for (int i = 0; i < TeamPlayerInfo.Count; i++)
            {
                if (TeamPlayerInfo[i].AccountId == account)
                {
                    return TeamPlayerInfo[i];
                }
            }

            return null;
        }

        public LobbyTeamInfo Clone()
        {
            return (LobbyTeamInfo) MemberwiseClone();
        }

        [EvosMessage(708)]
        public List<LobbyPlayerInfo> TeamPlayerInfo;
    }
}
