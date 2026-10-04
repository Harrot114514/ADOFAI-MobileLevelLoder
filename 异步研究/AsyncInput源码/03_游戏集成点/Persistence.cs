using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADOFAI;
using ADOFAI.SteamIntegration;
using GDMiniJSON;
using RDTools;
using Steamworks;
using UnityEngine;
using UnityEngine.UI.Extensions;

public class Persistence : RDClassDll
{
	private class PersistenceCoroutineExecuter : MonoBehaviour
	{
	}

	public const float inputOffsetNotSet = 999f;

	private const string keyPercentCompletion = "percentCompletion";

	private const string keyBestPercentAccuracy = "bestPercentAccuracy";

	private const string keyBestPercentXAccuracy = "bestPercentXAccuracy";

	private const string keyBestSpeedMultiplier = "bestSpeedMultiplier";

	private const string keyWorldAttempts = "worldAttempts";

	private const string keyWorldAttemptsWithoutNewBest = "worldAttemptsWithoutNewBest";

	private const string keyTutorialProgressWorld = "tutorialProgress";

	private const string keyIsHighestPossibleAccuracy = "isHighestPossibleAcc";

	private const string CustomWorldPrefix = "CustomWorld_";

	private const string keyCurrentLevel = "currentLevel";

	private const string keyPassedTutorial = "passedTutorial";

	private const string keyMaxLevel = "maxlevel";

	private const string keyReleaseVersion = "version";

	private const string keyCalibrationPresets = "calibrationPresets";

	private const string keyUnlockedXtra = "unlockedXtra";

	private const string keyUnlockedXC = "unlockedXC";

	private const string keyUnlockedXH = "unlockedXH";

	private const string keyUnlockedXR = "unlockedXR";

	private const string keyUnlockedMD = "unlockedMD";

	private const string keyPlayedFirst5WorldsCutscene = "playedFirst5WorldsCutscene";

	private const string keyPlayedWorld6Cutscene = "keyPlayedWorld6Cutscene";

	private const string keyColorRed = "colorRed";

	private const string keyColorBlue = "colorBlue";

	private const string keyLastUsedFolder = "lastUsedFolder";

	private const string keyLastOpenedLevel = "lastOpenedLevel";

	private const string keyAcceptedAgreement = "acceptedAgreement";

	private const string keyTargetFrameRate = "targetFramerate";

	private const string keyTargetFrameRateMobile = "targetFrameRateMobile";

	private const string keyNextRatingPromptDay = "nextRatingromptDay";

	private const string keyRatedGame = "ratedGame";

	private const string keyDisplayedCLSIntro = "displayedCLSIntro";

	private const string keyCLSSortingParameter = "CLSSortingParameter";

	private const string keyCLSSortingReversed = "CLSSortingReversed";

	private const string keyEditorScaleY = "editorScaleY";

	private const string keyEditorFavoriteEvents = "editorFavoriteEvents";

	private const string keyShowRDOffer = "showRDOffer";

	private const string keyShowXAccuracy = "showXAccuracy";

	private const string keyHitErrorMeterSize = "hitErrorMeterSize";

	private const string keyHitErrorMeterShape = "hitErrorMeterShape";

	private const string keyMultitapTileBehavior = "multitapTileBehavior";

	private const string keyHoldBehavior = "holdBehavior";

	private const string keyFreeroamInvuln = "freeroamInvuln";

	private const string keyShowDetailedResults = "showDetailedResults";

	private const string keySkipIntro = "skipIntro";

	private const string keyLastAnalyticsUpdate = "lastAnalyticsUpdate";

	private const string keyFXAA = "antiAliasing";

	private const string keySavedProgress = "savedProgress";

	private const string keyBanishmentPuzzleComplete = "banishmentPuzzleComplete";

	private const string keyKaboomClears = "kaboomClears";

	private const string keyKaboomDeaths = "kaboomDeaths";

	private const string keyEditorSteamDeckWarningPassed = "editorSteamDeckWarningPassed";

	private const string keyHideRichPresenceDetails = "hideRichPresenceDetails";

	private const string keyEnableProEvents = "enableProEvents";

	private const string keyForceVisualSettings = "forceVisualSettings";

	private const string keyUnlockAllLevels = "unlockAllLevels";

	private const string keyClearedTechFeatured = "clearedTechFeatured";

	private const string keyMobileTechUnlocked = "mobileTechUnlocked";

	private const string keyForceUnlockTech = "forceUnlockTech";

	private const string keyAudioBufferSize = "audioBufferSize";

	private const string keySamuraiRed = "samuraiRed";

	private const string keySamuraiBlue = "samuraiBlue";

	private const string keyEmojiRed = "faceRed";

	private const string keyEmojiBlue = "faceBlue";

	private const string keyInputOffset = "offset";

	private const string keyVisualOffset = "offset_v";

	private const string keyGlobalVolume = "globalVolume";

	private const string keyMusicVolume = "musicVolume";

	private const string keyHitsoundVolume = "hitsoundVolume";

	private const string keySfxVolume = "sfxVolume";

	private const string keyInterfaceVolume = "interfaceVolume";

	private const string keyVibrate = "vibrate";

	private const string keyVisualQuality = "visualQuality";

	private const string keyVisualEffects = "visualEffects";

	private const string keyLanguage = "language";

	private const string keyVsync = "vSyncKey";

	private const string keyPerfectsOnlyMode = "perfectsOnlyMode";

	private const string keyHitMarginLimit = "hitMarginLimit";

	private const string keyShortcutPlaySpeed = "shortcutPlaySpeed";

	private const string keyQuickScrubbedPlay = "quickScrubbedPlay";

	private const string keyAnimateSpeedChanges = "animateSpeedChanges";

	private const string keyHideCursorWhilePlaying = "hideCursorWhilePlaying";

	private const string keyUseAsynchronousInput = "useAsynchronousInput";

	private const string keyDefaultDifficulty = "defaultDifficulty";

	private const string keyEditorUseLegacyZoom = "editorUseLegacyZoom";

	private const string keyMarkFloorWithComment = "markFloorWithComment";

	private const string keyDisableRewindButton = "disableRewindButton";

	private const string keyDisableEventsPageRepeat = "disableEventsPageRepeat";

	private const string keyUnlockKeyLimiterButton = "unlockKeyLimiterButton";

	private const string keyDisableAutoAngleOffset = "disableAutoAngleOffset";

	private const string keyDisableCameraDecorationFocus = "disableCameraDecorationFocus";

	private const string keyHideEditorUIDuringAutoplay = "hideEditorUIDuringAutoplay";

	private const string keyHideGameplayUIDuringAutoplay = "hideGameplayUIDuringAutoplay";

	private const string keyDisableLevelEditorPause = "disableLevelEditorPause";

	private const string keyTaroStoryProgress = "dlcStoryProgress";

	private const string keyTaroEXProgress = "dlcEXProgress";

	private const string keyTaroMedalsPrefix = "dlcMedals";

	private const string keyTaroT5Time = "dlcT5Time";

	private static PersistenceCoroutineExecuter _coroutineExecuter;

	private static readonly int MaxDifficulty = Enum.GetValues(typeof(Difficulty)).Length - 1;

	public static readonly KeysSetting keyLimiterKeys = new KeysSetting("keyLimiterKeys");

	private static PersistenceCoroutineExecuter coroutineExecuter
	{
		get
		{
			if (_coroutineExecuter == null)
			{
				_coroutineExecuter = new GameObject("Persistence Saver").AddComponent<PersistenceCoroutineExecuter>();
				UnityEngine.Object.DontDestroyOnLoad(_coroutineExecuter.gameObject);
			}
			return _coroutineExecuter;
		}
	}

	public static int bonusWorldIndex => WorldData.dict["B"].index;

	public static string DataPath => Application.persistentDataPath;

	public static PlayerPrefsJson generalPrefs => PlayerPrefsJson.Get(PlayerPrefsJson.FileType.General);

	public static PlayerPrefsJson customPrefs => PlayerPrefsJson.Get(PlayerPrefsJson.FileType.CustomWorld);

	public static string savedCurrentLevel
	{
		get
		{
			return generalPrefs.GetString("currentLevel", "0-0");
		}
		set
		{
			generalPrefs.SetString("currentLevel", value);
			Save();
		}
	}

	public static bool showRDOffer
	{
		get
		{
			return generalPrefs.GetBool("showRDOffer", defaultValue: true);
		}
		set
		{
			generalPrefs.SetBool("showRDOffer", value);
		}
	}

	public static int kaboomClears
	{
		get
		{
			return generalPrefs.GetInt("kaboomClears");
		}
		set
		{
			generalPrefs.SetInt("kaboomClears", value);
			Save();
		}
	}

	public static int kaboomDeaths
	{
		get
		{
			return generalPrefs.GetInt("kaboomDeaths");
		}
		set
		{
			generalPrefs.SetInt("kaboomDeaths", value);
			Save();
		}
	}

	public static bool vibration
	{
		get
		{
			PlayerPrefsJson playerPrefsJson = generalPrefs;
			return playerPrefsJson.GetBool("vibrate", ADOBase.platform switch
			{
				Platform.Android => false, 
				Platform.iOS => true, 
				_ => false, 
			});
		}
		set
		{
			generalPrefs.SetBool("vibrate", value);
			GCS.d_vibrate = value;
			if (!value)
			{
				GameServices.Instance.CancelVibration();
			}
		}
	}

