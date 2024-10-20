using System;
using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(481)]
    public class LobbyGameConfig
    {
        public LobbyGameConfig()
        {
            Map = string.Empty;
            InstanceSubTypeBit = 0;
            TeamAPlayers = 4;
            TeamBPlayers = 4;
            TeamABots = 0;
            TeamBBots = 0;
            Spectators = 0;
            ResolveTimeoutLimit = 160;
            GameOptionFlags = GameOptionFlag.None;
        }

        private int SelectedSubTypeIndex => BitOperations.Log2(InstanceSubTypeBit);

        [JsonIgnore]
        public GameSubType SelectedSubType => SelectedSubTypeIndex < SubTypes.Count ? SubTypes[SelectedSubTypeIndex] : null;

        public LobbyGameConfig Clone()
        {
            LobbyGameConfig lobbyGameConfig = (LobbyGameConfig) MemberwiseClone();
            lobbyGameConfig.SubTypes = new List<GameSubType>();
            foreach (GameSubType gameSubType in SubTypes)
            {
                lobbyGameConfig.SubTypes.Add(gameSubType.Clone());
            }

            return lobbyGameConfig;
        }

        public bool HasGameOption(GameOptionFlag gameOptionFlag)
        {
            return GameOptionFlags.HasGameOption(gameOptionFlag);
        }

        public void SetGameOption(GameOptionFlag flag, bool on)
        {
            if (on)
            {
                GameOptionFlags = GameOptionFlags.WithGameOption(flag);
            }
            else
            {
                GameOptionFlags = GameOptionFlags.WithoutGameOption(flag);
            }
        }

//        [JsonIgnore]
//        public bool NeedsPreSelectedFreelancer
//        {
//            get { return SubTypes.Exists(p => p.NeedsPreSelectedFreelancer); }
//        }

        [JsonIgnore] public int TotalPlayers => TeamAPlayers + TeamBPlayers;

        [JsonIgnore] public int TotalBots => TeamABots + TeamBBots;

        [JsonIgnore] public int TotalHumanPlayers => TotalPlayers - TotalBots;

        [JsonIgnore] public int MaxGroupSize => Math.Max(TeamAPlayers, TeamBPlayers);

        [JsonIgnore] public int TeamAHumanPlayers => TeamAPlayers - TeamABots;

        [JsonIgnore] public int TeamBHumanPlayers => TeamBPlayers - TeamBBots;

        public const int MaxEquipPoints = 10;
        public string Map;
        public List<GameSubType> SubTypes;
        public GameType GameType;
        public GameOptionFlag GameOptionFlags;
        public bool IsActive;
        public float GameServerShutdownTime = 300f;
        public ushort InstanceSubTypeBit;
        public string RoomName;
        public int TeamAPlayers;
        public int TeamBPlayers;
        public int TeamABots;
        public int TeamBBots;
        public int Spectators;
        public int ResolveTimeoutLimit;
    }
}
