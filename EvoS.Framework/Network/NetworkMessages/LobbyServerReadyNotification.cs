using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using EvoS.Framework.Constants.Enums;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(773)]
    public class LobbyServerReadyNotification : WebSocketResponseMessage
    {
        public PersistedAccountData AccountData;
        public LobbyStatusNotification Status;
        [EvosMessage(776)]
        public List<PersistedCharacterData> CharacterDataList;
        public LobbyPlayerGroupInfo GroupInfo;
        public FriendStatusNotification FriendStatus;
        public FactionCompetitionNotification FactionCompetitionStatus;
        public ServerQueueConfigurationUpdateNotification ServerQueueConfiguration;
        public string CommerceURL;
        public EnvironmentType EnvironmentType;
        public LobbyAlertMissionDataNotification AlertMissionData;
        public LobbySeasonQuestDataNotification SeasonChapterQuests;

        public LobbyServerReadyNotification Clone()
        {
            return (LobbyServerReadyNotification)MemberwiseClone();
        }
    }
}
