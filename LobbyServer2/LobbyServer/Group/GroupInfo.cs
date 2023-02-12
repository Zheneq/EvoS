using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using log4net;

namespace CentralServer.LobbyServer.Group
{
    public class GroupInfo
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GroupInfo));
        
        public readonly long GroupId;
        public readonly List<long> Members;
        public long Leader { get; private set; }

        public GroupInfo(long id)
        {
            this.GroupId = id;
            this.Members = new List<long>();
            this.Leader = -1;
        }

        public bool IsEmpty()
        {
            return Members.Count == 0;
        }

        public bool IsSolo()
        {
            return Members.Count == 1;
        }

        public bool IsLeader(long accountId)
        {
            return accountId == Leader;
        }

        public void SetLeader(long accountId)
        {
            lock (GroupManager.Lock)
            {
                if (Members.Contains(accountId))
                {
                    Leader = accountId;
                }
            }
        }

        public void AddPlayer(long accountId)
        {
            if (Members.Contains(accountId))
            {
                log.Error($"Attempted to re-add player {accountId} to group {GroupId}");
            }
            Members.Add(accountId);
            if (Members.Count == 1)
            {
                Leader = accountId;
            }
        }

        public void RemovePlayer(long accountId)
        {
            if (!Members.Remove(accountId))
            {
                log.Error($"Attempted to remove player {accountId} from group {GroupId} when they weren't there");
                return;
            }
            if (Members.Count == 0)
            {
                Leader = -1;
            }
            else if (Leader == accountId)
            {
                Leader = Members[0];
            }
        }
    }
}