	public static int audioBufferSize
	{
		get
		{
			return PlayerPrefs.GetInt("audioBufferSize", ADOBase.isMobile ? 1024 : 256);
		}
		set
		{
			PlayerPrefs.SetInt("audioBufferSize", value);
			AudioConfiguration configuration = AudioSettings.GetConfiguration();
			configuration.dspBufferSize = value;
			AudioSettings.Reset(configuration);
		}
	}

	public static VisualQuality visualQuality
	{
		get
		{
			return (VisualQuality)generalPrefs.GetInt("visualQuality", 20);
		}
		set
		{
			generalPrefs.SetInt("visualQuality", (int)value);
		}
	}

	public static int targetFrameRate
	{
		get
		{
			int num = 60;
			Resolution[] resolutions = Screen.resolutions;
			foreach (Resolution resolution in resolutions)
			{
				num = Math.Max((int)Math.Round(resolution.refreshRateRatio.value), num);
			}
			if (num < 120)
			{
				num = 120;
			}
			return generalPrefs.GetInt("targetFramerate", num);
		}
		set
		{
			RDUtils.targetFrameRate = value;
			generalPrefs.SetInt("targetFramerate", value);
		}
	}

	public static int targetFrameRateMobile
	{
		get
		{
			return generalPrefs.GetInt("targetFrameRateMobile", -1);
		}
		set
		{
			generalPrefs.SetInt("targetFrameRateMobile", value);
		}
	}

	public static VisualEffects visualEffects
	{
		get
		{
			if (!ADOBase.isOfficialLevel && !forceVisualSettings)
			{
				return VisualEffects.Full;
			}
			return (VisualEffects)generalPrefs.GetInt("visualEffects", 1);
		}
		set
		{
			generalPrefs.SetInt("visualEffects", (int)value);
		}
	}

	public static VisualEffects realVisualEffects => (VisualEffects)generalPrefs.GetInt("visualEffects", 1);

	public static bool showDetailedResults
	{
		get
		{
			return generalPrefs.GetBool("showDetailedResults");
		}
		set
		{
			generalPrefs.SetBool("showDetailedResults", value);
		}
	}

	public static bool passedMobileMenuTutorial
	{
		get
		{
			bool result = generalPrefs.GetBool("passedTutorial");
			if (GetOverallProgressStage() > 0)
			{
				result = (passedMobileMenuTutorial = true);
			}
			return result;
		}
		set
		{
			generalPrefs.SetBool("passedTutorial", value);
		}
	}

	public static int globalVolume
	{
		get
		{
			return generalPrefs.GetInt("globalVolume", 10);
		}
		set
		{
			generalPrefs.SetInt("globalVolume", value);
		}
	}

	public static int musicVolume
	{
		get
		{
			return generalPrefs.GetInt("musicVolume", 10);
		}
		set
		{
			generalPrefs.SetInt("musicVolume", value);
		}
	}

	public static int hitSoundVolume
	{
		get
		{
			return generalPrefs.GetInt("hitsoundVolume", 10);
		}
		set
		{
			generalPrefs.SetInt("hitsoundVolume", value);
		}
	}

	public static int sfxVolume
	{
		get
		{
			return generalPrefs.GetInt("sfxVolume", 10);
		}
		set
		{
			generalPrefs.SetInt("sfxVolume", value);
		}
	}

	public static int interfaceVolume
	{
		get
		{
			return generalPrefs.GetInt("interfaceVolume", 10);
		}
		set
		{
			generalPrefs.SetInt("interfaceVolume", value);
		}
	}

	public static SystemLanguage language
	{
		get
		{
			SystemLanguage systemLanguage = Application.systemLanguage;
			return Enum.Parse<SystemLanguage>(generalPrefs.GetString("language", systemLanguage.ToString()));
		}
		set
		{
			generalPrefs.SetString("language", value.ToString());
		}
	}

	public static HitMarginLimit hitMarginLimit
	{
		get
		{
			return (HitMarginLimit)generalPrefs.GetInt("hitMarginLimit");
		}
		set
		{
			generalPrefs.SetInt("hitMarginLimit", (int)value);
		}
	}

	public static int vSync
	{
		get
		{
			return generalPrefs.GetInt("vSyncKey", QualitySettings.vSyncCount);
		}
		set
		{
			generalPrefs.SetInt("vSyncKey", value);
		}
	}

	public static bool unlockedXF
	{
		get
		{
			if (GCS.d_booth)
			{
				return false;
			}
			return generalPrefs.GetBool("unlockedXtra");
		}
		set
		{
			generalPrefs.SetBool("unlockedXtra", value);
		}
	}

	public static bool unlockedXC
	{
		get
		{
			return generalPrefs.GetBool("unlockedXC");
		}
		set
		{
			generalPrefs.SetBool("unlockedXC", value);
		}
	}

	public static bool unlockedXH
	{
		get
		{
			return generalPrefs.GetBool("unlockedXH");
		}
		set
		{
			generalPrefs.SetBool("unlockedXH", value);
		}
	}

	public static bool unlockedXR
	{
		get
		{
			return generalPrefs.GetBool("unlockedXR");
		}
		set
		{
			generalPrefs.SetBool("unlockedXR", value);
		}
	}

	public static bool unlockedMD
	{
		get
		{
			return generalPrefs.GetBool("unlockedMD");
		}
		set
		{
			generalPrefs.SetBool("unlockedMD", value);
		}
	}

	public static bool playedFirst5WorldsCutscene
	{
		get
		{
			return generalPrefs.GetBool("playedFirst5WorldsCutscene");
		}
		set
		{
			generalPrefs.SetBool("playedFirst5WorldsCutscene", value);
		}
	}

	public static bool playedWorld6Cutscene
	{
		get
		{
			return generalPrefs.GetBool("keyPlayedWorld6Cutscene");
		}
		set
		{
			generalPrefs.SetBool("keyPlayedWorld6Cutscene", value);
		}
	}

	public static List<LevelEventType> favoriteEditorEvents
	{
		get
		{
			List<object> list = generalPrefs.GetList("editorFavoriteEvents");
			List<LevelEventType> list2 = new List<LevelEventType>();
			foreach (object item in list)
			{
				list2.Add(Enum.Parse<LevelEventType>(item.ToString()));
			}
			return list2;
		}
	}

	public static float inputOffset
	{
		get
		{
			return PlayerPrefs.GetFloat("offset", 999f);
		}
		set
		{
			generalPrefs.SetFloat("offset", value);
		}
	}

	public static float visualOffset
	{
		get
		{
			return generalPrefs.GetFloat("offset_v");
		}
		set
		{
			generalPrefs.SetFloat("offset_v", value);
		}
	}

	public static bool acceptedAgreement
	{
		get
		{
			return generalPrefs.GetBool("acceptedAgreement");
		}
		set
		{
			generalPrefs.SetBool("acceptedAgreement", value);
		}
	}

	public static bool passedSteamDeckWarning
	{
		get
		{
			return generalPrefs.GetBool("editorSteamDeckWarningPassed");
		}
		set
		{
			generalPrefs.SetBool("editorSteamDeckWarningPassed", value);
		}
	}

	public static bool displayedCLSIntro
	{
		get
		{
			return generalPrefs.GetBool("displayedCLSIntro");
		}
		set
		{
			generalPrefs.SetBool("displayedCLSIntro", value);
		}
	}

	public static int antiAliasing
	{
		get
		{
			return generalPrefs.GetInt("antiAliasing", 1);
		}
		set
		{
			generalPrefs.SetInt("antiAliasing", value);
		}
	}

	public static int taroStoryProgress
	{
		get
		{
			return generalPrefs.GetInt("dlcStoryProgress");
		}
		set
		{
			generalPrefs.SetInt("dlcStoryProgress", value);
		}
	}

	public static int taroEXProgress
	{
		get
		{
			return generalPrefs.GetInt("dlcEXProgress");
		}
		set
		{
			generalPrefs.SetInt("dlcEXProgress", value);
		}
	}

	public static bool banishmentPuzzleComplete
	{
		get
		{
			return generalPrefs.GetBool("banishmentPuzzleComplete");
		}
		set
		{
			generalPrefs.SetBool("banishmentPuzzleComplete", value);
		}
	}

	public static float t5BestTime
	{
		get
		{
			return generalPrefs.GetFloat("dlcT5Time", 60f);
		}
		set
		{
			generalPrefs.SetFloat("dlcT5Time", value);
		}
	}

	public static float editorScale
	{
		get
		{
			return PlayerPrefs.GetFloat("editorScaleY");
		}
		set
		{
			PlayerPrefs.SetFloat("editorScaleY", value);
		}
	}

	public static OptionsPanelsCLS.OptionName clsSortingParameter
	{
		get
		{
			return (OptionsPanelsCLS.OptionName)generalPrefs.GetInt("CLSSortingParameter", 12);
		}
		set
		{
			generalPrefs.SetInt("CLSSortingParameter", (int)value);
		}
	}

