using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Numerics;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.GameData;

public class CharacterResourceLink
{
	public string m_displayName;
	// [TextArea(1, 5, order = 1)]
	public string m_charSelectTooltipDescription = "Edit this in the inspector";
	// [TextArea(1, 5, order = 1)]
	public string m_charSelectAboutDescription = "Edit this in the inspector";
	// [TextArea(1, 5, order = 1)]
	public string m_characterBio = "Edit this in the inspector";
	// [Header("-- Character Icons --")]
	// [AssetFileSelector("Assets/UI/Textures/Resources/CharacterIcons/", "CharacterIcons/", ".png")]
	public string m_characterIconResourceString;
	// [AssetFileSelector("Assets/UI/Textures/Resources/CharacterIcons/", "CharacterIcons/", ".png")]
	public string m_characterSelectIconResourceString;
	// [AssetFileSelector("Assets/UI/Textures/Resources/CharacterIcons/", "CharacterIcons/", ".png")]
	public string m_characterSelectIcon_bwResourceString;
	// [AssetFileSelector("Assets/UI/Textures/Resources/CharacterIcons/", "CharacterIcons/", ".png")]
	public string m_loadingProfileIconResourceString;
	public string m_actorDataResourcePath;
	// [Separator("Scale/Offset in frontend UI (character select, collections)")]
	public Vector3 m_loadScreenPosition;
	public float m_loadScreenScale;
	public float m_loadScreenDistTowardsCamera;
	// [Space(10f)]
	public CharacterType m_characterType;
	public CharacterRole m_characterRole;
	public Color m_characterColor;
	public int m_factionBannerID;
	public bool m_allowForBots;
	public bool m_allowForPlayers;
	public bool m_isHidden;
	public GameBalanceVars.CharResourceLinkCharUnlockData m_charUnlockData;
	public string m_isHiddenFromFreeRotationUntil;
	public string m_twitterHandle;
	// [Space(10f)]
	public CountryPrices Prices;
	// [Space(10f)]
	[Range(0f, 10f)]
	public int m_statHealth;
	[Range(0f, 10f)]
	public int m_statDamage;
	[Range(0f, 10f)]
	public int m_statSurvival;
	[Range(0f, 10f)]
	public int m_statDifficulty;
	public List<CharacterSkin> m_skins = new List<CharacterSkin>();
	public List<CharacterTaunt> m_taunts = new List<CharacterTaunt>();
	public List<CharacterAbilityVfxSwap> m_vfxSwapsForAbility0 = new List<CharacterAbilityVfxSwap>();
	public List<CharacterAbilityVfxSwap> m_vfxSwapsForAbility1 = new List<CharacterAbilityVfxSwap>();
	public List<CharacterAbilityVfxSwap> m_vfxSwapsForAbility2 = new List<CharacterAbilityVfxSwap>();
	public List<CharacterAbilityVfxSwap> m_vfxSwapsForAbility3 = new List<CharacterAbilityVfxSwap>();
	public List<CharacterAbilityVfxSwap> m_vfxSwapsForAbility4 = new List<CharacterAbilityVfxSwap>();
	public string camSequenceFolderName;
	// [Tooltip("Audio assets default prefabs. (For front end)")]
	// [Header("-- Audio Assets --")]
	public PrefabResourceLink[] m_audioAssetsFrontEndDefaultPrefabs;
	// [Tooltip("Audio assets default prefabs. (For in game)")]
	public PrefabResourceLink[] m_audioAssetsInGameDefaultPrefabs;
	// [Header("-- FX preloading --")]
	// [Tooltip("Checked if this character will ever have any VFX made with .pkfx files")]
	public bool m_willEverHavePkfx = true;
	// [LeafDirectoryPopup("Directory containing all .pkfx files for this skin", "PackFx/Character/Hero")]
	public string m_pkfxDirectoryDefault;

	internal const string c_heroPKFXRelativePath = "PackFx/Character/Hero";
	private const string kAssassionIcon = "iconAssassin";
	private const string kSupportIcon = "iconSupport";
	private const string kTankIcon = "iconTank";

