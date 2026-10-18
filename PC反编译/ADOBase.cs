using System;
using System.Collections.Generic;
using ADOFAI.Common.Platform;
using DG.Tweening;
using RDTools;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ADOBase : RDBaseDll
{
	public static Scene[] levelScenes;

	public static bool appIsInSteamLibrary = false;

	public static readonly Color ClearWhite = new Color(1f, 1f, 1f, 0f);

	public static Platform platform = Platform.None;

	public static AudioManager audioManager => AudioManager.Instance;

	public static scrConductor conductor => scrConductor.instance;

	public static scrLoader loader => scrLoader.instance;

	public static scrController controller => scrController.instance;

	public static scrLevelMaker lm => scrLevelMaker.instance;

	public static scrUIController uiController => scrUIController.instance;

	public static scnCLS cls => scnCLS.instance;

	public static scnEditor editor => scnEditor.instance;

	public static scnGame customLevel => scnGame.instance;

	public static string levelPath => scnGame.instance.levelPath;

	public static RDConstants gc => RDConstants.data;

	public static Dictionary<string, WorldData> worldData => WorldData.dict;

	public static scnLevelSelect levelSelect => scnLevelSelect.instance;

	public static IPlatformHelper platformHelper => PlatformHelper.instance;

	public static LevelSelectBase levelSelectBase => LevelSelectBase.instance;

	public static scrPlayerManager playerManager => scrPlayerManager.instance;

	public static bool isLevelEditor => editor != null;

	public static bool isScnGame
	{
		get
		{
			if (customLevel != null)
			{
				return !isLevelEditor;
			}
			return false;
		}
	}

	public static bool isCLSLevel
	{
		get
		{
			if (isScnGame)
			{
				return !isInternalLevel;
			}
			return false;
		}
	}

	public static bool isEditingLevel
	{
		get
		{
			if (isLevelEditor)
			{
				return controller.paused;
			}
			return false;
		}
	}

	public static bool isCLS => cls != null;

	public static bool isLevelSelect => LevelSelectBase.instance != null;

	public static bool isFreeroamScene
	{
		get
		{
			if (!isLevelSelect)
			{
				if (controller != null)
				{
					return controller.isPuzzleRoom;
				}
				return false;
			}
			return true;
		}
	}

	public static bool isInternalLevel => GCS.internalLevelName != null;

	public static bool isDLCLevel => WorldData.dict[scrController.currentWorldString].isDLC;

	public static bool isOfficialLevel
	{
		get
		{
			if (controller != null && !isCLSLevel)
			{
				return !isLevelEditor;
			}
			return false;
		}
	}

	public static bool isBossLevel => currentLevel.Contains("-X");

	public static bool isCLSBossLevel
	{
		get
		{
			if (isCLSLevel)
			{
				return GCS.customLevelIndex == GCS.customLevelPaths.Length - 1;
			}
			return false;
		}
	}

	public static bool isTechFeaturedLevel
	{
		get
		{
			if (GCS.customLevelId != null && uint.TryParse(GCS.customLevelId, out var result))
			{
				return FeaturedLevels.tech.ContainsKey(result);
			}
			return false;
		}
	}

	public static bool isBundleLevel => GCS.loadCustomFromBundle;

	public static bool isUnityEditor => Application.isEditor;

	public static bool levelIsMikoSkip => controller.levelName == "XM-X";

	public bool practiceAvailable
	{
		get
		{
			if (controller.isPuzzleRoom)
			{
				return false;
			}
			if (isCLSLevel)
			{
				return true;
			}
			if (controller.isbosslevel)
			{
				return true;
			}
			return false;
		}
	}

	public static string currentLevel => scrController.instance.levelName;

	public static bool playerIsOnIntroScene => sceneName == "scnIntro";

	public bool bb => GCS.bb;

	public float randomFloat => UnityEngine.Random.value;

	public HashSet<DLCManager> dlcManagers => DLCManager.DLCManagers;

	public NeoCosmosManager neoCosmosManager => NeoCosmosManager.instance;

	public VegaDLCManager vegaDLCManager => VegaDLCManager.instance;

	public FeaturedDLCManager featuredDLCManager => FeaturedDLCManager.instance;

	public static string sceneName
	{
		get
		{
			int sceneCount = SceneManager.sceneCount;
			Scene activeScene = SceneManager.GetActiveScene();
			if (activeScene.name != "scnLoading")
			{
				return activeScene.name;
			}
			for (int i = 0; i < sceneCount; i++)
			{
				Scene sceneAt = SceneManager.GetSceneAt(i);
				if (sceneAt.name != "scnLoading")
				{
					return sceneAt.name;
				}
			}
			return "";
		}
	}

	public static bool isGamepad
	{
		get
		{
			if (!isSwitch)
			{
				return RDC.runningOnSteamDeck;
			}
			return true;
		}
	}

	public static bool isSwitch => platform == Platform.Switch;

	public static bool isMobile
	{
		get
		{
			Platform platform = ADOBase.platform;
			return platform == Platform.Android || platform == Platform.iOS;
		}
	}

	public static bool isDesktop => true;

	public static bool isSteamworks => true;

	public static bool isIntelMac => false;

	public static bool isMobileMenu => false;

	public static bool isExpo => false;

	public static bool isTournament => false;

	public static bool IsNotAMikoSkipMandatorySprite(string filename)
	{
		if (levelIsMikoSkip && controller.visualQuality == VisualQuality.Low)
		{
			if (!filename.Contains("boat"))
			{
				return !filename.Contains("tile90");
			}
			return false;
		}
		return false;
	}

	public static int GetLevelNumber(string levelName = null)
	{
		if (levelName == null)
		{
			levelName = scrController.instance.levelName;
		}
		int num = levelName.IndexOf('-');
		int num2 = num + 1;
		string text = levelName.Substring(num2, levelName.Length - num2);
		if (text == "X")
		{
			string key = levelName.Substring(0, num);
			return WorldData.dict[key].levelCount;
		}
		return int.Parse(text);
	}

	public static string GetPreviousLevelName(string levelName = null)
	{
		if (levelName == null)
		{
			levelName = scrController.instance.levelName;
		}
		int num = Mathf.Max(GetLevelNumber(levelName) - 1, 1);
		return scrController.currentWorldString + "-" + num;
	}

	public static string GetNextLevelName(string levelName = null)
	{
		if (levelName == null)
		{
			levelName = scrController.instance.levelName;
		}
		int levelCount = WorldData.dict[scrController.currentWorldString].levelCount;
		int levelNumber = GetLevelNumber(levelName);
		string text = ((levelNumber + 1 == levelCount) ? "X" : Mathf.Min(levelNumber + 1, levelCount).ToString());
		return scrController.currentWorldString + "-" + text;
	}

	public static void GoToLevelSelect()
	{
		DOTween.KillAll();
		loader.LoadScene(GCNS.sceneLevelSelect);
	}

	public static void GoToCalibration(bool bypassUnsavedCheck = false)
	{
		if (isLevelEditor && !bypassUnsavedCheck)
		{
			PauseMenu pauseMenu = controller.pauseMenu;
			pauseMenu.settingsMenu.settingsThatRequireRestart = 0;
			pauseMenu.Hide();
			editor.CheckUnsavedChanges(delegate
			{
				GoToCalibration(bypassUnsavedCheck: true);
			});
			return;
		}
		DOTween.KillAll();
		Time.timeScale = 1f;
		AudioListener.pause = false;
		GCS.savedCheckpointNum = GCS.checkpointNum;
		GCS.checkpointNum = 0;
		GCS.wasInCalibration = true;
		if (GCS.savedCheckpointNum > 0)
		{
			scrController.checkpointsUsed++;
		}
		if (isLevelEditor)
		{
			string text = editor.customLevel.levelPath;
			if (!string.IsNullOrEmpty(text))
			{
				scnEditor.levelToOpenOnLoad = text;
			}
		}
		GCS.lastVisitedScene = sceneName;
		loader.LoadScene("scnCalibration");
	}

	public static void RestartScene()
	{
		string scene = SceneManager.GetActiveScene().name;
		if (isLevelEditor)
		{
			scene = "scnEditor";
			string text = editor.customLevel.levelPath;
			if (!string.IsNullOrEmpty(text))
			{
				scnEditor.levelToOpenOnLoad = text;
			}
		}
		DOTween.KillAll();
		loader.LoadScene(scene);
	}

	public void GoToLevelEditor()
	{
		DOTween.KillAll();
		loader.LoadScene("scnEditor");
	}

	private static (string, string) ProcessLevelName(string sceneName)
	{
		string text = sceneName;
		string text2 = sceneName.Substring(0, Math.Max(0, sceneName.IndexOf("-")));
		if (text2.IsVega())
		{
			sceneName = sceneName.Replace("EX-", "-E");
		}
		if (text2.EndsWith("TX"))
		{
			text = text.Replace("TX-", "-T");
		}
		if (GCS.FOOL_JOKER && text.EndsWith("J-X"))
		{
			text = sceneName.Replace("J", "");
			text += "?";
		}
		return (sceneName, text);
	}

	public static string GetLocalizedLevelName(string sceneName)
	{
		(string, string) tuple = ProcessLevelName(sceneName);
		var (text, _) = tuple;
		return tuple.Item2 + " " + RDString.Get(text + ".title");
	}

	public static string GetLocalizedLevelNameWithCheck(string sceneName, out bool exists)
	{
		(string, string) tuple = ProcessLevelName(sceneName);
		var (text, _) = tuple;
		return tuple.Item2 + " " + RDString.GetWithCheck(text + ".title", out exists);
	}

	public static bool IsAprilFools()
	{
		int month = DateTime.Now.Month;
		int day = DateTime.Now.Day;
		if (month == 4 && day >= 1 && day <= 8)
		{
			return Persistence.IsWorldComplete(0, ignoreFools: true);
		}
		return false;
	}

	public static bool IsHalloweenWeek()
	{
		int month = DateTime.Now.Month;
		int day = DateTime.Now.Day;
		if ((month == 10 && day >= 24) || (month == 11 && day <= 2))
		{
			return Persistence.IsWorldComplete(0);
		}
		return false;
	}

	public static bool IsCNY()
	{
		(int year, int month, int day) dateOfChineseNewYear = RDUtils.GetDateOfChineseNewYear();
		int item = dateOfChineseNewYear.year;
		int item2 = dateOfChineseNewYear.month;
		int item3 = dateOfChineseNewYear.day;
		DateTime dateTime = new DateTime(item, item2, item3);
		DateTime dateTime2 = dateTime.AddDays(15.0);
		_ = DateTime.Now;
		long ticks = DateTime.Now.Ticks;
		long ticks2 = dateTime.Ticks;
		long ticks3 = dateTime2.Ticks;
		if (ticks > ticks2)
		{
			return ticks < ticks3;
		}
		return false;
	}

	public static int GetDisplayWidth()
	{
		return Display.main.systemWidth;
	}

	public static int GetDisplayHeight()
	{
		return Display.main.systemHeight;
	}

	public void FlushUnusedMemory()
	{
		if (audioManager != null)
		{
			audioManager.FlushData();
		}
	}

	public virtual void OnBeat()
	{
	}
}