	public static bool clsSortingReversed
	{
		get
		{
			return generalPrefs.GetBool("CLSSortingReversed");
		}
		set
		{
			generalPrefs.SetBool("CLSSortingReversed", value);
		}
	}

	public static bool ratedGame
	{
		get
		{
			return generalPrefs.GetBool("ratedGame");
		}
		set
		{
			generalPrefs.SetBool("ratedGame", value);
		}
	}

	public static bool animateSpeedChange
	{
		get
		{
			return generalPrefs.GetBool("animateSpeedChanges", defaultValue: true);
		}
		set
		{
			generalPrefs.SetBool("animateSpeedChanges", value);
		}
	}

	public static bool showXAccuracy
	{
		get
		{
			return generalPrefs.GetBool("showXAccuracy");
		}
		set
		{
			generalPrefs.SetBool("showXAccuracy", value);
		}
	}

	public static ErrorMeterSize hitErrorMeterSize
	{
		get
		{
			return (ErrorMeterSize)generalPrefs.GetInt("hitErrorMeterSize");
		}
		set
		{
			generalPrefs.SetInt("hitErrorMeterSize", (int)value);
		}
	}

	public static ErrorMeterShape hitErrorMeterShape
	{
		get
		{
			return (ErrorMeterShape)generalPrefs.GetInt("hitErrorMeterShape");
		}
		set
		{
			generalPrefs.SetInt("hitErrorMeterShape", (int)value);
		}
	}

	public static SkipIntroBehavior skipIntroBehavior
	{
		get
		{
			return (SkipIntroBehavior)generalPrefs.GetInt("skipIntro", 1);
		}
		set
		{
			generalPrefs.SetInt("skipIntro", (int)value);
		}
	}

	public static MultitapTileBehavior multiTapTileBehavior
	{
		get
		{
			return (MultitapTileBehavior)generalPrefs.GetInt("multitapTileBehavior");
		}
		set
		{
			generalPrefs.SetInt("multitapTileBehavior", (int)value);
		}
	}

	public static HoldBehavior holdBehavior
	{
		get
		{
			return (HoldBehavior)generalPrefs.GetInt("holdBehavior");
		}
		set
		{
			generalPrefs.SetInt("holdBehavior", (int)value);
		}
	}

	public static bool freeroamInvulnerability
	{
		get
		{
			return generalPrefs.GetBool("freeroamInvuln");
		}
		set
		{
			generalPrefs.SetBool("freeroamInvuln", value);
		}
	}

	public static bool unlockAllLevels
	{
		get
		{
			return generalPrefs.GetBool("unlockAllLevels");
		}
		set
		{
			generalPrefs.SetBool("unlockAllLevels", value);
		}
	}

	public static bool markFloorWithComment
	{
		get
		{
			return generalPrefs.GetBool("markFloorWithComment", defaultValue: true);
		}
		set
		{
			generalPrefs.SetBool("markFloorWithComment", value);
		}
	}

	public static bool disableRewindButton
	{
		get
		{
			return generalPrefs.GetBool("disableRewindButton");
		}
		set
		{
			generalPrefs.SetBool("disableRewindButton", value);
		}
	}

	public static int shortcutPlaySpeed
	{
		get
		{
			return generalPrefs.GetInt("shortcutPlaySpeed", 50);
		}
		set
		{
			value = Mathf.Clamp(value, 1, 1000);
			generalPrefs.SetInt("shortcutPlaySpeed", value);
		}
	}

	public static int quickScrubbedPlay
	{
		get
		{
			return generalPrefs.GetInt("quickScrubbedPlay", 1000);
		}
		set
		{
			generalPrefs.SetInt("quickScrubbedPlay", value);
		}
	}

	public static int lastAnalyticsUpdate
	{
		get
		{
			return generalPrefs.GetInt("lastAnalyticsUpdate", -1);
		}
		set
		{
			generalPrefs.SetInt("lastAnalyticsUpdate", value);
		}
	}

	public static bool hideRichPresenceDetails
	{
		get
		{
			if (GCS.isDev)
			{
				return generalPrefs.GetBool("hideRichPresenceDetails");
			}
			return false;
		}
		set
		{
			generalPrefs.SetBool("hideRichPresenceDetails", value);
		}
	}

	public static bool enableProEvents
	{
		get
		{
			if (GCS.isDev)
			{
				return generalPrefs.GetBool("enableProEvents");
			}
			return false;
		}
		set
		{
			generalPrefs.SetBool("enableProEvents", value);
		}
	}

	public static bool forceVisualSettings
	{
		get
		{
			if (GCS.isDev)
			{
				return generalPrefs.GetBool("forceVisualSettings");
			}
			return false;
		}
		set
		{
			generalPrefs.SetBool("forceVisualSettings", value);
		}
	}

	public static bool showUnlockKeyLimiterButton => keyLimiterKeys.Count > 1000;

	public static bool clearedTechFeatured
	{
		get
		{
			if (!Application.isEditor)
			{
				return generalPrefs.GetBool("clearedTechFeatured");
			}
			return true;
		}
		set
		{
			generalPrefs.SetBool("clearedTechFeatured", value);
		}
	}

	public static bool mobileTechUnlocked
	{
		get
		{
			return generalPrefs.GetBool("mobileTechUnlocked");
		}
		set
		{
			generalPrefs.SetBool("mobileTechUnlocked", value);
		}
	}

	public static bool forceUnlockTech
	{
		get
		{
			return generalPrefs.GetBool("forceUnlockTech");
		}
		set
		{
			generalPrefs.SetBool("forceUnlockTech", value);
		}
	}

	public static bool hasBeatBonusLevel => IsWorldComplete(bonusWorldIndex);

	public static bool editorUseLegacyZoom
	{
		get
		{
			return generalPrefs.GetBool("editorUseLegacyZoom");
		}
		set
		{
			generalPrefs.SetBool("editorUseLegacyZoom", value);
		}
	}

	public static bool disableEventsPageRepeat
	{
		get
		{
			return generalPrefs.GetBool("disableEventsPageRepeat");
		}
		set
		{
			generalPrefs.SetBool("disableEventsPageRepeat", value);
		}
	}

	public static bool disableAutoAngleOffset
	{
		get
		{
			return generalPrefs.GetBool("disableAutoAngleOffset");
		}
		set
		{
			generalPrefs.SetBool("disableAutoAngleOffset", value);
		}
	}

	public static bool disableCameraDecorationFocus
	{
		get
		{
			return generalPrefs.GetBool("disableCameraDecorationFocus");
		}
		set
		{
			generalPrefs.SetBool("disableCameraDecorationFocus", value);
		}
	}

	public static bool hideEditorUIDuringAutoplay
	{
		get
		{
			return generalPrefs.GetBool("hideEditorUIDuringAutoplay");
		}
		set
		{
			generalPrefs.SetBool("hideEditorUIDuringAutoplay", value);
		}
	}

	public static bool hideGameplayUIDuringAutoplay
	{
		get
		{
			return generalPrefs.GetBool("hideGameplayUIDuringAutoplay");
		}
		set
		{
			generalPrefs.SetBool("hideGameplayUIDuringAutoplay", value);
		}
	}

	public static bool disableLevelEditorPause
	{
		get
		{
			return generalPrefs.GetBool("disableLevelEditorPause");
		}
		set
		{
			generalPrefs.SetBool("disableLevelEditorPause", value);
		}
	}

	public static float GetPercentCompletion(int worldZeroIndex, bool coop = false, bool ignoreFools = false)
	{
		return generalPrefs.GetFloat(GetCoopPrefix(coop) + "percentCompletion" + dd(worldZeroIndex, ignoreFools));
	}

	public static float GetBestPercentAccuracy(int worldZeroIndex, bool coop)
	{
		return generalPrefs.GetFloat(GetCoopPrefix(coop) + "bestPercentAccuracy" + dd(worldZeroIndex));
	}

	public static float GetBestPercentXAccuracy(int worldZeroIndex, bool coop)
	{
		return generalPrefs.GetFloat(GetCoopPrefix(coop) + "bestPercentXAccuracy" + dd(worldZeroIndex));
	}

	public static float GetBestSpeedMultiplier(int worldZeroIndex, bool coop)
	{
		return generalPrefs.GetFloat(GetCoopPrefix(coop) + "bestSpeedMultiplier" + dd(worldZeroIndex));
	}

	public static int GetWorldAttempts(int worldZeroIndex, bool coop)
	{
		return generalPrefs.GetInt(GetCoopPrefix(coop) + "worldAttempts" + dd(worldZeroIndex));
	}

	public static int GetWorldAttempts(int worldZeroIndex)
	{
		return GetWorldAttempts(worldZeroIndex, coop: false) + GetWorldAttempts(worldZeroIndex, coop: true);
	}

	public static int GetWorldAttemptsWithoutNewBest(int worldZeroIndex, bool coop)
	{
		return generalPrefs.GetInt(GetCoopPrefix(coop) + "worldAttemptsWithoutNewBest" + dd(worldZeroIndex));
	}

	public static bool GetIsHighestPossibleAcc(int worldZeroIndex, bool coop)
	{
		return generalPrefs.GetBool(GetCoopPrefix(coop) + "isHighestPossibleAcc" + dd(worldZeroIndex));
	}

