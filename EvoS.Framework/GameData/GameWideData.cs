using System;
using System.Collections.Generic;
using System.IO;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using log4net;

namespace EvoS.Framework.GameData
{
	public class GameWideData
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(GameWideData));
		
		// [Header("Buff [Haste]")]
		public int m_hasteHalfMovementAdjustAmount;
		public int m_hasteFullMovementAdjustAmount;
		public float m_hasteMovementMultiplier = 1f;
		// [Header("Debuff [SlowMovement]")]
		public int m_slowHalfMovementAdjustAmount;
		public int m_slowFullMovementAdjustAmount;
		public float m_slowMovementMultiplier = 1f;
		// [Header("Buff [Empowered]")]
		public AbilityModPropertyInt m_empoweredOutgoingDamageMod;
		public AbilityModPropertyInt m_empoweredOutgoingHealingMod;
		public AbilityModPropertyInt m_empoweredOutgoingAbsorbMod;
		// [Header("Debuff [Weakened]")]
		public AbilityModPropertyInt m_weakenedOutgoingDamageMod;
		public AbilityModPropertyInt m_weakenedOutgoingHealingMod;
		public AbilityModPropertyInt m_weakenedOutgoingAbsorbMod;
		// [Header("Buff [Armored]")]
		public AbilityModPropertyInt m_armoredIncomingDamageMod;
		// [Header("Debuff [Vulnerable] (values used if positive)")]
		public float m_vulnerableDamageMultiplier = -1f;
		public int m_vulnerableDamageFlatAdd;
		// [Separator("Gameplay Misc", true)]
		public List<StatusType> m_statusesToDelayFromCombatToNextTurn;
		public int m_killAssistMemory = 2;
		public int AdvancedSkinUnlockLevel = 6;
		public int ExpertSkinUnlockLevel = 8;
		public int MasterySkinUnlockLevel = 10;
		// public Ability m_gameModeAbility; // incompatible with old Evos types
		public int NumOverconsPerTurn = 3;
		public int NumOverconsPerMatch = 10;
		public int FreeAutomaticOverconOnCatalyst_OverconId = -1;
		public int FreeAutomaticOverconOnDeath_OverconID = -1;
		// [Space(10f)]
		public bool m_useEnergyStatusForPassiveRegen;
		// [Header("Buff [Energized]")]
		public AbilityModPropertyInt m_energizedEnergyGainMod;
		// [Header("Debuff [SlowEnergyGain]")]
		public AbilityModPropertyInt m_slowEnergyGainEnergyGainMod;
		// [Separator("Character Resource Links", true)]
		public CharacterResourceLink[] m_characterResourceLinks;
		private readonly Dictionary<CharacterType, CharacterResourceLink> m_characterResourceLinkDictionary = new();
		// public GameObject SpectatorPrefab;
		// [Separator("Map Data", true)]
		public MapData[] m_mapData;
		private Dictionary<string, MapData> m_mapDataDictionary;
		// [Separator("Targeting", true)]
		public float m_actorTargetingRadiusInSquares = 0.4f;
		public float m_laserInitialOffsetInSquares = 0.41f;
		public bool m_useActorRadiusForLaser;
		public bool m_useActorRadiusForCones;
		// [Header("-- Max angle for bouncing off actors")]
		public float m_maxAngleForBounceOnActor = 90f;
		// [Separator("Visibility On Ability Cast", true)]
		public bool m_abilityCasterVisibleOnCast;
		// [Header("-- Game Balance Vars --")]
		public GameBalanceVars m_gameBalanceVars = new GameBalanceVars();
		// [Header("-- Banned Words --")]
		public BannedWords m_bannedWords = new BannedWords();
		// [Header("-- Loot Matrix Packs --")]
		public LootMatrixPackData m_lootMatrixPackData = new LootMatrixPackData();
		// [Header("-- Game Packs --")]
		public GamePackData m_gamePackData = new GamePackData();
		// [Header("-- GG Boost Packs --")]
		public GGPackData m_ggPackData = new GGPackData();
		// [Header("-- Loading Tips --")]
		public string[] m_loadingTips;
		// [Separator("Timebank", true)]
		public float m_tbInitial;
		public float m_tbRecharge;
		public float m_tbRechargeCap;
		public int m_tbConsumables;
		public float m_tbConsumableDuration = 5f;
		public float m_tbGracePeriodBeforeConsuming = 0.2f;
		// [Separator("Key Command Data", true)]
		public KeyBindingCommand[] m_keyBindingData;
		private Dictionary<string, KeyBindingCommand> m_keyBindingDataDictionary;
		
		private static Lazy<GameWideData> _instance = new Lazy<GameWideData>(() =>
		{
			GameWideData data = DefaultJsonSerializer.DeserializeExtended<GameWideData>(
				File.ReadAllText("Config/GameData/GameWideData.json"));
			data.Awake();
			return data;
		});

		private void Awake()
		{
			if (m_characterResourceLinks.Length == 0)
			{
				throw new Exception("GameWideData failed to load (no character resource links)");
			}

			List<GameBalanceVars.CharacterUnlockData> list = new List<GameBalanceVars.CharacterUnlockData>();
			for (int i = 0; i < m_characterResourceLinks.Length; i++)
			{
				if (m_characterResourceLinks[i].m_characterType == CharacterType.None)
				{
					throw new Exception($"GameWideData failed to load (invalid data for character index {i})");
				}

				list.Add(m_characterResourceLinks[i].CreateUnlockData());
				m_characterResourceLinkDictionary.Add(m_characterResourceLinks[i].m_characterType, m_characterResourceLinks[i]);
			}

			m_gameBalanceVars.characterUnlockData = list.ToArray();
		}

		public static GameWideData Get()
		{
			return _instance.Value;
		}

		public bool UseActorRadiusForLaser()
		{
			return m_useActorRadiusForLaser;
		}

		public bool UseActorRadiusForCone()
		{
			return m_useActorRadiusForCones;
		}

		public bool ShouldMakeCasterVisibleOnCast()
		{
			return m_abilityCasterVisibleOnCast;
		}

		public CharacterResourceLink GetCharacterResourceLink(CharacterType characterType)
		{
			if (m_characterResourceLinkDictionary.TryGetValue(characterType, out CharacterResourceLink data))
			{
				return data;
			}

			throw new Exception($"Character resource link not found for: {characterType} in GameWideData.");
		}

		// custom
		public bool IsVisualInfoSelectionValid(CharacterType characterType, CharacterVisualInfo selection)
		{
			return m_characterResourceLinkDictionary.TryGetValue(characterType, out CharacterResourceLink data)
			       && data.IsVisualInfoSelectionValid(selection);
		}

		// public string GetCharacterDisplayName(CharacterType characterType)
		// {
		// 	return StringUtil.TR_CharacterName(characterType.ToString());
		// }
		//
		// public string GetLoadingScreenTip(int tipIndex)
		// {
		// 	return StringUtil.TR_LoadingScreenTip(tipIndex + 1);
		// }

		public MapData GetMapDataByDisplayName(string mapDisplayName)
		{
			if (mapDisplayName == null)
			{
				return null;
			}

			log.Debug("attempting to find: " + mapDisplayName);
			foreach (MapData mapData in m_mapData)
			{
				log.Debug(mapData.DisplayName);
				if (mapData.DisplayName.ToLower() == mapDisplayName.ToLower())
				{
					return mapData;
				}
			}

			return null;
		}

		public MapData GetMapData(string mapName)
		{
			if (mapName == null)
			{
				return null;
			}

			if (m_mapDataDictionary == null)
			{
				m_mapDataDictionary = new Dictionary<string, MapData>(StringComparer.OrdinalIgnoreCase);
				foreach (MapData mapData in m_mapData)
				{
					m_mapDataDictionary.Add(mapData.Name, mapData);
				}
			}

			if (m_mapDataDictionary.TryGetValue(mapName, out MapData value))
			{
				return value;
			}

			return null;
		}

		public string GetMapDisplayName(string mapName)
		{
			MapData mapData = GetMapData(mapName);
			if (mapData == null)
			{
				string text = StringUtil.TR_MapName(mapName);
				return !text.IsNullOrEmpty() ? text : mapName;
			}

			return mapData.GetDisplayName();
		}

		public KeyBindingCommand GetKeyBindingCommand(string keyBindName)
		{
			if (keyBindName == null)
			{
				return null;
			}

			if (m_keyBindingDataDictionary == null)
			{
				m_keyBindingDataDictionary = new Dictionary<string, KeyBindingCommand>();
				foreach (KeyBindingCommand keyBindingCommand in m_keyBindingData)
				{
					m_keyBindingDataDictionary.Add(keyBindingCommand.Name, keyBindingCommand);
				}
			}

			if (m_keyBindingDataDictionary.TryGetValue(keyBindName, out KeyBindingCommand value))
			{
				return value;
			}

			return null;
		}

		public string GetKeyBindingDisplayName(string keyBindName)
		{
			KeyBindingCommand keyBindingCommand = GetKeyBindingCommand(keyBindName);
			return keyBindingCommand != null
				? keyBindingCommand.GetDisplayName()
				: keyBindName;
		}

		public string GetUnlockString(GameBalanceVars.UnlockData unlock)
		{
			if (unlock == null)
			{
				return string.Empty;
			}

			string text = string.Empty;
			foreach (GameBalanceVars.UnlockCondition unlockCondition in unlock.UnlockConditions)
			{
				if (text != string.Empty)
				{
					text += Environment.NewLine;
				}

				switch (unlockCondition.ConditionType)
				{
					case GameBalanceVars.UnlockData.UnlockType.CharacterLevel:
					{
						CharacterType typeSpecificData = (CharacterType)unlockCondition.typeSpecificData;
						if (typeSpecificData != 0)
						{
							text += $"{GetCharacterResourceLink(typeSpecificData).m_displayName} Level {unlockCondition.typeSpecificData2}";
						}

						break;
					}
					case GameBalanceVars.UnlockData.UnlockType.PlayerLevel:
					{
						text += $"Account Level {unlockCondition.typeSpecificData}";
						break;
					}
					case GameBalanceVars.UnlockData.UnlockType.ELO:
					{
						text += $"ELO of {unlockCondition.typeSpecificData}";
						break;
					}
				}
			}

			return text;
		}

		private void OnValidate()
		{
			m_gameBalanceVars.OnValidate();
		}
	}
}
