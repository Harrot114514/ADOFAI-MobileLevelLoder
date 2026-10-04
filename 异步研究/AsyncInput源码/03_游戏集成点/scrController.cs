using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ADOFAI;
using ADOFAI.SteamIntegration;
using DG.Tweening;
using MobileMenu;
using MonsterLove.StateMachine;
using SkyHook;
using Steamworks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityStandardAssets.ImageEffects;

public class scrController : StateBehaviour
{
	public Dictionary<string, string> hitboxEventTagDecorations = new Dictionary<string, string>();

	public const float boothModeDebounceCooldownTime = 0.1f;

	private const float MaxTimeToTypeDebugCheat = 8f;

	public const bool HallOfMirrorsUsesRenderTextures = false;

	private readonly RDCheatCode debugModeCheatCode = new RDCheatCode("despacit0");

	private readonly RDCheatCode typingModeCheatCode = new RDCheatCode("typing");

	private readonly RDCheatCode hideHudCheatCode = new RDCheatCode("byehud");

	private readonly RDCheatCode hideAutoHudCheatCode = new RDCheatCode("byeotto");

	public static int volume;

	public static bool showDetailedResults;

	public static string currentWorldString;

	public static int deaths;

	public static int checkpointsUsed;

	private static scrController _instance;

	public static bool displayedMultiFingerHint = false;

	[NonSerialized]
	public int maximumUsedKeys;

	private static float lastTimeStatsUploaded;

	private const float IntervalToUpdateSteamStats = 120f;

	[Header("GameObjects and MonoBehaviours")]
	public scrCamera camy;

	public GameObject background;

	public GameObject lofiBackground;

	public scrDecorationManager decorationManager;

	[Header("Level Type")]
	public bool gameworld;

	public bool isbosslevel;

	public bool isPuzzleRoom;

	[Header("Level Settings")]
	public scrFloor firstFloor;

	public bool stickToFloor;

	public bool instantExplode;

	public bool forceHitTextOnScreen;

	public bool usingInitialTrackStyles;

	public float hitTextMinBorderDistance;

	public bool useComponentNotationForFx = true;

	[NonSerialized]
	public bool forceNoCountdown;

	[NonSerialized]
	public bool expoTutorial;

	[NonSerialized]
	public bool independentPlayers;

	public TileShape tileShape = TileShape.Long;

	[NonSerialized]
	public Vector2 customFloorDimensions;

	[NonSerialized]
	[Header("Variables")]
	public float d_speed = 1f;

	[NonSerialized]
	public bool safetyTilesArePresent;

	private int numFastPresses;

	private bool recommendsTwoFingers;

	[NonSerialized]
	public string caption;

	[NonSerialized]
	public string levelName;

	[NonSerialized]
	public bool goShown;

	[NonSerialized]
	public bool moving;

	[NonSerialized]
	public int curCountdown;

	[NonSerialized]
	public scrFloor conditionalFloor;

	[NonSerialized]
	public ffxSetInputEventPlus[][] inputEventFfx = new ffxSetInputEventPlus[EditorConstants.InputEventStateSize][];

	[NonSerialized]
	public States currentState;

	[NonSerialized]
	public int currentSeqID;

	[NonSerialized]
	public bool setupComplete;

	[NonSerialized]
	public bool startedFromCheckpoint;

	[NonSerialized]
	public float averageFrameTime;

	[NonSerialized]
	public scrMistakesManager.EndLevelInfo endLevelInfo;

	[NonSerialized]
	public float boothModeDebounceCounter;

	[NonSerialized]
	public Ease rotationEase = Ease.Linear;

	[NonSerialized]
	public int rotationEaseParts = 1;

	[NonSerialized]
	public EasePartBehavior rotationEasePartBehavior;

	[NonSerialized]
	public bool multipressPenalty;

	[NonSerialized]
	public bool multipressAndHasPressedFirstPress;

	[NonSerialized]
	public bool noFail;

	[NonSerialized]
	public bool unlockKeyLimiter;

	[NonSerialized]
	public bool noFailInfiniteMargin;

	[NonSerialized]
	public bool levelWasSkipped;

	[NonSerialized]
	public Portal portalDestination = Portal.EndOfLevel;

	private string portalArguments = "";

	[NonSerialized]
	public Text txtCongrats;

	[NonSerialized]
	public Text txtAllStrictClear;

	[NonSerialized]
	public Text txtPercent;

	[NonSerialized]
	public Text txtTryCalibrating;

	[NonSerialized]
	public Text txtLevelName;

	[NonSerialized]
	public Text txtAprilCongrats;

	[NonSerialized]
	public DetailedResults detailedResults;

	[NonSerialized]
	public string customTxtCongrats;

	[NonSerialized]
	public string customTxtPurePerfect;

	[NonSerialized]
	public PauseMenu pauseMenu;

	[NonSerialized]
	public scrHitErrorMeter errorMeter;

	[NonSerialized]
	public scrCreditsText creditsText;

	[NonSerialized]
	public List<scrLetterPress> typingLetters = new List<scrLetterPress>();

	[NonSerialized]
	public VisualQuality visualQuality;

	[NonSerialized]
	public VisualEffects visualEffects;

	[NonSerialized]
	public List<PlanetRenderer> dummyPlanets;

	[NonSerialized]
	public List<LineRenderer> multiPlanetLines;

	[NonSerialized]
	public Material lineMaterial;

	[NonSerialized]
	public Color lineColor;

	[NonSerialized]
	public bool homEnabled;

	[NonSerialized]
	public string originalLevelName;

	public Level level;

	[NonSerialized]
	public int curFreeRoamSection;

	private float freeroamUpTime;

	[NonSerialized]
	public float freeroamAngleInterval = 90f;

	[NonSerialized]
	public float freeroamAngleOffset;

	private bool levelNameShouldHide;

	[NonSerialized]
	public Vector2? txtLevelNameOriginalPosition;

	private bool _paused;

	private bool transitioningLevel;

	private float oldPercentComplete;

	private bool disableCongratsMessage;

	private float debugTileTime = -100f;

	private int numTimesSfxToggled;

	private int waitForStartCoCallCount;

	[NonSerialized]
	public bool benchmarkMode;

	[NonSerialized]
	public bool strictHolds;

	[NonSerialized]
	public bool strictHoldsSaved;

	[NonSerialized]
	public bool requireHolding;

	[NonSerialized]
	public bool freeroamInvulnerability;

	[NonSerialized]
	public int currentFloorID;

	private int lastTogglePauseFrame;

	private const float timeToRestart = 20f;

	public static float lastClickTime;

	private bool restartExpo;

	[NonSerialized]
	public bool isCutscene;

	[NonSerialized]
	public bool canExitLevel = true;

	private List<Tuple<double, double>> listBPM = new List<Tuple<double, double>>();

	[NonSerialized]
	public bool forceOK;

	[NonSerialized]
	public bool disableV15Features = true;

	private int frameStart;

	private const int DontScrub = -1;

	[NonSerialized]
	public float popuptime = 0.5f;

	[NonSerialized]
	public int lastCamPulseFloor = -1;

	[NonSerialized]
	public bool exitingToMainMenu;

	private scrPlanet scrubChosen;

	private PriorityQueue<SkyHookEvent, ulong> sortedKeyQueue = new PriorityQueue<SkyHookEvent, ulong>();

	private static bool _allowDevCached = false;

	private static bool _allowDebug = false;

	private int frameOnAsyncInputDetected;

	private int frameOnLastInput;

	private static readonly FieldInfo DestinationStateField = typeof(StateEngine).GetField("destinationState", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty);

	private bool levelNameTextPresent = true;

	private RectTransform lvlname;

	private Vector2 lvlnameAnchorPos;

	[NonSerialized]
	public float startVolume;

	private double startTime;

	private float winTime;

	public static readonly States[] deathStates = new States[2]
	{
		States.Fail,
		States.Fail2
	};

	public PlanetarySystem planetarySystem => playerOne.planetarySystem;

	public static int currentWorld => WorldData.dict[currentWorldString].index;

	public static bool coopMode => scrPlayerManager.playerCount > 1;

	public scrMistakesManager mistakesManager => ADOBase.playerManager.mistakesManager;

	public scrPlanet chosenPlanet => planetarySystem.chosenPlanet;

	public scrPlayer playerOne => ADOBase.playerManager.players[0];

	public float tileSize => baseFloorDimensions.x * 2f;

	public Vector2 baseFloorDimensions
	{
		get
		{
			if (tileShape != TileShape.Long)
			{
				if (tileShape != TileShape.Short)
				{
					return customFloorDimensions;
				}
				return scrFloor.ShortDimensions;
			}
			return scrFloor.LongDimensions;
		}
	}

	public scrPlanet planetBlue => planetarySystem.planetBlue;

	public scrPlanet planetRed => planetarySystem.planetRed;

	public scrPlanet planetGreen => planetarySystem.planetGreen;