	public static bool GetIsHighestPossibleAcc(int worldZeroIndex)
	{
		if (!GetIsHighestPossibleAcc(worldZeroIndex, coop: false))
		{
			return GetIsHighestPossibleAcc(worldZeroIndex, coop: true);
		}
		return true;
	}

	public static int GetLevelTutorialProgress(string world)
	{
		return GetLevelTutorialProgress(WorldData.dict[world].index);
	}

	public static int GetLevelTutorialProgress(int worldZeroIndexed)
	{
		return generalPrefs.GetInt("tutorialProgress" + dd(worldZeroIndexed));
	}

	public static PlanetColor GetPlayerColor(bool red)
	{
		string text = generalPrefs.GetString(red ? "colorRed" : "colorBlue", null);
		if (Enum.TryParse<PlanetColorPreset>(text, ignoreCase: true, out var result) && Enum.IsDefined(typeof(PlanetColorPreset), result))
		{
			return new PlanetColor(result);
		}
		if (RDUtils.TryHexToColor(text, out var color))
		{
			return new PlanetColor(color);
		}
		return new PlanetColor((!red) ? PlanetColorPreset.DefaultBlue : PlanetColorPreset.DefaultRed);
	}

	public static bool GetSamuraiMode(bool red)
	{
		return generalPrefs.GetBool(red ? "samuraiRed" : "samuraiBlue");
	}

	public static bool GetEmojiMode(bool red)
	{
		return generalPrefs.GetBool(red ? "faceRed" : "faceBlue");
	}

	public static int GetNextRatingPromptDay()
	{
		return generalPrefs.GetInt("nextRatingromptDay", -1);
	}

	public static bool GetDisableRewindButton()
	{
		return generalPrefs.GetBool("disableRewindButton");
	}

	public static bool HasSubscribedToFeatured(ulong levelId)
	{
		return customPrefs.GetBool("SubscribedToFeatured_" + levelId);
	}

	public static bool GetHideCursorWhilePlaying()
	{
		return generalPrefs.GetBool("hideCursorWhilePlaying");
	}

	public static bool GetChosenAsynchronousInput()
	{
		if (!RDC.isSteamDeckOnSteamOS)
		{
			return generalPrefs.GetBool("useAsynchronousInput");
		}
		return false;
	}

	public static Difficulty GetDefaultDifficulty()
	{
		return (Difficulty)generalPrefs.GetInt("defaultDifficulty", 1).Clamp(0, MaxDifficulty);
	}

	public static string GetCoopPrefix(bool coop)
	{
		if (!coop)
		{
			return "";
		}
		return "coop_";
	}

	public static void SetPercentCompletion(int worldZeroIndexed, float pct, bool coop)
	{
		generalPrefs.SetFloat(GetCoopPrefix(coop) + "percentCompletion" + dd(worldZeroIndexed), pct);
	}

	public static void SetBestPercentAccuracy(int worldZeroIndex, float pct, bool coop)
	{
		generalPrefs.SetFloat(GetCoopPrefix(coop) + "bestPercentAccuracy" + dd(worldZeroIndex), pct);
	}

	public static void SetBestPercentXAccuracy(int worldZeroIndex, float pct, bool coop)
	{
		generalPrefs.SetFloat(GetCoopPrefix(coop) + "bestPercentXAccuracy" + dd(worldZeroIndex), pct);
	}

	public static void SetBestSpeedTrial(int worldZeroIndex, float spd, bool coop)
	{
		generalPrefs.SetFloat(GetCoopPrefix(coop) + "bestSpeedMultiplier" + dd(worldZeroIndex), spd);
	}

	public static void SetWorldAttempts(int worldZeroIndex, int attempts, bool coop)
	{
		generalPrefs.SetInt(GetCoopPrefix(coop) + "worldAttempts" + dd(worldZeroIndex), attempts);
	}

	public static void IncrementWorldAttempts(int worldZeroIndex, bool coop)
	{
		generalPrefs.SetInt(GetCoopPrefix(coop) + "worldAttempts" + dd(worldZeroIndex), GetWorldAttempts(worldZeroIndex, coop) + 1);
		Save();
	}

	public static void SetLevelTutorialProgress(int worldZeroIndexed, int level)
	{
		generalPrefs.SetInt("tutorialProgress" + dd(worldZeroIndexed), level);
	}

	public static void SetLevelTutorialProgress(string world, int level)
	{
		SetLevelTutorialProgress(WorldData.dict[world].index, level);
	}

	public static void SetNextRatingPromptDay(int nextDay)
	{
		generalPrefs.SetInt("nextRatingromptDay", nextDay);
	}

	public static void SetIsHighestPossibleAcc(int worldZeroIndex, bool isHighest, bool coop = false, bool save = true)
	{
		generalPrefs.SetBool(GetCoopPrefix(coop) + "isHighestPossibleAcc" + dd(worldZeroIndex), isHighest);
		if (save)
		{
			Save();
		}
	}

	public static void SetWorldAttemptsWithoutNewBest(int worldZeroIndex, int attempts, bool coop)
	{
		generalPrefs.SetInt(GetCoopPrefix(coop) + "worldAttemptsWithoutNewBest" + dd(worldZeroIndex), attempts);
	}

	public static void IncrementWorldAttemptsWithoutNewBest(int worldZeroIndex, bool coop)
	{
		generalPrefs.SetInt(GetCoopPrefix(coop) + "worldAttemptsWithoutNewBest" + dd(worldZeroIndex), GetWorldAttemptsWithoutNewBest(worldZeroIndex, coop) + 1);
		Save();
	}

	public static void SetKaboomClears(int clears)
	{
		generalPrefs.SetInt("kaboomClears", clears);
	}

	public static void SetKaboomDeaths(int deaths)
	{
		generalPrefs.SetInt("kaboomDeaths", deaths);
	}

	public static void SetPlayerColor(PlanetColorPreset planetColor, bool red)
	{
		generalPrefs.SetString(red ? "colorRed" : "colorBlue", planetColor.ToString());
	}

	public static void SetPlayerColor(Color customColor, bool red)
	{
		generalPrefs.SetString(red ? "colorRed" : "colorBlue", ColorUtility.ToHtmlStringRGB(customColor));
	}

	public static void SetPlayerColor(PlanetColor planetColor, bool red)
	{
		if (planetColor.preset == PlanetColorPreset.Custom)
		{
			SetPlayerColor(planetColor.customColor.Value, red);
		}
		else
		{
			SetPlayerColor(planetColor.preset, red);
		}
	}

	public static void SetFavoriteEditorEvents(List<LevelEventType> events)
	{
		List<object> list = new List<object>();
		foreach (LevelEventType @event in events)
		{
			list.Add(@event.ToString());
		}
		generalPrefs.SetList("editorFavoriteEvents", list);
	}

	public static void SetSamuraiMode(bool enabled, bool red)
	{
		generalPrefs.SetBool(red ? "samuraiRed" : "samuraiBlue", enabled);
	}

	public static void ResetMedalsForDLCLevel(string taroWorld)
	{
		generalPrefs.SetString("dlcMedals" + taroWorld, "");
	}

	public static void SetEmojiMode(bool enabled, bool red)
	{
		generalPrefs.SetBool(red ? "faceRed" : "faceBlue", enabled);
	}

	public static int[] GetMedalsForDLCLevel(string taroWorld)
	{
		string text = generalPrefs.GetString("dlcMedals" + taroWorld);
		int medalCount = WorldData.dict[taroWorld].medalCount;
		int[] array = new int[medalCount];
		if (!string.IsNullOrEmpty(text) && text.Length == medalCount)
		{
			for (int i = 0; i < medalCount; i++)
			{
				array[i] = (int)char.GetNumericValue(text[i]);
			}
		}
		return array;
	}

	public static void SetMedalsForDLCLevel(string taroWorld, int[] medals)
	{
		int medalCount = WorldData.dict[taroWorld].medalCount;
		if (medals.Length != medalCount)
		{
			Debug.LogError("medals count should be " + medalCount + "!");
			return;
		}
		char[] array = new char[medalCount];
		for (int i = 0; i < medalCount; i++)
		{
			array[i] = medals[i].ToString()[0];
		}
		string value = new string(array);
		generalPrefs.SetString("dlcMedals" + taroWorld, value);
	}

	public static void SetMedalsForDLCLevel(string taroWorld, string medalsString)
	{
		int medalCount = WorldData.dict[taroWorld].medalCount;
		if (medalsString.Length != medalCount)
		{
			Debug.LogError("medals count should be " + medalCount + "!");
		}
		else
		{
			generalPrefs.SetString("dlcMedals" + taroWorld, medalsString);
		}
	}

	public static bool IsWorldComplete(int worldZeroIndex, bool coop, bool ignoreFools)
	{
		return GetPercentCompletion(worldZeroIndex, coop, ignoreFools) >= 1f;
	}

	public static bool IsWorldComplete(int worldZeroIndex, bool ignoreFools = false)
	{
		if (RDC.forceUnlockAllLevels)
		{
			return true;
		}
		if (!IsWorldComplete(worldZeroIndex, coop: false, ignoreFools))
		{
			return IsWorldComplete(worldZeroIndex, coop: true, ignoreFools);
		}
		return true;
	}

