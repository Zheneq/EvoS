using CentralServer.LobbyServer.Group;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using System;
using System.Collections.Generic;
using System.Text;

namespace CentralServer.LobbyServer.Matchmaking
{
    internal class MatchmakingGroupInfo
    {
        public int GroupID;
        public int Players;
        public Team Team;

        public MatchmakingGroupInfo(int groupID)
        {
            GroupID = groupID;
            Players = GroupManager.GetGroup(groupID).Members.Count;
            Team = Team.Invalid;
        }
    }
}
