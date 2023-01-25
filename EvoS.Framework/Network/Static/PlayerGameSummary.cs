using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    public class PlayerGameSummary : StatDisplaySettings.IPersistatedStatValueSupplier
    {
        public bool IsInTeamA()
        {
            return Team == 0;
        }

        public bool IsInTeamB()
        {
            return Team == 1;
        }

        public bool IsSpectator()
        {
            return Team == 3;
        }

        public GameResult ResultForWin()
        {
            return (!IsInTeamA()) ? GameResult.TeamBWon : GameResult.TeamAWon;
        }

        public int GetTotalGainedXPAccount()
        {
            return BaseXPGainedAccount + GGXPGainedAccount + PlayWithFriendXPGainedAccount +
                   QuestXPGainedAccount + FirstWinXpGainedAccount + QueueTimeXpGainedAccount +
                   FreelancerOwnedXPAmount;
        }

        public int GetTotalGainedXPCharacter()
        {
            return BaseXPGainedCharacter + GGXPGainedCharacter + ConsumableXPGainedCharacter +
                   PlayWithFriendXPGainedCharacter + QuestXPGainedCharacter + FirstWinXpGainedCharacter +
                   QueueTimeXpGainedCharacter + FreelancerOwnedXPAmount;
        }

        public int GetTotalGainedISO()
        {
            return BaseISOGained + GGISOGained;
        }

        public int GetTotalGainedFreelancerCurrency()
        {
            return BaseFreelancerCurrencyGained + WinFreelancerCurrencyGained +
                   GGFreelancerCurrencyGained + EventBonusFreelancerCurrencyGained;
        }

        public int GetContribution()
        {
            return TotalPlayerDamage + TotalPlayerAbsorb + GetTotalHealingFromAbility();
        }

        public int GetTotalHealingFromAbility()
        {
            return (from a in AbilityGameSummaryList
                select a.TotalHealing).Sum();
        }

        public float? GetNumLives()
        {
            if (TotalGameTurns > 0)
            {
                return Math.Max(1f, NumDeaths + 1);
            }

            return null;
        }

        public float? GetDamageDealtPerTurn()
        {
            if (TotalGameTurns > 0)
            {
                return TotalPlayerDamage / (float) TotalGameTurns;
            }

            return null;
        }

        public float? GetTeamEnergyBoostedByMePerTurn()
        {
            if (TotalGameTurns > 0)
            {
                return TeamExtraEnergyByEnergizedFromMe / (float) TotalGameTurns;
            }

            return null;
        }

        public float? GetTeamDamageSwingByMePerTurn()
        {
            if (TotalGameTurns > 0)
            {
                return GetTotalTeamDamageAdjustedByMe() / (float) TotalGameTurns;
            }

            return null;
        }

        public float? GetDamageTakenPerLife()
        {
            float? numLives = GetNumLives();
            if (numLives != null)
            {
                return TotalPlayerDamageReceived / numLives.Value;
            }

            return null;
        }

        public float GetBoostedDamagePerTurn()
        {
            if (TotalGameTurns > 0)
            {
                return MyOutgoingExtraDamageFromEmpowered / (float) TotalGameTurns;
            }

            return 0f;
        }

        public int GetTotalTeamDamageAdjustedByMe()
        {
            return TeamOutgoingDamageIncreasedByEmpoweredFromMe + TeamIncomingDamageReducedByWeakenedFromMe;
        }

        public string ToPlayerInfoString()
        {
            return string.Format(
                "- - - - - - - - - -\n[User Name] {0}\n[Account Id] {1}\n[Character Played] {2}\n[Skin] {3}\n[Team] {4}\n", InGameName, AccountId, CharacterPlayed, CharacterSkinIndex, Team);
        }

        public float? GetTankingPerLife()
        {
            float? numLives = GetNumLives();
            if (numLives != null)
            {
                return (TotalPlayerDamageReceived + NetDamageAvoidedByEvades +
                        MyIncomingDamageReducedByCover) / numLives.Value;
            }

            return null;
        }

        public float? GetTeamMitigation()
        {
            float num = TeamIncomingDamageReducedByWeakenedFromMe + TotalTeamDamageReceived;
            if (num == 0f)
            {
                return null;
            }

            float num2 = EffectiveHealing + TotalPlayerAbsorb +
                         TeamIncomingDamageReducedByWeakenedFromMe;
            return num2 / num;
        }

        public float? GetSupportPerTurn()
        {
            float num = EffectiveHealing + TotalPlayerAbsorb;
            float num2 = TotalGameTurns;
            if (num2 == 0f)
            {
                return null;
            }

            return num / num2;
        }

        public float? GetDamageDonePerLife()
        {
            float? numLives = GetNumLives();
            if (numLives != null)
            {
                return TotalPlayerDamage / numLives.Value;
            }

            return null;
        }

        public float? GetNetDamageDodgedPerLife()
        {
            float? numLives = GetNumLives();
            if (numLives != null)
            {
                return NetDamageAvoidedByEvades / numLives.Value;
            }

            return null;
        }

        public float? GetIncomingDamageMitigatedByCoverPerLife()
        {
            float? numLives = GetNumLives();
            if (numLives != null)
            {
                return MyIncomingDamageReducedByCover / numLives.Value;
            }

            return null;
        }

        public float? GetAvgLifeSpan()
        {
            float? numLives = GetNumLives();
            if (numLives != null)
            {
                return Math.Max(1, TotalGameTurns) / numLives.Value;
            }

            return null;
        }

        public float? GetDamageTakenPerTurn()
        {
            if (TotalGameTurns > 0)
            {
                return TotalPlayerDamageReceived / (float) TotalGameTurns;
            }

            return null;
        }

        public float GetMovementDeniedPerTurn()
        {
            if (TotalGameTurns > 0)
            {
                return MovementDeniedByMe / TotalGameTurns;
            }

            return 0f;
        }

        public float? GetStat(StatDisplaySettings.StatType TypeOfStat)
        {
            if (TypeOfStat == StatDisplaySettings.StatType.IncomingDamageDodgeByEvade)
            {
                return GetNetDamageDodgedPerLife();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.IncomingDamageReducedByCover)
            {
                return GetIncomingDamageMitigatedByCoverPerLife();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TotalAssists)
            {
                return NumAssists;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TotalDeaths)
            {
                return NumDeaths;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TotalBadgePoints)
            {
                return TotalBadgePoints;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.MovementDenied)
            {
                return GetMovementDeniedPerTurn();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.EnergyGainPerTurn)
            {
                return EnergyGainPerTurn;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.DamagePerTurn)
            {
                return GetDamageDealtPerTurn();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.NetBoostedOutgoingDamage)
            {
                return GetBoostedDamagePerTurn();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.DamageEfficiency)
            {
                return DamageEfficiency;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.KillParticipation)
            {
                return KillParticipation;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.EffectiveHealAndAbsorb)
            {
                return GetSupportPerTurn();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TeamDamageAdjustedByMe)
            {
                return GetTeamDamageSwingByMePerTurn();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TeamExtraEnergyByEnergizedFromMe)
            {
                return GetTeamEnergyBoostedByMePerTurn();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.DamageTakenPerLife)
            {
                return GetDamageTakenPerLife();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.EnemiesSightedPerLife)
            {
                return EnemiesSightedPerTurn;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TotalTurns)
            {
                return TotalGameTurns;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TotalTeamDamageReceived)
            {
                return TotalTeamDamageReceived;
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TankingPerLife)
            {
                return GetTankingPerLife();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.TeamMitigation)
            {
                return GetTeamMitigation();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.SupportPerTurn)
            {
                return GetSupportPerTurn();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.DamageDonePerLife)
            {
                return GetDamageDonePerLife();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.DamageTakenPerTurn)
            {
                return GetDamageTakenPerTurn();
            }

            if (TypeOfStat == StatDisplaySettings.StatType.AvgLifeSpan)
            {
                return GetAvgLifeSpan();
            }

            return null;
        }

        public float? GetFreelancerStat(int FreelancerStatIndex)
        {
            if (-1 < FreelancerStatIndex && FreelancerStatIndex < FreelancerStats.Count)
            {
                return FreelancerStats[FreelancerStatIndex];
            }

            return null;
        }

        public long AccountId;
        public string InGameName;
        public CharacterType CharacterPlayed;
        public FreelancerSelectionOnus FreelancerSelectionOnus;
        public string CharacterName;
        public int CharacterSkinIndex;
        public int Team;
        public int TeamSlot;
        public int PlayerId;
        public string PrepCatalystName;
        public string DashCatalystName;
        public string CombatCatalystName;
        public bool PrepCatalystUsed;
        public bool DashCatalystUsed;
        public bool CombatCatalystUsed;
        public int PowerupsCollected;
        public PlayerGameResult PlayerGameResult = PlayerGameResult.NoResult;
        public MatchResultsStats MatchResults;
        public int TotalGameTurns;
        public int TotalPotentialAbsorb;
        public int TotalTechPointsGained;
        public int TotalHealingReceived;
        public int TotalAbsorbReceived;
        public int NumKills;
        public int NumDeaths;
        public int NumAssists;
        public int TotalPlayerDamage;
        public int TotalPlayerHealing;
        public int TotalPlayerAbsorb;
        public int TotalPlayerDamageReceived;
        public int TotalTeamDamageReceived;
        public int NetDamageAvoidedByEvades;
        public int DamageAvoidedByEvades;
        public int DamageInterceptedByEvades;
        public int MyIncomingDamageReducedByCover;
        public int MyOutgoingDamageReducedByCover;
        public int MyOutgoingExtraDamageFromEmpowered;
        public int MyOutgoingReducedDamageFromWeakened;
        public int TeamOutgoingDamageIncreasedByEmpoweredFromMe;
        public int TeamIncomingDamageReducedByWeakenedFromMe;
        public float MovementDeniedByMe;
        public float EnergyGainPerTurn;
        public float DamageEfficiency;
        public float KillParticipation;
        public int EffectiveHealing;
        public int EffectiveHealingFromAbility;
        public int TeamExtraEnergyByEnergizedFromMe;
        public float EnemiesSightedPerTurn;
        public int CharacterSpecificStat;
        public List<int> FreelancerStats = new List<int>();
        public int BaseISOGained;
        public int BaseFreelancerCurrencyGained;
        public int BaseXPGainedAccount;
        public int BaseXPGainedCharacter;
        public int WinFreelancerCurrencyGained;
        public int FirstWinFreelancerCurrencyGained;
        public int EventBonusFreelancerCurrencyGained;
        public int GGPacksSelfUsed;
        public int GGNonSelfCount;
        public int GGISOGained;
        public int GGFreelancerCurrencyGained;
        public int GGXPGainedAccount;
        public int GGXPGainedCharacter;
        public int ConsumableXPGainedCharacter;
        public int PlayWithFriendXPGainedAccount;
        public int PlayWithFriendXPGainedCharacter;
        public int QuestXPGainedAccount;
        public int QuestXPGainedCharacter;
        public int EventBonusXPGainedAccount;
        public int EventBonusXPGainedCharacter;
        public int FirstWinXpGainedAccount;
        public int FirstWinXpGainedCharacter;
        public int QueueTimeXpGainedAccount;
        public int QueueTimeXpGainedCharacter;
        public int FreelancerOwnedXPAmount;
        public int ActorIndex;
        public TimeSpan QueueTime;
        public List<AbilityGameSummary> AbilityGameSummaryList = new List<AbilityGameSummary>();
        public List<int> TimebankUsage = new List<int>();
        public Dictionary<string, float> AccountEloDeltas;
        public Dictionary<string, float> CharacterEloDeltas;
        public int RankedSortKarmaDelta;
        public float UsedMMR;
        public float TotalBadgePoints;

        public void Deserialize(NetworkReader reader)
        {
            AccountId = reader.ReadInt64();
            InGameName = reader.ReadString();
            CharacterPlayed = (CharacterType)reader.ReadByte();
            FreelancerSelectionOnus = (FreelancerSelectionOnus)reader.ReadByte();
            CharacterName = reader.ReadString();
            CharacterSkinIndex = reader.ReadInt32();
            Team = reader.ReadInt32();
            TeamSlot = reader.ReadInt32();
            PlayerId = reader.ReadInt32();
            PrepCatalystName = reader.ReadString();
            DashCatalystName = reader.ReadString();
            CombatCatalystName = reader.ReadString();
            PrepCatalystUsed = reader.ReadBoolean();
            DashCatalystUsed = reader.ReadBoolean();
            CombatCatalystUsed = reader.ReadBoolean();
            PowerupsCollected = reader.ReadInt32();
            PlayerGameResult = (PlayerGameResult)reader.ReadByte();
            AllianceMessageBase.DeserializeObject(out MatchResults, reader);
            TotalGameTurns = reader.ReadInt32();
            TotalPotentialAbsorb = reader.ReadInt32();
            TotalTechPointsGained = reader.ReadInt32();
            TotalHealingReceived = reader.ReadInt32();
            TotalAbsorbReceived = reader.ReadInt32();
            NumKills = reader.ReadInt32();
            NumDeaths = reader.ReadInt32();
            NumAssists = reader.ReadInt32();
            TotalPlayerDamage = reader.ReadInt32();
            TotalPlayerHealing = reader.ReadInt32();
            TotalPlayerAbsorb = reader.ReadInt32();
            TotalPlayerDamageReceived = reader.ReadInt32();
            TotalTeamDamageReceived = reader.ReadInt32();
            NetDamageAvoidedByEvades = reader.ReadInt32();
            DamageAvoidedByEvades = reader.ReadInt32();
            DamageInterceptedByEvades = reader.ReadInt32();
            MyIncomingDamageReducedByCover = reader.ReadInt32();
            MyOutgoingDamageReducedByCover = reader.ReadInt32();
            MyOutgoingExtraDamageFromEmpowered = reader.ReadInt32();
            MyOutgoingReducedDamageFromWeakened = reader.ReadInt32();
            TeamOutgoingDamageIncreasedByEmpoweredFromMe = reader.ReadInt32();
            TeamIncomingDamageReducedByWeakenedFromMe = reader.ReadInt32();
            MovementDeniedByMe = reader.ReadSingle();
            EnergyGainPerTurn = reader.ReadSingle();
            DamageEfficiency = reader.ReadSingle();
            KillParticipation = reader.ReadSingle();
            EffectiveHealing = reader.ReadInt32();
            EffectiveHealingFromAbility = reader.ReadInt32();
            TeamExtraEnergyByEnergizedFromMe = reader.ReadInt32();
            EnemiesSightedPerTurn = reader.ReadSingle();
            CharacterSpecificStat = reader.ReadInt32();
            int num = reader.ReadInt32();
            FreelancerStats = new List<int>();
            for (int i = 0; i < num; i++)
            {
                FreelancerStats.Add(reader.ReadInt32());
            }
            BaseISOGained = reader.ReadInt32();
            BaseFreelancerCurrencyGained = reader.ReadInt32();
            BaseXPGainedAccount = reader.ReadInt32();
            BaseXPGainedCharacter = reader.ReadInt32();
            WinFreelancerCurrencyGained = reader.ReadInt32();
            FirstWinFreelancerCurrencyGained = reader.ReadInt32();
            EventBonusFreelancerCurrencyGained = reader.ReadInt32();
            GGPacksSelfUsed = reader.ReadInt32();
            GGNonSelfCount = reader.ReadInt32();
            GGISOGained = reader.ReadInt32();
            GGFreelancerCurrencyGained = reader.ReadInt32();
            GGXPGainedAccount = reader.ReadInt32();
            GGXPGainedCharacter = reader.ReadInt32();
            ConsumableXPGainedCharacter = reader.ReadInt32();
            PlayWithFriendXPGainedAccount = reader.ReadInt32();
            PlayWithFriendXPGainedCharacter = reader.ReadInt32();
            QuestXPGainedAccount = reader.ReadInt32();
            QuestXPGainedCharacter = reader.ReadInt32();
            EventBonusXPGainedAccount = reader.ReadInt32();
            EventBonusXPGainedCharacter = reader.ReadInt32();
            FirstWinXpGainedAccount = reader.ReadInt32();
            FirstWinXpGainedCharacter = reader.ReadInt32();
            QueueTimeXpGainedAccount = reader.ReadInt32();
            QueueTimeXpGainedCharacter = reader.ReadInt32();
            FreelancerOwnedXPAmount = reader.ReadInt32();
            ActorIndex = reader.ReadInt32();
            QueueTime = TimeSpan.FromTicks(reader.ReadInt64());
            num = reader.ReadInt32();
            AbilityGameSummaryList = new List<AbilityGameSummary>();
            for (int j = 0; j < num; j++)
            {
                AbilityGameSummary abilityGameSummary = new AbilityGameSummary();
                abilityGameSummary.Deserialize(reader);
                AbilityGameSummaryList.Add(abilityGameSummary);
            }
            num = reader.ReadInt32();
            TimebankUsage = new List<int>();
            for (int k = 0; k < num; k++)
            {
                int item = reader.ReadInt32();
                TimebankUsage.Add(item);
            }
            TotalBadgePoints = reader.ReadSingle();
        }

        // added in rogues
        public void Serialize(NetworkWriter writer)
        {
            writer.Write(AccountId);
            writer.Write(InGameName);
            writer.Write((byte)CharacterPlayed);
            writer.Write((byte)FreelancerSelectionOnus);
            writer.Write(CharacterName);
            writer.Write(CharacterSkinIndex);
            writer.Write(Team);
            writer.Write(TeamSlot);
            writer.Write(PlayerId);
            writer.Write(PrepCatalystName);
            writer.Write(DashCatalystName);
            writer.Write(CombatCatalystName);
            writer.Write(PrepCatalystUsed);
            writer.Write(DashCatalystUsed);
            writer.Write(CombatCatalystUsed);
            writer.Write(PowerupsCollected);
            writer.Write((byte)PlayerGameResult);
            AllianceMessageBase.SerializeObject(MatchResults, writer);
            writer.Write(TotalGameTurns);
            writer.Write(TotalPotentialAbsorb);
            writer.Write(TotalTechPointsGained);
            writer.Write(TotalHealingReceived);
            writer.Write(TotalAbsorbReceived);
            writer.Write(NumKills);
            writer.Write(NumDeaths);
            writer.Write(NumAssists);
            writer.Write(TotalPlayerDamage);
            writer.Write(TotalPlayerHealing);
            writer.Write(TotalPlayerAbsorb);
            writer.Write(TotalPlayerDamageReceived);
            writer.Write(TotalTeamDamageReceived);
            writer.Write(NetDamageAvoidedByEvades);
            writer.Write(DamageAvoidedByEvades);
            writer.Write(DamageInterceptedByEvades);
            writer.Write(MyIncomingDamageReducedByCover);
            writer.Write(MyOutgoingDamageReducedByCover);
            writer.Write(MyOutgoingExtraDamageFromEmpowered);
            writer.Write(MyOutgoingReducedDamageFromWeakened);
            writer.Write(TeamOutgoingDamageIncreasedByEmpoweredFromMe);
            writer.Write(TeamIncomingDamageReducedByWeakenedFromMe);
            writer.Write(MovementDeniedByMe);
            writer.Write(EnergyGainPerTurn);
            writer.Write(DamageEfficiency);
            writer.Write(KillParticipation);
            writer.Write(EffectiveHealing);
            writer.Write(EffectiveHealingFromAbility);
            writer.Write(TeamExtraEnergyByEnergizedFromMe);
            writer.Write(EnemiesSightedPerTurn);
            writer.Write(CharacterSpecificStat);
            writer.Write(FreelancerStats.Count);
            foreach (int num in FreelancerStats)
            {
                writer.Write(num);
            }
            writer.Write(BaseISOGained);
            writer.Write(BaseFreelancerCurrencyGained);
            writer.Write(BaseXPGainedAccount);
            writer.Write(BaseXPGainedCharacter);
            writer.Write(WinFreelancerCurrencyGained);
            writer.Write(FirstWinFreelancerCurrencyGained);
            writer.Write(EventBonusFreelancerCurrencyGained);
            writer.Write(GGPacksSelfUsed);
            writer.Write(GGNonSelfCount);
            writer.Write(GGISOGained);
            writer.Write(GGFreelancerCurrencyGained);
            writer.Write(GGXPGainedAccount);
            writer.Write(GGXPGainedCharacter);
            writer.Write(ConsumableXPGainedCharacter);
            writer.Write(PlayWithFriendXPGainedAccount);
            writer.Write(PlayWithFriendXPGainedCharacter);
            writer.Write(QuestXPGainedAccount);
            writer.Write(QuestXPGainedCharacter);
            writer.Write(EventBonusXPGainedAccount);
            writer.Write(EventBonusXPGainedCharacter);
            writer.Write(FirstWinXpGainedAccount);
            writer.Write(FirstWinXpGainedCharacter);
            writer.Write(QueueTimeXpGainedAccount);
            writer.Write(QueueTimeXpGainedCharacter);
            writer.Write(FreelancerOwnedXPAmount);
            writer.Write(ActorIndex);
            writer.Write(QueueTime.Ticks);
            writer.Write(AbilityGameSummaryList.Count);
            foreach (AbilityGameSummary abilityGameSummary in AbilityGameSummaryList)
            {
                abilityGameSummary.Serialize(writer);
            }
            writer.Write(TimebankUsage.Count);
            foreach (int num2 in TimebankUsage)
            {
                writer.Write(num2);
            }
            writer.Write(TotalBadgePoints);
        }
    }
}