	public static bool IsWorldComplete(string world)
	{
		return IsWorldComplete(WorldData.dict[world].index);
	}

	public static void SetPercentCompletion(string world, float pct, bool coop = false)
	{
		SetPercentCompletion(WorldData.dict[world].index, pct, coop);
	}

	public static bool IsWorldPerfect(int worldZeroIndex, bool coop)
	{
		return GetBestPercentAccuracy(worldZeroIndex, coop) >= 1f;
	}

	public static bool IsWorldPerfect(int worldZeroIndex)
	{
		return Mathf.Max(GetBestPercentAccuracy(worldZeroIndex, coop: false), GetBestPercentAccuracy(worldZeroIndex, coop: true)) >= 1f;
	}

	public static bool IsSpeedTrialComplete(int worldZeroIndex, bool coop)
	{
		if (RDC.forceUnlockAllLevels)
		{
			return true;
		}
		string world = null;
		foreach (KeyValuePair<string, WorldData> item in WorldData.dict)
		{
			_ = item.Value.index;
			if (item.Value.index == worldZeroIndex)
			{
				world = item.Key;
				break;
			}
		}
		float num = GetSpeedTrialAimForWorld(world) - 0.01f;
		return GetBestSpeedMultiplier(worldZeroIndex, coop) > num;
	}

	public static bool IsSpeedTrialComplete(int worldZeroIndex)
	{
		if (!IsSpeedTrialComplete(worldZeroIndex, coop: false))
		{
			return IsSpeedTrialComplete(worldZeroIndex, coop: true);
		}
		return true;
	}

	public static float GetSpeedTrialAimForWorld(string world)
	{
		if (GCS.FOOL_JOKER && !world.EndsWith("J") && WorldData.dict.ContainsKey(world + "J"))
		{
			world += "J";
		}
		return WorldData.dict[world].trialAim;
	}

	public static bool ShouldShowSpeedTrials()
	{
		return GetOverallProgressStage() >= 5;
	}

	public static void SetSubscribedToFeatured(ulong levelId, bool subscribed)
	{
		customPrefs.SetBool("SubscribedToFeatured_" + levelId, subscribed);
	}

	public static void SetHideCursorWhilePlaying(bool hide)
	{
		generalPrefs.SetBool("hideCursorWhilePlaying", hide);
	}

	public static void SetChosenAsynchronousInput(bool enabled)
	{
		generalPrefs.SetBool("useAsynchronousInput", enabled);
	}

	public static void SetDefaultDifficulty(Difficulty difficulty)
	{
		generalPrefs.SetInt("defaultDifficulty", (int)difficulty);
	}

	public static bool ShowTechLevels()
	{
		if (forceUnlockTech)
		{
			return true;
		}
		if (clearedTechFeatured)
		{
			return WorldData.crownWorlds.All(IsWorldComplete);
		}
		return false;
	}

	public static Dictionary<string, object> GetSavedProgress()
	{
		return generalPrefs.GetDict("savedProgress");
	}

	public static void SetSavedProgress(Dictionary<string, object> dict)
	{
		generalPrefs.SetDict("savedProgress", dict);
	}

	public static void DeleteSavedProgress()
	{
		generalPrefs.RemoveKey("savedProgress");
	}

	public static float GetCustomWorldCompletion(string hash)
	{
		return customPrefs.GetFloat("CustomWorld_" + hash + "_Completion");
	}

	public static int GetCustomWorldAttempts(string hash)
	{
		return customPrefs.GetInt("CustomWorld_" + hash + "_Attempts");
	}

	public static float GetCustomWorldAccuracy(string hash)
	{
		return customPrefs.GetFloat("CustomWorld_" + hash + "_Accuracy");
	}

	public static float GetCustomWorldXAccuracy(string hash)
	{
		return customPrefs.GetFloat("CustomWorld_" + hash + "_XAccuracy");
	}

	public static float GetCustomWorldSpeedTrial(string hash)
	{
		return customPrefs.GetFloat("CustomWorld_" + hash + "_SpeedTrial");
	}

	public static int GetCustomWorldMinDeaths(string hash)
	{
		return customPrefs.GetInt("CustomWorld_" + hash + "_MinDeaths", -1);
	}

	public static int GetCustomWorldPlayIndex(string hash)
	{
		return customPrefs.GetInt("CustomWorld_" + hash + "_PlayIndex", -1);
	}

	public static bool GetCustomWorldHasBeenEntered(string hash)
	{
		if (GetCustomWorldPlayIndex(hash) <= 0)
		{
			return GetCustomWorldCompletion(hash) > 0f;
		}
		return true;
	}

	public static bool GetCustomWorldIsHighestPossibleAcc(string hash)
	{
		return customPrefs.GetBool("CustomWorld_" + hash + "_isHighestPossibleAcc");
	}

	public static int GetCLSTotalPlays()
	{
		return customPrefs.GetInt("CLSTotalPlays");
	}

	public static void SetCustomWorldCompletion(string hash, float completion)
	{
		customPrefs.SetFloat("CustomWorld_" + hash + "_Completion", completion);
	}

	public static void SetCustomWorldAttempts(string hash, int attempts)
	{
		customPrefs.SetInt("CustomWorld_" + hash + "_Attempts", attempts);
	}

	public static void SetCustomWorldAccuracy(string hash, float accuracy)
	{
		customPrefs.SetFloat("CustomWorld_" + hash + "_Accuracy", accuracy);
	}

	public static void SetCustomWorldXAccuracy(string hash, float accuracy)
	{
		customPrefs.SetFloat("CustomWorld_" + hash + "_XAccuracy", accuracy);
	}

	public static void SetCustomWorldSpeedTrial(string hash, float multiplier)
	{
		customPrefs.SetFloat("CustomWorld_" + hash + "_SpeedTrial", multiplier);
	}

	public static void SetCustomWorldMinDeaths(string hash, int deaths)
	{
		customPrefs.SetInt("CustomWorld_" + hash + "_MinDeaths", deaths);
	}

	public static void SetCustomWorldPlayIndex(string hash, int playIndex)
	{
		customPrefs.SetInt("CustomWorld_" + hash + "_PlayIndex", playIndex);
	}

	public static void SetCustomWorldIsHighestPossibleAcc(string hash, bool isHighest)
	{
		customPrefs.SetBool("CustomWorld_" + hash + "_isHighestPossibleAcc", isHighest);
	}

	public static void SetCLSTotalPlays(int totalPlays)
	{
		customPrefs.SetInt("CLSTotalPlays", totalPlays);
	}

	public static void IncrementCustomWorldAttempts(string hash)
	{
		int attempts = GetCustomWorldAttempts(hash) + 1;
		SetCustomWorldAttempts(hash, attempts);
		Save();
	}

	public static void IncrementCLSTotalPlays()
	{
		SetCLSTotalPlays(GetCLSTotalPlays() + 1);
		Save();
	}

	public static string GetWorldAchievementPrefix(string world)
	{
		int index = WorldData.dict[world].index;
		string text = ((index > 12) ? world : index.ToString());
		return "World" + text;
	}

	public static void UnlockAchievementWithName(string name)
	{
		SteamController.Instance.Achievements.Add(name);
	}

	public static void GiveAchievements()
	{
		if (!SteamController.initialized)
		{
			return;
		}
		string[] allWorlds = WorldData.allWorlds;
		foreach (string text in allWorlds)
		{
			if (!(text == "B"))
			{
				int index = WorldData.dict[text].index;
				string worldAchievementPrefix = GetWorldAchievementPrefix(text);
				if (IsWorldComplete(index))
				{
					UnlockAchievementWithName(worldAchievementPrefix + "Complete");
				}
				if (IsWorldPerfect(index))
				{
					UnlockAchievementWithName(worldAchievementPrefix + "Perfect");
				}
				if (IsSpeedTrialComplete(index))
				{
					UnlockAchievementWithName(worldAchievementPrefix + "Trial");
				}
			}
		}
		bool flag = true;
		allWorlds = WorldData.dlcWorlds;
		foreach (string text2 in allWorlds)
		{
			if (text2.EndsWith("EX") && !IsWorldComplete(WorldData.dict[text2].index))
			{
				flag = false;
				break;
			}
		}
		bool flag2 = true;
		bool flag3 = true;
		allWorlds = WorldData.xtraWorlds;
		foreach (string key in allWorlds)
		{
			int index2 = WorldData.dict[key].index;
			if (!IsWorldComplete(index2))
			{
				flag3 = false;
			}
			if (!IsSpeedTrialComplete(index2))
			{
				flag2 = false;
			}
		}
		bool flag4 = true;
		allWorlds = WorldData.museDashWorlds;
		foreach (string text3 in allWorlds)
		{
			if (!(text3 == "MO") && !IsWorldComplete(text3))
			{
				flag4 = false;
				break;
			}
		}
		if (flag3)
		{
			UnlockAchievementWithName("XtraComplete");
		}
		if (flag2)
		{
			UnlockAchievementWithName("XtraTrial");
		}
		if (flag4)
		{
			UnlockAchievementWithName("MuseDashComplete");
		}
		if (flag)
		{
			UnlockAchievementWithName("NeoCosmosEXComplete");
		}
		if (IsWorldComplete(6))
		{
			UnlockAchievementWithName("BonusComplete");
		}
		if (GetOverallProgressStage() >= 9)
		{
			UnlockAchievementWithName("Game100PercentComplete");
		}
		SteamUserStats.StoreStats();
	}