	public GameBalanceVars.CharacterUnlockData CreateUnlockData()
	{
		GameBalanceVars.CharacterUnlockData characterUnlockData = new GameBalanceVars.CharacterUnlockData();
		characterUnlockData.character = m_characterType;
		m_charUnlockData.CopyValuesTo(characterUnlockData);
		characterUnlockData.Name = m_displayName;
		List<GameBalanceVars.SkinUnlockData> list = new List<GameBalanceVars.SkinUnlockData>();
		for (int i = 0; i < m_skins.Count; i++)
		{
			CharacterSkin characterSkin = m_skins[i];
			GameBalanceVars.SkinUnlockData skinUnlockData = new GameBalanceVars.SkinUnlockData();
			characterSkin.m_skinUnlockData.CopyValuesTo(skinUnlockData);
			skinUnlockData.m_isHidden = characterSkin.m_isHidden;
			skinUnlockData.Name = characterSkin.m_name;
			skinUnlockData.SetCharacterTypeInt((int)m_characterType);
			skinUnlockData.SetID(i);
			List<GameBalanceVars.PatternUnlockData> list2 = new List<GameBalanceVars.PatternUnlockData>();
			for (int j = 0; j < characterSkin.m_patterns.Count; j++)
			{
				CharacterPattern characterPattern = characterSkin.m_patterns[j];
				GameBalanceVars.PatternUnlockData patternUnlockData = new GameBalanceVars.PatternUnlockData();
				characterPattern.m_patternUnlockData.CopyValuesTo(patternUnlockData);
				patternUnlockData.m_isHidden = characterPattern.m_isHidden;
				patternUnlockData.Name = characterPattern.m_name;
				patternUnlockData.SetCharacterTypeInt((int)m_characterType);
				patternUnlockData.SetSkinIndex(i);
				patternUnlockData.SetID(j);
				List<GameBalanceVars.ColorUnlockData> list3 = new List<GameBalanceVars.ColorUnlockData>();
				for (int k = 0; k < characterPattern.m_colors.Count; k++)
				{
					CharacterColor characterColor = characterPattern.m_colors[k];
					GameBalanceVars.ColorUnlockData colorUnlockData = new GameBalanceVars.ColorUnlockData();
					characterColor.m_colorUnlockData.CopyValuesTo(colorUnlockData);
					colorUnlockData.m_isHidden = characterColor.m_isHidden;
					colorUnlockData.m_sortOrder = characterColor.m_sortOrder;
					colorUnlockData.Name = characterColor.m_name;
					colorUnlockData.SetCharacterTypeInt((int)m_characterType);
					colorUnlockData.SetSkinIndex(i);
					colorUnlockData.SetPatternIndex(j);
					colorUnlockData.SetID(k);
					list3.Add(colorUnlockData);
				}
				patternUnlockData.colorUnlockData = list3.ToArray();
				list2.Add(patternUnlockData);
			}
			skinUnlockData.patternUnlockData = list2.ToArray();
			list.Add(skinUnlockData);
		}
		characterUnlockData.skinUnlockData = list.ToArray();
		List<GameBalanceVars.TauntUnlockData> list4 = new List<GameBalanceVars.TauntUnlockData>();
		for (int l = 0; l < m_taunts.Count; l++)
		{
			GameBalanceVars.TauntUnlockData tauntUnlockData = m_taunts[l].m_tauntUnlockData.Clone();
			tauntUnlockData.Name = m_taunts[l].m_tauntName;
			tauntUnlockData.m_isHidden = m_taunts[l].m_isHidden;
			tauntUnlockData.SetCharacterTypeInt((int)m_characterType);
			tauntUnlockData.SetID(l);
			list4.Add(tauntUnlockData);
		}
		characterUnlockData.tauntUnlockData = list4.ToArray();
		List<GameBalanceVars.AbilityVfxUnlockData> list5 = new List<GameBalanceVars.AbilityVfxUnlockData>();
		GenerateVfxSwapUnlockData(m_vfxSwapsForAbility0, 0, list5);
		GenerateVfxSwapUnlockData(m_vfxSwapsForAbility1, 1, list5);
		GenerateVfxSwapUnlockData(m_vfxSwapsForAbility2, 2, list5);
		GenerateVfxSwapUnlockData(m_vfxSwapsForAbility3, 3, list5);
		GenerateVfxSwapUnlockData(m_vfxSwapsForAbility4, 4, list5);
		characterUnlockData.abilityVfxUnlockData = list5.ToArray();
		return characterUnlockData;
	}

	// custom
	public bool IsVisualInfoSelectionValid(CharacterVisualInfo selection)
	{
		return selection.skinIndex >= 0
		       && selection.skinIndex < m_skins.Count
		       && selection.patternIndex >= 0
		       && selection.patternIndex < m_skins[selection.skinIndex].m_patterns.Count
		       && selection.colorIndex >= 0
		       && selection.colorIndex < m_skins[selection.skinIndex].m_patterns[selection.patternIndex].m_colors.Count;
	}

	private void GenerateVfxSwapUnlockData(List<CharacterAbilityVfxSwap> input, int abilityIndex, List<GameBalanceVars.AbilityVfxUnlockData> genUnlockDataList)
	{
		if (input != null)
		{
			for (int i = 0; i < input.Count; i++)
			{
				GameBalanceVars.AbilityVfxUnlockData abilityVfxUnlockData = input[i].m_vfxSwapUnlockData.Clone();
				abilityVfxUnlockData.m_isHidden = input[i].m_isHidden;
				abilityVfxUnlockData.SetCharacterTypeInt((int)m_characterType);
				abilityVfxUnlockData.SetSwapAbilityId(abilityIndex);
				abilityVfxUnlockData.SetID(input[i].m_uniqueID);
				abilityVfxUnlockData.Name = input[i].m_swapName;
				genUnlockDataList.Add(abilityVfxUnlockData);
			}
		}
		else
		{
			// Debug.LogWarning("Vfx Swap Data is null on " + gameObject.name);
		}
	}
}
