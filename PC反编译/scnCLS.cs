using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADOFAI;
using ADOFAI.CLS;
using ADOFAI.SteamIntegration;
using DG.Tweening;
using GDMiniJSON;
using Steamworks;
using Steamworks.Data;
using Steamworks.Ugc;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class scnCLS : ADOBase
{
	private enum SortType
	{
		Difficulty = 0,
		LastPlayed = 1,
		Song = 2,
		Artist = 3,
		Author = 4
	}

	public enum Category
	{
		Selection = 0,
		Workshop = 1,
		Featured = 2,
		Tech = 3
	}

	public enum FeaturedLevelsSource
	{
		None = 0,
		Workshop = 1,
		DLC = 2,
		Local = 3
	}

	public const string FeaturedLevelsLocalPath = "FeaturedLevels";

	public const string MainFilename = "main.adofai";

	public const string BackupFilename = "backup.adofai";

	private const string FolderPrefix = "Folder:";

	private const string NewLevelColor = "#368BE6";

	private static readonly Dictionary<string, float> badgeScales = new Dictionary<string, float>
	{
		{ "CommunityFeatured", 0.7f },
		{ "LOTY2023Winner", 0.65f }
	};

	public static scnCLS instance;

	public static Category entryCategory;

	public static Category lastPlayedCategory;

	[Header("Components")]
	public OptionsPanelsCLS optionsPanels;

	public PreviewSongPlayer previewSongPlayer;

	public Transform gemTop;

	public Transform gemBottom;

	public Transform gemExitFolder;

	public GameObject chainTop;

	public GameObject chainBottom;

	public Text currentSearchText;

	public Text loadingText;

	public ImportLevelsCLS levelImporter;

	[Header("Canvas")]
	public RectTransform levelInfoCanvas;

	public CanvasScaler canvasScaler;

	public CanvasGroup levelInfoCanvasGroup;

	public ImportLevelsCLS importLevelPanel;

	[Header("Floor")]
	public Transform floorContainer;

	public scrFloor entranceTile;

	public SpriteRenderer downloadIcon;

	public SpriteRenderer downloadingIcon;

	public TMP_Text downloadText;

	public TMP_Text downloadingText;

	public GameObject DLCEntranceIcon;

	[Header("Portal")]
	public Transform signContainer;

	public Transform portalAndSign;

	public Transform portalContainer;

	public PortalQuad portalQuad;

	public RectTransform seizureWarning;

	public RectTransform newlyInstalledSign;

	public RectTransform DLCWarningSign;

	public Text[] DLCWarningText;

	public UnityEngine.UI.Image waveTag;

	public Text waveTagText;

	public ParticleSystem portalTransitionParticle;

	public Texture2D emptyTexture;

	public GameObject padlockContainer;

	public scrBadgeContainer badgeContainer;

	public Transform badgeContainerTransform;

	public GameObject badgePrefab;

	public scrClickableBadgeCLS badgeButton;

	[Header("Start Screen")]
	public scrPortal portalScript;

	public Transform portalSign;

	public Text workshopSignNameText;

	public Text classicCurrentWaveNumber;

	public Text techCurrentWaveNumber;

	[Header("Level Information")]
	public Text portalArtist;

	public Transform artistMediaContainer;

	public Text portalName;

	public GameObject localSourceIcon;

	public GameObject workshopSourceIcon;

	public TMP_Text portalDescription;

	public Text portalDifficultyText;

	public DifficultyIndicator portalDifficulty;

	public Text portalAuthor;

	public Text portalStats;

	public Text portalSourceText;

	public UnityEngine.Color portalSourceNormalColor = new UnityEngine.Color(0.7f, 0.7f, 0.7f, 1f);

	public UnityEngine.Color portalSourceHoverColor = new UnityEngine.Color(0.4f, 0.6f, 1f, 1f);

	public Text portalFutureVersion;

	public LevelRadar levelRadar;

	public CanvasGroup levelRadarCanvasGroup;

	[Header("Prefabs")]
	public GameObject tilePrefab;

	public GameObject mediaButton;

	public Sprite folderTagImage;

	public Sprite classicTagImage;

	public Sprite techTagImage;

	[Header("Tweakables")]
	public float levelCountForLoop;

	public float secondsForHold;

	public float secondsForHoldExtra;

	public float autoScrollInterval;

	public float portalTransitionTimeNormal;

	public float portalTransitionTimeInstant;

	public float portalImageLoadDelay;

	[Header("Initial Menu")]
	public GameObject initialMenu;

	public Texture2D workshopPortalTexture;

	public Canvas initialQuitLabel;

	[NonSerialized]
	public int gemTopY = 99;

	[NonSerialized]
	public int gemBottomY = -99;

	[NonSerialized]
	public int levelCount;

	[NonSerialized]
	public bool showingInitialMenu = true;

	[NonSerialized]
	public List<string> sortedLevelKeys;

	[NonSerialized]
	public HashSet<string> newlyInstalledLevelKeys = new HashSet<string>();

	[NonSerialized]
	public OptionsPanelsCLS.OptionName sortingMethod = OptionsPanelsCLS.OptionName.Wave;

	public Dictionary<string, GenericDataCLS> loadedLevels = new Dictionary<string, GenericDataCLS>();

	private Dictionary<string, GenericDataCLS> extraLevels = new Dictionary<string, GenericDataCLS>();

	private Dictionary<string, GenericDataCLS> techExtraLevels = new Dictionary<string, GenericDataCLS>();

	private Dictionary<string, string> loadedLevelDirs = new Dictionary<string, string>();

	private Dictionary<string, CustomLevelTile> loadedLevelTiles = new Dictionary<string, CustomLevelTile>();

	private Dictionary<string, bool> loadedLevelIsDeleted = new Dictionary<string, bool>();

	private Dictionary<string, bool> isWorkshopLevel = new Dictionary<string, bool>();

	private LayerMask floorLayerMask;

	private string levelToSelect;

	private float holdTimer;

	private float autoscrollTimer;

	private float levelTransitionTimer;

	private bool changingLevel;

	private bool disablePlanets;

	private scrCamera camera;

	private string newSongKey;

	private bool instantSelect;

	private TextureManager textureManager;

	[NonSerialized]
	public string searchParameter = "";

	private List<string> lastSongsLoaded;

	private List<string> lastTexturesLoaded;

	private CancellationTokenSource refreshTokenSource;

	private string localFeaturedPath;

	private string currentFolderName;

	private bool wasInPortalPreviousFrame;

	private Category category;

	private bool enteredFromSelection;

	private FeaturedLevelsSource featuredLevelsSource;

	[NonSerialized]
	public bool refreshing;

	[NonSerialized]
	public bool initializing = true;

	private bool steamworksAvailable;

	private string portalSourceValue;

	private UnityEngine.UI.Image localSourceIconImage;

	private UnityEngine.UI.Image workshopSourceIconImage;

	private bool overlayActive;

	private float currentSongVolume;

	private Tween seizureWarningAnimation;

	private Tween newLevelAnimation;

	private Tween DLCWarningAnimation;

	private Tween delayedTextureLoad;

	public TMP_Text badgeText => badgeButton.text;

	private bool canReturnToSelection
	{
		get
		{
			if (enteredFromSelection && steamworksAvailable)
			{
				return featuredLevelsSource != FeaturedLevelsSource.None;
			}
			return false;
		}
	}

	public bool featuredLevelsMode
	{
		get
		{
			Category category = this.category;
			return category == Category.Featured || category == Category.Tech;
		}
	}

	public bool levelDeleted
	{
		get
		{
			if (levelToSelect != null)
			{
				return loadedLevelIsDeleted.GetValueOrDefault(levelToSelect);
			}
			return false;
		}
	}

	public bool currentLevelIsWorkshop
	{
		get
		{
			if (levelToSelect != null)
			{
				return isWorkshopLevel.GetValueOrDefault(levelToSelect);
			}
			return false;
		}
	}

	public bool currentLevelCanBeDeleted
	{
		get
		{
			if (levelToSelect == null || levelDeleted)
			{
				return false;
			}
			if (!currentLevelIsWorkshop)
			{
				return true;
			}
			if (!steamworksAvailable)
			{
				return false;
			}
			if (loadedLevels.TryGetValue(levelToSelect, out var value) && value.isFolder)
			{
				return value.folder.containingLevels.Keys.Any(IsWorkshopItemInstalled);
			}
			return IsWorkshopItemInstalled(levelToSelect);
		}
	}

	public OptionsPanelsCLS.OptionName effectiveSortingMethod
	{
		get
		{
			if (sortingMethod != OptionsPanelsCLS.OptionName.Wave || featuredLevelsMode)
			{
				return sortingMethod;
			}
			return OptionsPanelsCLS.OptionName.Difficulty;
		}
	}

	private bool techFeaturedLevelsMode => category == Category.Tech;

	private bool localFeaturedLevels
	{
		get
		{
			if (featuredLevelsMode)
			{
				return !steamworksAvailable;
			}
			return false;
		}
	}

	public static string localWorldsPath { get; private set; }

	public static string tempLevelsFolder => Path.Combine(Persistence.DataPath, "Temp");

	public bool isImportPanelOpen => instance.importLevelPanel.gameObject.activeSelf;

	private static bool IsWorkshopItemInstalled(string workshopIdKey)
	{
		if (!ulong.TryParse(workshopIdKey, out var result))
		{
			return false;
		}
		Item item = new Item(result);
		if (item.IsSubscribed)
		{
			return item.IsInstalled;
		}
		return false;
	}

	private void Awake()
	{
		instance = this;
		initializing = true;
		scrPlayerManager.SetPlayerCount(1);
		if (SteamController.initialized)
		{
			steamworksAvailable = true;
		}
		sortingMethod = Persistence.clsSortingParameter;
		SetupPortalSourceText();
		category = entryCategory;
		enteredFromSelection = category == Category.Selection;
		entryCategory = Category.Selection;
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "A Dance of Fire and Ice");
		localWorldsPath = Path.Combine(path, "Worlds");
		localFeaturedPath = Path.Combine(path, "Featured");
		if (ADOBase.isSwitch)
		{
			featuredLevelsSource = FeaturedLevelsSource.Local;
		}
		else if (steamworksAvailable)
		{
			featuredLevelsSource = FeaturedLevelsSource.Workshop;
		}
		else if (RDDirectory.Exists(localFeaturedPath))
		{
			featuredLevelsSource = FeaturedLevelsSource.Local;
		}
		else
		{
			featuredLevelsSource = FeaturedLevelsSource.None;
		}
		floorLayerMask = LayerMask.GetMask("Floor");
		camera = scrCamera.instance;
		Text[] array = new Text[6]
		{
			portalStats,
			portalDifficultyText,
			portalScript.sign.worldName,
			portalAuthor,
			portalArtist,
			portalName
		};
		foreach (Text obj in array)
		{
			obj.SetLocalizedFont();
			obj.text = "";
		}
		TMP_Text[] array2 = new TMP_Text[1] { portalDescription };
		foreach (TMP_Text obj2 in array2)
		{
			obj2.SetLocalizedFont();
			obj2.text = "";
		}
		loadingText.SetLocalizedFont();
		loadingText.text = RDString.Get("status.loading");
		textureManager = new TextureManager();
		Dictionary<string, string> levelJsonCache = new Dictionary<string, string>();
		Dictionary<string, AsyncOperationHandle> levelJsonHandle = new Dictionary<string, AsyncOperationHandle>();
		if ((steamworksAvailable || base.featuredDLCManager.installed) && category != Category.Workshop)
		{
			foreach (uint key2 in FeaturedLevels.classic.Keys)
			{
				extraLevels.Add(key2.ToString(), DecodeFeaturedLevel(key2));
			}
			foreach (uint key3 in FeaturedLevels.tech.Keys)
			{
				techExtraLevels.Add(key3.ToString(), DecodeFeaturedLevel(key3));
			}
			FeaturedLevels.FeaturedFolder[] featuredFolders = FeaturedLevels.featuredFolders;
			foreach (FeaturedLevels.FeaturedFolder folder in featuredFolders)
			{
				ProcessFolder(folder);
			}
		}
		levelImporter.Initialize();
		SteamWorkshop.OnItemDownloaded += HandleWorkshopItemDownloaded;
		UpdateWorkshopSignText();
		string text = "[dlc]";
		string text2 = RDString.Get("cls.needsDLC");
		int num = text2.IndexOf(text);
		DLCWarningText[0].text = text2.Substring(0, num);
		DLCWarningText[0].SetLocalizedFont();
		DLCWarningText[1].text = text2.Substring(num + text.Length);
		DLCWarningText[1].SetLocalizedFont();
		foreach (AsyncOperationHandle value2 in levelJsonHandle.Values)
		{
			Addressables.Release(value2);
		}
		classicCurrentWaveNumber.text = RDString.Get("cls.waveNumber").Replace("[number]", 13.ToString());
		techCurrentWaveNumber.text = RDString.Get("cls.waveNumber").Replace("[number]", 4.ToString());
		LevelDataCLS DecodeFeaturedLevel(uint id)
		{
			string text3 = null;
			string text4 = null;
			if (featuredLevelsSource == FeaturedLevelsSource.Workshop)
			{
				string directory = new Item(id).Directory;
				if (!string.IsNullOrEmpty(directory))
				{
					string text5 = Path.Combine(directory, "main.adofai");
					if (RDFile.Exists(text5))
					{
						text3 = text5;
					}
				}
			}
			else if (featuredLevelsSource == FeaturedLevelsSource.DLC)
			{
				string key = Path.Combine("FeaturedLevels", id.ToString(), "main");
				if (levelJsonCache.ContainsKey(key))
				{
					text4 = string.Copy(levelJsonCache[key]);
				}
				else
				{
					AsyncOperationHandle<TextAsset> asyncOperationHandle = Addressables.LoadAssetAsync<TextAsset>(key);
					text4 = string.Copy(asyncOperationHandle.WaitForCompletion().text);
					levelJsonCache.Add(key, string.Copy(text4));
					levelJsonHandle.Add(key, asyncOperationHandle);
				}
			}
			else if (featuredLevelsSource == FeaturedLevelsSource.Local)
			{
				string text6 = Path.Combine(localFeaturedPath, id.ToString(), "main.adofai");
				if (RDFile.Exists(text6))
				{
					text3 = text6;
				}
			}
			string path2 = Path.Combine("FeaturedLevels", id.ToString(), "main");
			if (text4 == null)
			{
				text4 = ((text3 != null) ? RDFile.ReadAllText(text3) : Resources.Load<TextAsset>(path2).text);
			}
			Dictionary<string, object> rootDict = Json.DeserializePartially(text4, "actions") as Dictionary<string, object>;
			LevelDataCLS levelDataCLS = new LevelDataCLS();
			levelDataCLS.Decode(rootDict);
			string[] tags = levelDataCLS.tags;
			foreach (string text7 in tags)
			{
				if (!badgeContainer.badges.ContainsKey(text7))
				{
					Sprite sprite = Resources.Load<Sprite>("CLS/Badges/" + text7);
					if (!(sprite == null))
					{
						GameObject gameObject = UnityEngine.Object.Instantiate(badgePrefab, badgeContainerTransform);
						gameObject.GetComponent<UnityEngine.UI.Image>().sprite = sprite;
						if (text7 == "CommunityFeatured")
						{
							gameObject.GetComponent<Outline>().enabled = false;
						}
						if (badgeScales.TryGetValue(text7, out var value))
						{
							gameObject.transform.localScale = Vector3.one * value;
						}
						badgeContainer.badges.Add(text7, gameObject);
					}
				}
			}
			return levelDataCLS;
		}
		void ProcessFolder(FeaturedLevels.FeaturedFolder featuredFolder)
		{
			string text3 = "Folder:" + featuredFolder.folderId;
			string key = "workshop." + featuredFolder.folderId + "Folder.description";
			_ = featuredFolder.isTech;
			FolderDataCLS folderDataCLS = new FolderDataCLS(featuredFolder.title, featuredFolder.difficulty, featuredFolder.artist, "", RDString.Get(key), "portal.png", "icon.png", featuredFolder.iconColor.HexToColor());
			Dictionary<string, GenericDataCLS> dictionary = (featuredFolder.isTech ? techExtraLevels : extraLevels);
			uint[] levelIds = featuredFolder.levelIds;
			for (int j = 0; j < levelIds.Length; j++)
			{
				uint id = levelIds[j];
				LevelDataCLS value = DecodeFeaturedLevel(id);
				folderDataCLS.containingLevels.Add(id.ToString(), value);
				dictionary[id.ToString()].parentFolderName = text3;
			}
			dictionary.Add(text3, folderDataCLS);
		}
	}

	private void SetupPortalSourceText()
	{
		if (!ADOBase.isDesktop)
		{
			portalSourceText.gameObject.SetActive(value: false);
			return;
		}
		localSourceIconImage = localSourceIcon.GetComponent<UnityEngine.UI.Image>();
		AttachSourceTriggers(localSourceIcon);
		workshopSourceIconImage = workshopSourceIcon.GetComponent<UnityEngine.UI.Image>();
		AttachSourceTriggers(workshopSourceIcon);
		AttachSourceTriggers(portalSourceText.gameObject);
	}

	private void AttachSourceTriggers(GameObject obj)
	{
		EventTrigger obj2 = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
		obj2.triggers.Clear();
		AddSourceTrigger(obj2, EventTriggerType.PointerEnter, delegate
		{
			RefreshPortalSourceText(portalSourceHoverColor);
		});
		AddSourceTrigger(obj2, EventTriggerType.PointerExit, delegate
		{
			RefreshPortalSourceText(portalSourceNormalColor);
		});
		AddSourceTrigger(obj2, EventTriggerType.PointerClick, OpenSourceLocation);
	}

	private void RefreshPortalSourceText(UnityEngine.Color valueColor)
	{
		portalSourceText.text = "<color=#" + ColorUtility.ToHtmlStringRGBA(valueColor) + ">" + portalSourceValue + "</color>";
	}

	private static void AddSourceTrigger(EventTrigger trigger, EventTriggerType type, Action handler)
	{
		EventTrigger.Entry entry = new EventTrigger.Entry
		{
			eventID = type
		};
		entry.callback.AddListener(delegate
		{
			handler();
		});
		trigger.triggers.Add(entry);
	}

	private void OpenSourceLocation()
	{
		if (levelToSelect != null)
		{
			string value;
			if (currentLevelIsWorkshop)
			{
				Application.OpenURL("steam://url/CommunityFilePage/" + levelToSelect);
			}
			else if (loadedLevelDirs.TryGetValue(levelToSelect, out value) && !string.IsNullOrEmpty(value))
			{
				RDEditorUtils.RevealInExplorer(value, selectInExplorer: true);
			}
		}
	}

	private void Start()
	{
		RDInput.SetMapping("CLS");
		ulong[] featuredWorkshopLevels = ADOBase.gc.featuredWorkshopLevels;
		foreach (ulong num in featuredWorkshopLevels)
		{
			if (!Persistence.HasSubscribedToFeatured(num))
			{
				SteamWorkshop.Subscribe(num);
				Persistence.SetSubscribedToFeatured(num, subscribed: true);
			}
		}
		loadingText.rectTransform.DOScale(1.1f * Vector3.one, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
		ShowLevelSelection(show: false);
		ADOBase.controller.chosenPlanet.transform.LocalMoveXY(0f, 0f);
		portalFutureVersion.text = RDString.Get("cls.worldFutureVersion");
		string fileName = Path.GetFileName(Path.GetDirectoryName(GCS.customLevelPaths?.Last()));
		if (!fileName.IsNullOrEmpty())
		{
			Category category;
			if (lastPlayedCategory != Category.Selection)
			{
				category = lastPlayedCategory;
			}
			else
			{
				uint result;
				bool flag = uint.TryParse(fileName, out result);
				category = ((flag && FeaturedLevels.classic.ContainsKey(result)) ? Category.Featured : ((!flag || !FeaturedLevels.tech.ContainsKey(result)) ? Category.Workshop : Category.Tech));
			}
			EnterCategory(category);
		}
		else if ((!steamworksAvailable && this.category == Category.Selection) || featuredLevelsSource == FeaturedLevelsSource.None)
		{
			EnterCategory(Category.Workshop);
		}
		else if (this.category != Category.Selection)
		{
			EnterCategory(this.category);
		}
		initializing = false;
	}

	private void Update()
	{
		float num = (float)Screen.width * 1f / (float)Screen.height;
		float num2 = canvasScaler.referenceResolution.x / canvasScaler.referenceResolution.y;
		bool flag = num >= num2;
		canvasScaler.matchWidthOrHeight = (flag ? 1f : 0f);
		float num3 = Mathf.Max(1f, num2 / num);
		camera.camobj.orthographicSize = 5f * num3;
		signContainer.LocalMoveY(camera.camobj.orthographicSize - 1.4f);
		bool flag2 = Mathf.Approximately(ADOBase.controller.chosenPlanet.transform.localPosition.x, -1f);
		if (flag2)
		{
			_ = !wasInPortalPreviousFrame;
		}
		else
			_ = 0;
		wasInPortalPreviousFrame = flag2;
		badgeContainerTransform.eulerAngles = Vector3.forward * Mathf.Sin(Time.time * MathF.PI * 0.5f) * 7.5f;
		if (disablePlanets)
		{
			ADOBase.playerManager.SetAllPlayerResponsive(responsive: false);
		}
		if (steamworksAvailable && levelToSelect != null)
		{
			if (loadedLevels[levelToSelect].isLevel && ulong.TryParse(levelToSelect, out var result))
			{
				Item item = new Item(result);
				if (item.IsSubscribed && item.IsInstalled)
				{
					SetDownloadIconsVisibility(download: false, downloading: false);
					if (loadedLevelDirs.TryGetValue(levelToSelect, out var value) && value == null)
					{
						string directory = item.Directory;
						if (!string.IsNullOrEmpty(directory))
						{
							loadedLevelDirs[levelToSelect] = directory;
							if (flag2)
							{
								EnterLevel();
							}
						}
					}
				}
				else if (item.IsSubscribed && !item.IsInstalled)
				{
					SetDownloadIconsVisibility(download: false, downloading: true);
					string text = $"{(item.IsDownloading ? item.DownloadAmount : 0f) * 100f:0.00}%";
					downloadingText.text = RDString.Get("editor.dialog.downloading") + "\n" + text;
				}
				else
				{
					SetDownloadIconsVisibility(download: true, downloading: false);
				}
			}
			else
			{
				SetDownloadIconsVisibility(download: false, downloading: false);
			}
			downloadingIcon.transform.localEulerAngles = Vector3.back * Time.unscaledTime * 150f;
		}
		if (!ADOBase.controller.paused && !optionsPanels.searchMode && !showingInitialMenu)
		{
			if (optionsPanels.CheckInputs() || !ADOBase.controller.playerOne.responsive)
			{
				return;
			}
			if (!ADOBase.controller.moving)
			{
				if (RDInput.upPress || RDInput.downPress)
				{
					holdTimer += Time.deltaTime;
				}
				else
				{
					holdTimer = 0f;
					autoscrollTimer = 0f;
				}
				if (holdTimer > secondsForHold)
				{
					float num4 = ((holdTimer > secondsForHoldExtra) ? 2f : 1f);
					autoscrollTimer += Time.deltaTime * num4;
					if (autoscrollTimer > autoScrollInterval)
					{
						ShiftPlanet(!RDInput.upPress);
						autoscrollTimer = 0f;
					}
				}
				else if (RDInput.upPress || Input.mouseScrollDelta.y > 0.4f)
				{
					ShiftPlanet(down: false);
				}
				else if (RDInput.downPress || Input.mouseScrollDelta.y < -0.4f)
				{
					ShiftPlanet(down: true);
				}
			}
		}
		if (!changingLevel || disablePlanets)
		{
			return;
		}
		float num5 = (instantSelect ? portalTransitionTimeInstant : portalTransitionTimeNormal);
		if (levelTransitionTimer >= num5)
		{
			DisplayLevel(levelToSelect);
			if (loadedLevels[levelToSelect].isLevel)
			{
				LevelDataCLS level = loadedLevels[levelToSelect].level;
				if (!string.IsNullOrEmpty(level.songFilename))
				{
					string text2 = loadedLevelDirs[levelToSelect];
					if (text2 != null)
					{
						string text3 = Path.Combine(text2, level.songFilename);
						newSongKey = Path.GetFileName(text3) + "*external";
						if (!text3.ToLower().EndsWith(".mp3"))
						{
							StartCoroutine(LoadSong(text3, newSongKey));
						}
					}
				}
			}
			instantSelect = false;
		}
		else
		{
			levelTransitionTimer += Time.deltaTime;
		}
		void SetDownloadIconsVisibility(bool download, bool downloading)
		{
			SpriteRenderer spriteRenderer = downloadIcon;
			bool flag3 = (downloadText.enabled = download);
			spriteRenderer.enabled = flag3;
			SpriteRenderer spriteRenderer2 = downloadingIcon;
			flag3 = (downloadingText.enabled = downloading);
			spriteRenderer2.enabled = flag3;
		}
	}

	private void LateUpdate()
	{
		portalAndSign.MoveY(camera.transform.position.y);
		bool flag = SteamController.Instance.OverlayActive;
		if (flag != overlayActive)
		{
			overlayActive = flag;
			ADOBase.controller.pauseMenu.enabled = !overlayActive;
		}
	}

	private void OnDestroy()
	{
		refreshTokenSource?.Cancel();
		textureManager.Unload(onlyIfUnused: false);
		SteamWorkshop.OnItemDownloaded -= HandleWorkshopItemDownloaded;
	}

	private void HandleWorkshopItemDownloaded(PublishedFileId id, bool success)
	{
		if (success)
		{
			UpdateWorkshopSignText();
			if (!featuredLevelsMode && !refreshing)
			{
				Refresh();
			}
		}
	}

	private void UpdateWorkshopSignText()
	{
		int num = 0;
		if (!string.IsNullOrEmpty(localWorldsPath) && RDDirectory.Exists(localWorldsPath))
		{
			num += Directory.GetDirectories(localWorldsPath).Count((string dir) => RDFile.Exists(AdoPackageInstaller.FindLevelFile(dir).Value));
		}
		if (SteamController.initialized)
		{
			num += SteamWorkshop.itemIds.Length;
		}
		string text = RDString.Get("cls.library");
		object key;
		switch (num)
		{
		case 0:
			workshopSignNameText.lineSpacing = 0.6f;
			workshopSignNameText.text = text;
			return;
		default:
			key = "cls.items";
			break;
		case 1:
			key = "cls.itemsSingle";
			break;
		}
		string text2 = RDString.Get((string)key, new Dictionary<string, object> { { "count", num } });
		workshopSignNameText.lineSpacing = 0.4f;
		workshopSignNameText.text = text + "\n<size=2>" + text2 + "</size>";
	}

	private IEnumerator LoadSong(string path, string songKey)
	{
		AudioManager audioManager = AudioManager.Instance;
		if (songKey.EndsWith("*external"))
		{
			yield return audioManager.FindOrLoadAudioClipExternal(path, mp3Streaming: true);
		}
		else
		{
			yield return audioManager.LoadAddressableAudio(path);
		}
		if (audioManager.audioLib.TryGetValue(songKey, out var value) && value != null && loadedLevels[levelToSelect].isLevel)
		{
			LevelDataCLS level = loadedLevels[levelToSelect].level;
			if (songKey == newSongKey)
			{
				previewSongPlayer.Play(value, level.previewSongStart, level.previewSongDuration, currentSongVolume);
			}
			lastSongsLoaded.Add(songKey);
			while (lastSongsLoaded.Count > 5)
			{
				string key = lastSongsLoaded[0];
				lastSongsLoaded.RemoveAt(0);
				if (audioManager.audioLibHandles.TryGetValue(key, out var value2))
				{
					Addressables.Release(value2);
					audioManager.audioLibHandles.Remove(key);
					audioManager.audioLib.Remove(key);
				}
				else if (audioManager.audioLib.ContainsKey(key))
				{
					AudioClip audioClip = audioManager.audioLib[key];
					audioManager.audioLib.Remove(key);
					audioClip.UnloadAudioData();
					UnityEngine.Object.Destroy(audioClip);
				}
			}
		}
		else
		{
			Debug.LogWarning("Sound preview not loaded for level: " + songKey);
		}
	}

	public void DeleteLevel()
	{
		if (ADOBase.isSwitch || levelToSelect == null || loadedLevelIsDeleted.GetValueOrDefault(levelToSelect) || !currentLevelCanBeDeleted)
		{
			return;
		}
		scrFlash.Flash(UnityEngine.Color.white, 0.5f);
		changingLevel = true;
		levelTransitionTimer = 0f;
		loadedLevelTiles[levelToSelect].SetDeleted();
		if (isWorkshopLevel.GetValueOrDefault(levelToSelect))
		{
			IEnumerable<string> enumerable2;
			if (!loadedLevels.TryGetValue(levelToSelect, out var value) || !value.isFolder)
			{
				IEnumerable<string> enumerable = new string[1] { levelToSelect };
				enumerable2 = enumerable;
			}
			else
			{
				IEnumerable<string> enumerable = value.folder.containingLevels.Keys;
				enumerable2 = enumerable;
			}
			foreach (string item in enumerable2)
			{
				if (!ulong.TryParse(item, out var result))
				{
					continue;
				}
				foreach (Item item2 in SteamWorkshop.items)
				{
					if ((ulong)item2.Id == result)
					{
						SteamWorkshop.Unsubscribe(item2.Id);
						Debug.Log($"Unsubscribed from ${item2.Id}");
					}
				}
			}
		}
		else
		{
			string text = loadedLevelDirs[levelToSelect];
			if (text != null)
			{
				Directory.Delete(text, recursive: true);
			}
		}
		loadedLevelIsDeleted[levelToSelect] = true;
		DisplayLevel();
		if (!loadedLevels.Keys.Any((string key) => !loadedLevelIsDeleted[key]))
		{
			Refresh();
		}
	}

	public void OpenImportPanel()
	{
		importLevelPanel.OnOpenImportPanel();
	}

	public void CloseImportPanel()
	{
		importLevelPanel.OnCloseImportPanel();
	}

	public async Task Refresh(bool setup = false)
	{
		if (showingInitialMenu)
		{
			return;
		}
		refreshing = true;
		_ = Time.realtimeSinceStartup;
		refreshTokenSource?.Cancel();
		refreshTokenSource = new CancellationTokenSource();
		CancellationToken cancelToken = refreshTokenSource.Token;
		ShowLevelSelection(show: false);
		DisablePlanets(disable: true);
		levelToSelect = null;
		loadingText.gameObject.SetActive(value: true);
		lastSongsLoaded = new List<string>();
		lastTexturesLoaded = new List<string>();
		if (SteamController.initialized)
		{
			await Task.Yield();
		}
		if (!setup && levelCount > 0)
		{
			foreach (CustomLevelTile value in loadedLevelTiles.Values)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
			loadedLevels = new Dictionary<string, GenericDataCLS>();
			loadedLevelDirs = new Dictionary<string, string>();
			loadedLevelTiles = new Dictionary<string, CustomLevelTile>();
			loadedLevelIsDeleted = new Dictionary<string, bool>();
			levelCount = 0;
		}
		_ = Time.realtimeSinceStartup;
		try
		{
			await ScanLevels(cancelToken);
			if (!steamworksAvailable && levelCount == 0)
			{
				await Task.Delay(1500, cancelToken);
			}
			loadingText.gameObject.SetActive(value: false);
		}
		catch (Exception)
		{
			refreshing = false;
			return;
		}
		if (levelCount > 0)
		{
			ShowLevelSelection(show: true);
			DisablePlanets(disable: false);
			CreateFloors();
			if (importLevelPanel.gameObject.activeSelf && importLevelPanel.noLevelsPanel.gameObject.activeSelf)
			{
				importLevelPanel.HideImportPanel();
			}
		}
		else
		{
			importLevelPanel.OnOpenImportPanel();
		}
		optionsPanels.searchMode = false;
		optionsPanels.searchInputField.text = string.Empty;
		currentSearchText.text = RDString.Get("cls.shortcut.find");
		currentSearchText.SetLocalizedFont();
		optionsPanels.UpdateOrderText();
		UpdateWorkshopSignText();
		_ = Time.realtimeSinceStartup;
		refreshing = false;
	}

	private void ShowLevelSelection(bool show)
	{
		StopCurrentLevelSong();
		levelInfoCanvas.gameObject.SetActive(show);
		optionsPanels.gameObject.SetActive(show);
		entranceTile.gameObject.SetActive(show);
		portalAndSign.gameObject.SetActive(show);
		gemTop.gameObject.SetActive(value: false);
		gemBottom.gameObject.SetActive(value: false);
		chainTop.SetActive(value: false);
		chainBottom.SetActive(value: false);
	}

	private void DisablePlanets(bool disable)
	{
		disablePlanets = disable;
		ADOBase.playerManager.SetAllPlayerResponsive(!disable);
		if (disable)
		{
			ADOBase.controller.chosenPlanet.transform.MoveX(-99f);
			ADOBase.controller.chosenPlanet.other.transform.MoveX(-99f);
		}
	}

	private void ShiftPlanet(bool down)
	{
		int num = ((!down) ? 1 : (-1));
		Vector3 position = ADOBase.controller.chosenPlanet.transform.position;
		position = new Vector3(position.x, position.y + (float)num, position.z);
		MoveToFloorAtPosition(position);
		instantSelect = true;
	}

	private void MoveToFloorAtPosition(Vector3 position, bool ignoreFfx = false)
	{
		RaycastHit2D[] array = Physics2D.RaycastAll(new Vector2(position.x, position.y), new Vector2(0f, 0f), 0f, floorLayerMask);
		if (array.Length == 0 || array == null)
		{
			return;
		}
		Transform transform = array[0].collider.transform;
		Transform transform2 = ADOBase.controller.chosenPlanet.transform;
		transform2.position = transform.position;
		ADOBase.controller.chosenPlanet.currfloor = transform.GetComponent<scrFloor>();
		if (!ignoreFfx)
		{
			ffxPlusBase[] components = transform.GetComponents<ffxPlusBase>();
			foreach (ffxPlusBase item in components)
			{
				ADOBase.controller.chosenPlanet.DoFFX(item);
			}
		}
		Vector2 destination = new Vector2(camera.transform.position.x, transform2.transform.position.y);
		camera.Refocus(destination);
	}

	public void SelectLevel(CustomLevelTile tileToSelect, bool snap)
	{
		Transform component = tileToSelect.GetComponent<Transform>();
		if (snap)
		{
			camera.frompos.y = component.position.y;
			tileToSelect.Highlight(highlight: true, snap);
			MoveToFloorAtPosition(tileToSelect.transform.position);
			camera.timer += float.MaxValue;
		}
		foreach (CustomLevelTile value in loadedLevelTiles.Values)
		{
			if (value != tileToSelect)
			{
				value.Highlight(highlight: false, snap);
			}
		}
		int targetIndex = -1;
		for (int i = 0; i < sortedLevelKeys.Count; i++)
		{
			if (loadedLevelTiles[sortedLevelKeys[i]] == tileToSelect)
			{
				targetIndex = i;
				break;
			}
		}
		LoadTileIconsNearby(targetIndex);
		if (!changingLevel)
		{
			DisplayLevel();
		}
		levelToSelect = tileToSelect.levelKey;
		StopCurrentLevelSong();
		changingLevel = true;
		levelTransitionTimer = 0f;
	}

	private void StopCurrentLevelSong()
	{
		if (previewSongPlayer.playing)
		{
			previewSongPlayer.Stop();
		}
		AudioClipData audioClipData = ADOBase.audioManager.audioClipData;
		if (audioClipData != null && !audioClipData.loaded)
		{
			audioClipData.StopLoading();
			ADOBase.audioManager.audioClipData = null;
		}
	}

	public void LoadTexture(Texture2D texture, string levelKey)
	{
		textureManager.customTextures[levelKey] = new TextureManager.CustomTexture(texture, DateTime.Now, isInternal: false, isFromBundle: false);
		LoadTexture(string.Empty, levelKey);
	}

	public void LoadTexture(string path, string levelKey)
	{
		if (!RDFile.Exists(path))
		{
			return;
		}
		if (!textureManager.customTextures.ContainsKey(levelKey))
		{
			textureManager.AddTexture(levelKey, out var _, path, 512);
			lastTexturesLoaded.Add(levelKey);
			while (lastTexturesLoaded.Count > 5)
			{
				string key = lastTexturesLoaded[0];
				textureManager.UnloadTexture(key);
				lastTexturesLoaded.RemoveAt(0);
			}
		}
		if (levelKey == levelToSelect)
		{
			Texture2D texture = textureManager.customTextures[levelKey].texture;
			portalQuad.SetTexture(texture);
		}
		portalTransitionParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
	}

	public void ReloadDisplay()
	{
		DisplayLevel(levelToSelect);
	}

	public void DisplayLevel(string levelKey = null)
	{
		changingLevel = false;
		bool flag = levelKey != null;
		bool flag2 = false;
		if (flag && loadedLevelIsDeleted[levelKey])
		{
			return;
		}
		int num;
		int num2;
		if (flag)
		{
			num = ((!loadedLevels[levelKey].isFolder) ? 1 : 0);
			if (num != 0)
			{
				num2 = (isWorkshopLevel.GetValueOrDefault(levelKey) ? 1 : 0);
				goto IL_0074;
			}
		}
		else
		{
			num = 0;
		}
		num2 = 0;
		goto IL_0074;
		IL_0074:
		bool flag3 = (byte)num2 != 0;
		bool flag4 = num != 0 && !featuredLevelsMode;
		if (flag)
		{
			localSourceIcon.SetActive(flag4 && !flag3);
			workshopSourceIcon.SetActive(flag4 && flag3);
		}
		if (portalSourceText != null && ADOBase.isDesktop)
		{
			if (flag4)
			{
				portalSourceValue = RDString.Get(flag3 ? "cls.workshop" : "cls.localLevel");
				portalSourceText.SetLocalizedFont();
				RefreshPortalSourceText(portalSourceNormalColor);
			}
			else if (flag)
			{
				portalSourceText.text = string.Empty;
			}
		}
		float animDur = 0.5f;
		bool isLandable = flag;
		bool purePerfected;
		if (flag)
		{
			bool flag5 = false;
			bool flag6 = false;
			foreach (Transform item in artistMediaContainer.transform)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			GenericDataCLS genericDataCLS = loadedLevels[levelKey];
			if (genericDataCLS.isFolder)
			{
				waveTag.sprite = folderTagImage;
				waveTagText.text = RDString.Get("cls.folder");
				waveTag.gameObject.SetActive(value: true);
				LayoutRebuilder.MarkLayoutForRebuild(waveTag.transform.parent.GetComponent<RectTransform>());
				FolderDataCLS folder = genericDataCLS.folder;
				flag5 = true;
				foreach (LevelDataCLS value3 in folder.containingLevels.Values)
				{
					if (!value3.seizureWarning)
					{
						flag5 = false;
					}
				}
			}
			else
			{
				LevelDataCLS level = genericDataCLS.level;
				string hash = level.Hash;
				float customWorldCompletion = Persistence.GetCustomWorldCompletion(hash);
				bool flag7 = customWorldCompletion >= 1f;
				Persistence.GetCustomWorldPlayIndex(hash);
				bool customWorldHasBeenEntered = Persistence.GetCustomWorldHasBeenEntered(hash);
				float customWorldAccuracy = Persistence.GetCustomWorldAccuracy(hash);
				float customWorldXAccuracy = Persistence.GetCustomWorldXAccuracy(hash);
				bool flag8 = Persistence.showXAccuracy && customWorldXAccuracy != 0f;
				float num3 = (flag8 ? customWorldXAccuracy : customWorldAccuracy);
				int customWorldAttempts = Persistence.GetCustomWorldAttempts(hash);
				float customWorldSpeedTrial = Persistence.GetCustomWorldSpeedTrial(hash);
				int customWorldMinDeaths = Persistence.GetCustomWorldMinDeaths(hash);
				purePerfected = (flag8 ? (customWorldXAccuracy == 1f) : Persistence.GetCustomWorldIsHighestPossibleAcc(hash));
				string text = RDString.Get("levelSelect.multiplier", new Dictionary<string, object> { 
				{
					"multiplier",
					customWorldSpeedTrial.ToString("0.0")
				} });
				Dictionary<string, object> parameters = new Dictionary<string, object>
				{
					{
						"pctCompleted",
						"<color=white>" + Mathf.FloorToInt(customWorldCompletion * 100f).ToString("0") + "%</color>"
					},
					{
						"pctAccuracy",
						"<color=white>" + GoldAccuracy((num3 * 100f).ToString("0.00") + "%") + "</color>"
					},
					{
						"speedTrial",
						"<color=white>" + text + "</color>"
					},
					{
						"attempts",
						"<color=white>" + customWorldAttempts.ToString("0") + "</color>"
					}
				};
				bool flag9 = customWorldSpeedTrial > 1f;
				string text2;
				if (customWorldHasBeenEntered)
				{
					text2 = ((!flag7) ? RDString.Get("cls.worldStatsIncomplete", parameters) : RDString.Get(flag8 ? "cls.worldStatsCompleteXAccuracyWithoutSpeedTrial" : "cls.worldStatsCompleteWithoutSpeedTrial", parameters));
				}
				else
				{
					text2 = RDString.Get("levelSelect.neverPlayed", parameters);
					if (newlyInstalledLevelKeys.Contains(levelKey))
					{
						text2 = "<color=#368BE6>" + text2 + "</color>";
					}
				}
				float num4 = (float)Math.Round(level.speedTrialAim, 1);
				bool flag10 = num4 > 0f;
				if (flag10)
				{
					flag9 = num4 <= customWorldSpeedTrial;
				}
				if (optionsPanels.speedTrial && (flag10 || customWorldHasBeenEntered))
				{
					bool flag11 = flag10 && flag9;
					string key = (flag10 ? "cls.speedTrialBestWithAim" : "cls.speedTrialBest");
					string value = RDString.Get("levelSelect.multiplier", new Dictionary<string, object> { 
					{
						"multiplier",
						num4.ToString("0.0")
					} });
					text2 = text2 + "\n" + RDString.Get(key, new Dictionary<string, object>
					{
						{
							"speedTrial",
							(flag11 ? "<color=#FFDA00>" : "<color=white>") + text + "</color><color=white>"
						},
						{ "aimMultiplier", value }
					}) + "</color>";
				}
				if (customWorldMinDeaths >= 0)
				{
					string text3 = ((customWorldMinDeaths == 0) ? RDString.Get("cls.noCheckpointsUsed") : customWorldMinDeaths.ToString());
					text2 = text2 + "\n" + RDString.Get("cls.lowestCheckpointsUsed", new Dictionary<string, object> { 
					{
						"checkpoints",
						"<color=white>" + text3 + "</color>"
					} });
				}
				portalStats.text = text2;
				PortalSign sign = portalScript.sign;
				string text4 = loadedLevelDirs[levelKey];
				char directorySeparatorChar = Path.DirectorySeparatorChar;
				int num5 = scnGame.GetWorldPaths(text4 + directorySeparatorChar + "main.adofai").Length;
				if ((bool)sign.lanterns)
				{
					sign.lanterns.UpdateStates(flag7, customWorldAccuracy >= 1f, flag9);
				}
				sign.worldName.text = ((num5 > 1) ? (RDString.Get("cls.worldCount", new Dictionary<string, object> { { "levelCount", num5 } }).Replace("\n", $"\n<size={Mathf.RoundToInt((float)(sign.worldName.fontSize * 2) / 3f)}>") + "</size>") : RDString.Get("cls.singleLevel"));
				if (uint.TryParse(levelKey, out var result) && FeaturedLevels.all.TryGetValue(result, out var value2))
				{
					waveTag.gameObject.SetActive(value: true);
					waveTag.sprite = ((value2.type == FeaturedLevels.LevelType.Classic) ? classicTagImage : techTagImage);
					int waveNumber = value2.waveNumber;
					waveTagText.text = RDString.Get("cls.waveNumber").Replace("[number]", waveNumber.ToString());
					LayoutRebuilder.MarkLayoutForRebuild(waveTag.transform.parent.GetComponent<RectTransform>());
				}
				else
				{
					waveTag.gameObject.SetActive(value: false);
				}
				currentSongVolume = (float)level.volume / 100f;
				if (!ADOBase.isSwitch)
				{
					string artistLinks = level.artistLinks;
					if (artistLinks != "")
					{
						string[] array = artistLinks.Replace(" ", "").Split(',');
						for (int i = 0; i < array.Length; i++)
						{
							string link = array[i];
							string[] array2 = link.Replace("https://", "").Replace("http://", "").Replace("www.", "")
								.Split('.');
							if (array2.Length < 2)
							{
								continue;
							}
							GameObject gameObject = UnityEngine.Object.Instantiate(mediaButton, artistMediaContainer);
							string text5 = "link_white";
							switch (array2[0])
							{
							case "youtube":
							case "youtu":
								text5 = "youtube";
								break;
							case "spotify":
							case "bandcamp":
							case "twitter":
							case "soundcloud":
								text5 = array2[0];
								break;
							}
							string text6 = array2[1];
							if (text6 == "bandcamp" || text6 == "spotify")
							{
								text5 = array2[1];
							}
							gameObject.GetComponent<UnityEngine.UI.Image>().sprite = Resources.Load<Sprite>("CLS/MediaIcons/" + text5);
							gameObject.GetComponent<Button>().onClick.AddListener(delegate
							{
								if (!link.StartsWith("http://") && !link.StartsWith("https://") && !link.StartsWith("www."))
								{
									link = "https://" + link;
								}
								ADOBase.platformHelper.OpenURL(link);
								EventSystem.current.SetSelectedGameObject(null);
							});
						}
					}
				}
				if (level.loadResult == LoadResult.FutureVersion)
				{
					portalFutureVersion.gameObject.SetActive(value: true);
					isLandable = false;
					entranceTile.floorRenderer.material.DOColor(UnityEngine.Color.gray, animDur);
				}
				else if (portalFutureVersion.gameObject.activeSelf)
				{
					portalFutureVersion.gameObject.SetActive(value: false);
					entranceTile.floorRenderer.material.DOColor(UnityEngine.Color.white, animDur);
				}
				flag5 = level.seizureWarning;
				flag6 = level.requiredDLCs.Length != 0;
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate(portalArtist.GetComponent<RectTransform>());
			uint result2;
			bool num6 = uint.TryParse(levelKey, out result2);
			bool flag12 = FeaturedLevels.tech.ContainsKey(result2);
			bool exists;
			string withCheck = RDString.GetWithCheck("workshop." + levelKey + ".description", out exists);
			if (!exists)
			{
				withCheck = RDString.GetWithCheck(genericDataCLS.description, out exists);
			}
			string s = ((exists && !withCheck.IsNullOrEmpty()) ? withCheck : ((genericDataCLS.description == string.Empty) ? string.Empty : ("\"" + RDUtils.RemoveRichTags(genericDataCLS.description) + "\"")));
			if (num6 && flag12)
			{
				flag2 = true;
			}
			portalDescription.text = RDString.AddSpacesToChineseString(s);
			portalDescription.SetLocalizedFont();
			string text7 = RDUtils.RemoveRichTags(genericDataCLS.artist);
			portalArtist.text = text7;
			portalArtist.SetLocalizedFont();
			string text8 = RDUtils.RemoveRichTags(genericDataCLS.title);
			portalName.text = text8;
			portalName.SetLocalizedFont();
			string text9 = RDString.Get("cls.difficulty");
			portalDifficultyText.text = text9;
			int difficulty = genericDataCLS.difficulty;
			portalDifficulty.SetStars(difficulty);
			string text10 = RDString.Get("cls.worldAuthor", new Dictionary<string, object> { 
			{
				"author",
				"<color=white>" + RDUtils.RemoveRichTags(genericDataCLS.author) + "</color>"
			} });
			portalAuthor.text = text10;
			portalAuthor.SetLocalizedFont();
			if (techFeaturedLevelsMode && flag12)
			{
				Vector4 normalizedTraits = FeaturedLevels.tech[result2].normalizedTraits;
				levelRadar.SetLevelData(normalizedTraits, genericDataCLS.difficulty);
			}
			else
			{
				flag2 = false;
			}
			if (delayedTextureLoad != null && delayedTextureLoad.IsActive())
			{
				delayedTextureLoad.Kill();
			}
			Texture2D featuredLevelPortalTexture = null;
			if (levelKey != null)
			{
				featuredLevelPortalTexture = Resources.Load<Texture2D>(Path.Combine("FeaturedLevels", levelKey, "portal"));
			}
			if (genericDataCLS.isFolder)
			{
				portalScript.sign.worldName.text = portalName.text;
				string path = levelKey.Replace("Folder:", "");
				Texture2D texture = Resources.Load<Texture2D>(Path.Combine("FeaturedLevels", path, "portal"));
				delayedTextureLoad = DOVirtual.DelayedCall(portalImageLoadDelay, delegate
				{
					portalQuad.SetTexture(texture);
					portalTransitionParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
				});
			}
			else if ((bool)featuredLevelPortalTexture)
			{
				delayedTextureLoad = DOVirtual.DelayedCall(portalImageLoadDelay, delegate
				{
					portalQuad.SetTexture(featuredLevelPortalTexture);
					portalTransitionParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
				});
			}
			else if (genericDataCLS.previewImage != "")
			{
				string text11 = loadedLevelDirs[levelKey];
				if (text11 != null)
				{
					string portalImagePath = Path.Combine(text11, genericDataCLS.previewImage);
					delayedTextureLoad = DOVirtual.DelayedCall(portalImageLoadDelay, delegate
					{
						LoadTexture(portalImagePath, levelKey);
					});
				}
			}
			else
			{
				delayedTextureLoad = DOVirtual.DelayedCall(portalImageLoadDelay, delegate
				{
					portalQuad.SetTexture(emptyTexture);
				});
			}
			portalContainer.DOScale(new Vector3(1f, 1f, 0f), animDur).SetEase(Ease.OutBack);
			portalQuad.Fade(1f, animDur);
			if (flag6 && !ADOBase.isSwitch)
			{
				DoWarningAnimation(DLCWarningSign, DLCWarningAnimation, left: false);
			}
			else if (flag5)
			{
				DoWarningAnimation(seizureWarning, seizureWarningAnimation, left: false);
			}
			if (!featuredLevelsMode && newlyInstalledLevelKeys.Contains(levelKey))
			{
				DoWarningAnimation(newlyInstalledSign, newLevelAnimation, left: true);
			}
			if (!base.neoCosmosManager.installed)
			{
				DLCEntranceIcon.SetActive(flag6);
				padlockContainer.SetActive(flag6);
			}
			portalStats.enabled = genericDataCLS.isLevel;
			portalAuthor.enabled = !genericDataCLS.author.IsNullOrEmpty();
			if (featuredLevelsMode)
			{
				foreach (Transform item2 in badgeContainerTransform)
				{
					item2.gameObject.SetActive(value: false);
				}
				badgeContainer.ResetBadges();
				string[] tags = genericDataCLS.tags;
				foreach (string text12 in tags)
				{
					if (badgeContainer.badges.ContainsKey(text12))
					{
						badgeContainer.activeBadges.Add(text12);
					}
				}
				badgeContainerTransform.DOKill();
				badgeContainerTransform.localScale = Vector3.zero;
				badgeContainerTransform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
			}
		}
		else
		{
			portalTransitionParticle.Play(withChildren: true);
			portalContainer.DOScale(new Vector3(0.85f, 0.85f, 0f), animDur).SetEase(Ease.InOutSine);
			portalQuad.Fade(0f, animDur);
			DLCEntranceIcon.SetActive(value: false);
			padlockContainer.SetActive(value: false);
			DoWarningAnimation2(seizureWarning, seizureWarningAnimation);
			DoWarningAnimation2(newlyInstalledSign, newLevelAnimation);
			DoWarningAnimation2(DLCWarningSign, DLCWarningAnimation);
			badgeContainerTransform.DOKill();
			badgeContainerTransform.DOScale(0f, animDur * 0.5f).SetEase(Ease.InBack);
		}
		float endValue = (flag ? 1f : 0f);
		levelInfoCanvasGroup.DOFade(endValue, animDur);
		levelRadarCanvasGroup.DOFade(flag2 ? 1f : 0f, animDur);
		portalSign.DOLocalMoveY(flag ? 0f : 11f, animDur).SetEase((!flag) ? Ease.Linear : Ease.OutSine);
		entranceTile.isLandable = isLandable;
		static void DoWarningAnimation(RectTransform rt, Tween tween, bool left)
		{
			tween?.Kill();
			int num8 = ((!left) ? 1 : (-1));
			tween = DOTween.Sequence().Append(rt.DOScale(Vector3.zero, 0f)).Join(rt.DORotate(new Vector3(0f, 0f, 30 * num8), 0f))
				.Join(rt.DOScale(new Vector3(1f, 1f, 1f), 1f / 3f).SetEase(Ease.OutBack))
				.Join(rt.DORotate(new Vector3(0f, 0f, -30 * num8), 0.5f).SetEase(Ease.OutBack));
		}
		void DoWarningAnimation2(RectTransform rt, Tween tween)
		{
			tween?.Pause();
			rt.DOKill();
			tween = rt.DOScale(Vector3.zero, animDur * 0.5f).SetEase(Ease.InBack);
		}
		string GoldAccuracy(string accText)
		{
			if (!purePerfected)
			{
				return accText;
			}
			return "<color=#FFDA00>" + accText + "</color>";
		}
	}

	public void EnterLevel()
	{
		lastPlayedCategory = category;
		GCS.customLevelIndex = 0;
		GCS.speedTrialMode = optionsPanels.speedTrial;
		GCS.practiceMode = false;
		ADOBase.audioManager.StopLoadingMP3File();
		string text = loadedLevelDirs[levelToSelect];
		GenericDataCLS genericDataCLS = loadedLevels[levelToSelect];
		if (genericDataCLS.isFolder)
		{
			EnterFolder();
			return;
		}
		List<DLCManager> list = genericDataCLS.level.requiredDLCs.Where((DLCManager x) => !x.installed).ToList();
		if (text == null && steamworksAvailable)
		{
			SteamWorkshop.Subscribe(ulong.Parse(levelToSelect));
			return;
		}
		if (genericDataCLS.isLevel && list.Count > 0 && !ADOBase.isSwitch)
		{
			DLCManager dLCManager = list[0];
			SteamFriends.OpenWebOverlay("https://store.steampowered.com/app/" + dLCManager.steamAppId);
			return;
		}
		LevelDataCLS levelDataCLS = (LevelDataCLS)genericDataCLS;
		GCS.customLevelIndex = 0;
		GCS.speedTrialMode = optionsPanels.speedTrial;
		string text2 = ((text != null) ? AdoPackageInstaller.FindLevelFile(text).Value : ("FeaturedLevels/" + levelToSelect + "/main"));
		ADOBase.audioManager.StopLoadingMP3File();
		string hash = loadedLevels[levelToSelect].Hash;
		bool flag = Persistence.GetCustomWorldPlayIndex(hash) == -1;
		Persistence.IncrementCLSTotalPlays();
		Persistence.SetCustomWorldPlayIndex(hash, Persistence.GetCLSTotalPlays());
		float num = (float)Math.Round(levelDataCLS.speedTrialAim, 1);
		float num2 = ((num > 0f && num <= 1f) ? num : 1.1f);
		GCS.nextSpeedRun = (GCS.speedTrialMode ? num2 : 1f);
		bool skipToMain = Persistence.GetCustomWorldAttempts(hash) > 0;
		if (flag && levelDataCLS.tags.Contains("Sightreading"))
		{
			GCS.useNoFail = true;
		}
		if (GCS.speedTrialMode)
		{
			ADOBase.controller.LoadCustomLevel(text2, levelToSelect, text == null);
		}
		else
		{
			ADOBase.controller.LoadCustomWorld(text2, skipToMain, levelToSelect, text == null);
		}
	}

	public void EnterFolder()
	{
		DOVirtual.DelayedCall(0f, delegate
		{
			currentFolderName = levelToSelect;
			SortLevelKeys();
			SearchLevels(searchParameter);
		});
	}

	public void ExitFolder()
	{
		DOVirtual.DelayedCall(0f, delegate
		{
			string key = currentFolderName;
			currentFolderName = null;
			SortLevelKeys();
			SearchLevels(searchParameter, alsoSelect: false);
			SelectLevel(loadedLevelTiles[key], snap: true);
		});
	}

	private async Task ScanLevels(CancellationToken cancelToken)
	{
		if (category == Category.Workshop && !RDDirectory.Exists(localWorldsPath))
		{
			Debug.LogWarning("First time launching CLS, making directory");
			RDDirectory.CreateDirectory(localWorldsPath);
			return;
		}
		levelRadar.gameObject.SetActive(techFeaturedLevelsMode);
		if (!featuredLevelsMode)
		{
			List<string> list = new List<string>();
			Dictionary<string, string[]> workshopTags = new Dictionary<string, string[]>();
			if (steamworksAvailable)
			{
				foreach (Item item in SteamWorkshop.items)
				{
					string directory = item.Directory;
					if (directory != null)
					{
						list.Add(directory);
						workshopTags[directory] = item.Tags ?? Array.Empty<string>();
						isWorkshopLevel[Path.GetFileName(directory)] = true;
					}
				}
			}
			_ = new string[0];
			string[] directories = Directory.GetDirectories(localWorldsPath);
			string[] itemDirs = directories.Concat(list).ToArray();
			cancelToken.ThrowIfCancellationRequested();
			List<Task<Dictionary<string, object>>> list2 = new List<Task<Dictionary<string, object>>>();
			string[] array = itemDirs;
			foreach (string text in array)
			{
				string fileName = Path.GetFileName(text);
				loadedLevelIsDeleted.TryGetValue(fileName, out var value);
				if (value)
				{
					continue;
				}
				PackageInstallerResult<string> packageInstallerResult = AdoPackageInstaller.FindLevelFile(text);
				if (RDFile.Exists(packageInstallerResult.Value))
				{
					string levelFilePath = packageInstallerResult.Value;
					string capturedLevelDir = text;
					list2.Add(Task.Run(delegate
					{
						try
						{
							return Json.DeserializePartially(RDFile.ReadAllText(levelFilePath), "actions") as Dictionary<string, object>;
						}
						catch (Exception ex2)
						{
							Debug.LogError("Failed to read/parse level at " + capturedLevelDir + ": " + ex2.Message);
							return (Dictionary<string, object>)null;
						}
					}, cancelToken));
				}
				else
				{
					Debug.LogWarning("No level file at " + text + "!");
					list2.Add(Task.FromResult<Dictionary<string, object>>(null));
				}
			}
			cancelToken.ThrowIfCancellationRequested();
			Dictionary<string, object>[] array2 = await Task.WhenAll(list2);
			cancelToken.ThrowIfCancellationRequested();
			for (int num = 0; num < itemDirs.Length; num++)
			{
				string text2 = itemDirs[num];
				string fileName2 = Path.GetFileName(text2);
				Dictionary<string, object> dictionary = array2[num];
				if (dictionary == null)
				{
					continue;
				}
				try
				{
					LevelDataCLS levelDataCLS = new LevelDataCLS();
					if (workshopTags.TryGetValue(text2, out var value2))
					{
						levelDataCLS.workshopTags = value2;
					}
					if (levelDataCLS.Decode(dictionary) && !levelDataCLS.IsMissingRequiredMetadata() && !loadedLevels.ContainsKey(fileName2))
					{
						loadedLevels.Add(fileName2, levelDataCLS);
						loadedLevelDirs.Add(fileName2, text2);
						loadedLevelIsDeleted[fileName2] = false;
					}
				}
				catch (Exception ex)
				{
					Debug.LogError("Failed to load level '" + fileName2 + "' at " + text2 + ": " + ex.Message + "\n" + ex.StackTrace);
				}
			}
		}
		else
		{
			foreach (KeyValuePair<string, GenericDataCLS> item2 in techFeaturedLevelsMode ? techExtraLevels : extraLevels)
			{
				string key = item2.Key;
				if (!loadedLevels.ContainsKey(key))
				{
					loadedLevels.Add(key, item2.Value);
					string value3 = ((featuredLevelsSource == FeaturedLevelsSource.Local) ? Path.Combine(localFeaturedPath, key) : null);
					loadedLevelDirs.Add(key, value3);
					loadedLevelIsDeleted[key] = false;
					isWorkshopLevel[key] = true;
				}
			}
		}
		levelCount = loadedLevels.Count;
	}

	private void CreateFloors()
	{
		if (loadedLevels.Count == 0)
		{
			return;
		}
		bool flag = false;
		string path = Path.Combine(Persistence.DataPath, "clslevels.txt");
		newlyInstalledLevelKeys = new HashSet<string>();
		if (!featuredLevelsMode)
		{
			if (RDFile.Exists(path))
			{
				List<string> list = new List<string>();
				string[] source = File.ReadAllLines(path);
				foreach (string key in loadedLevels.Keys)
				{
					if (!source.Contains(key))
					{
						newlyInstalledLevelKeys.Add(key);
					}
					else
					{
						list.Add(key);
					}
				}
				if (newlyInstalledLevelKeys.Count != 0)
				{
					flag = true;
				}
			}
			else
			{
				flag = true;
			}
		}
		else
		{
			foreach (string key2 in loadedLevels.Keys)
			{
				GenericDataCLS genericDataCLS = loadedLevels[key2];
				bool flag2 = true;
				if (genericDataCLS.isFolder)
				{
					foreach (GenericDataCLS value2 in genericDataCLS.folder.containingLevels.Values)
					{
						if (!Persistence.GetCustomWorldHasBeenEntered(value2.Hash))
						{
							flag2 = false;
							break;
						}
					}
				}
				else
				{
					flag2 = !Persistence.GetCustomWorldHasBeenEntered(genericDataCLS.Hash);
				}
				if (flag2)
				{
					newlyInstalledLevelKeys.Add(key2);
				}
			}
		}
		SortLevelKeys();
		if (flag)
		{
			RDFile.WriteAllLines(path, loadedLevels.Keys);
		}
		string directoryName = Path.GetDirectoryName(GCS.customLevelPaths?.Last());
		string fileName = Path.GetFileName(directoryName);
		bool flag3 = directoryName != null && loadedLevels.ContainsKey(fileName);
		CustomLevelTile customLevelTile = null;
		CustomLevelTile customLevelTile2 = null;
		int num = loadedLevels.Count((KeyValuePair<string, GenericDataCLS> d) => d.Value.parentFolderName != currentFolderName);
		if (flag3)
		{
			currentFolderName = loadedLevels[fileName].parentFolderName;
		}
		List<CustomLevelTile> list2 = new List<CustomLevelTile>();
		int num2 = 0;
		foreach (string sortedLevelKey in sortedLevelKeys)
		{
			GenericDataCLS genericDataCLS2 = loadedLevels[sortedLevelKey];
			GameObject gameObject = UnityEngine.Object.Instantiate(tilePrefab, floorContainer);
			gameObject.name = sortedLevelKey;
			gameObject.GetComponent<scrFloor>().topGlow.gameObject.SetActive(value: false);
			gameObject.transform.LocalMoveY(gameObject.transform.localPosition.y - (float)num2 + (float)Mathf.FloorToInt((levelCount - num) / 2));
			CustomLevelTile component = gameObject.GetComponent<CustomLevelTile>();
			loadedLevelTiles.Add(sortedLevelKey, component);
			if (genericDataCLS2.isFolder)
			{
				_ = genericDataCLS2.folder;
			}
			else
			{
				LevelDataCLS level = loadedLevels[sortedLevelKey].level;
				if (flag3 && (loadedLevelDirs[sortedLevelKey] == directoryName || sortedLevelKey == fileName))
				{
					customLevelTile = component;
				}
				if (level.loadResult == LoadResult.FutureVersion)
				{
					component.MarkUnavailable();
				}
			}
			if (num2 == Mathf.FloorToInt((levelCount - num) / 2))
			{
				customLevelTile2 = component;
			}
			component.levelKey = sortedLevelKey;
			string text = RDUtils.RemoveRichTags(genericDataCLS2.title);
			bool flag4 = newlyInstalledLevelKeys.Contains(sortedLevelKey);
			component.title.text = (flag4 ? ("<color=#368BE6>" + text + "</color>") : text);
			string text2 = RDUtils.RemoveRichTags(genericDataCLS2.artist);
			component.artist.text = text2;
			component.image.enabled = false;
			UnityEngine.UI.Image image = component.waveTag;
			if (uint.TryParse(sortedLevelKey, out var result) && FeaturedLevels.all.TryGetValue(result, out var value))
			{
				int num3 = ((value.type == FeaturedLevels.LevelType.Classic) ? 13 : 4);
				image.gameObject.SetActive(value.waveNumber == num3);
				image.sprite = ((category == Category.Featured) ? classicTagImage : techTagImage);
				component.waveTagText.text = RDString.Get("cls.new");
			}
			else if (genericDataCLS2.isFolder)
			{
				image.gameObject.SetActive(value: true);
				image.sprite = folderTagImage;
				component.waveTagText.text = RDString.Get("cls.folder");
			}
			else
			{
				image.gameObject.SetActive(value: false);
			}
			if (genericDataCLS2.parentFolderName != currentFolderName)
			{
				component.gameObject.SetActive(value: false);
				continue;
			}
			list2.Add(component);
			num2++;
		}
		UpdateLevelListObjects(list2);
		SelectLevel((customLevelTile != null) ? customLevelTile : customLevelTile2, snap: true);
		ADOBase.controller.chosenPlanet.cosmeticRadius = 1f;
	}

	private void UpdateLevelListObjects(List<CustomLevelTile> levelTiles)
	{
		bool flag = currentFolderName != null;
		bool flag2 = (float)levelTiles.Count >= levelCountForLoop && !flag;
		int num = Mathf.RoundToInt(ADOBase.controller.chosenPlanet.transform.position.y);
		if (levelTiles.Count != 0)
		{
			CustomLevelTile customLevelTile = levelTiles.First();
			CustomLevelTile customLevelTile2 = levelTiles.Last();
			float y = customLevelTile.transform.position.y;
			float y2 = customLevelTile2.transform.position.y;
			gemTopY = Mathf.RoundToInt(y + 1f);
			gemBottomY = Mathf.RoundToInt(y2 - 1f);
			gemTop.MoveY(gemTopY);
			gemBottom.MoveY(gemBottomY);
			gemExitFolder.MoveY(gemTopY);
			chainTop.transform.MoveY(y);
			chainBottom.transform.MoveY(y2);
		}
		else
		{
			chainTop.transform.MoveY(num);
			chainBottom.transform.MoveY(num);
		}
		gemTop.gameObject.SetActive(flag2);
		gemBottom.gameObject.SetActive(flag2);
		chainTop.gameObject.SetActive(!flag2 && !flag);
		chainBottom.gameObject.SetActive(!flag2);
		gemExitFolder.gameObject.SetActive(flag);
	}

	public void SearchLevels(string sub, bool alsoSelect = true)
	{
		if (initializing || refreshing)
		{
			return;
		}
		searchParameter = sub;
		List<CustomLevelTile> list = new List<CustomLevelTile>();
		string value = sub.ToLower();
		foreach (string sortedLevelKey in sortedLevelKeys)
		{
			CustomLevelTile customLevelTile = loadedLevelTiles[sortedLevelKey];
			GenericDataCLS genericDataCLS = loadedLevels[sortedLevelKey];
			string[] array = new string[3] { genericDataCLS.artist, genericDataCLS.author, genericDataCLS.title };
			bool flag = false;
			if (loadedLevels[sortedLevelKey].parentFolderName == currentFolderName)
			{
				if (!sub.IsNullOrEmpty())
				{
					string[] array2 = array;
					for (int i = 0; i < array2.Length; i++)
					{
						if (array2[i].RemoveRichTags().ToLower().Contains(value))
						{
							flag = true;
							break;
						}
					}
				}
				else
				{
					flag = true;
				}
			}
			if (flag)
			{
				list.Add(customLevelTile);
			}
			else
			{
				customLevelTile.gameObject.SetActive(value: false);
			}
		}
		int num = Mathf.RoundToInt(ADOBase.controller.chosenPlanet.transform.position.y);
		for (int j = 0; j < list.Count; j++)
		{
			CustomLevelTile customLevelTile2 = list[j];
			customLevelTile2.gameObject.SetActive(value: true);
			customLevelTile2.transform.MoveY(num - j);
		}
		UpdateLevelListObjects(list);
		if (list.Count != 0 && alsoSelect)
		{
			SelectLevel(list[0], snap: true);
		}
		else
		{
			DisplayLevel();
			StopCurrentLevelSong();
		}
		string text = RDString.Get("cls.shortcut.find");
		if (!sub.IsNullOrEmpty())
		{
			text = text + " <color=#ffd000><i>" + RDString.Get("cls.currentlySearching", new Dictionary<string, object> { { "filter", sub } }) + "</i></color>";
		}
		currentSearchText.text = text;
	}

	public void LoadTileIconsNearby(int targetIndex, int nearbyCount = 10)
	{
		targetIndex = Mathf.Clamp(targetIndex, 0, sortedLevelKeys.Count - 1);
		int num = Math.Max(0, targetIndex - nearbyCount);
		int num2 = Math.Min(sortedLevelKeys.Count - 1, targetIndex + nearbyCount);
		for (int i = num; i <= num2; i++)
		{
			string text = sortedLevelKeys[i];
			GenericDataCLS genericDataCLS = loadedLevels[text];
			CustomLevelTile customLevelTile = loadedLevelTiles[text];
			if (!customLevelTile.gameObject.activeSelf || string.IsNullOrEmpty(genericDataCLS.previewIcon) || customLevelTile.didStartLoadingIcon || customLevelTile.didProcessIcon)
			{
				continue;
			}
			if (featuredLevelsMode)
			{
				string path = (genericDataCLS.isFolder ? text.Replace("Folder:", "") : text);
				Texture2D texture2D = Resources.Load<Texture2D>(Path.Combine("FeaturedLevels", path, "icon"));
				if (texture2D != null)
				{
					customLevelTile.ProcessIconTexture(texture2D, genericDataCLS.previewIconColor);
				}
			}
			else
			{
				string iconPath = Path.Combine(loadedLevelDirs[text], genericDataCLS.previewIcon);
				customLevelTile.LoadTileIcon(iconPath, genericDataCLS.previewIconColor);
			}
		}
	}

	private void HideInitialMenu()
	{
		ShowLevelSelection(show: true);
		initialMenu.SetActive(value: false);
		camera.positionState = PositionState.CLS;
		ADOBase.controller.chosenPlanet.other.transform.LocalMoveXY(0f, 0f);
		initialQuitLabel.enabled = false;
		showingInitialMenu = false;
		optionsPanels.UpdateOrderText();
		optionsPanels.currentOrderText.SetLocalizedFont();
	}

	public void FeaturedLevelsPortal()
	{
		EnterCategory(Category.Featured);
	}

	public void TechFeaturedLevelsPortal()
	{
		EnterCategory(Category.Tech);
	}

	public void WorkshopLevelsPortal()
	{
		EnterCategory(Category.Workshop);
	}

	public async void EnterCategory(Category category)
	{
		HideInitialMenu();
		this.category = category;
		optionsPanels.RefreshOptionsForCategory();
		await Refresh(setup: true);
	}

	public void QuitPortal()
	{
		GCS.customLevelPaths = null;
		if (canReturnToSelection && !showingInitialMenu)
		{
			ADOBase.loader.LoadSceneWithTransition(WipeDirection.StartsFromRight, "scnCLS");
		}
		else
		{
			ADOBase.controller.QuitToMainMenu();
		}
	}

	public static void DeactivateCustomLevelModifiers()
	{
		GCS.useNoFail = false;
		GCS.useUnlockKeyLimiter = false;
	}

	public void UpdateSorting(OptionsPanelsCLS.OptionName sortingMethod)
	{
		this.sortingMethod = sortingMethod;
		SortLevelKeys();
		SearchLevels(ADOBase.cls.searchParameter);
		Persistence.clsSortingParameter = sortingMethod;
	}

	private static int WaveNumberOf(string levelKey, GenericDataCLS data)
	{
		if (!data.isFolder)
		{
			return WaveNumberOf(levelKey);
		}
		return WaveNumberOf(data.folder.containingLevels.Keys.FirstOrDefault());
	}

	private static int WaveNumberOf(string levelKey)
	{
		if (!uint.TryParse(levelKey, out var result) || !FeaturedLevels.all.TryGetValue(result, out var value))
		{
			return 0;
		}
		return value.waveNumber;
	}

	public void SortLevelKeys()
	{
		sortedLevelKeys = new List<string>();
		Dictionary<string, GenericDataCLS> source = ADOBase.cls.loadedLevels;
		IOrderedEnumerable<KeyValuePair<string, GenericDataCLS>> orderedEnumerable = null;
		switch (effectiveSortingMethod)
		{
		case OptionsPanelsCLS.OptionName.Difficulty:
			orderedEnumerable = ((!instance.featuredLevelsMode || ADOBase.cls.newlyInstalledLevelKeys.Count <= 0) ? source.OrderByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.difficulty;
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.artist.RemoveRichTags();
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.title.RemoveRichTags();
			}) : source.OrderByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				HashSet<string> hashSet = ADOBase.cls.newlyInstalledLevelKeys;
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return hashSet.Contains(keyValuePair.Key);
			}).ThenByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.difficulty;
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.artist.RemoveRichTags();
			})
				.ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
				{
					KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
					return keyValuePair.Value.title.RemoveRichTags();
				}));
			break;
		case OptionsPanelsCLS.OptionName.LastPlayed:
			orderedEnumerable = ((ADOBase.cls.newlyInstalledLevelKeys.Count <= 0) ? source.OrderByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return Persistence.GetCustomWorldPlayIndex(keyValuePair.Value.Hash);
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.artist;
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.title;
			}) : source.OrderBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				HashSet<string> hashSet = ADOBase.cls.newlyInstalledLevelKeys;
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return hashSet.Contains(keyValuePair.Key);
			}).ThenByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return Persistence.GetCustomWorldPlayIndex(keyValuePair.Value.Hash);
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.artist;
			})
				.ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
				{
					KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
					return keyValuePair.Value.title;
				}));
			break;
		case OptionsPanelsCLS.OptionName.Song:
			orderedEnumerable = source.OrderBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.title.RemoveRichTags();
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.artist.RemoveRichTags();
			}).ThenByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.difficulty;
			});
			break;
		case OptionsPanelsCLS.OptionName.Artist:
			orderedEnumerable = source.OrderBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.artist.RemoveRichTags();
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.title.RemoveRichTags();
			}).ThenByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.difficulty;
			});
			break;
		case OptionsPanelsCLS.OptionName.Author:
			orderedEnumerable = source.OrderBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.author.RemoveRichTags();
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.title.RemoveRichTags();
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.artist.RemoveRichTags();
			});
			break;
		case OptionsPanelsCLS.OptionName.Wave:
			orderedEnumerable = source.OrderByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				string key = keyValuePair.Key;
				keyValuePair = pair;
				return WaveNumberOf(key, keyValuePair.Value);
			}).ThenByDescending(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.difficulty;
			}).ThenBy(delegate(KeyValuePair<string, GenericDataCLS> pair)
			{
				KeyValuePair<string, GenericDataCLS> keyValuePair = pair;
				return keyValuePair.Value.title.RemoveRichTags();
			});
			break;
		}
		foreach (KeyValuePair<string, GenericDataCLS> item in orderedEnumerable)
		{
			sortedLevelKeys.Add(item.Key);
		}
	}
}