	public static void ClearData()
	{
		GCS.maxLevel = 0;
		generalPrefs.SetInt("maxlevel", 0);
		generalPrefs.SetInt("currentLevel", 0);
		passedMobileMenuTutorial = false;
		savedCurrentLevel = "0-0";
		unlockedXF = false;
		unlockedXC = false;
		unlockedXH = false;
		unlockedXR = false;
		unlockedMD = false;
		playedFirst5WorldsCutscene = false;
		playedWorld6Cutscene = false;
		acceptedAgreement = false;
		generalPrefs.dict.Remove("colorRed");
		generalPrefs.dict.Remove("colorBlue");
		SetEmojiMode(enabled: false, red: true);
		SetEmojiMode(enabled: false, red: false);
		string[] allWorlds = WorldData.allWorlds;
		for (int i = 0; i < allWorlds.Length; i++)
		{
			ResetWorldProgress(allWorlds[i]);
		}
		GCS.worldEntrance = null;
		GCS.lastLevelPlayed = 0;
		GCS.checkpointNum = 0;
		scrController.currentWorldString = null;
		ResetTaroStoryProgress();
		Save();
	}

	public static void ClearDataAll()
	{
		PlayerPrefsJson.FileType[] loadedFileTypes = PlayerPrefsJson.LoadedFileTypes;
		for (int i = 0; i < loadedFileTypes.Length; i++)
		{
			PlayerPrefsJson.Get(loadedFileTypes[i])?.dict.Clear();
		}
		Save();
	}

	public static void ResetTaroStoryProgress()
	{
		taroStoryProgress = 0;
		taroEXProgress = 0;
		banishmentPuzzleComplete = false;
		string[] dlcWorlds = WorldData.dlcWorlds;
		for (int i = 0; i < dlcWorlds.Length; i++)
		{
			ResetWorldProgress(dlcWorlds[i]);
		}
		Save();
	}

	public static string GetLastUsedFolder()
	{
		return PlayerPrefs.GetString("lastUsedFolder", DataPath);
	}

	public static void UpdateLastUsedFolder(string levelPath)
	{
		PlayerPrefs.SetString("lastUsedFolder", Path.GetDirectoryName(levelPath));
	}

	public static string GetLastOpenedLevel()
	{
		return PlayerPrefs.GetString("lastOpenedLevel", "");
	}

	public static void UpdateLastOpenedLevel(string levelPath)
	{
		PlayerPrefs.SetString("lastOpenedLevel", levelPath);
	}

	public static bool GetSpeedTrialsAvailable()
	{
		return GetOverallProgressStage() >= 5;
	}

	public static int GetOverallProgressStage()
	{
		int num = -1;
		if (savedCurrentLevel != "1-1" && savedCurrentLevel != "0-0")
		{
			num = 0;
		}
		if (IsWorldComplete(0))
		{
			num = 1;
		}
		if (IsWorldComplete(0) && IsWorldComplete(1) && IsWorldComplete(2) && IsWorldComplete(3) && IsWorldComplete(4))
		{
			num = 3;
		}
		if (IsWorldComplete(5))
		{
			num = 5;
		}
		if (IsWorldComplete(0) && IsWorldComplete(1) && IsWorldComplete(2) && IsWorldComplete(3) && IsWorldComplete(4) && IsWorldComplete(5))
		{
			num = 6;
		}
		int num2;
		if (IsSpeedTrialComplete(0) && IsSpeedTrialComplete(1) && IsSpeedTrialComplete(2) && IsSpeedTrialComplete(3) && IsSpeedTrialComplete(4))
		{
			num2 = (IsSpeedTrialComplete(5) ? 1 : 0);
			if (num2 != 0)
			{
				num = 7;
			}
		}
		else
		{
			num2 = 0;
		}
		if (num2 != 0 && IsWorldComplete(bonusWorldIndex))
		{
			num = 8;
		}
		if (IsWorldPerfect(0) && IsWorldPerfect(1) && IsWorldPerfect(2) && IsWorldPerfect(3) && IsWorldPerfect(4) && IsWorldPerfect(5) && IsWorldComplete(bonusWorldIndex) && !GCS.FOOL_JOKER)
		{
			num = 9;
		}
		if (num == 9 && GetIsHighestPossibleAcc(0) && GetIsHighestPossibleAcc(1) && GetIsHighestPossibleAcc(2) && GetIsHighestPossibleAcc(3) && GetIsHighestPossibleAcc(4) && GetIsHighestPossibleAcc(5) && GetIsHighestPossibleAcc(bonusWorldIndex))
		{
			num = 10;
		}
		return num;
	}

	public static void CompleteFirst()
	{
		ResetAllWorldsProgress();
		CompleteWorld(0);
		GiveAchievementsAndSave();
	}

	public static void SaveMaxLevel(int currentLevel)
	{
		GCS.maxLevel = Mathf.Max(GCS.maxLevel, currentLevel);
		generalPrefs.SetInt("maxlevel", GCS.maxLevel);
		Save();
	}

	public static void CompleteWorld(int worldZeroIndexed, bool includingSpeedTrials = false, bool goldenLantern = false, bool coop = false)
	{
		SetPercentCompletion(worldZeroIndexed, 1f, coop);
		SetBestSpeedTrial(worldZeroIndexed, 1f, coop);
		SetBestPercentAccuracy(worldZeroIndexed, goldenLantern ? 1.1f : 0.9f, coop);
		SetBestPercentXAccuracy(worldZeroIndexed, goldenLantern ? 1f : 0.85f, coop);
		if (goldenLantern)
		{
			SetIsHighestPossibleAcc(worldZeroIndexed, coop, coop: true);
		}
		SetWorldAttempts(worldZeroIndexed, 1, coop);
		SetWorldAttemptsWithoutNewBest(worldZeroIndexed, 0, coop);
		SetLevelTutorialProgress(worldZeroIndexed, 99);
		if (includingSpeedTrials)
		{
			SetBestSpeedTrial(worldZeroIndexed, 2f, coop);
		}
	}

	public static void ResetWorldProgress(string world)
	{
		ResetWorldProgress(world, false);
		ResetWorldProgress(world, coop: true);
	}

	public static void ResetWorldProgress(string world, bool coop = false)
	{
		int index = WorldData.dict[world].index;
		SetPercentCompletion(index, 0f, coop);
		SetBestSpeedTrial(index, 0f, coop);
		SetBestPercentAccuracy(index, 0f, coop);
		SetBestPercentXAccuracy(index, 0f, coop);
		SetIsHighestPossibleAcc(index, isHighest: false, coop);
		SetWorldAttempts(index, 0, coop);
		SetWorldAttemptsWithoutNewBest(index, 0, coop);
		SetLevelTutorialProgress(index, 0);
		if (world.IsTaro())
		{
			ResetMedalsForDLCLevel(world);
		}
	}

	public static void ResetAllWorldsProgress()
	{
		string[] allWorlds = WorldData.allWorlds;
		for (int i = 0; i < allWorlds.Length; i++)
		{
			ResetWorldProgress(allWorlds[i]);
		}
	}

	public static void CompleteAllMainLevels()
	{
		ResetAllWorldsProgress();
		for (int i = 0; i < 6; i++)
		{
			CompleteWorld(i);
		}
		GiveAchievementsAndSave();
	}

	public static void CompleteAllMainLevelsAndSpeedTrials()
	{
		ResetAllWorldsProgress();
		for (int i = 0; i < 6; i++)
		{
			CompleteWorld(i, includingSpeedTrials: true);
		}
		GiveAchievementsAndSave();
	}

	public static void CompleteFirst5()
	{
		ResetAllWorldsProgress();
		for (int i = 0; i < 5; i++)
		{
			CompleteWorld(i, includingSpeedTrials: true);
		}
		GiveAchievementsAndSave();
	}

	public static void CompleteAllWorlds()
	{
		string[] allWorlds = WorldData.allWorlds;
		foreach (string key in allWorlds)
		{
			CompleteWorld(WorldData.dict[key].index, includingSpeedTrials: true);
		}
		GiveAchievementsAndSave();
	}

	public static void Complete100()
	{
		string[] allWorlds = WorldData.allWorlds;
		foreach (string key in allWorlds)
		{
			CompleteWorld(WorldData.dict[key].index, includingSpeedTrials: true);
		}
		unlockedXF = true;
		unlockedXC = true;
		unlockedXH = true;
		unlockedXR = true;
		GiveAchievementsAndSave();
	}

	public static void UnlockXtra()
	{
		unlockedXF = true;
		GiveAchievementsAndSave();
	}

