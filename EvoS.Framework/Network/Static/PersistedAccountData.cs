using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkMessages;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(554)]
    public class PersistedAccountData : ICloneable
    {

        [BsonId]
        public long AccountId { get; set; }
        public string UserName { get; set; }
        public string Handle { get; set; }
        [EvosMessage(561, ignoreGenericArgs: true)]
        public SchemaVersion<AccountSchemaChange> SchemaVersion { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public AccountComponent AccountComponent { get; set; }
        public AdminComponent AdminComponent { get; set; }
        public BankComponent BankComponent { get; set; }
        public ExperienceComponent ExperienceComponent { get; set; }
        public InventoryComponent InventoryComponent { get; set; }
        public QuestComponent QuestComponent { get; set; }
        public SocialComponent SocialComponent { get; set; }

        [EvosMessage(586)]
        public Dictionary<CharacterType, PersistedCharacterData> CharacterData { get; set; }
        [EvosMessage(602)]
        private List<PersistedCharacterMatchData> AddedMatchData { get; set; }

        [JsonIgnore] public string PersistedUserName { get; set; }
        [JsonIgnore] public string PersistedHandle { get; set; }
        [JsonIgnore] public bool Snapshotting { get; set; }
        [JsonIgnore] public string SnapshottingUserName { get; set; }
        [JsonIgnore] public PersistedAccountDataSnapshotReason SnapshotReason { get; set; }

        [JsonIgnore] public string SnapshotNote { get; set; }

        public PersistedAccountData()
        {
            SchemaVersion = new SchemaVersion<AccountSchemaChange>();
            AccountComponent = new AccountComponent();
            AdminComponent = new AdminComponent();
            BankComponent = new BankComponent(new List<CurrencyData>());
            ExperienceComponent = new ExperienceComponent();
            ExperienceComponent.Level = 0;
            InventoryComponent = new InventoryComponent();
            SocialComponent = new SocialComponent();
            CharacterData = new Dictionary<CharacterType, PersistedCharacterData>();
            AddedMatchData = new List<PersistedCharacterMatchData>();
            QuestComponent = new QuestComponent();
        }
        
        

//        [JsonIgnore]
//        public int SeasonLevel
//        {
//            get { return QuestComponent.SeasonLevel; }
//            set { QuestComponent.SeasonLevel = value; }
//        }
//
//        [JsonIgnore]
//        public int SeasonXPProgressThroughLevel
//        {
//            get { return QuestComponent.SeasonXPProgressThroughLevel; }
//            set { QuestComponent.SeasonXPProgressThroughLevel = value; }
//        }
//
//        [JsonIgnore]
//        public int HighestSeasonChapter
//        {
//            get { return QuestComponent.HighestSeasonChapter; }
//            set { QuestComponent.HighestSeasonChapter = value; }
//        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        public PersistedAccountData CloneForClient()
        {
            return new PersistedAccountData
            {
                AccountId = AccountId,
                UserName = UserName,
                Handle = Handle,
                SchemaVersion = SchemaVersion,
                CreateDate = CreateDate,
                UpdateDate = UpdateDate,
                AccountComponent = AccountComponent,
                AdminComponent = AdminComponent.CloneForClient(),
                BankComponent = BankComponent,
                ExperienceComponent = ExperienceComponent,
                InventoryComponent = InventoryComponent.CloneForClient(),
                SocialComponent = SocialComponent,
                QuestComponent = QuestComponent,
                CharacterData = CharacterData, // TODO clone
            };
        }

        public PersistedAccountData CloneForAdminClient()
        {
            return new PersistedAccountData
            {
                AccountId = AccountId,
                UserName = UserName,
                Handle = Handle,
                SchemaVersion = SchemaVersion,
                CreateDate = CreateDate,
                UpdateDate = UpdateDate,
                AccountComponent = AccountComponent,
                AdminComponent = AdminComponent,
                BankComponent = BankComponent,
                ExperienceComponent = ExperienceComponent,
                InventoryComponent = InventoryComponent,
                SocialComponent = SocialComponent,
                QuestComponent = QuestComponent
            };
        }

        public PersistedAccountData CloneForSearchResult()
        {
            return new PersistedAccountData
            {
                AccountId = AccountId,
                UserName = UserName,
                Handle = Handle,
                SchemaVersion = SchemaVersion,
                CreateDate = CreateDate,
                UpdateDate = UpdateDate,
                AccountComponent = AccountComponent,
                AdminComponent = AdminComponent,
                ExperienceComponent = ExperienceComponent
            };
        }

//        public void AddMatchData(PersistedCharacterMatchData matchData)
//        {
//            AddedMatchData.Add(matchData);
//        }
//
//        public IEnumerable<PersistedCharacterMatchData> GetAddedMatchData()
//        {
//            return AddedMatchData;
//        }
//
//        public void ClearAddedMatchData()
//        {
//            AddedMatchData.Clear();
//        }

//        public int GetReactorLevel(List<SeasonTemplate> seasons)
//        {
//            return this.QuestComponent.GetReactorLevel(seasons);
//        }

        public override string ToString()
        {
            return string.Format("[{0}] {1}", AccountId, Handle);
        }

        public LobbyCharacterInfo GetCharacterInfo()
        {
            if (CharacterData.TryGetValue(AccountComponent.LastCharacter, out PersistedCharacterData data))
            {
                return LobbyCharacterInfo.Of(data);
            }

            return null;
        }
    }
}
