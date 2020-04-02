using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using Newtonsoft.Json;

namespace EvoS.Framework.Misc
{
    [Serializable]
    public class LobbyServerTeamInfo
    {
        [JsonIgnore]
        public IEnumerable<LobbyServerPlayerInfo> TeamAPlayerInfo => TeamInfo(Team.TeamA);

        [JsonIgnore]
        public IEnumerable<LobbyServerPlayerInfo> TeamBPlayerInfo => TeamInfo(Team.TeamB);

        [JsonIgnore]
        public IEnumerable<LobbyServerPlayerInfo> SpectatorInfo => TeamInfo(Team.Spectator);

        public LobbyServerTeamInfo()
        {
            TeamPlayerInfo = new List<LobbyServerPlayerInfo>();
            TierChangeMins = new Dictionary<long, TierPlacement>();
            TierChangeMaxs = new Dictionary<long, TierPlacement>();
            TierCurrents = new Dictionary<long, TierPlacement>();
        }

        public IEnumerable<LobbyServerPlayerInfo> TeamInfo(Team team)
        {
            if (TeamPlayerInfo == null)
            {
                return Enumerable.Empty<LobbyServerPlayerInfo>();
            }

            return from p in TeamPlayerInfo
                where p.TeamId == team
                select p;
        }

        public List<LobbyServerPlayerInfo> TeamPlayerInfo;
        public Dictionary<long, TierPlacement> TierChangeMins;
        public Dictionary<long, TierPlacement> TierChangeMaxs;
        public Dictionary<long, TierPlacement> TierCurrents;
    }
}