	public static void Load()
	{
		PlayerPrefsJson.nonSyncedKeys = new HashSet<string>
		{
			"globalVolume", "musicVolume", "hitsoundVolume", "sfxVolume", "interfaceVolume", "language", "offset", "offset_v", "acceptedAgreement", "visualQuality",
			"visualEffects", "targetFramerate", keyLimiterKeys.unityKeysName, keyLimiterKeys.asyncKeysName
		};
		try
		{
			bool flag = true;
			PlayerPrefsJson.files.Clear();
			flag = PlayerPrefsJson.LoadAllFiles();
			foreach (PlayerPrefsJson value7 in PlayerPrefsJson.files.Values)
			{
				if (value7.dict.Count == 0)
				{
					value7.SetInt("version", 148);
				}
			}
			if (flag)
			{
				if (generalPrefs != null && customPrefs != null)
				{
					string[] array = generalPrefs.dict.Keys.Where((string k) => !customPrefs.dict.ContainsKey(k) && StartsWithAnyOf(k, new string[3] { "CLSTotalPlays", "CustomWorld_", "SubscribedToFeatured_" })).ToArray();
					foreach (string key in array)
					{
						customPrefs.dict.Add(key, generalPrefs.dict[key]);
						generalPrefs.dict.Remove(key);
					}
				}
				if (generalPrefs != null)
				{
					if (generalPrefs.dict.ContainsKey("skipIntroAfterFirstTry"))
					{
						skipIntroBehavior = (generalPrefs.GetBool("skipIntroAfterFirstTry", defaultValue: true) ? SkipIntroBehavior.AfterFirstTry : SkipIntroBehavior.Off);
						generalPrefs.dict.Remove("skipIntroAfterFirstTry");
					}
					if (generalPrefs.dict.ContainsKey("perfectsOnlyMode"))
					{
						hitMarginLimit = (generalPrefs.GetBool("perfectsOnlyMode") ? HitMarginLimit.PerfectsOnly : HitMarginLimit.None);
						generalPrefs.dict.Remove("perfectsOnlyMode");
					}
				}
			}
			int num2 = generalPrefs.GetInt("version");
			if (generalPrefs.dict.ContainsKey("currentLevel") && (num2 < 35 || generalPrefs.dict["currentLevel"] is int))
			{
				savedCurrentLevel = "0-0";
			}
			if (num2 < 48)
			{
				unlockedXF = false;
				string[] array = WorldData.allWorlds;
				foreach (string text in array)
				{
					if (text.IsXtra())
					{
						ResetWorldProgress(text);
					}
				}
			}
			if (num2 < 53 && flag)
			{
				_ = generalPrefs.dict;
				if (generalPrefs.GetList("calibrationPresets").Count == 0)
				{
					CalibrationPreset item = new CalibrationPreset
					{
						confident = true,
						inputOffset = Mathf.RoundToInt(inputOffset * 1000f),
						outputName = "*",
						outputType = AudioOutputType.Speaker
					};
					scrConductor.defaultPresets.Add(item);
					item.outputType = AudioOutputType.Wired;
					scrConductor.defaultPresets.Add(item);
				}
			}
			Dictionary<string, object> dict = generalPrefs.dict;
			if (dict.TryGetValue("faceBlue", out var value) && value is string)
			{
				dict["faceBlue"] = bool.Parse(value as string);
			}
			if (dict.TryGetValue("faceRed", out var value2) && value2 is string)
			{
				dict["faceRed"] = bool.Parse(value2 as string);
			}
			if (num2 < 141 && flag)
			{
				Debug.Log("migrating tech cleared bool...");
				foreach (uint key2 in FeaturedLevels.tech.Keys)
				{
					TextAsset textAsset = Resources.Load<TextAsset>(Path.Combine("FeaturedLevels", key2.ToString(), "main"));
					if (textAsset == null)
					{
						Debug.Log("asset is null, continue");
						continue;
					}
					Dictionary<string, object> obj = (Json.DeserializePartially(textAsset.text, "actions") as Dictionary<string, object>)["settings"] as Dictionary<string, object>;
					object value3;
					string text2 = (obj.TryGetValue("author", out value3) ? (value3 as string) : "");
					object value4;
					string text3 = (obj.TryGetValue("artist", out value4) ? (value4 as string) : "");
					object value5;
					string text4 = (obj.TryGetValue("song", out value5) ? (value5 as string) : "");
					if (text2.IsNullOrEmpty() || text3.IsNullOrEmpty() || text4.IsNullOrEmpty())
					{
						Debug.Log($"missing metadata for level {key2}, continue");
						continue;
					}
					string hash = MD5Hash.GetHash(text2 + text3 + text4);
					float num3 = GetCustomWorldCompletion(hash) * 1f;
					if (!(num3 >= 1f))
					{
						Debug.Log($"not completed level: {hash} {key2} {text3} {text4} {num3}, continue");
						continue;
					}
					Debug.Log($"cleared level: {key2} {text3} {text4}");
					clearedTechFeatured = true;
					break;
				}
			}
			scrConductor.userPresets = new List<CalibrationPreset>();
			foreach (object item3 in generalPrefs.GetList("calibrationPresets"))
			{
				CalibrationPreset item2 = default(CalibrationPreset);
				item2.confident = true;
				item2.FromDict(item3 as Dictionary<string, object>);
				scrConductor.userPresets.Add(item2);
			}
			int num4 = taroStoryProgress;
			int num5 = num4;
			if (num4 < 6 && IsWorldComplete("T5"))
			{
				num5 = 7;
			}
			else if (num4 < 4 && IsWorldComplete("T4"))
			{
				num5 = 4;
			}
			else if (num4 < 3 && IsWorldComplete("T3"))
			{
				num5 = 3;
			}
			taroStoryProgress = num5;
			generalPrefs.SetInt("version", 148);
			customPrefs.SetInt("version", 148);
		}
		catch (IOException ex) when (((Func<bool>)delegate
		{
			// Could not convert BlockContainer to single expression
			int num = ex.HResult & 0xFFFF;
			return num == 39 || num == 112;
		}).Invoke())
		{
			Notification.instance.ShowNoSpace();
		}
		static bool StartsWithAnyOf(string str, string[] strings)
		{
			bool result = false;
			foreach (string value6 in strings)
			{
				if (str.StartsWith(value6))
				{
					return true;
				}
			}
			return result;
		}
	}

	public static void RecoverSaveDataFromAchievements()
	{
		if (!SteamController.initialized)
		{
			return;
		}
		string[] allWorlds = WorldData.allWorlds;
		foreach (string text in allWorlds)
		{
			if (text == "B")
			{
				continue;
			}
			int index = WorldData.dict[text].index;
			string worldAchievementPrefix = GetWorldAchievementPrefix(text);
			if (GetAchieved(worldAchievementPrefix + "Complete"))
			{
				if (GetPercentCompletion(index) < 1f)
				{
					SetComplete(index);
				}
				if (text.IsTaro())
				{
					int medalCount = WorldData.dict[text].medalCount;
					int[] array = new int[medalCount];
					for (int j = 0; j < medalCount; j++)
					{
						array[j] = 1;
					}
					SetMedalsForDLCLevel(text, array);
				}
			}
			if (GetAchieved(worldAchievementPrefix + "Perfect"))
			{
				float bestPercentAccuracy = GetBestPercentAccuracy(index, coop: false);
				float bestPercentXAccuracy = GetBestPercentXAccuracy(index, coop: false);
				SetBestPercentAccuracy(index, Mathf.Max(1f, bestPercentAccuracy), coop: false);
				SetBestPercentXAccuracy(index, Mathf.Max(1f, bestPercentXAccuracy), coop: false);
			}
			if (GetAchieved(worldAchievementPrefix + "Trial"))
			{
				float speedTrialAimForWorld = GetSpeedTrialAimForWorld(text);
				if (GetBestSpeedMultiplier(index, coop: false) < speedTrialAimForWorld)
				{
					SetBestSpeedTrial(index, speedTrialAimForWorld, coop: false);
				}
			}
		}
		if (GetAchieved("BonusComplete"))
		{
			SetPercentCompletion(WorldData.dict["B"].index, 1f, coop: false);
		}
		if (GetAchieved("Game100PercentComplete"))
		{
			for (int k = 0; k < 6; k++)
			{
				SetIsHighestPossibleAcc(k, isHighest: true, coop: false, save: false);
			}
		}
		if (GetAchieved("NeoCosmosEXComplete"))
		{
			taroStoryProgress = 7;
			taroEXProgress = 4;
			allWorlds = WorldData.dlcWorlds;
			foreach (string key in allWorlds)
			{
				int index2 = WorldData.dict[key].index;
				if (GetPercentCompletion(index2) < 1f)
				{
					SetComplete(index2);
				}
			}
		}
		else if (GetAchieved("WorldT5Complete"))
		{
			taroStoryProgress = 7;
		}
		else if (GetAchieved("WorldT4Complete"))
		{
			taroStoryProgress = 4;
		}
		else if (GetAchieved("WorldT3Complete"))
		{
			taroStoryProgress = 3;
		}
		else if (GetAchieved("WorldT1Complete") && GetAchieved("WorldT2Complete"))
		{
			taroStoryProgress = 2;
		}
		static bool GetAchieved(string achievementName)
		{
			return SteamController.Instance.Achievements.Has(achievementName);
		}
		static void SetComplete(int worldIndex)
		{
			SetWorldAttempts(worldIndex, 1, coop: false);
			SetWorldAttemptsWithoutNewBest(worldIndex, 0, coop: false);
			SetBestPercentAccuracy(worldIndex, 0.95f, coop: false);
			SetBestPercentXAccuracy(worldIndex, 0.9f, coop: false);
			SetLevelTutorialProgress(worldIndex, 99);
			SetPercentCompletion(worldIndex, 1f, coop: false);
		}
	}

