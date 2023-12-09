using System.Collections.Generic;
using CentralServer.LobbyServer.Group;
using EvoS.Framework.Constants.Enums;

namespace CentralServer.LobbyServer.Matchmaking
{
    internal class MatchmakingGroupInfo
    {
        public long GroupID;
        public int Players;
        public Team Team;

        public MatchmakingGroupInfo(long groupID)
        {
            GroupID = groupID;
            Players = GroupManager.GetGroup(groupID).Members.Count;
            Team = Team.Invalid;
        }

        public List<long> Members => GroupManager.GetGroup(GroupID).Members;
    }
}