	public static scrController instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = UnityEngine.Object.FindAnyObjectByType<scrController>();
			}
			return _instance;
		}
	}

	public bool audioPaused
	{
		get
		{
			return AudioListener.pause;
		}
		set
		{
			AudioListener.pause = value;
		}
	}

	public bool paused
	{
		get
		{
			return _paused;
		}
		set
		{
			lastTogglePauseFrame = Time.frameCount;
			_paused = value;
			UpdateErrorMeterVisibility();
			RDInput.SetMapping(paused ? "Pause" : (ADOBase.isLevelSelect ? "LevelSelect" : (ADOBase.isCLS ? "CLS" : "Gameplay")));
		}
	}

	public scrFloor currFloor => chosenPlanet.currfloor;

	public float percentComplete => (float)(ADOBase.controller.currentSeqID + 1) / (float)ADOBase.lm.listFloors.Count;

	public bool toggledPausedThisFrame => lastTogglePauseFrame == Time.frameCount;

	public bool saveProgressConditions
	{
		get
		{
			if (ADOBase.isOfficialLevel && ADOBase.isBossLevel && !GCS.speedTrialMode)
			{
				return !GCS.practiceMode;
			}
			return false;
		}
	}

	public bool legacyTween
	{
		get
		{
			if (ADOBase.customLevel != null)
			{
				return ADOBase.customLevel.levelData?.legacyTween ?? false;
			}
			return false;
		}
	}

	private void Awake()
	{
		RDInput.SetMapping("Gameplay");
		if (gameworld)
		{
			scrTextDecoration.MakeNewFontDictionary();
		}
		if (!gameworld || ADOBase.isScnGame)
		{
			FlushUnusedMemory();
		}
		if (RDC.deleteSavedProgress)
		{
			RDC.deleteSavedProgress = false;
			Persistence.DeleteSavedProgress();
		}
		ADOBase.playerManager.ControllerAwake();
		if (ADOBase.sceneName.Contains('-') && !ADOBase.sceneName.Contains("TP") && ADOBase.sceneName != "1-0")
		{
			tileShape = TileShape.Custom;
			FloorMesh component = ADOBase.lm.listFloors[0].GetComponent<FloorMesh>();
			customFloorDimensions = new Vector2(((component._length > 0.6f) ? scrFloor.LongDimensions : scrFloor.ShortDimensions).x, component._width);
		}
		canExitLevel = true;
		dummyPlanets = new List<PlanetRenderer>();
		multiPlanetLines = new List<LineRenderer>();
		lineMaterial = new Material(Shader.Find("ADOFAI/ScrollingSprite"));
		lineMaterial.SetTexture("_MainTex", ADOBase.gc.planetPolygonTex);
		lineMaterial.SetVector("_ScrollSpeed", new Vector2(-0.4f, 0f));
		lineMaterial.SetFloat("_Time0", 0f);
		lineColor = new Color(1f, 1f, 1f, 0.5f);
		if (GCS.wasInCalibration)
		{
			GCS.checkpointNum = GCS.savedCheckpointNum;
			GCS.savedCheckpointNum = 0;
			GCS.wasInCalibration = false;
		}
		if (RDC.customCheckpoint && ADOBase.isUnityEditor)
		{
			RDC.customCheckpoint = false;
			GCS.checkpointNum = RDC.customCheckpointPos;
		}
		if (GCS.previousScene == null)
		{
			GCS.previousScene = base.gameObject.scene.name;
		}
		if (GCS.previousScene != base.gameObject.scene.name)
		{
			GCS.checkpointNum = 0;
			GCS.previousScene = GCS.sceneToLoad;
		}
		if (GCS.showVirtualAvatar)
		{
			UnityEngine.Object.Instantiate(ADOBase.gc.virtualAvatarPrefab);
		}
		levelName = GCS.internalLevelName ?? base.gameObject.scene.name;
		if (ADOBase.isInternalLevel)
		{
			isbosslevel = levelName.IsBossLevel();
		}
		if (saveProgressConditions)
		{
			Dictionary<string, object> savedProgress = Persistence.GetSavedProgress();
			if (savedProgress.Keys.Count > 2 && (string)savedProgress["level"] == levelName)
			{
				scrMistakesManager.LoadCheckpointProgress();
			}
		}
		if (GCS.turnOnBenchmarkMode)
		{
			benchmarkMode = true;
			GCS.turnOnBenchmarkMode = false;
		}
		RDC.auto = false;
		Initialize<States>();
		if (levelName.Contains("-"))
		{
			txtLevelName = scrUIController.instance.txtLevelName;
			txtLevelName.SetLocalizedFont();
			caption = ADOBase.GetLocalizedLevelName(levelName);
			string text = levelName.Substring(0, levelName.IndexOf('-'));
			currentWorldString = (ADOBase.worldData.ContainsKey(text) ? text : "Template");
		}
		if (gameworld)
		{
			if (background == null)
			{
				background = GameObject.Find("BG");
			}
			if (background == null)
			{
				background = GameObject.Find("Tutorial BG");
			}
			if (ADOBase.isBossLevel)
			{
				if (background != null)
				{
					background.SetActive(visualQuality == VisualQuality.High);
				}
				if (visualQuality == VisualQuality.Low && lofiBackground != null && (ADOBase.isOfficialLevel || Persistence.forceVisualSettings))
				{
					SpriteRenderer component2 = lofiBackground.GetComponent<SpriteRenderer>();
					if (ADOBase.isInternalLevel)
					{
						string text2 = GCS.internalLevelName.Split('-')[0];
						string text3 = "InternalLevels/" + text2 + "/lofi-bg";
						if (ADOBase.isDLCLevel)
						{
							Sprite sprite = Addressables.LoadAssetAsync<Sprite>(text3).WaitForCompletion();
							component2.sprite = sprite;
						}
						else
						{
							component2.sprite = Resources.Load<Sprite>(text3);
						}
					}
					Sprite sprite2 = component2.sprite;
					if ((bool)sprite2)
					{
						Vector3 size = sprite2.bounds.size;
						float num = size.x / size.y;
						float num2 = (float)Screen.width * 1f / (float)Screen.height;
						float num3 = ((num2 > num) ? (1f / size.x * 10f * num2) : (1f / size.y * 10f));
						lofiBackground.transform.localScale = new Vector3(num3, num3, 1f);
						lofiBackground.SetActive(value: true);
					}
					else
					{
						MonoBehaviour.print("oh no sprite is null, lets just ignore the sprite entirely");
					}
				}
			}
		}
		if (ADOBase.isCLS || ADOBase.isFreeroamScene || ADOBase.isLevelEditor)
		{
			GCS.practiceMode = false;
			GCS.checkpointBeforePractice = 0;
			GCS.speedTrialModeBeforePractice = false;
			GCS.speedRunBeforePractice = 1f;
			GCS.internalLevelName = null;
			GCS.customLevelId = null;
		}
		if (!gameworld || GCS.speedTrialMode)
		{
			GCS.checkpointNum = 0;
		}
		Button pauseButton = scrUIController.instance.pauseButton;
		if (ADOBase.isMobile || (ADOBase.isSwitch && (MobileMenuController.instance != null || ADOBase.isCLS)))
		{
			pauseButton.gameObject.SetActive(!scnMobileMenu.firstTimeLoadingScene);
			pauseButton.onClick.AddListener(delegate
			{
				if (!ADOBase.controller.paused)
				{
					TogglePauseGame();
				}
			});
		}
		else
		{
			pauseButton.gameObject.SetActive(value: false);
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(ADOBase.gc.pauseMenuPrefab);
		if (!GCS.lofiVersion || ADOBase.isMobileMenu)
		{
			pauseMenu = gameObject.GetComponent<PauseMenu>();
		}
		if (gameworld)
		{
			errorMeter = UnityEngine.Object.Instantiate(ADOBase.gc.errorMeterPrefab).GetComponent<scrHitErrorMeter>();
			errorMeter.gameObject.SetActive(value: false);
			errorMeter.UpdateLayout(Persistence.hitErrorMeterSize, Persistence.hitErrorMeterShape);
		}
		SaveDataSetup();
		paused = ADOBase.isLevelEditor;
		Awake_Rewind();
		if (benchmarkMode)
		{
			base.gameObject.AddComponent<scrBenchmark>();
		}
		if (ADOBase.IsHalloweenWeek())
		{
			_ = ADOBase.conductor?.song;
			if (ADOBase.conductor != null && ADOBase.conductor.song != null && ADOBase.conductor.song.clip != null && ADOBase.conductor.song.clip.name.StartsWith("1-X"))
			{
				ADOBase.conductor.song.clip = ADOBase.gc.halloweenMusic;
			}
		}
		lastTimeStatsUploaded = Time.unscaledTime;
		noFail = GCS.useNoFail;
		if (noFail)
		{
			freeroamInvulnerability = true;
		}
		unlockKeyLimiter = GCS.useUnlockKeyLimiter;
		if (ADOBase.sceneName == "XT-X")
		{
			RDString.LoadLevelEditorFonts();
		}
		ResetInputEventFfx();
	}

	public void UpdateErrorMeterVisibility()
	{
		if (ADOBase.isLevelEditor && ADOBase.editor.shouldHideGameplayUIForAutoplay)
		{
			errorMeter.gameObject.SetActive(value: false);
		}
		else if ((bool)errorMeter && gameworld && Persistence.hitErrorMeterSize != ErrorMeterSize.Off)
		{
			errorMeter.gameObject.SetActive(!paused);
		}
	}

	public void ResetInputEventFfx()
	{
		for (int i = 0; i < EditorConstants.InputEventStateSize; i++)
		{
			inputEventFfx[i] = new ffxSetInputEventPlus[EditorConstants.InputEventTargetSize];
		}
	}

	public void UpdateVisualSettings()
	{
		visualQuality = Persistence.visualQuality;
		visualEffects = Persistence.visualEffects;
		string text = ADOBase.sceneName;
		if (levelName == "MO-X" || text.IsTaro())
		{
			visualQuality = VisualQuality.High;
		}
		if (!(levelName == "MO-X"))
		{
			switch (text)
			{
			default:
				return;
			case "ML-X":
			case "MN-X":
			case "XN-X":
				break;
			}
		}
		visualEffects = VisualEffects.Full;
	}

	public void SetupImportantVariables()
	{
		UpdateVisualSettings();
		strictHoldsSaved = Persistence.holdBehavior == HoldBehavior.Normal;
		strictHolds = strictHoldsSaved;
		requireHolding = Persistence.holdBehavior < HoldBehavior.NoHoldNeeded;
		freeroamInvulnerability = Persistence.freeroamInvulnerability;
	}

	public void Awake_Rewind()
	{
		UpdateVisualSettings();
		if (ADOBase.sceneName == "XN-X")
		{
			level = new LevelTNO();
		}
		if (ADOBase.sceneName == "ML-X")
		{
			level = new LevelML();
		}
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			item.Rewind();
		}
		if (scrVfxPlus.instance != null)
		{
			scrVfxPlus.instance.enabled = true;
		}
		frameStart = Time.frameCount;
		Time.timeScale = 1f;
		AudioListener.pause = false;
		audioPaused = false;
		goShown = false;
		multipressAndHasPressedFirstPress = false;
		multipressPenalty = false;
		forceOK = false;
		curFreeRoamSection = 0;
		freeroamUpTime = 0f;
		curCountdown = 0;
		levelWasSkipped = false;
		winTime = 0f;
		levelNameShouldHide = false;
		ChangeState(States.Start);
		if (gameworld)
		{
			if (ADOBase.isOfficialLevel && !GCS.FOOL_JOKER)
			{
				Persistence.savedCurrentLevel = levelName;
			}
			txtCongrats = scrUIController.instance.txtCongrats;
			txtCongrats.SetLocalizedFont();
			txtAllStrictClear = scrUIController.instance.txtAllStrictClear;
			txtAllStrictClear.SetLocalizedFont();
			detailedResults = scrUIController.instance.txtResults;
			detailedResults.textComponent.SetLocalizedFont();
			detailedResults.textComponent.text = "";
			txtTryCalibrating = scrUIController.instance.txtTryCalibrating;
			txtTryCalibrating.SetLocalizedFont();
			txtTryCalibrating.text = "";
			txtPercent = scrUIController.instance.txtPercent;
			txtLevelName = scrUIController.instance.txtLevelName;
			txtAprilCongrats = scrUIController.instance.txtAprilCongrats;
			txtAprilCongrats.SetLocalizedFont();
			float a = (GCS.speedTrialMode ? GCS.currentSpeedTrial : (ADOBase.isLevelEditor ? ADOBase.editor.playbackSpeed : 1f));
			if (Mathf.Approximately(a, 1f))
			{
				txtLevelName.text = caption;
			}
			else
			{
				string text = RDString.Get("levelSelect.multiplier", new Dictionary<string, object> { 
				{
					"multiplier",
					a.ToString("0.0#")
				} });
				txtLevelName.text = caption + " (" + text + ")";
			}
			txtLevelName.SetLocalizedFont();
		}
		GameObject gameObject = GameObject.Find("BGMovingCam");
		if (gameObject != null && gameObject.TryGetComponent<CameraMotionBlur>(out var component))
		{
			component.enabled = false;
		}
		if (!gameworld && scrUIController.instance != null)
		{
			scrUIController.instance.SetToBlack();
		}
		camy.GetComponent<Grayscale>().enabled = false;
		if (gameworld)
		{
			foreach (scrFloor listFloor in scrLevelMaker.instance.listFloors)
			{
				if (listFloor == null)
				{
					return;
				}
				if (GCS.practiceMode)
				{
					int num = Math.Min(GCS.checkpointNum + GCS.practiceLength, scrLevelMaker.instance.listFloors.Count - 1);
					bool flag = listFloor.seqID == num;
					if (flag || listFloor.isportal)
					{
						listFloor.isportal = flag;
						listFloor.levelnumber = Portal.EndOfLevel;
						listFloor.UpdateIconSprite();
					}
				}
				if (listFloor.checkIsPortal)
				{
					if (listFloor.GetComponentsInChildren<scrPortalParticles>().Length != 0)
					{
						break;
					}
					listFloor.SpawnPortalParticles();
				}
			}
		}
		if ((bool)errorMeter && gameworld)
		{
			errorMeter.Reset();
			errorMeter.wrapperRectTransform.gameObject.SetActive(value: true);
		}
	}

	private void PostSong()
	{
		if (!(currFloor == null) && ((!gameworld && !currFloor.freeroamGenerated) || isPuzzleRoom))
		{
			ADOBase.conductor.StartMusic(PostSong);
		}
	}

	private void SaveDataSetup()
	{
		if (ADOBase.isOfficialLevel)
		{
			if (gameworld || (!gameworld && isPuzzleRoom))
			{
				GCS.worldEntrance = (ADOBase.isExpo ? null : currentWorldString);
			}
			else
			{
				GCS.maxLevel = PlayerPrefs.GetInt("maxlevel", 0);
			}
			GCS.maxCalibrationRank = PlayerPrefs.GetInt("maxcalibrationrank", 0);
			if (GCS.d_kong)
			{
				scrKongAPI.Submit("Highest Level", GCS.maxLevel - 1);
				scrKongAPI.Submit("Calibration Rank", GCS.maxCalibrationRank);
			}
		}
		if (GCS.d_customhitmargins)
		{
			GCS.HITMARGIN_COUNTED = PlayerPrefs.GetFloat("difficulty", 60f);
		}
		setupComplete = true;
		if (!gameworld && !expoTutorial)
		{
			currentWorldString = null;
		}
	}

	private void Start()
	{
		if (GCS.d_recording)
		{
			RDC.auto = true;
			RDC.noHud = true;
		}
		if ((bool)txtLevelName)
		{
			txtLevelNameOriginalPosition = txtLevelName.rectTransform.anchoredPosition;
		}
		if (ADOBase.isLevelEditor)
		{
			base.enabled = false;
			return;
		}
		if (gameworld)
		{
			if (!GCS.practiceMode)
			{
				GCS.currentSpeedTrial = GCS.nextSpeedRun;
			}
			if (!ADOBase.customLevel)
			{
				ADOBase.conductor.song.pitch *= GCS.currentSpeedTrial;
			}
			if (!ADOBase.isScnGame)
			{
				StartCoroutine(WaitForStartCo());
			}
		}
		else
		{
			Start_Rewind();
		}
		CheckForAudioOutputChange();
		UpdateVolumeFromPersistence();
		if (!ADOBase.isScnGame)
		{
			DiscordController.instance?.UpdatePresence();
		}
		if (!ADOBase.isIntelMac)
		{
			Debug.Log("Shader.WarmupAllShaders ();");
			Shader.WarmupAllShaders();
		}
		if (Persistence.GetChosenAsynchronousInput() && !AsyncInputManager.isActive)
		{
			AsyncInputManager.ToggleHook(active: true);
		}
		if (ADOBase.isExpo)
		{
			scrLivesCounter obj = scrLivesCounter.instance;
			if ((object)obj != null && obj.lives > 1)
			{
				goto IL_0125;
			}
		}
		scrLivesCounter.instance.gameObject.SetActive(value: false);
		goto IL_0125;
		IL_0125:
		if (GCS.checkpointNum == 0)
		{
			mistakesManager.Reset();
			checkpointsUsed = 0;
		}
		mistakesManager.RevertToLastCheckpoint();
		if (ADOBase.isOfficialLevel)
		{
			ADOBase.uiController.LevelFinishedLoading();
		}
	}

	public static void CheckForAudioOutputChange()
	{
		if (scrConductor.HasAudioOutputChanged())
		{
			scrConductor.UpdateCurrentAudioOutput();
			Notification.instance.ShowCalibration();
		}
	}

	public IEnumerator WaitForStartCo(int seqID = 0, bool remakeFloors = true)
	{
		waitForStartCoCallCount++;
		scrUIController.instance.canvas.enabled = true;
		txtLevelName.text = caption;
		if (GCS.speedTrialMode)
		{
			string text = RDString.Get("levelSelect.multiplier", new Dictionary<string, object> { 
			{
				"multiplier",
				GCS.currentSpeedTrial.ToString("0.0#")
			} });
			txtLevelName.text = caption + " (" + text + ")";
		}
		else if (GCS.practiceMode)
		{
			string text2 = RDString.Get("status.practiceMode");
			txtLevelName.text = caption + "\n(" + text2 + ")";
		}
		txtLevelName.SetLocalizedFont();
		if (!ADOBase.isScnGame)
		{
			ADOBase.lm.CalculateFloorEntryTimes();
		}
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			item.planetarySystem.chosenPlanet.cosmeticRadius = 0f;
			item.planetarySystem.chosenPlanet.FirstFloorAngleSetup();
		}
		HashSet<Filter> filters = new HashSet<Filter>();
		foreach (scrFloor listFloor in ADOBase.lm.listFloors)
		{
			ffxSetFilterPlus[] components = listFloor.GetComponents<ffxSetFilterPlus>();
			foreach (ffxSetFilterPlus ffxSetFilterPlus2 in components)
			{
				if (ffxSetFilterPlus2.enableFilter)
				{
					filters.Add(ffxSetFilterPlus2.filter);
				}
			}
		}
		foreach (Filter item2 in filters)
		{
			scrVfxPlus.instance.filterToComp[item2].enabled = true;
		}
		yield return null;
		yield return null;
		foreach (Filter item3 in filters)
		{
			scrVfxPlus.instance.filterToComp[item3].enabled = false;
		}
		if (!ADOBase.isScnGame)
		{
			scnGame.SetFxPlusFromComponents(ADOBase.lm.listFloors, useComponentNotationForFx);
			scnGame.PrepVfx(ADOBase.lm.listFloors, GCS.checkpointNum);
			ADOBase.lm.ColorFreeroam();
			ADOBase.lm.DrawHolds();
			ADOBase.lm.DrawMultiPlanet();
		}
		else
		{
			ADOBase.customLevel.PrepVfx(seqID, remakeFloors);
		}
		if (GCS.checkpointNum != 0)
		{
			foreach (scrFloor item4 in ADOBase.lm.listFloors.FindAll((scrFloor x) => x.seqID <= GCS.checkpointNum))
			{
				camy.torot += item4.rotatecamera;
				camy.fromrot = camy.torot;
				ffxPlusBase[] componentsInChildren = item4.GetComponentsInChildren<ffxPlusBase>();
				foreach (ffxPlusBase ffxPlusBase2 in componentsInChildren)
				{
					if (ffxPlusBase2.runOnHit)
					{
						if (!(ffxPlusBase2 is ffxCheckpoint))
						{
							ffxPlusBase2.StartEffect(ADOBase.controller.playerOne.planetarySystem.chosenPlanet);
							continue;
						}
						item4.floorIcon = FloorIcon.Checkpoint;
						item4.UpdateIconSprite();
					}
				}
			}
			DOTween.PlayingTweens().ForEach(delegate(Tween t)
			{
				t.Complete(withCallbacks: true);
			});
			foreach (scrPlayer item5 in ADOBase.playerManager)
			{
				PlanetarySystem planetarySystem = item5.planetarySystem;
				planetarySystem.chosenPlanet.ScrubToFloorNumber(GCS.checkpointNum, -1f);
				camy.ViewObjectInstant(planetarySystem.chosenPlanet.transform);
			}
			scrVfxPlus scrVfxPlus2 = scrVfxPlus.instance;
			if (scrVfxPlus2 != null)
			{
				int index = GCS.checkpointNum;
				ffxCheckpoint component = ADOBase.lm.listFloors[GCS.checkpointNum].GetComponent<ffxCheckpoint>();
				if (component != null && component.scrubFourBack)
				{
					index = FindScrubStart(GCS.checkpointNum);
				}
				scrVfxPlus2.pausedTweens.Clear();
				scrVfxPlus2.ScrubToTime((float)ADOBase.lm.listFloors[index].entryTimeAfterExtraBeats);
				scrVfxPlus2.pausedTweens.ForEach(delegate(Tween x)
				{
					if (x.active)
					{
						x.Pause();
					}
				});
			}
			if (currFloor.stickToFloor)
			{
				foreach (scrPlayer item6 in ADOBase.playerManager)
				{
					item6.planetarySystem.chosenPlanet.transform.position = ADOBase.lm.listFloors[GCS.checkpointNum].transform.position;
				}
			}
		}
		scrPressToStart pressToStart = scrUIController.instance.txtPressToStart.GetComponent<scrPressToStart>();
		pressToStart.ShowText();
		if (ADOBase.isScnGame)
		{
			yield return null;
			ADOBase.customLevel.isLoading = false;
		}
		bool prevIsLevelEditor = ADOBase.isLevelEditor;
		int prevWaitForStartCoCallCount = waitForStartCoCallCount;
		while (!levelWasSkipped && (!ADOBase.playerManager.AnyValidInputWasTriggered() || isCutscene))
		{
			if (ADOBase.isLevelEditor != prevIsLevelEditor || prevWaitForStartCoCallCount != waitForStartCoCallCount)
			{
				pressToStart.HideText();
				yield break;
			}
			if (ADOBase.isLevelEditor && !exitingToMainMenu)
			{
				break;
			}
			if (!paused && ADOBase.uiController.difficultyUIMode != DifficultyUIMode.DontShow && !isCutscene)
			{
				if (RDInput.leftPress)
				{
					ADOBase.uiController.DifficultyArrowPressed(rightPressed: false);
				}
				else if (RDInput.rightPress)
				{
					ADOBase.uiController.DifficultyArrowPressed(rightPressed: true);
				}
			}
			foreach (scrPlayer item7 in ADOBase.playerManager)
			{
				item7.holdKeys.Clear();
			}
			yield return null;
		}
		pressToStart.HideText();
		scrUIController.instance.txtCountdown.GetComponent<scrCountdown>().ShowGetReady();
		ADOBase.conductor.Rewind();
		ADOBase.conductor.Start();
		Start_Rewind();
		if (isbosslevel && ADOBase.isOfficialLevel)
		{
			oldPercentComplete = Persistence.GetPercentCompletion(currentWorld, coopMode);
			Persistence.IncrementWorldAttempts(currentWorld, coopMode);
			Persistence.IncrementWorldAttemptsWithoutNewBest(currentWorld, coopMode);
		}
		startedFromCheckpoint = GCS.checkpointNum > 0;
		if (gameworld && levelNameShouldHide && currFloor != null)
		{
			DOVirtual.DelayedCall((float)(ADOBase.conductor.crotchetAtStart / (double)currFloor.speed / (double)ADOBase.conductor.song.pitch * (double)ADOBase.conductor.adjustedCountdownTicks), LevelNameTextAway);
		}
		if (ADOBase.isScnGame)
		{
			ADOBase.customLevel.FinishCustomLevelLoading(seqID);
		}
	}

	public void Start_Rewind(int _currentSeqID = -1)
	{
		if (gameworld)
		{
			while (_currentSeqID < ADOBase.lm.listFloors.Count - 1 && _currentSeqID > 1 && ADOBase.lm.listFloors[_currentSeqID].freeroam)
			{
				_currentSeqID--;
			}
		}
		safetyTilesArePresent = false;
		if (Persistence.freeroamInvulnerability)
		{
			safetyTilesArePresent = true;
		}
		else if (ADOBase.lm != null)
		{
			foreach (scrFloor listFloor in ADOBase.lm.listFloors)
			{
				if (listFloor != null && listFloor.isSafe)
				{
					safetyTilesArePresent = true;
					break;
				}
			}
		}
		if (ADOBase.isLevelEditor)
		{
			DOTween.KillAll(complete: true);
			foreach (scrPlayer item in ADOBase.playerManager)
			{
				item.Rewind();
			}
		}
		if (_currentSeqID != -1)
		{
			GCS.checkpointNum = _currentSeqID;
		}
		if (GCS.checkpointNum != 0 && ADOBase.isLevelEditor)
		{
			scrUIController.instance.SetToBlack();
		}
		if (GCS.d_oldConductor)
		{
			ChangeState(States.Countdown);
		}
		if (GCS.checkpointNum != 0)
		{
			ADOBase.conductor.song.volume = 0f;
		}
		ADOBase.conductor.StartMusic(PostSong, OnMusicScheduled);
		if (gameworld)
		{
			if (!ADOBase.isScnGame)
			{
				ADOBase.lm.CalculateFloorEntryTimes();
			}
			ADOBase.lm.CalculateFloorAngleLengths();
			if (gameworld)
			{
				listBPM.Add(new Tuple<double, double>(0.0, ADOBase.conductor.bpm));
				float num = 1f;
				foreach (scrFloor listFloor2 in scrLevelMaker.instance.listFloors)
				{
					if (listFloor2 == null)
					{
						return;
					}
					if (listFloor2.speed != num)
					{
						listBPM.Add(new Tuple<double, double>(listFloor2.entryBeat, (double)ADOBase.conductor.bpm * (double)listFloor2.speed));
					}
				}
			}
		}
		if (gameworld)
		{
			double num2 = -100.0;
			recommendsTwoFingers = false;
			foreach (scrFloor listFloor3 in ADOBase.lm.listFloors)
			{
				double num3 = listFloor3.entryTime - num2;
				if (num3 > 0.001 && num3 <= 0.125 * (double)ADOBase.conductor.song.pitch)
				{
					numFastPresses++;
				}
				num2 = listFloor3.entryTime;
			}
			if (numFastPresses > 20 || (float)numFastPresses > (float)ADOBase.lm.listFloors.Count / 10f)
			{
				recommendsTwoFingers = true;
			}
		}
		foreach (scrPlayer item2 in ADOBase.playerManager)
		{
			PlanetarySystem obj = item2.planetarySystem;
			obj.chosenPlanet.FirstFloorAngleSetup();
			obj.LoadPlanetColors(item2.playerID);
		}
		ADOBase.conductor.hasSongStarted = false;
		if ((bool)ADOBase.customLevel)
		{
			ADOBase.playerManager.hitTextManager.hitTextContainer.SetActive(value: true);
		}
		mistakesManager.RevertToLastCheckpoint();
	}

	private void OnMusicScheduled()
	{
		if (GCS.checkpointNum != 0)
		{
			ADOBase.conductor.hasSongStarted = true;
			Scrub(GCS.checkpointNum, RDC.auto && (bool)ADOBase.customLevel);
			ChangeState(States.Checkpoint);
		}
		else if (!GCS.d_oldConductor)
		{
			States states = ((gameworld && !forceNoCountdown) ? States.Countdown : States.PlayerControl);
			ChangeState(states);
		}
		ADOBase.uiController.MinimizeDifficultyContainer();
		if (GCS.checkpointNum != 0)
		{
			scrDebugHUDMessage.Log("OnMusicStart");
			if (ADOBase.isLevelEditor)
			{
				scrUIController.instance.FadeFromBlack();
			}
		}
		if (!gameworld)
		{
			scrUIController.instance.WipeFromBlack(scrLoader.instance.startingGame);
			scrLoader.instance.startingGame = false;
		}
		popuptime = Mathf.Min(50f / (ADOBase.conductor.bpm * ADOBase.conductor.song.pitch), 0.5f);
		float b = popuptime;
		popuptime = Mathf.Min(popuptime, b);
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			scrPlanet planet = item.planetarySystem.chosenPlanet;
			if (planet.currfloor != null && planet.currfloor.nextfloor != null)
			{
				b = (float)planet.currfloor.nextfloor.entryTimePitchAdj - (float)planet.currfloor.entryTimePitchAdj;
			}
			if (planet != null && currFloor != null)
			{
				DOTween.To(() => planet.cosmeticRadius, delegate(float x)
				{
					planet.cosmeticRadius = x;
				}, tileSize * currFloor.radiusScale, popuptime);
			}
			foreach (scrPlanet planet2 in item.planetarySystem.planetList)
			{
				if (!planet2.isChosen && currFloor != null)
				{
					planet2.cosmeticRadius = tileSize * currFloor.radiusScale;
				}
			}
		}
	}

	private void Update()
	{
		if (ADOBase.isExpo && !paused && !(base.gameObject.scene.name == GCNS.sceneLevelSelect) && !(base.gameObject.scene.name == "scnSplash"))
		{
			if (Input.anyKeyDown || RDInput.mainPress)
			{
				lastClickTime = Time.unscaledTime;
				restartExpo = false;
			}
			if (!restartExpo && Time.unscaledTime - lastClickTime > 20f)
			{
				restartExpo = true;
				GCS.customLevelPaths = null;
				ADOBase.controller.SaveProgress(save: true);
				ADOBase.loader.LoadScene(GCNS.sceneLevelSelect);
			}
		}
		currentState = (States)(object)base.stateMachine.GetState();
		moving = false;
		bool flag = false;
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			if ((!ADOBase.isLevelSelect && !ADOBase.isCLS) || item.currFloor == null || item.currFloor.tag != "MovingFloor")
			{
				continue;
			}
			flag = true;
			if (!item.currFloor.GetComponent<scrMenuMovingFloor>().moving)
			{
				continue;
			}
			foreach (scrPlayer item2 in ADOBase.playerManager)
			{
				scrPlanet scrPlanet2 = item2.planetarySystem.chosenPlanet;
				if (item2 != item)
				{
					scrPlanet2.SyncPlanetWithAnother(item.planetarySystem.chosenPlanet);
				}
				scrPlanet2.transform.position = item.currFloor.transform.position;
			}
			moving = true;
			break;
		}
		if (flag)
		{
			ADOBase.playerManager.SetAllPlayerResponsive(!moving);
		}
		if (!ADOBase.isMobile && ((debugModeCheatCode.CheckCheatCode() && Time.unscaledTime - debugTileTime < 8f) || (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Home)) || Input.GetKeyDown(KeyCode.F2)))
		{
			RDC.debug = !RDC.debug;
		}
		if (typingModeCheatCode.CheckCheatCode())
		{
			GCS.typingMode = !GCS.typingMode;
			scrFlash.Flash(Color.white);
		}
		if (hideHudCheatCode.CheckCheatCode())
		{
			RDC.noHud = !RDC.noHud;
		}
		if (hideAutoHudCheatCode.CheckCheatCode())
		{
			RDC.noAutoHud = !RDC.noAutoHud;
		}
		if (RDInput.cancelPress && !paused)
		{
			bool flag2 = ADOBase.cls != null && ADOBase.cls.importLevelPanel.gameObject.activeSelf;
			if (GCS.lofiVersion && !gameworld && ADOBase.platform == Platform.Windows)
			{
				Application.Quit();
			}
			else if (ADOBase.cls == null || !ADOBase.cls.optionsPanels.justHidPanels)
			{
				MobileMenuController mobileMenuController = MobileMenuController.instance;
				bool flag3 = !ADOBase.isMobileMenu || mobileMenuController == null || (mobileMenuController != null && mobileMenuController.pauseIsPossible);
				if (flag2)
				{
					ADOBase.cls.CloseImportPanel();
				}
				else if (flag3)
				{
					TogglePauseGame();
				}
			}
		}
		if (GCS.d_drumcontroller)
		{
			if (ADOBase.controller.boothModeDebounceCounter > 0f)
			{
				ADOBase.controller.boothModeDebounceCounter -= Time.deltaTime;
			}
			if (gameworld && Input.GetKeyDown(KeyCode.Tab))
			{
				disableCongratsMessage = true;
				OnLandOnPortal(planetRed, Portal.EndOfLevel, null);
				PortalTravelAction(portalDestination);
			}
		}
		if (RDEditorUtils.CheckForKeyCombo(control: true, shift: true, KeyCode.F))
		{
			GCS.showFPS = !GCS.showFPS;
		}
		if (RDEditorUtils.CheckForKeyCombo(control: true, shift: true, KeyCode.D))
		{
			numTimesSfxToggled++;
			int num = 25;
			if (numTimesSfxToggled != num)
			{
				GCS.playWilhelm = false;
				if (GCS.playDeathSound)
				{
					GCS.playDeathSound = false;
					scrSfx.instance.PlaySfx(SfxSound.OttoDeactivate, MixerGroup.InterfaceParent);
				}
				else
				{
					GCS.playDeathSound = true;
					scrSfx.instance.PlaySfx(SfxSound.OttoActivate, MixerGroup.InterfaceParent);
				}
			}
			else
			{
				GCS.playDeathSound = true;
				GCS.playWilhelm = true;
				scrSfx.instance.PlaySfx(SfxSound.Wilhelm, MixerGroup.InterfaceParent);
			}
		}
		int num2 = currentFloorID;
		currentFloorID = Math.Max(currentFloorID, currFloor?.seqID ?? 0);
		if (currentFloorID > num2)
		{
			level?.Hit(currentFloorID);
		}
		DebugUpdate();
		AnalyticsUpdate();
	}

	public void AnalyticsUpdate()
	{
		if ((gameworld || (!gameworld && currFloor != null && currFloor.freeroamGenerated)) && !ADOBase.customLevel)
		{
			Analytics.OfficialLevelsTime += Time.unscaledDeltaTime;
		}
		if (ADOBase.isCLSLevel)
		{
			Analytics.customLevelsTime += Time.unscaledDeltaTime;
		}
		if (Time.unscaledTime > lastTimeStatsUploaded + 120f)
		{
			Analytics.UploadStatsToSteam();
			lastTimeStatsUploaded = Time.unscaledTime;
		}
	}

	public void DebugUpdate()
	{
		if ((!RDC.debug && (!ADOBase.isUnityEditor || ADOBase.isLevelEditor)) || !RDInput.holdingShift)
		{
			return;
		}
		if (KeyCode.A.WentDown())
		{
			RDC.auto = !RDC.auto;
		}
		if (RDInput.holdingControl && KeyCode.H.WentDown())
		{
			ClearAllAchievements();
		}
		if (KeyCode.H.WentDown())
		{
			Persistence.GiveAchievements();
		}
		if (KeyCode.M.WentDown())
		{
			GCS.d_hitsounds = !GCS.d_hitsounds;
			if (!GCS.d_hitsounds)
			{
				AudioManager.Instance.StopAllSounds();
			}
			scrDebugHUDMessage.LogBool(GCS.d_hitsounds, "Hit Sounds");
		}
		if (gameworld && KeyCode.N.WentDown())
		{
			BeatLevel();
		}
		if (KeyCode.R.WentDown() && !GCS.lofiVersion)
		{
			if (KeyCode.LeftControl.IsDown())
			{
				GCS.checkpointNum = 0;
			}
			Restart();
		}
		if (KeyCode.U.WentDown() && !GCS.lofiVersion)
		{
			GCS.checkpointNum = GCS.customInternalUseCheckpoint;
			Restart();
		}
		if (KeyCode.Comma.IsDown())
		{
			ScrubAdjacent(forward: false);
		}
		if (KeyCode.Period.IsDown())
		{
			ScrubAdjacent(forward: true);
		}
		if (gameworld && KeyCode.P.WentDown())
		{
			DOTween.KillAll();
			ADOBase.loader.LoadScene(ADOBase.GetPreviousLevelName());
		}
		if (KeyCode.Z.WentDown())
		{
			GCS.d_stationary = !GCS.d_stationary;
			scrDebugHUDMessage.LogBool(GCS.d_stationary, "Stationary");
		}
		if (KeyCode.O.WentDown())
		{
			RDC.noHud = !RDC.noHud;
			scrDebugHUDMessage.LogBool(RDC.noHud, "No HUD");
		}
		if (KeyCode.Y.WentDown())
		{
			RDC.partialNoHud = !RDC.partialNoHud;
			scrDebugHUDMessage.LogBool(RDC.partialNoHud, "No HUD (Partial)");
		}
		if (KeyCode.Backslash.WentDown() && background != null)
		{
			background.SetActive(!background.activeSelf);
		}
	}

	public void BeatLevel()
	{
		OnLandOnPortal(planetRed, Portal.EndOfLevel, null);
		PortalTravelAction(portalDestination);
		if (currentWorldString.IsTaro() && isbosslevel && !isPuzzleRoom)
		{
			int medalCount = WorldData.dict[currentWorldString].medalCount;
			int[] array = new int[medalCount];
			for (int i = 0; i < medalCount; i++)
			{
				array[i] = 3;
			}
			Persistence.SetMedalsForDLCLevel(currentWorldString, array);
		}
	}

	public void OnLandOnPortal(scrPlanet planetThatWon, Portal portalDestination, string portalArguments)
	{
		if (winTime != 0f)
		{
			return;
		}
		winTime = Time.unscaledTime;
		bool flag = false;
		if (GCS.d_newgrounds)
		{
			scrNewgroundsAPIManager.StaticCheckMedals();
		}
		this.portalArguments = portalArguments;
		this.portalDestination = portalDestination;
		scrFlash.Flash(Color.white.WithAlpha(0.4f));
		if (GCS.practiceMode)
		{
			DOTween.KillAll();
			ADOBase.conductor.song.DOFade(0f, 1f).OnKill(delegate
			{
				ADOBase.conductor.song.Stop();
			});
		}
		if (levelNameShouldHide)
		{
			LevelNameTextRestore();
		}
		if (ADOBase.sceneName == "TP-X")
		{
			txtCongrats = scrUIController.instance.txtCongrats;
			txtCongrats.gameObject.SetActive(value: true);
			txtCongrats.text = RDString.Get("status.congratulations");
			scrSfx.instance.PlaySfx(SfxSound.Applause, MixerGroup.InterfaceParent);
		}
		if (gameworld || (!gameworld && !isPuzzleRoom && currFloor.freeroamGenerated))
		{
			ADOBase.playerManager.SetAllPlayerResponsive(responsive: false);
			foreach (scrPlayer item in ADOBase.playerManager)
			{
				if (item != planetThatWon.player && item.alive)
				{
					int seqID = item.planetarySystem.chosenPlanet.currfloor.seqID;
					int num = ADOBase.lm.listFloors.Count - 2;
					if (seqID == num)
					{
						ADOBase.lm.listFloors.Last();
						item.planetarySystem.chosenPlanet.cachedAngle = item.planetarySystem.chosenPlanet.targetExitAngle;
						item.planetarySystem.chosenPlanet = item.planetarySystem.chosenPlanet.SwitchChosen();
					}
					else if (seqID < num)
					{
						item.Die();
					}
				}
				if (!item.alive)
				{
					item.marginTracker.RegisterDeadTiles(currentSeqID);
				}
				item.marginTracker.CalculatePercentAcc();
			}
			bool flag2 = mistakesManager.IsAllPurePerfect();
			bool flag3 = false;
			if (GCS.practiceMode)
			{
				txtCongrats.text = string.Empty;
				txtAprilCongrats.text = string.Empty;
			}
			else if (!ADOBase.isOfficialLevel)
			{
				string text = (flag2 ? customTxtPurePerfect : customTxtCongrats);
				string key = (flag2 ? "status.allPurePerfect" : "status.congratulations");
				txtCongrats.text = (string.IsNullOrEmpty(text) ? RDString.Get(key) : text);
				if (ADOBase.isCLSLevel)
				{
					mistakesManager.SaveCustom(ADOBase.customLevel.levelData.Hash, wonLevel: true, GCS.currentSpeedTrial);
					ADOBase.uiController.ShowEndscreenLanterns();
				}
				if (GCS.FOOL_SWIRL && ADOBase.customLevel != null && (GCS.customLevelPaths == null || GCS.customLevelIndex >= GCS.customLevelPaths.Length - 1))
				{
					txtAprilCongrats.text = RDString.Get("taro.aprilFools");
					txtAprilCongrats.transform.localPosition = txtCongrats.transform.localPosition + Vector3.up * 75f + Vector3.right * 400f;
				}
				flag = flag2;
			}
			else if (isbosslevel)
			{
				mistakesManager.CalculateTotalAccuracy();
				_ = RDString.language;
				string text2 = null;
				string text3 = null;
				if (currentWorldString == "7" && flag2)
				{
					text2 = RDString.Get("status.world7Purrfect");
				}
				else if (currentWorldString == "11" && !flag2)
				{
					text3 = RDString.Get("status.world11Congratulations");
				}
				string text4 = (flag2 ? text2 : text3);
				if (text4 == null || text4.Contains("[Don't translate]"))
				{
					text4 = RDString.Get(flag2 ? "status.allPurePerfect" : "status.congratulations");
				}
				txtCongrats.text = text4;
				if (GCS.FOOL_SWIRL)
				{
					txtAprilCongrats.text = RDString.Get("taro.aprilFools");
					txtAprilCongrats.transform.localPosition = txtCongrats.transform.localPosition + Vector3.up * 75f + Vector3.right * 400f;
				}
				flag = flag2;
				if (!GCS.practiceMode && !GCS.d_booth)
				{
					endLevelInfo = mistakesManager.Save(currentWorld, wonLevel: true, GCS.currentSpeedTrial);
					if (!flag3)
					{
						ADOBase.uiController.ShowEndscreenLanterns();
					}
				}
				else
				{
					endLevelInfo.endLevelType = EndLevelType.WinInPracticeMode;
				}
				if (GCS.speedTrialMode)
				{
					GCS.nextSpeedRun = GCS.currentSpeedTrial + 0.1f;
				}
				if (saveProgressConditions)
				{
					Persistence.DeleteSavedProgress();
				}
			}
			else if (!levelName.IsNullOrEmpty())
			{
				flag3 = true;
				string text5 = levelName;
				int num2 = int.Parse(text5[text5.Length - 1].ToString());
				int worldZeroIndexed = currentWorld;
				if (Persistence.GetLevelTutorialProgress(worldZeroIndexed) < num2)
				{
					Persistence.SetLevelTutorialProgress(worldZeroIndexed, num2);
				}
			}
			VirtualAvatarCanvas.instance?.Win();
			if (disableCongratsMessage || flag3)
			{
				txtCongrats.text = "";
			}
			ADOBase.controller.txtCongrats.gameObject.SetActive(value: true);
			ADOBase.controller.txtAprilCongrats.gameObject.SetActive(value: true);
			if (!isPuzzleRoom && showDetailedResults && !flag3)
			{
				detailedResults.Show();
			}
		}
		if (!RDC.auto && flag)
		{
			scrSfx.instance.PlaySfx(SfxSound.PurePerfect, MixerGroup.ConductorSfx);
		}
		if (gameworld && scrMistakesManager.hardestDifficulty == Difficulty.Strict && txtAllStrictClear != null && !startedFromCheckpoint)
		{
			txtAllStrictClear.text = RDString.Get("status.allStrictClear");
			txtAllStrictClear.gameObject.SetActive(value: true);
		}
		ChangeState(States.Won);
	}

	public void PortalTravelAction(Portal destination)
	{
		if (transitioningLevel)
		{
			return;
		}
		portalDestination = destination;
		bool flag = false;
		WipeDirection wipeDirection = WipeDirection.StartsFromRight;
		switch (portalDestination)
		{
		case Portal.EndOfLevel:
			if (currentWorldString == "T4" && isbosslevel && Persistence.taroStoryProgress < 4)
			{
				Persistence.taroStoryProgress = 4;
				Persistence.Save();
			}
			if (currentWorldString == "T5" && isbosslevel && Persistence.taroStoryProgress < 6)
			{
				Persistence.taroStoryProgress = 6;
				Persistence.Save();
			}
			if (ADOBase.isScnGame)
			{
				if (GCS.speedTrialMode || GCS.practiceMode)
				{
					if (GCS.speedTrialMode)
					{
						GCS.nextSpeedRun = GCS.currentSpeedTrial + 0.1f;
					}
					StartCoroutine(ResetCustomLevel(remakeFloors: true));
				}
				else if (ADOBase.isInternalLevel)
				{
					LevelSource levelSource = WorldData.dict[currentWorldString].levelSource;
					if (isbosslevel)
					{
						QuitToMainMenu();
						flag = true;
						break;
					}
					string nextLevelName = ADOBase.GetNextLevelName();
					if (levelSource == LevelSource.Mixed && nextLevelName.IsBossLevel())
					{
						GCS.internalLevelName = null;
						GCS.sceneToLoad = nextLevelName;
					}
					else
					{
						GCS.internalLevelName = nextLevelName;
					}
				}
				else if (GCS.customLevelIndex >= GCS.customLevelPaths.Length - 1)
				{
					QuitToMainMenu();
					flag = true;
				}
				else
				{
					GCS.customLevelIndex++;
				}
			}
			else if (GCS.speedTrialMode || GCS.practiceMode)
			{
				if (endLevelInfo.endLevelType == EndLevelType.FirstWinSpeedTrial)
				{
					QuitToMainMenu();
					flag = true;
					break;
				}
				if (GCS.speedTrialMode)
				{
					GCS.nextSpeedRun = GCS.currentSpeedTrial + 0.1f;
				}
				if (GCS.practiceMode)
				{
					checkpointsUsed = 1;
				}
				GCS.sceneToLoad = levelName;
			}
			else if (isbosslevel)
			{
				if (currentWorldString == "6" && endLevelInfo.endLevelType == EndLevelType.FirstWin)
				{
					GCS.worldEntrance = null;
				}
				QuitToMainMenu();
				flag = true;
			}
			else
			{
				GCS.sceneToLoad = ADOBase.GetNextLevelName();
			}
			break;
		case Portal.LastLevelPlayed:
		{
			string worldAndLevel = Persistence.savedCurrentLevel;
			if (!RDUtils.CheckDLCLevelPlayable(worldAndLevel))
			{
				worldAndLevel = "1-1";
			}
			EnterLevel(worldAndLevel);
			flag = true;
			break;
		}
		case Portal.CalibrationScene:
			GCS.sceneToLoad = "scnCalibration";
			break;
		case Portal.EditorScene:
			if (!ADOBase.isScnGame)
			{
				GCS.speedTrialMode = false;
				GCS.practiceMode = false;
			}
			GCS.sceneToLoad = "scnEditor";
			GCS.worldEntrance = null;
			SteamStats.IncrementEditorEntered();
			break;
		case Portal.FoolJoker:
			GCS.FOOL_JOKER = !GCS.FOOL_JOKER;
			GCS.sceneToLoad = GCNS.sceneLevelSelect;
			break;
		case Portal.TaroDLCMap:
			GCS.sceneToLoad = GetTaroMenuToGoTo();
			break;
		case Portal.VegaDLCMap:
			GCS.sceneToLoad = "scnVegaMenu";
			break;
		case Portal.PuzzleTest:
			GCS.sceneToLoad = "TP-Test";
			break;
		case Portal.Puzzle1:
			GCS.sceneToLoad = "TP-1";
			break;
		case Portal.Puzzle2:
			GCS.sceneToLoad = "TP-2";
			break;
		case Portal.Puzzle3:
			GCS.sceneToLoad = "TP-X";
			break;
		case Portal.TaroDLCMap3:
			Persistence.taroStoryProgress = 5;
			Persistence.Save();
			GCS.sceneToLoad = "scnTaroMenu3";
			break;
		case Portal.TaroDLCMapExit:
			QuitToMainMenu();
			break;
		case Portal.CustomLevelsScene:
			GCS.speedTrialMode = false;
			GCS.practiceMode = false;
			GCS.currentSpeedTrial = 1f;
			GCS.nextSpeedRun = 1f;
			GCS.sceneToLoad = "scnCLS";
			GCS.worldEntrance = null;
			SteamStats.IncrementClsEntered();
			break;
		case Portal.RDSteamPage:
			SteamFriends.OpenWebOverlay("https://store.steampowered.com/app/774181/Rhythm_Doctor/");
			GCS.sceneToLoad = GCNS.sceneLevelSelect;
			break;
		case Portal.PreviousLevel:
			wipeDirection = WipeDirection.StartsFromLeft;
			GCS.sceneToLoad = ADOBase.GetPreviousLevelName();
			break;
		case Portal.NextLevel:
			GCS.sceneToLoad = ADOBase.GetNextLevelName();
			break;
		case Portal.LowerSpeed:
			wipeDirection = WipeDirection.StartsFromLeft;
			GCS.nextSpeedRun = GCS.currentSpeedTrial - 0.1f;
			GCS.sceneToLoad = levelName;
			break;
		case Portal.HigherSpeed:
			GCS.nextSpeedRun = GCS.currentSpeedTrial + 0.1f;
			GCS.sceneToLoad = levelName;
			break;
		case Portal.GoToLevel:
			EnterLevel(portalArguments);
			flag = true;
			return;
		case Portal.GoToLevelSpeedTrial:
			EnterLevel(portalArguments, speedTrial: true);
			flag = true;
			return;
		case Portal.GoToWorldBossIfReached:
			EnterWorld(portalArguments);
			flag = true;
			return;
		}
		if (!flag)
		{
			StartLoadingScene(wipeDirection);
		}
		transitioningLevel = true;
	}

	public void StartLoadingScene(WipeDirection wipeDirection = WipeDirection.StartsFromRight)
	{
		if (!GCS.lofiVersion)
		{
			ADOBase.loader.LoadSceneWithTransition(wipeDirection);
		}
		else
		{
			DOTween.KillAll();
		}
		deaths = 0;
	}

	public string GetTaroMenuToGoTo()
	{
		scrPlayerManager.SetPlayerCount(1);
		int taroStoryProgress = Persistence.taroStoryProgress;
		string result = ((taroStoryProgress < 3) ? "scnTaroMenu1" : ((taroStoryProgress < 5) ? "scnTaroMenu2" : ((taroStoryProgress < 6) ? "scnTaroMenu3" : "scnTaroMenu0")));
		if (RDC.forceUnlockAllLevels)
		{
			result = "scnTaroMenu0";
		}
		if (currentWorldString == "T4" && taroStoryProgress == 4)
		{
			result = "TP-1";
		}
		if (ADOBase.sceneName == "TP-X" && taroStoryProgress == 4)
		{
			result = "scnTaroMenu3";
		}
		return result;
	}

	public void EnterWorld(string worldOrLevel, bool speedTrial = false)
	{
		string text = worldOrLevel.Split('-').First();
		if (GCS.FOOL_JOKER)
		{
			text += "J";
		}
		WorldData obj = WorldData.dict[text];
		int index = obj.index;
		int levelCount = obj.levelCount;
		int worldAttempts = Persistence.GetWorldAttempts(index);
		int levelTutorialProgress = Persistence.GetLevelTutorialProgress(index);
		string text2 = ((worldAttempts > 0 || levelTutorialProgress >= levelCount - 1) ? "X" : (levelTutorialProgress + 1).ToString());
		string worldAndLevel = text + "-" + text2;
		EnterLevel(worldAndLevel, speedTrial);
	}

	public void EnterLevel(string worldAndLevel, bool speedTrial = false)
	{
		GCS.speedTrialMode = speedTrial;
		GCS.nextSpeedRun = (speedTrial ? 1.1f : 1f);
		GCS.practiceMode = false;
		GCS.customLevelPaths = null;
		GCS.customLevelIndex = 0;
		string[] source = worldAndLevel.Split('-');
		string text = source.First();
		if (GCS.FOOL_JOKER && !text.EndsWith("J"))
		{
			text += "J";
		}
		WorldData worldData = WorldData.dict[text];
		_ = worldData.index;
		string text2 = source.Last();
		int levelCount = worldData.levelCount;
		if (!(text2 == "X"))
		{
			int.Parse(text2);
		}
		worldAndLevel = text + "-" + text2;
		if (speedTrial)
		{
			float trialAim = worldData.trialAim;
			if (trialAim > 0f && trialAim <= 1.1f)
			{
				GCS.nextSpeedRun = trialAim;
			}
		}
		bool num = IsWorldAndLevelInternalLevel(worldAndLevel);
		GCS.internalLevelName = (num ? worldAndLevel : null);
		GCS.sceneToLoad = (num ? "scnGame" : worldAndLevel);
		StartLoadingScene();
		transitioningLevel = true;
	}

	public static bool IsWorldAndLevelInternalLevel(string worldAndLevel)
	{
		string[] source = worldAndLevel.Split('-');
		string key = source.First();
		string text = source.Last();
		bool flag = text == "X";
		LevelSource levelSource = WorldData.dict[key].levelSource;
		if (levelSource != LevelSource.Files)
		{
			if (levelSource == LevelSource.Mixed && !flag)
			{
				return text != "0";
			}
			return false;
		}
		return true;
	}

	public void LoadCustomWorld(string levelPath, bool skipToMain = false, string levelId = null, bool fromBundle = false)
	{
		GCS.sceneToLoad = "scnGame";
		GCS.customLevelPaths = scnGame.GetWorldPaths(levelPath, excludeMain: false, renamed: true);
		GCS.loadCustomFromBundle = fromBundle;
		GCS.customLevelIndex = (skipToMain ? (GCS.customLevelPaths.Length - 1) : 0);
		if (levelId != null)
		{
			GCS.customLevelId = levelId;
		}
		StartLoadingScene();
	}

	public void LoadCustomLevel(string levelPath, string levelId = null, bool fromBundle = false)
	{
		GCS.sceneToLoad = "scnGame";
		GCS.customLevelPaths = new string[1];
		GCS.customLevelPaths[0] = levelPath;
		GCS.loadCustomFromBundle = fromBundle;
		if (levelId != null)
		{
			GCS.customLevelId = levelId;
		}
		StartLoadingScene();
	}

	public void GoToNextLevel()
	{
		PortalTravelAction(GCS.speedTrialMode ? Portal.HigherSpeed : Portal.NextLevel);
	}

	public void GoToPrevLevel()
	{
		PortalTravelAction(GCS.speedTrialMode ? Portal.LowerSpeed : Portal.PreviousLevel);
	}

	public void QuitToMainMenu()
	{
		ADOBase.audioManager.StopLoadingMP3File();
		if (GCS.webVersion)
		{
			ADOBase.loader.LoadScene("scnIntro");
		}
		else
		{
			exitingToMainMenu = true;
			ADOBase.loader.LoadSceneWithTransition(WipeDirection.StartsFromRight);
			if (GCS.customLevelPaths == null)
			{
				string text = currentWorldString;
				bool flag = false;
				foreach (DLCManager dlcManager in base.dlcManagers)
				{
					if (dlcManager.IsDLCLevel(text))
					{
						flag = true;
						GCS.sceneToLoad = dlcManager.GetMenuScene();
					}
				}
				if (!flag)
				{
					GCS.sceneToLoad = GCNS.sceneLevelSelect;
				}
			}
			else
			{
				GCS.sceneToLoad = "scnCLS";
			}
		}
		deaths = 0;
		GCS.currentSpeedTrial = 1f;
	}

	public int FindScrubStart(int floorNum, bool forceDontStartMusicFourTilesBefore = false)
	{
		int result = floorNum;
		if (!forceDontStartMusicFourTilesBefore)
		{
			double num = ADOBase.conductor.crotchetAtStart / (double)ADOBase.lm.listFloors[floorNum].speed;
			for (int num2 = floorNum - 1; num2 >= 1; num2--)
			{
				if (ADOBase.lm.listFloors[num2].entryTime <= ADOBase.lm.listFloors[floorNum].entryTime - (double)ADOBase.conductor.adjustedCountdownTicks * num)
				{
					result = num2;
					break;
				}
			}
			if (ADOBase.lm.listFloors[1].entryTime > ADOBase.lm.listFloors[floorNum].entryTime - (double)ADOBase.conductor.adjustedCountdownTicks * num)
			{
				result = 1;
			}
		}
		return result;
	}

	public void Scrub(int floorNum, bool forceDontStartMusicFourTilesBefore = false)
	{
		scrFloor scrFloor2 = ADOBase.lm.listFloors[floorNum];
		if (floorNum > scrLevelMaker.instance.listFloors.Count - 1 || floorNum < 0)
		{
			scrDebugHUDMessage.Log("Past the limit");
			return;
		}
		if (scrFloor2.midSpin && (bool)scrFloor2.nextfloor && scrFloor2.numPlanets > 2)
		{
			floorNum++;
		}
		while (floorNum > 1 && scrFloor2.freeroam)
		{
			floorNum--;
		}
		curFreeRoamSection = 0;
		for (int i = 0; i < scrLevelMaker.instance.listFreeroamStartTiles.Count; i++)
		{
			if (floorNum > scrLevelMaker.instance.listFreeroamStartTiles[i].seqID)
			{
				curFreeRoamSection++;
			}
		}
		int num = 4;
		if (scrFloor2.countdownTicks > 1 && scrFloor2.extraBeats >= (float)scrFloor2.countdownTicks)
		{
			num = scrFloor2.countdownTicks;
		}
		double num2 = ADOBase.conductor.crotchetAtStart / (double)scrFloor2.speed;
		double val = (ADOBase.conductor.separateCountdownTime ? (ADOBase.conductor.crotchetAtStart * (double)ADOBase.conductor.adjustedCountdownTicks) : 0.0);
		double num3 = num2 * (double)((!forceDontStartMusicFourTilesBefore) ? num : 0);
		double num4 = Math.Max(scrFloor2.entryTimeAfterExtraBeats - num3, val);
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			item.planetarySystem.ScrubToFloorNumber(floorNum, (float)(scrFloor2.entryTime - num4) / ADOBase.conductor.song.pitch, (bool)ADOBase.customLevel || RDC.debug);
		}
		if (RDC.debug)
		{
			camy.ViewObjectInstant(playerOne.planetarySystem.chosenPlanet.transform);
		}
		AudioListener.pause = true;
		ADOBase.conductor.ScrubMusicToTime(num4);
		GameObject gameObject = GameObject.Find("Vfx");
		if (gameObject == null)
		{
			return;
		}
		scrVfxPlus component = gameObject.GetComponent<scrVfxPlus>();
		if (!(component == null))
		{
			if ((bool)ADOBase.customLevel)
			{
				component.ScrubToTime((float)num4);
			}
			AudioListener.pause = false;
			ADOBase.conductor.PlayHitTimes();
		}
	}

	public void ScrubAdjacent(bool forward)
	{
		int floorNum = currFloor.seqID + (forward ? 1 : (-1));
		Scrub(floorNum, forceDontStartMusicFourTilesBefore: true);
	}

	public bool TogglePauseGame()
	{
		AsyncInputManager.offsetTickUpdated = false;
		if (ADOBase.isScnGame && (Time.frameCount - frameStart < 4 || (ADOBase.customLevel != null && ADOBase.customLevel.isLoading)))
		{
			return paused;
		}
		if (!ADOBase.isLevelEditor && scrUIController.instance.transitionPanel.gameObject.activeSelf)
		{
			return paused;
		}
		if (ADOBase.isCLS && ADOBase.cls.refreshing)
		{
			return paused;
		}
		if (GCS.d_boothDisablePossibleMessUpButtons || GCS.webVersion)
		{
			QuitToMainMenu();
		}
		paused = !paused;
		audioPaused = paused;
		base.enabled = !paused;
		Time.timeScale = (paused ? 0f : 1f);
		if (!ADOBase.isLevelEditor || ADOBase.isScnGame)
		{
			if (paused)
			{
				if (!GCS.lofiVersion || ADOBase.isMobileMenu)
				{
					pauseMenu.Show(PauseMenu.Submenu.Main, playSound: true);
				}
			}
			else
			{
				CheckForAudioOutputChange();
				pauseMenu.Hide();
			}
		}
		scrVfxPlus scrVfxPlus2 = scrVfxPlus.instance;
		VideoPlayer videoPlayer = (scrVfxPlus2 ? scrVfxPlus2.videoBG : null);
		if ((bool)videoPlayer && videoPlayer.isActiveAndEnabled && (videoPlayer.isPaused || videoPlayer.isPlaying))
		{
			if (paused)
			{
				videoPlayer.Pause();
			}
			else
			{
				videoPlayer.Play();
			}
		}
		if (!paused)
		{
			AsyncInputUtils.UpdateOffsetTime(1L);
		}
		return paused;
	}

	public void Restart(bool fromBeginning = false)
	{
		if (fromBeginning)
		{
			RestartProgress();
		}
		ADOBase.loader.LoadSceneWithTransition(WipeDirection.StartsFromRight, ADOBase.sceneName);
	}

	public void RestartProgress()
	{
		GCS.checkpointNum = 0;
		Persistence.DeleteSavedProgress();
	}

	private int GetFloorByTime(float time)
	{
		int i;
		for (i = 0; i + 1 < ADOBase.lm.listFloors.Count; i++)
		{
			scrFloor obj = ADOBase.lm.listFloors[i + 1];
			if ((object)obj == null || !(obj.entryTime < (double)time))
			{
				break;
			}
		}
		return i;
	}

	public void SetPracticeMode(bool practice)
	{
		GCS.practiceMode = practice;
		if (practice)
		{
			float num = (float)ADOBase.lm.listFloors[currentSeqID].entryTime;
			List<scrFloor> listFloors = ADOBase.lm.listFloors;
			float num2 = (float)listFloors[listFloors.Count - 1].entryTime;
			float num3 = num - 2.5f;
			float num4 = num + 10f;
			if (num4 - num3 < 10f)
			{
				num3 = num2 - 10f;
			}
			int num5 = GetFloorByTime(num3);
			int num6 = GetFloorByTime(num4);
			if (num6 - num5 <= 0)
			{
				num5--;
				num6 = num5 + 1;
			}
			GCS.checkpointBeforePractice = GCS.checkpointNum;
			GCS.checkpointNum = num5;
			GCS.practiceLength = num6 - num5;
			checkpointsUsed = 1;
			if (GCS.speedTrialMode)
			{
				GCS.speedTrialModeBeforePractice = GCS.speedTrialMode;
				GCS.speedRunBeforePractice = GCS.currentSpeedTrial;
			}
			GCS.currentSpeedTrial = 0.9f;
			GCS.nextSpeedRun = 0.9f;
			GCS.speedTrialMode = false;
		}
		else
		{
			GCS.checkpointNum = GCS.checkpointBeforePractice;
			checkpointsUsed = 0;
			if (GCS.speedTrialModeBeforePractice)
			{
				GCS.nextSpeedRun = GCS.speedRunBeforePractice;
				GCS.speedTrialMode = true;
			}
			else
			{
				GCS.nextSpeedRun = 1f;
			}
		}
		if ((bool)ADOBase.customLevel)
		{
			StartCoroutine(ResetCustomLevel(remakeFloors: true));
		}
		else
		{
			Restart();
		}
	}

	public void SkipLevel()
	{
		PortalTravelAction(portalDestination);
	}

	public bool IsScreenPointInsideUIElements(Vector2 position)
	{
		float num = (float)Screen.height / 1000f;
		if (currentState != States.PlayerControl && ADOBase.uiController.difficultyUIMode != DifficultyUIMode.DontShow)
		{
			float num2 = num * 260f;
			float num3 = num * 555f;
			if (position.x > (float)Screen.width - num3 && position.y < num2)
			{
				return true;
			}
		}
		float num4 = num * 200f;
		float num5 = num4;
		if (position.x > (float)Screen.width - num5 && position.y > (float)Screen.height - num4)
		{
			return true;
		}
		return false;
	}

	private void LateUpdate()
	{
		UpdateListenerVolume();
		ADOBase.uiController.mutedImage.gameObject.SetActive(volume == 0);
		FloorMesh.UpdateAllRequired();
	}

	public void UpdateListenerVolume()
	{
		float num = (float)volume * 0.1f;
		AudioListener.volume = 0.5f * num * num;
	}

	private void UpdateVolumeFromPersistence()
	{
		RDUtils.SetMixerVolume("MusicVolume", (float)Persistence.musicVolume / 10f);
		RDUtils.SetMixerVolume("HitsoundsVolume", (float)Persistence.hitSoundVolume / 10f);
		RDUtils.SetMixerVolume("SfxVolume", (float)Persistence.sfxVolume / 10f);
		RDUtils.SetMixerVolume("InterfaceVolume", (float)Persistence.interfaceVolume / 10f);
	}

	public void ChangeToStartState()
	{
		ChangeState(States.Start);
	}

	public bool IsPercentCompleteBest()
	{
		return percentComplete > oldPercentComplete;
	}

	private void OnApplicationQuit()
	{
		SteamClient.Shutdown();
	}

	public void ClearAllAchievements()
	{
		SteamController.Instance.Achievements.Clear();
	}

	public void OnApplicationPauseCallback(bool pauseStatus)
	{
		if (!gameworld || (bool)ADOBase.editor)
		{
			return;
		}
		if (pauseStatus)
		{
			if (!paused)
			{
				TogglePauseGame();
			}
		}
		else
		{
			CheckForAudioOutputChange();
		}
	}

	public void DebugTileEnter()
	{
		debugTileTime = Time.unscaledTime;
	}

	public void UpdateFreeroam()
	{
		if (ADOBase.lm == null)
		{
			return;
		}
		List<scrFloor> listFreeroamStartTiles = ADOBase.lm.listFreeroamStartTiles;
		if (listFreeroamStartTiles == null || listFreeroamStartTiles.Count == 0 || curFreeRoamSection >= listFreeroamStartTiles.Count)
		{
			return;
		}
		freeroamUpTime += Time.deltaTime;
		scrFloor scrFloor2 = listFreeroamStartTiles[curFreeRoamSection];
		if (!(freeroamUpTime > 0.1f) || !(ADOBase.conductor.songposition_minusi > scrFloor2.nextfloor.entryTime - ADOBase.conductor.crotchetAtStart / (double)scrFloor2.speed * (double)scrFloor2.freeroamEndEarlyBeats))
		{
			return;
		}
		if (currentState == States.PlayerControl || ((bool)ADOBase.customLevel && !ADOBase.controller.paused))
		{
			foreach (scrFloor item in ADOBase.lm.listFreeroam[curFreeRoamSection])
			{
				if (item != chosenPlanet.currfloor)
				{
					item.ToggleCollider(collEn: false);
					item.isLandable = false;
					item.TweenOpacity(0f, (float)ADOBase.conductor.crotchetAtStart);
					continue;
				}
				item.ToggleCollider(collEn: false);
				item.isLandable = false;
				int freeroamEndEarlyBeats = scrFloor2.freeroamEndEarlyBeats;
				float num = (float)ADOBase.conductor.crotchetAtStart * ((float)freeroamEndEarlyBeats - 0.8f) / scrFloor2.speed;
				if (ADOBase.conductor.crotchetAtStart / (double)scrFloor2.speed * (double)scrFloor2.freeroamEndEarlyBeats - (double)num < 0.05000000074505806)
				{
					num = 0f;
				}
				if (num > 0f)
				{
					chosenPlanet.player.LockInput(num);
				}
				float iFrames = (float)ADOBase.conductor.crotchetAtStart * ((float)freeroamEndEarlyBeats - 0.8f) / scrFloor2.speed;
				for (int i = 0; i < planetarySystem.planetList.Count; i++)
				{
					planetarySystem.planetList[i].iFrames = iFrames;
				}
				if (scrFloor2.moveCamAtFreeroamEnd)
				{
					MoveCameraToTile(scrFloor2.nextfloor, item, (float)(ADOBase.conductor.crotchetAtStart / (double)scrFloor2.nextfloor.speed) * (float)freeroamEndEarlyBeats * 0.5f / ADOBase.conductor.song.pitch, scrFloor2.freeroamEndEase, 1f);
				}
				Vector3 vector = tileSize * scrFloor2.nextfloor.radiusScale * new Vector3(Mathf.Sin((float)scrFloor2.exitangle), Mathf.Cos((float)scrFloor2.exitangle), 0f);
				Vector3 endValue = scrFloor2.nextfloor.transform.position - vector;
				float duration = (float)(ADOBase.conductor.crotchetAtStart / (double)scrFloor2.speed) * (float)freeroamEndEarlyBeats * 0.5f / ADOBase.conductor.song.pitch;
				if (item.moveTweens.ContainsKey(TweenType.Position))
				{
					item.moveTweens[TweenType.Position].Kill();
				}
				item.transform.DOMove(endValue, duration).SetEase(scrFloor2.freeroamEndEase);
				if (!currFloor.stickToFloor)
				{
					chosenPlanet.transform.DOMove(endValue, duration).SetEase(scrFloor2.freeroamEndEase);
				}
				DOTween.Sequence().AppendInterval((float)(ADOBase.conductor.crotchetAtStart / (double)scrFloor2.speed) * (float)freeroamEndEarlyBeats / ADOBase.conductor.song.pitch).Append(item.TweenOpacity(0f, (float)ADOBase.conductor.crotchetAtStart));
				double num2 = scrMisc.mod(chosenPlanet.angle, 6.2831854820251465);
				playerOne.lastHit = scrFloor2.nextfloor.entryTime - ADOBase.conductor.crotchetAtStart / (double)scrFloor2.speed * (double)freeroamEndEarlyBeats;
				item.isCCW = item.nextfloor.isCCW;
				ADOBase.controller.playerOne.planetarySystem.isCW = !item.isCCW;
				int num3 = ((!item.isCCW) ? 1 : (-1));
				double num4 = scrFloor2.nextfloor.entryangle - (double)(MathF.PI * (float)num3);
				double snappedLastAngle = num4 - (double)(MathF.PI * (float)freeroamEndEarlyBeats * (float)num3);
				chosenPlanet.SetTargetExitAngle(num4);
				chosenPlanet.SetSnappedLastAngle(snappedLastAngle);
				chosenPlanet.Update_RefreshAngles();
				double num5 = scrMisc.mod(chosenPlanet.angle, 6.2831854820251465);
				chosenPlanet.TweenCosmeticAngle(0f - (float)(num5 - num2));
				gameworld = true;
			}
		}
		curFreeRoamSection++;
	}

	public void UpdateInput()
	{
		if (!_allowDevCached)
		{
			_allowDevCached = true;
			_allowDebug = GCS.allowDebug;
		}
		if (!RDInput.asyncKeyboard.isActive && !RDInput.asyncKeyboardLeft.isActive && !RDInput.asyncKeyboardRight.isActive)
		{
			return;
		}
		ulong num = 0uL;
		AsyncInputManager.keyDownMask.Clear();
		AsyncInputManager.keyUpMask.Clear();
		AsyncInputManager.frameDependentKeyDownMask.Clear();
		AsyncInputManager.frameDependentKeyUpMask.Clear();
		if (!Application.isFocused)
		{
			AsyncInputManager.ClearKeys();
		}
		bool flag = false;
		SkyHookEvent result;
		while (AsyncInputManager.keyQueue.TryDequeue(out result))
		{
			sortedKeyQueue.Enqueue(result, (ulong)result.GetTimeInTicks());
		}
		SkyHookEvent element;
		ulong priority;
		while (sortedKeyQueue.TryDequeue(out element, out priority))
		{
			if (element.Type == SkyHook.EventType.KeyPressed)
			{
				flag = true;
			}
			AsyncKeyCode item = new AsyncKeyCode(element.Key, element.Label);
			ulong timeInTicks = (ulong)element.GetTimeInTicks();
			if (timeInTicks != num)
			{
				if (num != 0L)
				{
					ProcessKeyInputs(num);
				}
				num = timeInTicks;
				AsyncInputManager.keyDownMask.Clear();
				AsyncInputManager.keyUpMask.Clear();
			}
			if (element.Type == SkyHook.EventType.KeyPressed)
			{
				if (!AsyncInputManager.keyMask.Contains(item))
				{
					AsyncInputManager.keyMask.Add(item);
					AsyncInputManager.keyDownMask.Add(item);
					AsyncInputManager.frameDependentKeyMask.Add(item);
					AsyncInputManager.frameDependentKeyDownMask.Add(item);
					if (_allowDebug)
					{
						Debug.Log("key down event received!\n" + $" - current frame tick: {AsyncInputManager.currFrameTick}\n" + $" - event tick: {timeInTicks}");
					}
				}
			}
			else
			{
				AsyncInputManager.keyMask.Remove(item);
				AsyncInputManager.keyUpMask.Add(item);
				AsyncInputManager.frameDependentKeyMask.Remove(item);
				AsyncInputManager.frameDependentKeyUpMask.Add(item);
			}
		}
		if (flag)
		{
			frameOnAsyncInputDetected = Time.frameCount;
		}
		if (RDInput.keyboardInput.MainIgnoreActive(ButtonState.WentDown) > 0)
		{
			frameOnLastInput = Time.frameCount;
		}
		if (!flag)
		{
			if (frameOnLastInput > 0 && Time.frameCount - frameOnLastInput > 5)
			{
				frameOnLastInput = 0;
				if (Time.frameCount - frameOnAsyncInputDetected > 10 && base.state != States.PlayerControl)
				{
					Debug.LogWarning("Async Input >> WARNING: Async input is not working properly!");
					RDInputType_AsyncKeyboard.IncrementDefuncWarn();
				}
			}
		}
		else
		{
			frameOnLastInput = 0;
		}
		ProcessKeyInputs(num);
	}

	private static States GetDestinationState(StateEngine stateMachine)
	{
		return (States)(object)((StateMapping)DestinationStateField.GetValue(stateMachine)).state;
	}

	private void ProcessKeyInputs(ulong eventTick)
	{
		if (paused)
		{
			return;
		}
		ulong num = ((eventTick != 0L) ? eventTick : AsyncInputManager.currFrameTick);
		if (base.state == States.PlayerControl && GetDestinationState(base.stateMachine) == States.PlayerControl)
		{
			foreach (scrPlayer item in ADOBase.playerManager)
			{
				item.Simulated_PlayerControl_Update(num);
			}
		}
		AsyncInputManager.lastReportedTargetTick = num;
	}

	public void ScreenShake(float duration, float strength)
	{
		DOTween.Shake(() => camy.shake, delegate(Vector3 x)
		{
			camy.shake = x;
		}, duration, strength, 100);
	}

	public void MoveCameraToTile(scrFloor floor, scrFloor from, float fSecs, Ease ease, float zoom = -1f)
	{
		ffxCameraPlus ffxCameraPlus2 = chosenPlanet.currfloor.gameObject.AddComponent<ffxCameraPlus>();
		ffxCameraPlus2.duration = fSecs;
		ffxCameraPlus2.targetPos = new Vector2(floor.transform.position.x - from.transform.position.x, floor.transform.position.y - from.transform.position.y);
		ffxCameraPlus2.targetRot = camy.transform.eulerAngles.z;
		if (zoom <= 0f)
		{
			ffxCameraPlus2.targetZoom = camy.zoomSize;
		}
		else
		{
			ffxCameraPlus2.targetZoom = zoom;
		}
		ffxCameraPlus2.ease = ease;
		ffxCameraPlus2.movementType = CamMovementType.Tile;
		ffxCameraPlus2.StartEffectManual();
	}

	public void MoveCameraToObject(GameObject o, float fSecs, Ease ease, float zoom = -1f)
	{
		ffxCameraPlus ffxCameraPlus2 = new ffxCameraPlus();
		ffxCameraPlus2.ForceUpdateCamParent();
		ffxCameraPlus2.duration = fSecs;
		ffxCameraPlus2.targetPos = new Vector2(o.transform.position.x, o.transform.position.y);
		ffxCameraPlus2.targetRot = camy.transform.eulerAngles.z;
		if (zoom <= 0f)
		{
			ffxCameraPlus2.targetZoom = camy.zoomSize;
		}
		else
		{
			ffxCameraPlus2.targetZoom = zoom;
		}
		ffxCameraPlus2.ease = ease;
		ffxCameraPlus2.movementType = CamMovementType.Tile;
		ffxCameraPlus2.StartEffectManual();
	}

	public void MoveCameraToPlayer(float fSecs, Ease ease, float zoom = -1f)
	{
		ffxCameraPlus ffxCameraPlus2 = new ffxCameraPlus();
		ffxCameraPlus2.ForceUpdateCamParent();
		ffxCameraPlus2.duration = fSecs;
		ffxCameraPlus2.targetPos = Vector2.zero;
		ffxCameraPlus2.targetRot = camy.transform.eulerAngles.z;
		if (zoom <= 0f)
		{
			ffxCameraPlus2.targetZoom = camy.zoomSize;
		}
		else
		{
			ffxCameraPlus2.targetZoom = zoom;
		}
		ffxCameraPlus2.ease = ease;
		ffxCameraPlus2.movementType = CamMovementType.Player;
		ffxCameraPlus2.StartEffectManual();
	}

	public void LevelNameTextAway()
	{
		levelNameTextPresent = false;
		lvlname = scrUIController.instance.txtLevelName.transform.GetComponent<RectTransform>();
		lvlnameAnchorPos = lvlname.anchoredPosition;
		lvlname.DOAnchorPosY(200f, 1f).SetEase(Ease.InBack);
	}

	public void LevelNameTextRestore()
	{
		if (!levelNameTextPresent)
		{
			lvlname.DOAnchorPosY(lvlnameAnchorPos.y, 1f).SetEase(Ease.OutBack);
		}
	}

	public void SaveProgress(bool save)
	{
		if (saveProgressConditions)
		{
			scrMistakesManager.SaveCheckpointProgress(save);
		}
	}

	public void EnableHallOfMirrors(bool homEnabled)
	{
		this.homEnabled = homEnabled;
		scrCamera.instance.Bgcamstatic.clearFlags = (homEnabled ? CameraClearFlags.Depth : CameraClearFlags.Color);
	}

	public float GetAutoBlinkDuration()
	{
		return (float)((currFloor.holdLength > -1 && currFloor.nextfloor != null) ? (currFloor.nextfloor.entryTime - (double)(float)currFloor.entryTime) : (60.0 / ((double)(ADOBase.conductor.bpm * ADOBase.conductor.song.pitch) * playerOne.planetarySystem.speed) / 2.0));
	}

	private void Countdown_Update()
	{
		if ((float)ADOBase.conductor.beatNumber >= ADOBase.conductor.adjustedCountdownTicks || !gameworld || forceNoCountdown)
		{
			ChangeState(States.PlayerControl);
		}
		if (camy.followMode)
		{
			camy.topos = chosenPlanet.transform.position.WithZ(camy.transform.position.z);
		}
	}

	private void Checkpoint_Enter()
	{
		startTime = ADOBase.conductor.songposition_minusi;
	}

	private void Checkpoint_Update()
	{
		if (!ADOBase.isLevelEditor || !ADOBase.editor.inStrictlyEditingMode)
		{
			double num = (ADOBase.conductor.songposition_minusi - startTime) / (ADOBase.lm.listFloors[GCS.checkpointNum].entryTimeAfterExtraBeats - startTime);
			ADOBase.conductor.song.volume = Mathf.Lerp(0f, startVolume, (float)num);
			if (ADOBase.conductor.songposition_minusi >= ADOBase.lm.listFloors[GCS.checkpointNum].entryTimeAfterExtraBeats)
			{
				ChangeState(States.PlayerControl);
			}
			if (camy.followMode)
			{
				camy.topos = chosenPlanet.transform.position.WithZ(camy.transform.position.z);
			}
		}
	}

	private void Checkpoint_Exit()
	{
		camy.GetComponent<Grayscale>().enabled = false;
		ADOBase.conductor.song.volume = startVolume;
	}

	private void PlayerControl_Enter()
	{
		if (gameworld)
		{
			camy.GetComponent<Grayscale>().enabled = false;
		}
		scrVfxPlus scrVfxPlus2 = scrVfxPlus.instance;
		if (scrVfxPlus2 != null)
		{
			scrVfxPlus2.pausedTweens.ForEach(delegate(Tween x)
			{
				if (x != null && x.active)
				{
					x.Play();
				}
			});
			scrVfxPlus2.pausedTweens.Clear();
		}
		AsyncInputUtils.UpdateOffsetTime(1L);
	}

	private void PlayerControl_Update()
	{
		if (!AsyncInputManager.isActive)
		{
			foreach (scrPlayer item in ADOBase.playerManager)
			{
				item.Simulated_PlayerControl_Update();
			}
		}
		averageFrameTime = 0.5f * averageFrameTime + 0.5f * Time.deltaTime;
		if (camy.followMode && camy.followMovingPlatforms)
		{
			_ = chosenPlanet.transform.position;
			camy.topos = chosenPlanet.transform.position.WithZ(camy.transform.position.z);
		}
		foreach (scrPlayer item2 in ADOBase.playerManager)
		{
			item2.UpdateLockInput();
		}
		UpdateFreeroam();
	}

	private void Won_Enter()
	{
		if (!gameworld && !currFloor.freeroamGenerated)
		{
			PortalTravelAction(portalDestination);
		}
	}

	private void Won_Update()
	{
		bool flag = ADOBase.playerManager.AnyValidInputWasTriggered();
		if (!ADOBase.isLevelEditor && flag && Time.unscaledTime - winTime > 1f && canExitLevel)
		{
			PortalTravelAction(portalDestination);
		}
	}

	public void OnPlayerDied(scrPlayer deadPlayer, bool overload = false, bool multipress = false, string failMessage = "", bool hitbox = false)
	{
		int num = 0;
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			if (item != deadPlayer && item.alive && !item.doingRevivalCountdown)
			{
				num++;
			}
		}
		if (num > 0)
		{
			return;
		}
		foreach (scrPlayer item2 in ADOBase.playerManager)
		{
			if (item2.doingRevivalCountdown)
			{
				item2.invincibilityTimer = 0f;
				item2.alive = false;
				item2.planetarySystem.Die(PlanetarySystem.DeathAnimation.CrumbleAndExplode);
			}
		}
		FailAction(overload, multipress, failMessage, hitbox);
	}

	public void FailAction(bool overload = false, bool multipress = false, string failMessage = "", bool hitbox = false)
	{
		ADOBase.controller.SaveProgress(save: false);
		if (currFloor.nextfloor != null)
		{
			_ = currFloor.nextfloor.auto;
		}
		else
			_ = 0;
		ChangeState(States.Fail);
		scrVfxPlus scrVfxPlus2 = scrVfxPlus.instance;
		if (scrVfxPlus2 != null)
		{
			scrVfxPlus2.enabled = false;
			VideoPlayer videoBG = scrVfxPlus2.videoBG;
			if (videoBG != null && videoBG.isPlaying)
			{
				scrVfxPlus2.videoBG.Pause();
			}
		}
		if (overload)
		{
			scrUIController.instance.txtCountdown.GetComponent<scrCountdown>().ShowOverload();
			AdjustTryCalibratingTextPosition();
			if (failMessage.IsNullOrEmpty())
			{
				string key = (multipress ? "status.multipressExplainer" : "status.overloadExplainer");
				txtTryCalibrating.text = RDString.Get(key);
			}
		}
		if (!failMessage.IsNullOrEmpty())
		{
			txtTryCalibrating.text = failMessage;
		}
		ADOBase.conductor.song.Stop();
		if (ADOBase.controller.isPuzzleRoom || ((bool)ADOBase.controller.currFloor && ADOBase.controller.currFloor.freeroamGenerated))
		{
			ADOBase.conductor.song.volume = 0f;
		}
		AudioManager.Instance.StopAllSounds();
		if (ADOBase.isLevelEditor && RDC.auto)
		{
			ADOBase.editor.blinkTimer.Kill();
			ADOBase.editor.autoFailed = true;
		}
		if ((bool)conditionalFloor)
		{
			foreach (ffxPlusBase lossEffect in conditionalFloor.lossEffects)
			{
				lossEffect.StartEffect();
			}
		}
		if ((GCS.checkpointNum > 0 && (!ADOBase.isLevelEditor || GCS.checkpointNum != ADOBase.editor.selectedFloorCached)) || GCS.practiceMode)
		{
			checkpointsUsed++;
		}
	}

	public void Fail2Action()
	{
		if (base.state != States.Fail || (!gameworld && !currFloor.freeroam))
		{
			return;
		}
		ChangeState(States.Fail2);
		mistakesManager.CalculateTotalAccuracy();
		if (!isPuzzleRoom)
		{
			if (isbosslevel)
			{
				endLevelInfo = mistakesManager.Save(currentWorld, wonLevel: false, GCS.currentSpeedTrial);
			}
			else if (ADOBase.isCLSBossLevel && !GCS.practiceMode)
			{
				endLevelInfo = mistakesManager.SaveCustom(ADOBase.customLevel.levelData.Hash, wonLevel: false, GCS.currentSpeedTrial);
			}
			if (endLevelInfo.newBestType == NewBestType.Applause && !instantExplode)
			{
				scrSfx.instance.PlaySfx(SfxSound.ApplauseQuiet, MixerGroup.ConductorSfx, 3f);
			}
			txtPercent.GetComponent<scrPercentageComplete>().UpdatePercent();
			txtPercent.gameObject.SetActive(value: true);
		}
		deaths++;
		scrUIController.deathCounterToFixDotweenBug++;
		if (GCS.d_booth)
		{
			return;
		}
		string text = "";
		AdjustTryCalibratingTextPosition();
		if (deaths == 1000)
		{
			text = "status.1000Attempts";
		}
		else if (deaths == 100)
		{
			text = "status.100Attempts";
		}
		else if (!ADOBase.isLevelEditor)
		{
			bool flag = false;
			if (playerOne.keyTotal > 5)
			{
				foreach (KeyValuePair<object, int> item in playerOne.keyFrequency)
				{
					object key = item.Key;
					if ((key is KeyCode || key is int) && (float)item.Value / (float)playerOne.keyTotal > 0.95f)
					{
						flag = true;
						break;
					}
				}
			}
			if (deaths == 5 && Persistence.GetOverallProgressStage() < 3)
			{
				text = "status.tryCalibrating";
			}
			else if (deaths == 5 || deaths % 10 == 0)
			{
				if (currFloor.freeroamGenerated || isPuzzleRoom)
				{
					text = "status.tryFreeroamInvulnerability";
				}
				else if (recommendsTwoFingers && flag && !displayedMultiFingerHint)
				{
					text = "status.tryTwoFingers";
					displayedMultiFingerHint = true;
				}
				else
				{
					displayedMultiFingerHint = false;
					if (base.practiceAvailable && !GCS.practiceMode)
					{
						text = "status.tryPractice";
					}
				}
			}
		}
		if (!text.IsNullOrEmpty() && txtTryCalibrating.text.IsNullOrEmpty())
		{
			txtTryCalibrating.text = RDString.Get(text);
		}
	}

	private void Fail2_Update()
	{
		if (!ADOBase.playerManager.AnyValidInputWasTriggered() || scrUIController.instance.isWipingToBlack)
		{
			return;
		}
		if ((bool)ADOBase.customLevel)
		{
			if (!ADOBase.isLevelEditor || !ADOBase.editor.inStrictlyEditingMode)
			{
				StartCoroutine(ResetCustomLevel());
			}
		}
		else
		{
			Restart();
		}
	}

	public IEnumerator ResetCustomLevel(bool remakeFloors = false)
	{
		if (ADOBase.isScnGame)
		{
			bool complete = false;
			scrUIController.instance.WipeToBlack(WipeDirection.StartsFromRight, delegate
			{
				complete = true;
			});
			while (!complete)
			{
				yield return null;
			}
			scrUIController.instance.HideEndscreenLanterns();
		}
		foreach (scrFloor listFloor in ADOBase.lm.listFloors)
		{
			if ((bool)listFloor.bottomGlow)
			{
				listFloor.bottomGlow.gameObject.SetActive(value: false);
			}
			listFloor.topGlow.gameObject.SetActive(value: false);
		}
		if (!GCS.practiceMode && ADOBase.isScnGame)
		{
			GCS.currentSpeedTrial = GCS.nextSpeedRun;
		}
		ADOBase.customLevel.ResetScene(remakeFloors);
		ADOBase.customLevel.Play(GCS.checkpointNum, remakeFloors);
		transitioningLevel = false;
		if (ADOBase.isScnGame)
		{
			yield return null;
			scrUIController.instance.WipeFromBlack();
			ADOBase.playerManager.SetAllPlayerResponsive(responsive: true);
		}
	}

	private void AdjustTryCalibratingTextPosition()
	{
		if (!(txtTryCalibrating == null))
		{
			if (ADOBase.controller?.errorMeter?.gameObject.activeInHierarchy == true)
			{
				txtTryCalibrating.alignment = TextAnchor.UpperCenter;
			}
			else
			{
				txtTryCalibrating.alignment = TextAnchor.MiddleCenter;
			}
		}
	}
}
