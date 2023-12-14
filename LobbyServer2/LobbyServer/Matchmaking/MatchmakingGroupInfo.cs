using System;
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
        public DateTime QueueTime;

        public MatchmakingGroupInfo(long groupID, DateTime queueTime = default)
        {
            GroupID = groupID;
            Players = GroupManager.GetGroup(groupID).Members.Count;
            Team = Team.Invalid;
            QueueTime = queueTime;
        }

        public List<long> Members => GroupManager.GetGroup(GroupID).Members;
    }
}