	public static void GiveAchievementsAndSave()
	{
		if (!Application.isEditor)
		{
			GiveAchievements();
		}
		Save();
	}

	public static void Save(bool instant = false)
	{
		if (instant)
		{
			SaveAction();
			return;
		}
		coroutineExecuter.StopAllCoroutines();
		IEnumerator routine = SaveCo(0.5f);
		coroutineExecuter.StartCoroutine(routine);
	}

	private static void SaveAction()
	{
		WriteSaveToDisk();
		GameServices instance = GameServices.Instance;
		if (instance.Initialized && instance.IsLoadStatusComplete)
		{
			instance.LoadGame();
		}
	}

	private static IEnumerator SaveCo(float timeToWait)
	{
		yield return new WaitForSecondsRealtime(timeToWait);
		SaveAction();
	}

	public static void WriteSaveToDisk()
	{
		List<object> list = new List<object>();
		foreach (CalibrationPreset userPreset in scrConductor.userPresets)
		{
			list.Add(userPreset.ToDict());
		}
		generalPrefs.SetList("calibrationPresets", list);
		generalPrefs.SetInt("version", 148);
		customPrefs.SetInt("version", 148);
		PlayerPrefsJson.SaveAllFiles();
	}

	public static string dd(int num, bool ignoreFools = false)
	{
		if (!ignoreFools && GCS.FOOL_JOKER && num < 1000)
		{
			num += 1000;
		}
		return num.ToString("00");
	}

	public static bool IsCompatibleWithCloud(PlayerPrefsJson cloudPrefs)
	{
		return 148 >= cloudPrefs.GetInt("version");
	}

	public static void CheckWithCloud(PlayerPrefsJson cloudPrefs, bool firstTime)
	{
		string[] allWorlds = WorldData.allWorlds;
		foreach (string text in allWorlds)
		{
			int index = WorldData.dict[text].index;
			SyncStats(coop: false);
			SyncStats(coop: true);
			int num = cloudPrefs.GetInt("tutorialProgress" + dd(index));
			if (num > GetLevelTutorialProgress(index))
			{
				SetLevelTutorialProgress(index, num);
			}
			if (!text.IsTaro())
			{
				continue;
			}
			int[] medalsForDLCLevel = GetMedalsForDLCLevel(text);
			int[] array = GetMedalsCloud(text);
			if (medalsForDLCLevel.Length != array.Length || medalsForDLCLevel == array)
			{
				continue;
			}
			for (int j = 0; j < medalsForDLCLevel.Length; j++)
			{
				if (array[j] > medalsForDLCLevel[j])
				{
					medalsForDLCLevel[j] = array[j];
				}
			}
			SetMedalsForDLCLevel(text, medalsForDLCLevel);
			void SyncStats(bool coop)
			{
				string coopPrefix = GetCoopPrefix(coop);
				float num8 = cloudPrefs.GetFloat(coopPrefix + "percentCompletion" + dd(index));
				if (num8 > GetPercentCompletion(index))
				{
					SetPercentCompletion(index, num8, coop);
				}
				float num9 = cloudPrefs.GetFloat(coopPrefix + "bestPercentAccuracy" + dd(index));
				if (num9 > GetBestPercentAccuracy(index, coop))
				{
					SetBestPercentAccuracy(index, num9, coop);
				}
				float num10 = cloudPrefs.GetFloat(coopPrefix + "bestPercentXAccuracy" + dd(index));
				if (num10 > GetBestPercentXAccuracy(index, coop))
				{
					SetBestPercentXAccuracy(index, num10, coop);
				}
				float num11 = cloudPrefs.GetFloat(coopPrefix + "bestSpeedMultiplier" + dd(index));
				if (num11 > GetBestSpeedMultiplier(index, coop))
				{
					SetBestSpeedTrial(index, num11, coop);
				}
				int num12 = cloudPrefs.GetInt(coopPrefix + "worldAttempts" + dd(index));
				if (num12 > GetWorldAttempts(index, coop))
				{
					SetWorldAttempts(index, num12, coop);
				}
				int num13 = cloudPrefs.GetInt(coopPrefix + "worldAttemptsWithoutNewBest" + dd(index));
				if (num13 > GetWorldAttemptsWithoutNewBest(index, coop))
				{
					SetWorldAttemptsWithoutNewBest(index, num13, coop);
				}
				if (cloudPrefs.GetBool(coopPrefix + "isHighestPossibleAcc" + dd(index)))
				{
					SetIsHighestPossibleAcc(index, isHighest: true, coop, save: false);
				}
			}
		}
		if (cloudPrefs.GetBool("unlockedXtra"))
		{
			unlockedXF = true;
		}
		if (cloudPrefs.GetBool("unlockedXC"))
		{
			unlockedXC = true;
		}
		if (cloudPrefs.GetBool("unlockedXH"))
		{
			unlockedXH = true;
		}
		if (cloudPrefs.GetBool("unlockedXR"))
		{
			unlockedXR = true;
		}
		if (cloudPrefs.GetBool("unlockedMD"))
		{
			unlockedMD = true;
		}
		int num2 = cloudPrefs.GetInt("dlcStoryProgress");
		if (num2 > taroStoryProgress)
		{
			taroStoryProgress = num2;
		}
		int num3 = cloudPrefs.GetInt("dlcEXProgress");
		if (num3 > taroEXProgress)
		{
			taroEXProgress = num3;
		}
		if (cloudPrefs.GetBool("passedTutorial"))
		{
			passedMobileMenuTutorial = true;
		}
		if (cloudPrefs.GetBool("playedFirst5WorldsCutscene"))
		{
			playedFirst5WorldsCutscene = true;
		}
		if (cloudPrefs.GetBool("keyPlayedWorld6Cutscene"))
		{
			playedWorld6Cutscene = true;
		}
		if (cloudPrefs.GetBool("acceptedAgreement"))
		{
			acceptedAgreement = true;
		}
		if (cloudPrefs.GetBool("ratedGame"))
		{
			ratedGame = true;
		}
		int num4 = cloudPrefs.GetInt("kaboomClears");
		if (num4 > kaboomClears)
		{
			kaboomClears = num4;
		}
		int num5 = cloudPrefs.GetInt("kaboomDeaths");
		if (num5 > kaboomDeaths)
		{
			kaboomDeaths = num5;
		}
		if (cloudPrefs.GetBool("banishmentPuzzleComplete"))
		{
			banishmentPuzzleComplete = true;
		}
		float num6 = cloudPrefs.GetFloat("dlcT5Time", 60f);
		if (num6 < t5BestTime)
		{
			t5BestTime = num6;
		}
		if (firstTime)
		{
			ErrorMeterSize errorMeterSize = (ErrorMeterSize)cloudPrefs.GetInt("hitErrorMeterSize");
			if (errorMeterSize != hitErrorMeterSize)
			{
				hitErrorMeterSize = errorMeterSize;
			}
			ErrorMeterShape errorMeterShape = (ErrorMeterShape)cloudPrefs.GetInt("hitErrorMeterShape");
			if (errorMeterShape != hitErrorMeterShape)
			{
				hitErrorMeterShape = errorMeterShape;
			}
			MultitapTileBehavior multitapTileBehavior = (MultitapTileBehavior)cloudPrefs.GetInt("multitapTileBehavior");
			if (multitapTileBehavior != multiTapTileBehavior)
			{
				multiTapTileBehavior = multitapTileBehavior;
			}
			HoldBehavior holdBehavior = (HoldBehavior)cloudPrefs.GetInt("holdBehavior");
			if (holdBehavior != Persistence.holdBehavior)
			{
				Persistence.holdBehavior = holdBehavior;
			}
			bool flag = cloudPrefs.GetBool("freeroamInvuln");
			if (flag != freeroamInvulnerability)
			{
				freeroamInvulnerability = flag;
			}
			bool flag2 = cloudPrefs.GetBool("showDetailedResults");
			if (flag2 != showDetailedResults)
			{
				showDetailedResults = flag2;
			}
			int num7 = cloudPrefs.GetInt("antiAliasing", 1);
			if (num7 != antiAliasing)
			{
				antiAliasing = num7;
			}
		}
		int[] GetMedalsCloud(string taroWorld)
		{
			string text2 = cloudPrefs.GetString("dlcMedals" + taroWorld);
			int medalCount = WorldData.dict[taroWorld].medalCount;
			int[] array2 = new int[medalCount];
			if (!string.IsNullOrEmpty(text2) && text2.Length == medalCount)
			{
				for (int k = 0; k < medalCount; k++)
				{
					array2[k] = (int)char.GetNumericValue(text2[k]);
				}
			}
			return array2;
		}
	}

	public static void SetOverallProgressStage(int i)
	{
	}
}
