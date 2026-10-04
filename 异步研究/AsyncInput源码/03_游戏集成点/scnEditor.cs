using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ADOFAI;
using ADOFAI.Editor;
using ADOFAI.Editor.Actions;
using ADOFAI.Editor.Components.Gradients;
using ADOFAI.Editor.Panels;
using ADOFAI.Editor.ParticleEditor;
using ADOFAI.Editor.Preferences;
using ADOFAI.LevelEditor.Controls;
using ADOFAI.SteamIntegration;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GDMiniJSON;
using Steamworks.Data;
using Steamworks.Ugc;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityFileDialog;
using UnityStandardAssets.ImageEffects;

public class scnEditor : ADOBase
{
	private enum SettingsTabType
	{
		None = 0,
		Level = 1,
		Song = 2
	}

	public enum PopupType
	{
		SaveBeforeSongImport = 0,
		SaveBeforeImageImport = 1,
		SaveBeforeVideoImport = 2,
		SaveBeforeLevelExport = 3,
		ExportLevel = 4,
		MissingExportParams = 5,
		MissingFiles = 6,
		OpenURL = 7,
		CopyrightWarning = 8,
		OggEncode = 9,
		ConversionSuccessful = 10,
		ConversionError = 11,
		UnsavedChanges = 12,
		MacAppStoreFolderRestriction = 13,
		MacAppStoreFileOutsideDownloads = 14,
		Spoiler = 15,
		GenericText = 16,
		SteamDeckWarning = 17,
		BetaBranchesWorkshopPublish = 18,
		Confirm = 19,
		WorkshopLevelReupload = 20
	}

	[Serializable]
	public class LevelState
	{
		public LevelData data;

		public List<int> selectedFloors = new List<int>();

		public int[] selectedDecorationIndices;

		public int selectedDecorationItemIndex = -1;

		public LevelEventType settingsEventType;

		public LevelEventType floorEventType;

		public int floorEventTypeIndex;

		public LevelState(LevelData data, List<int> selectedFloors, int[] currentDecorationItemIndices, bool dataHasChanged = true)
		{
			this.data = (dataHasChanged ? data : null);
			this.selectedFloors = selectedFloors;
			selectedDecorationIndices = currentDecorationItemIndices;
		}

		public override string ToString()
		{
			if (data == null)
			{
				return "no data";
			}
			string text = "";
			foreach (LevelEvent levelEvent in data.levelEvents)
			{
				text += levelEvent.ToString();
			}
			return text;
		}
	}

	public struct NotificationAction
	{
		public string text;

		public Action action;

		public NotificationAction(string text, Action action)
		{
			this.text = text;
			this.action = action;
		}
	}

	public enum ClipboardContent
	{
		None = 0,
		Floors = 1,
		Decorations = 2
	}

	public struct FloorData
	{
		public char stringDirection;

		public float floatDirection;

		public List<LevelEvent> levelEventData;

		public List<LevelEvent> attachedDecorations;

		public FloorData(char stringDir, float floatDir, List<LevelEvent> events, List<LevelEvent> attachedDecorations)
		{
			stringDirection = stringDir;
			floatDirection = floatDir;
			levelEventData = events;
			this.attachedDecorations = attachedDecorations;
		}
	}

	private enum PointerDownObjectType
	{
		Decoration = 0,
		Floor = 1,
		Gizmo = 2,
		None = 3
	}

	private const float CameraZPosition = -10f;

	private const int PrimaryMouseButton = 0;

	private const int SecondaryMouseButton = 1;

	private const int MiddleMouseButton = 2;

	private const int AutoOff = 0;

	private const int AutoOn = 1;

	private const int AutoLeft = 2;

	private const int AutoRight = 3;

	private const int AutoOffLeft = 4;

	private const int AutoOffRight = 5;

	private const int AutoMiss = 6;

	private const int AutoNervousOn = 7;

	private const int AutoNervousOff = 8;

	private const int AutoPet = 9;

	private const float DragAxisMagnetValue = 1f;

	private const float IntervalToUpdateSteamStats = 120f;

	private const int EventsInBar = 11;

	private const string shortcutCol = "<color=#00000077>";

	private const string selectedColorTweenId = "selectedColorTween";

	public const int MaxUndoSteps = 100;

	private readonly UnityEngine.Color grayColor = new UnityEngine.Color(36f / 85f, 36f / 85f, 36f / 85f);

	private readonly UnityEngine.Color lineGreen = new UnityEngine.Color(0.4f, 1f, 0.4f, 1f);

	private readonly UnityEngine.Color lineYellow = new UnityEngine.Color(1f, 1f, 0.4f, 1f);

	private readonly UnityEngine.Color linePurple = new UnityEngine.Color(0.75f, 0.5f, 1f, 1f);

	private readonly UnityEngine.Color lineBlue = new UnityEngine.Color(0.4f, 0.4f, 1f, 1f);

	public static scnEditor instance;

	public static string savedLevelString;

	public static bool applyEventsToFloorsOnPlay = true;

	public static bool selectingFloorID = false;

	public static bool editorHasBeenEntered = false;

	private static float lastTimeStatsUploaded;

	public static string levelToOpenOnLoad = null;

	private bool fromScnGame;

	[NonSerialized]
	public int version;

	[Header("Components")]
	public GameObject levelEditorScene;

	public GameObject levelStringPanel;

	public GameObject shortcutsPanel;

	public GameObject popupPanel;

	public GameObject popupWindow;

	public GameObject savePopupContainer;

	public GameObject urlPopupContainer;

	public GameObject copyrightPopupContainer;

	public GameObject paramsPopupContainer;

	public GameObject missingFilesPopupContainer;

	public GameObject oggPopupContainer;

	public GameObject okPopupContainer;

	public GameObject largeOkPopupContainer;

	public GameObject spoilerPopupContainer;

	public GameObject unsavedChangesPopupContainer;

	public GameObject steamDeckPopupContainer;

	public GameObject confirmPopupContainer;

	public TMP_Text savePopupText;

	public TMP_Text paramsPopupText;

	public TMP_Text missingFilesPopupText;

	public TMP_Text copyrightText;

	public Slider oggConversionBar;

	public TMP_Text oggConversionBarText;

	public TMP_Text okPopupText;

	public TMP_Text largeOkPopupText;

	public TMP_Text spoilerPopupText;

	public TMP_Text confirmPopupText;

	public TMP_Text eventPickerText;

	public TMP_Text categoryText;

	public TMP_Text selectingFloorIDText;

	public RectTransform selectingFloorIDRectTransform;

	public Button playPause;

	public UnityEngine.UI.Image playPauseIcon;

	public Button rewind;

	public EventSystem eventSystem;

	public PublishWindow publishWindow;

	public WorkshopThumbnailMaker thumbnailMaker;

	public RDColorPickerPopup colorPickerPopup;

	public GradientEditor gradientEditorPopup;

	public EditorDifficultySelector editorDifficultySelector;

	public EditorSpeedIndicator speedIndicator;

	public FloorDirectionButton[] floorDirectionButtons;

	[Header("Level/song configuration panel")]
	public Transform settingsPanelsContainer;

	[Header("Buttons")]
	public Button buttonFileActionDropdown;

	public Button buttonNew;

	public Button buttonOpen;

	public Button buttonOpenRecent;

	public Button buttonOpenURL;

	public Button buttonSave;

	public Button buttonSaveAs;

	public Button buttonPreferences;

	public Button buttonExit;

	public Button buttonSettings;

	public Button buttonHelp;

	public Button buttonCloseHelp;

	public Button buttonDiscord;

	public Button buttonAuto;

	public Button buttonNextPage;

	public Button buttonPrevPage;

	public Button buttonNoFail;

	public Button buttonUnlockKeyLimiter;

	public RectTransform buttonUnlockKeyLimiterRT;

	[Header("Popup buttons")]
	public Button popupSaveOk;

	public Button popupSaveSaveAs;

	public Button popupURLDownload;

	public Button popupURLCancel;

	public Button popupCopyrightAccept;

	public Button popupCopyrightReturn;

	public Button popupParamsCancel;

	public Button popupMissingFilesCancel;

	public Button popupOggCancel;

	public Button popupOggConvert;

	public Button popupOkOk;

	public Button popupLargeOkOk;

	public Button popupSpoilerOk;

	private TweenCallback popupOkCallback;

	public Button popupUnsavedChangesCancel;

	public Button popupUnsavedChangesDiscard;

	public Button popupUnsavedChangesSave;

	public Button popupBlocker;

	public Button popupSteamDeckExit;

	public Button popupSteamDeckContinue;

	public Button popupConfirmOk;

	public Button popupConfirmCancel;

	private TweenCallback popupConfirmCallback;

	[Header("Floor stuff")]
	public Button buttonD;

	public Button buttonW;

	public Button buttonA;

	public Button buttonS;

	public Button buttonE;

	public Button buttonQ;

	public Button buttonZ;

	public Button buttonC;

	public Button buttonT;

	public Button buttonG;

	public Button buttonF;

	public Button buttonB;

	public Button buttonH;

	public Button buttonJ;

	public Button buttonM;

	public Button buttonN;

	public Button buttonBackQuoteT;

	public Button buttonBackQuoteY;

	public Button buttonBackQuoteV;

	public Button buttonBackQuoteB;

	public Button buttonBackQuoteH;

	public Button buttonBackQuoteJ;

	public Button buttonBackQuoteM;

	public Button buttonBackQuoteN;

	public Button buttonSpace;

	public Button buttonTab;

	public Button buttonToggleAngleInput;

	public UnityEngine.UI.Image EventCircle;

	public Canvas floorButtonCanvas;

	public Canvas floorButtonPrimaryCanvas;

	public Canvas floorButtonExtraCanvas;

	public Canvas floorButtonExtraBackQuoteCanvas;

	public Canvas floorButtonLeftRightCanvas;

	public GameObject floorButtonContainer;

	public GameObject floorButtonArbitraryContainer;

	public TMP_InputField floorButtonArbitrary;

	public KeyIndicator tabIndicator;

	[Header("Colliders")]
	public GameObject Collider15;

	public GameObject Collider30;

	public GameObject Collider45;

	public GameObject Collider60;

	public GameObject Collider75;

	public GameObject Collider90;

	public GameObject Collider105;

	public GameObject Collider120;

	public GameObject Collider135;

	public GameObject Collider165;

	public GameObject Collider180;

	[Header("Inspector")]
	public InspectorPanel levelEventsPanel;

	public InspectorPanel settingsPanel;

	public GameObject inspectorTabs;

	public GameObject inspectorPanels;

	public RectTransform propertyHelpContainer;

	public RectTransform propertyHelpImage;

	[Header("Settings Popup")]
	public UnityEngine.UI.Image prefsContainer;

	public EditorPreferencesMenu prefsMenu;

	[Header("Help Button Popup")]
	public TMP_Text propertyHelpText;

	public Button propertyHelpURLButton;

	public TMP_Text propertyHelpURLButtonText;

	[Header("Find Floor Popup")]
	public GameObject findFloorPanel;

	public TMP_Text findFloorPanelTitle;

	public UnityEngine.UI.Image findArrow;

	public Property findType;

	public Property findValue;

	public TMP_Text findFloorSelectedInfo;

	public Button findButton;

	[Header("Find Comment Popup")]
	public FindCommentPanel findCommentPanel;

	[Header("Notification Popup")]
	public GameObject notificationPopupContainer;

	public RectTransform notificationPopupScrollview;

	public RectTransform notificationPopupScrollviewContent;

	public RectTransform notificationPopupActionsContainer;

	public GameObject notificationPopupScrollviewVertical;

	public GameObject notificationPopupScrollviewHorizontal;

	public RectTransform notificationPopupWindow;

	public TMP_Text notificationPopupTitle;

	public TMP_Text notificationPopupContent;

	public Button notificationOkButton;

	private List<Button> notificationPopupActions = new List<Button>();

	[Header("Particle Editor Popup")]
	public UnityEngine.UI.Image particleEditorContainer;

	public ParticleEditor particleEditor;

	[Header("Shortcut")]
	public RectTransform shortcutTabsContainer;

	public RectTransform shortcutContentContainer;

	public GameObject shortcutTabPrefab;

	public GameObject shortcutContentPrefab;

	public GameObject shortcutTextPrefab;

	public TMP_Text shortcutTitle;

	private List<GameObject> shortcutTabs = new List<GameObject>();

	private List<GameObject> shortcutContent = new List<GameObject>();

	private Dictionary<EditorTabKey, List<EditorConstants.EditorKeyShortcut>> shortcutsDictionary = new Dictionary<EditorTabKey, List<EditorConstants.EditorKeyShortcut>>
	{
		[EditorTabKey.BasicEditing] = new List<EditorConstants.EditorKeyShortcut>(),
		[EditorTabKey.SelectionAndDeletion] = new List<EditorConstants.EditorKeyShortcut>(),
		[EditorTabKey.FlippingAndRotation] = new List<EditorConstants.EditorKeyShortcut>(),
		[EditorTabKey.AdvancedEditing] = new List<EditorConstants.EditorKeyShortcut>(),
		[EditorTabKey.Bookmarks] = new List<EditorConstants.EditorKeyShortcut>(),
		[EditorTabKey.EditorWorkflow] = new List<EditorConstants.EditorKeyShortcut>(),
		[EditorTabKey.Gameplay] = new List<EditorConstants.EditorKeyShortcut>(),
		[EditorTabKey.Other] = new List<EditorConstants.EditorKeyShortcut>()
	};

	[Header("Level Events Bar")]
	public RectTransform levelEventsBar;

	public RectTransform levelEventsBarButtons;

	public RectTransform levelEventsBarCategories;

	public HorizontalLayoutGroup levelEventsBarCategoriesLayout;

	[Header("Others")]
	public GameObject fileActionsPanel;

	public Canvas levelEditorCanvas;

	public Canvas ottoCanvas;

	public Text controlsTip;

	public TMP_Text filenameText;

	public GameObject notificationText;

	public UnityEngine.UI.Image fileIcon;

	public UnityEngine.UI.Image fileArrow;

	public TMP_InputField levelLinkInput;

	public EditorWebServices webServices;

	public UnityEngine.UI.Image lockIcon;

	public UnityEngine.UI.Image lockBackground;

	public DecorationPivot decPivot;

	public DecoTransformGizmoHolder decTransformGizmo;

	[NonSerialized]
	public LevelEventCategory currentCategory;

	[NonSerialized]
	public List<LevelEventType> favoriteEvents = new List<LevelEventType>();

	[NonSerialized]
	public Dictionary<LevelEventCategory, List<LevelEventButton>> eventButtons = new Dictionary<LevelEventCategory, List<LevelEventButton>>();

	private List<CategoryTab> categoryTabs = new List<CategoryTab>();

	[NonSerialized]
	public int currentPage;

	private int maxPage = 1;

	[NonSerialized]
	public List<scrFloor> selectedFloors = new List<scrFloor>();

	[NonSerialized]
	public int selectedFloorCached;

	[NonSerialized]
	public List<LevelEvent> selectedDecorations = new List<LevelEvent>();

	[NonSerialized]
	public int cacheSelectedEventIndex;

	[NonSerialized]
	public scrFloor multiSelectPoint;

	[NonSerialized]
	public List<object> clipboard = new List<object>();

	[NonSerialized]
	private List<int> clipboardIndices = new List<int>();

	[NonSerialized]
	public ClipboardContent clipboardContent;

	[NonSerialized]
	public PropertyControl_DecorationsList propertyControlDecorationsList;

	[NonSerialized]
	public PropertyControl_EventsList propertyControlEventsList;

	[NonSerialized]
	private bool refreshBgSprites;

	[NonSerialized]
	private bool refreshDecSprites;

	[NonSerialized]
	public float camUserSizeMultiplier = 1f;

	[Header("Tweakables")]
	public float scrollSpeed;

	public UnityEngine.Color selectedColor0;

	public UnityEngine.Color selectedColor1;

	public UnityEngine.Color defaultColor;

	private UnityEngine.Color currentSecondaryTrackColor;

	public float shortcutsPanelOpenHeight;

	public float shortcutsPanelCloseHeight;

	public float filePanelOpenWidth;

	public float filePanelCloseWidth;

	public float filePanelMoveDuration;

	public float findFloorPanelOpenHeight;

	public float findFloorPanelCloseHeight;

	public float findCommentPanelOpenHeight;

	public float findCommentPanelCloseHeight;

	public float floorButtonPulseSize;

	public float floorButtonPulseDuration;

	public float cameraSelectDuration;

	public Ease panelShowEase;

	public Ease panelHideEase;

	public Ease UIPanelEaseMode;

	public float UIPanelEaseDur;

	public float backupInterval = 30f;

	public float maxShadowDistance;

	public float maxOverlapRadius;

	public UnityEngine.Color vfxIconColor;

	public UnityEngine.Color shortcutsLockColor;

	public UnityEngine.Color shortcutsLockIconColor;

	public UnityEngine.Color notificationErrorColor;

	public UnityEngine.Color editingColor = new UnityEngine.Color(0.898f, 0.376f, 0.376f, 1f);

	public UnityEngine.Color defaultButtonColor = UnityEngine.Color.white;

	[Header("Prefabs")]
	public GameObject prefab_levelEventButton;

	public GameObject prefab_eventCategoryTab;

	public GameObject prefab_tileFlash;

	public GameObject prefab_editorNum;

	[Header("Data")]
	public Dictionary<string, AudioClip> preparedAudioClips;

	public new scnGame customLevel;

	[NonSerialized]
	[Header("Undo Redo")]
	public bool initialized;

	[NonSerialized]
	public int changingState;

	[NonSerialized]
	public List<LevelState> undoStates = new List<LevelState>();

	[NonSerialized]
	public List<LevelState> redoStates = new List<LevelState>();

	[NonSerialized]
	public LevelEventType settingsEventType;

	[NonSerialized]
	public LevelEventType filteredEventType;

	private int saveStateLastFrame;

	[Header("Sprites")]
	public Sprite pauseButtonIcon;

	public Sprite playButtonIcon;

	public Sprite[] autoSprites;

	public UnityEngine.UI.Image autoImage;

	public UnityEngine.UI.Image unlockKeyLimiterImage;

	public Sprite fileSpriteUp;

	public Sprite fileSpriteDown;

	public Sprite lockSpriteOff;

	public Sprite lockSpriteOn;

	public Sprite show8DirectionSprite;

	public Sprite showArbitraryAngleSprite;

	private List<GameObject> floorConnectorGOs = new List<GameObject>();

	private GameObject floorConnectors;

	public Texture2D floorConnectorTex;

	private float dragGridSize = 0.5f;

	private Shader spritesDefaultShader;

	private float autoPetTime;

	[Header("Audio Sources")]
	[SerializeField]
	private AudioSource ottoAudioSrc;

	[SerializeField]
	private AudioSource interfaceAudioSrc;

	[NonSerialized]
	[Header("Runtime")]
	public string soundToConvert;

	[NonSerialized]
	public Action<string> soundConversionCallback;

	private CanvasScaler canvasScaler;

	[NonSerialized]
	public RectTransform decorationsListContent;

	[NonSerialized]
	public RectTransform eventsListContent;

	private SettingsTabType selectedSettingsTab;

	private Vector3 mousePosition0;

	private Vector3 cameraPositionAtDragStart;

	private Vector3 evIndPosAtDragStart;

	private LayerMask floorLayerMask;

	private LayerMask foregroundMask;

	private LayerMask handlesLayerMask;

	private LayerMask textDecoLayerMask;

	private scrFloor lastSelectedFloor;

	private int lastSelectedFloorsCount;

	private Tween copiedColorTween;

	private Sequence notificationSeq;

	[NonSerialized]
	public Sequence blinkTimer;

	public bool autoFailed;

	[NonSerialized]
	public Camera camera;

	private UnityWebRequest www;

	private CancellationTokenSource downloadCancellation;

	private GameObject[] previouslyFoundObjects;

	private int selectedObjectIndexOfBunch;

	public Texture2D workshopThumbnail;

	[NonSerialized]
	public float speedMultiplier;

	private bool dragging;

	private bool cancelDrag;

	public EventIndicator draggedEvIndicator;

	private float freeAngle;

	private bool freeAngleMode;

	private bool showingFileActions;

	private bool showingShortcuts;

	private bool showingPopup;

	private bool showingFindFloorPanel;

	private bool showingFindCommentPanel;

	private bool hoveringFileActions;

	private bool hoveringFindFloorPanel;

	private bool isOttoBlinking;

	private int ottoBlinkCounter;

	private bool downloadingLevel;

	private bool animatingPropertyHelp;

	private bool showingPropertyHelp;

	private float backupTimer;

	private bool _unsavedChanges;

	private bool wasUsingCustomFrameRate;

	private float lastCustomFrameRate;

	[NonSerialized]
	public bool lockPathEditing;

	private bool decorationWasSelected;

	private List<LevelEvent> lastSelectedDecorations;

	[NonSerialized]
	public TransformGizmo lastHoveredGizmo;

	private Material lineMaterial;

	private UnityEngine.Color defaultLockColor;

	private UnityEngine.Color defaultLockIconColor;

	private EditorKeybindManager keybindManager;

	private Tween _anchorZoomTween;

	private List<Action> _popupStack = new List<Action>();

	private int _currentPopupSortOrder = 30000;

	private bool _pausedInPlayMode;

	[NonSerialized]
	public bool inStrictlyEditingMode;

	[NonSerialized]
	public bool isLoading;

	[NonSerialized]
	public bool selectingFloorIDTextMoving;

	private Dictionary<string, string> errorImageResult = new Dictionary<string, string>();

	private bool isUnauthorizedAccess;

	[NonSerialized]
	public bool showFloorNums;

	public bool steamDeckWarningPassed;

	private Action quitLevelAction;

	private bool forceQuit;

	private bool closePopupImmediately;

	private AsyncOperation scnGameLoad;

	private LevelEventType[] whitelistedEvents = new LevelEventType[2]
	{
		LevelEventType.Checkpoint,
		LevelEventType.Bookmark
	};

	private GameObject[] foundObjects;

	private int lastFrameUpdated = -1;

	private bool useAbsoluteArbitraryAngle;

	private bool mpWarned;

	private LevelEvent copiedTrackColor;

	private LevelEvent previousTrackColor;

	private LevelEvent copiedHitsound;

	private LevelEvent previousHitsound;

	private bool popupIsAnimating;

	private bool stallFileDialog;

	private float lastOttoPetTime;

	private Vector3 lastOttoPetPosition;

	private int saveBackupLastFrame;

	public float playbackSpeed = 1f;

	private Dictionary<scrFloor, Vector3> floorPositionsAtDragStart = new Dictionary<scrFloor, Vector3>();

	private Dictionary<scrDecoration, Vector2> decorationPositionsAtDragStart = new Dictionary<scrDecoration, Vector2>();

	private PointerDownObjectType pointerDownObjectType = PointerDownObjectType.None;

	public TransformGizmo draggingGizmo;

	private float addXDragCache = 1f;

	private float addYDragCache;

	public bool selectedFirstFloor
	{
		get
		{
			if (selectedFloors.Count > 0)
			{
				return selectedFloors[0].seqID == 0;
			}
			return false;
		}
	}

	private bool holdingShift => RDInput.holdingShift;

	private bool holdingControl => RDInput.holdingControl;

	private bool holdingAlt => RDInput.holdingAlt;

	public List<scrFloor> floors => customLevel.levelMaker.listFloors;

	public LevelData levelData => customLevel.levelData;

	public EventsArray<LevelEvent> events => levelData.levelEvents;

	public DecorationsArray<LevelEvent> decorations => levelData.decorations;

	public List<scrDecoration> allDecorations => scrDecorationManager.instance.allDecorations;

	public bool pausedInPlayMode
	{
		get
		{
			return _pausedInPlayMode;
		}
		set
		{
			_pausedInPlayMode = value;
			if (_pausedInPlayMode)
			{
				wasUsingCustomFrameRate = scrCamera.instance.enableCustomFPS;
				lastCustomFrameRate = scrCamera.instance.frameRate;
				scrCamera.instance.SetCustomFrameRate(enable: false);
			}
			else if (wasUsingCustomFrameRate)
			{
				scrCamera.instance.SetCustomFrameRate(enable: true, lastCustomFrameRate);
			}
		}
	}

	public bool playMode
	{
		get
		{
			if (!pausedInPlayMode)
			{
				return !ADOBase.controller.paused;
			}
			return true;
		}
	}

	public bool shouldHideEditorUIForAutoplay
	{
		get
		{
			if (RDC.auto)
			{
				return Persistence.hideEditorUIDuringAutoplay;
			}
			return false;
		}
	}

	public bool shouldHideGameplayUIForAutoplay
	{
		get
		{
			if (RDC.auto)
			{
				return Persistence.hideGameplayUIDuringAutoplay;
			}
			return false;
		}
	}

	private bool highBPM => customLevel.highestBPM >= 300f;

	private bool isOldLevel => levelData.isOldLevel;

	private EditorSelectTarget currentSelectTarget => (EditorSelectTarget)levelData.miscSettings["selectTarget"];

	public bool userIsEditingAnInputField
	{
		get
		{
			GameObject currentSelectedGameObject = eventSystem.currentSelectedGameObject;
			if (currentSelectedGameObject == null)
			{
				return false;
			}
			TMP_InputField component = currentSelectedGameObject.GetComponent<TMP_InputField>();
			if (component != null)
			{
				return component.isFocused;
			}
			return false;
		}
	}

	private bool unsavedChanges
	{
		get
		{
			return _unsavedChanges;
		}
		set
		{
			_unsavedChanges = value;
			RefreshFilenameText();
		}
	}

	private bool controlPressed
	{
		get
		{
			if (!Input.GetKey(KeyCode.LeftControl))
			{
				return Input.GetKey(KeyCode.RightControl);
			}
			return true;
		}
	}

	private bool controlPressChanged
	{
		get
		{
			if (!Input.GetKeyDown(KeyCode.LeftControl) && !Input.GetKeyDown(KeyCode.RightControl) && !Input.GetKeyUp(KeyCode.LeftControl))
			{
				return Input.GetKeyUp(KeyCode.RightControl);
			}
			return true;
		}
	}

	private void UpdateCanvasScalerResolution(float height)
	{
		height = Mathf.Clamp(height, 900f, Screen.height * 2);
		float x = (float)Screen.width * 1f / (float)Screen.height * height;
		canvasScaler.referenceResolution = new Vector2(x, height);
		ottoCanvas.GetComponent<CanvasScaler>().referenceResolution = canvasScaler.referenceResolution;
		Persistence.editorScale = height;
	}

	private void Awake()
	{
		RDString.LoadLevelEditorFonts();
		scrPlayerManager.SetPlayerCount(1);
		instance = this;
		LoadGameScene();
	}

	private void RegisterKeybinds()
	{
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Tab), new CreateMidspinFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.Space), new Create360FloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyCode.P), new PlayEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.P), new PlayWithSpeedEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyCode.Space), new PlayEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Space), new PlayWithSpeedEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.C), new CopyFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.X), new CutFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.V), new PasteFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control | KeyModifier.Alt, KeyCode.V), new PasteFloorWithoutDecorationsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Z), new UndoEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.Z), new RedoEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Y), new RedoEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Return), new CreateArbitraryFloorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.LeftArrow), new SelectPreviousFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.RightArrow), new SelectNextFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.LeftArrow), new MoveSelectionLeftEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.RightArrow), new MoveSelectionRightEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.LeftArrow), new SelectFirstFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Home), new SelectFirstFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.RightArrow), new SelectLastFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.End), new SelectLastFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.LeftArrow), new SelectToFirstFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.RightArrow), new SelectToLastFloorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Backspace), new DeleteFloorsEditorAction(backwards: true));
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Delete), new DeleteFloorsEditorAction(backwards: false));
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Backspace), new DeletePrecedingFloorsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Delete), new DeleteSubsequentFloorsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Escape), new ToggleFileActionsPanelEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Escape), new DeselectAllEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.L), new FlipFloorsHorizontalEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.L), new FlipFloorsVerticalEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Comma), new RotateFloors90CounterClockwiseEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Period), new RotateFloors90ClockwiseEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Slash), new RotateFloors180EditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.F5), new CreatePentagonEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.F5), new CreateUpsideDownPentagonEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.F7), new CreateHeptagonEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.F7), new CreateUpsideDownHeptagonEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.C), new CopyEventsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.X), new CutEventsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control | KeyModifier.Alt, KeyCode.C), new CopyAllSameTypeEventsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control | KeyModifier.Alt, KeyCode.X), new CutAllSameTypeEventsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.V), new PasteEventsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control | KeyModifier.Alt, KeyCode.V), new PasteEventsEditorAction(alsoPasteDecorations: false));
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.E), new CopyTrackColorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.R), new PasteTrackColorEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.T), new PasteTrackColorSingleTileEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.Alpha1), new CopyHitSoundEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.Alpha2), new PasteHitSoundSingleTileEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.L), new LockSelectedDecorationsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.D), new DuplicateDecorationsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Backspace), new DeleteDecorationsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Delete), new DeleteDecorationsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Alt, KeyCode.B), new ToggleBookmarkEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Alt, KeyCode.LeftArrow), new SelectPreviousBookmarkEditorAction(selectRelative: true));
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Alt, KeyCode.RightArrow), new SelectNextBookmarkEditorAction(selectRelative: true));
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Alt, KeyCode.Semicolon), new ToggleFindFloorPanelEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Alt, KeyCode.F), new ToggleFindCommentPanelEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.A), new ToggleAutoEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Alt, KeyCode.A), new ToggleAutoEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.N), new ToggleNoFailEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Alt, KeyCode.N), new ToggleNoFailEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.S), new SaveLevelEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.S), new SaveLevelAsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.O), new OpenLevelEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.U), new OpenUrlEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.O), new OpenRecentEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.N), new NewLevelEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.P), new OpenPreferencesEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Minus), new ZoomOutCameraEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.KeypadMinus), new ZoomOutCameraEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.Equals), new ZoomInCameraEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.KeypadPlus), new ZoomInCameraEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Minus), new ZoomOutUiEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Equals), new ZoomInUiEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.UpArrow), new CyclePreviousEventTabEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.DownArrow), new CycleNextEventTabEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.UpArrow), new CyclePreviousSelectedEventEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.DownArrow), new CycleNextSelectedEventEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.LeftBracket), new ShowPreviousEventPageEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, KeyCode.RightBracket), new ShowNextEventPageEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.LeftBracket), new ShowFirstEventPageEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.RightBracket), new ShowLastEventPageEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.Q), new TryQuitToMenuEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.H, ctrlIsCmd: false), new ToggleShortcutsPanelEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift, KeyCode.F1), new ShowCopyrightPopupEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control | KeyModifier.Alt, KeyCode.L), new OpenLogDirectoryEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, KeyCode.F), new ToggleFloorNumsEditorAction());
		keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Shift | KeyModifier.Control, KeyCode.F), new ShowCurrentFloorNumberEditorAction());
		for (KeyCode keyCode = KeyCode.Alpha0; keyCode <= KeyCode.Alpha9; keyCode++)
		{
			keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.None, keyCode), new AddNumberedEventEditorAction((int)(keyCode - 48)));
			keybindManager.RegisterKeybind(new EditorKeybind(KeyModifier.Control, keyCode), new SelectEventCategoryEditorAction((int)(keyCode - 48)));
		}
		KeyModifier[] array = new KeyModifier[2]
		{
			KeyModifier.None,
			KeyModifier.Shift
		};
		foreach (KeyModifier keyModifier in array)
		{
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.D), new CreateFloorWithCharOrAngleEditorAction(0f, 'R'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.W), new CreateFloorWithCharOrAngleEditorAction(0f, 'U'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.A), new CreateFloorWithCharOrAngleEditorAction(0f, 'L'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.S), new CreateFloorWithCharOrAngleEditorAction(0f, 'D'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.X), new CreateFloorWithCharOrAngleEditorAction(0f, 'D'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.E), new CreateFloorWithCharOrAngleEditorAction(0f, 'E'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.Q), new CreateFloorWithCharOrAngleEditorAction(0f, 'Q'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.Z), new CreateFloorWithCharOrAngleEditorAction(0f, 'Z'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.C), new CreateFloorWithCharOrAngleEditorAction(0f, 'C'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.Y), new CreateFloorWithCharOrAngleEditorAction(0f, 'T'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.T), new CreateFloorWithCharOrAngleEditorAction(0f, 'G'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.V), new CreateFloorWithCharOrAngleEditorAction(0f, 'F'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.B), new CreateFloorWithCharOrAngleEditorAction(0f, 'B'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.J), new CreateFloorWithCharOrAngleEditorAction(0f, 'J'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.H), new CreateFloorWithCharOrAngleEditorAction(0f, 'H'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.N), new CreateFloorWithCharOrAngleEditorAction(0f, 'N'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier, KeyCode.M), new CreateFloorWithCharOrAngleEditorAction(0f, 'M'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.D), new CreateFloorWithCharOrAngleEditorAction(0f, 'R'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.W), new CreateFloorWithCharOrAngleEditorAction(0f, 'U'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.A), new CreateFloorWithCharOrAngleEditorAction(0f, 'L'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.S), new CreateFloorWithCharOrAngleEditorAction(0f, 'D'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.X), new CreateFloorWithCharOrAngleEditorAction(0f, 'D'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.E), new CreateFloorWithCharOrAngleEditorAction(0f, 'E'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.Q), new CreateFloorWithCharOrAngleEditorAction(0f, 'Q'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.Z), new CreateFloorWithCharOrAngleEditorAction(0f, 'Z'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.C), new CreateFloorWithCharOrAngleEditorAction(0f, 'C'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.Y), new CreateFloorWithCharOrAngleEditorAction(0f, 'o'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.T), new CreateFloorWithCharOrAngleEditorAction(0f, 'q'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.V), new CreateFloorWithCharOrAngleEditorAction(0f, 'V'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.B), new CreateFloorWithCharOrAngleEditorAction(0f, 'Y'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.J), new CreateFloorWithCharOrAngleEditorAction(0f, 'p'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.H), new CreateFloorWithCharOrAngleEditorAction(0f, 'W'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.N), new CreateFloorWithCharOrAngleEditorAction(0f, 'x'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.M), new CreateFloorWithCharOrAngleEditorAction(0f, 'A'));
			keybindManager.RegisterKeybind(new EditorKeybind(keyModifier | KeyModifier.BackQuote, KeyCode.Tab), new CreateMidspinFloorEditorAction());
		}
	}

	private bool TryApplicationQuit()
	{
		if (unsavedChanges && !forceQuit)
		{
			if (playMode)
			{
				TogglePause();
			}
			CheckUnsavedChanges(delegate
			{
				ApplicationQuit();
			});
			return false;
		}
		return true;
	}

	private void ApplicationQuit()
	{
		forceQuit = true;
		Application.Quit();
	}

	public void CheckUnsavedChanges(Action quitLevelAction, bool skipCloseAnim = false)
	{
		if (unsavedChanges)
		{
			ShowPopup(show: true, PopupType.UnsavedChanges);
			this.quitLevelAction = quitLevelAction;
			closePopupImmediately = skipCloseAnim;
		}
		else
		{
			quitLevelAction();
		}
	}

	private void SaveAndQuit()
	{
		SaveLevel();
		if (!string.IsNullOrEmpty(ADOBase.levelPath))
		{
			DoQuitAction();
		}
	}

	private void DoQuitAction()
	{
		ShowPopup(show: false, PopupType.SaveBeforeSongImport, closePopupImmediately);
		quitLevelAction();
		closePopupImmediately = false;
	}

	public void TryQuitToMenu()
	{
		CheckUnsavedChanges(delegate
		{
			QuitToMenu();
		});
	}

	private void QuitToMenu()
	{
		Application.wantsToQuit -= TryApplicationQuit;
		if (GCS.customLevelPaths != null)
		{
			ADOBase.loader.LoadSceneWithTransition(WipeDirection.StartsFromRight, "scnCLS");
		}
		else
		{
			ADOBase.controller.QuitToMainMenu();
		}
	}

	private void RefreshFilenameText()
	{
		string text = (string.IsNullOrEmpty(ADOBase.levelPath) ? RDString.Get("editor.levelNotSaved") : Path.GetFileName(ADOBase.levelPath));
		if (unsavedChanges)
		{
			text += "*";
		}
		filenameText.text = text;
		filenameText.fontStyle = FontStyles.Normal;
	}

	private void LoadGameScene()
	{
		if (scnGame.instance != null)
		{
			fromScnGame = true;
		}
		else
		{
			SceneManager.LoadScene("scnGame", LoadSceneMode.Additive);
		}
	}

	private void Start()
	{
		customLevel = scnGame.instance;
		eventSystem = EventSystem.current;
		Application.wantsToQuit += TryApplicationQuit;
		ottoBlinkCounter = 0;
		LoadLevelEventSprites();
		LoadLevelCategorySprites();
		canvasScaler = GetComponent<CanvasScaler>();
		UpdateCanvasScalerResolution(Persistence.editorScale);
		spritesDefaultShader = Shader.Find("Sprites/Default");
		defaultLockColor = lockBackground.color;
		defaultLockIconColor = lockIcon.color;
		lineMaterial = new Material(Shader.Find("ADOFAI/ScrollingSprite"));
		lineMaterial.SetTexture("_MainTex", floorConnectorTex);
		lineMaterial.SetVector("_ScrollSpeed", new Vector2(-0.4f, 0f));
		lineMaterial.SetFloat("_Time0", 0f);
		GCS.filteredEvent = LevelEventType.None;
		floorLayerMask = LayerMask.GetMask("Floor");
		foregroundMask = LayerMask.GetMask("Foreground");
		handlesLayerMask = LayerMask.GetMask("Handles");
		camera = ADOBase.controller.camy.camobj;
		preparedAudioClips = new Dictionary<string, AudioClip>();
		prefsContainer.gameObject.SetActive(value: false);
		LoadEditorProperties();
		ottoAudioSrc.ignoreListenerPause = true;
		interfaceAudioSrc.ignoreListenerPause = true;
		floorButtonCanvas.gameObject.SetActive(value: false);
		backupTimer = Time.unscaledTime;
		if (Persistence.GetDisableRewindButton())
		{
			rewind.gameObject.SetActive(value: false);
			playPause.transform.position -= new Vector3(38.5f, 0f);
		}
		lastTimeStatsUploaded = Time.unscaledTime;
		keybindManager = new EditorKeybindManager(this);
		RegisterKeybinds();
		CloseAllPanels();
		TogglePause();
		UpdateSongAndLevelSettings();
		levelEventsPanel.HideAllInspectorTabs();
		levelEventsPanel.ShowInspector(show: false);
		propertyControlDecorationsList.OnItemSelected = OnDecorationSelected;
		propertyControlDecorationsList.OnAllItemsDeselected = OnDecorationAllItemsDeselected;
		propertyHelpImage.ScaleXY(0f, 0f);
		popupPanel.SetActive(value: false);
		buttonNew.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.newLevel") + "<color=#00000077> (" + RDEditorUtils.KeyComboToString(control: true, shift: false, KeyCode.N) + ")</color>";
		buttonOpen.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.open") + "<color=#00000077> (" + RDEditorUtils.KeyComboToString(control: true, shift: false, KeyCode.O) + ")</color>";
		buttonOpenURL.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.openURL") + "<color=#00000077> (" + RDEditorUtils.KeyComboToString(control: true, shift: false, KeyCode.U) + ")</color>";
		buttonSave.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.save") + "<color=#00000077> (" + RDEditorUtils.KeyComboToString(control: true, shift: false, KeyCode.S) + ")</color>";
		buttonSaveAs.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.saveAs") + "<color=#00000077> (" + RDEditorUtils.KeyComboToString(control: true, shift: true, KeyCode.S) + ")</color>";
		buttonPreferences.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.preferences") + "<color=#00000077> (" + RDEditorUtils.KeyComboToString(control: true, shift: true, KeyCode.P) + ")</color>";
		buttonHelp.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.help") + "<color=#00000077> (" + RDEditorUtils.KeyComboToString(control: true, shift: false, KeyCode.H) + ")</color>";
		buttonDiscord.transform.GetChild(0).GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.shareLevels");
		notificationOkButton.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.ok");
		selectingFloorIDText.text = RDString.Get("editor.selectingFloor");
		SetupHelpMenu();
		playPause.onClick.AddListener(delegate
		{
			Play();
		});
		rewind.onClick.AddListener(delegate
		{
			SelectFirstFloor();
			DeselectAnyUIGameObject();
		});
		buttonExit.onClick.AddListener(delegate
		{
			TryQuitToMenu();
		});
		buttonSettings.onClick.AddListener(delegate
		{
			ADOBase.controller.pauseMenu.Show(PauseMenu.Submenu.Settings, playSound: true);
		});
		buttonPreferences.onClick.AddListener(ShowPreferences);
		SetupFindFloorPanel();
		buttonFileActionDropdown.onClick.AddListener(delegate
		{
			if (!OpenDirectory(customLevel.levelPath))
			{
				ToggleFileActionsPanel();
			}
		});
		buttonNew.onClick.AddListener(delegate
		{
			NewLevel();
		});
		buttonOpen.onClick.AddListener(delegate
		{
			OpenLevel();
		});
		buttonOpenRecent.onClick.AddListener(delegate
		{
			OpenRecent(checkCtrl: true);
		});
		buttonOpenURL.onClick.AddListener(delegate
		{
			CheckUnsavedChanges(delegate
			{
				ShowPopup(show: true, PopupType.OpenURL);
			}, skipCloseAnim: true);
		});
		buttonSave.onClick.AddListener(delegate
		{
			SaveLevel();
		});
		buttonSaveAs.onClick.AddListener(delegate
		{
			SaveLevelAs();
		});
		buttonHelp.onClick.AddListener(delegate
		{
			ShowShortcutsPanel(show: true);
		});
		buttonCloseHelp.onClick.AddListener(delegate
		{
			ShowShortcutsPanel(show: false);
		});
		buttonDiscord.onClick.AddListener(delegate
		{
			OpenDiscord();
		});
		buttonD.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'R');
		});
		buttonE.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'E');
		});
		buttonW.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'U');
		});
		buttonQ.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'Q');
		});
		buttonA.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'L');
		});
		buttonZ.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'Z');
		});
		buttonS.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'D');
		});
		buttonC.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'C');
		});
		buttonT.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'T');
		});
		buttonG.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'G');
		});
		buttonF.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'F');
		});
		buttonB.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'B');
		});
		buttonBackQuoteT.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'o');
		});
		buttonBackQuoteY.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'q');
		});
		buttonBackQuoteV.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'V');
		});
		buttonBackQuoteB.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'Y');
		});
		buttonJ.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'J');
		});
		buttonH.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'H');
		});
		buttonN.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'N');
		});
		buttonM.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'M');
		});
		buttonBackQuoteJ.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'p');
		});
		buttonBackQuoteH.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'W');
		});
		buttonBackQuoteN.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'x');
		});
		buttonBackQuoteM.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(0f, 'A');
		});
		buttonSpace.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(FloorHelper.Transform(selectedFloors[0].floatDirection, FloorHelper.TransformOperation.ClockwiseRotation180), FloorHelper.Transform(selectedFloors[0].stringDirection, FloorHelper.TransformOperation.ClockwiseRotation180), pulseFloorButtons: true, fullSpin: true);
		});
		buttonTab.onClick.AddListener(delegate
		{
			CreateFloorWithCharOrAngle(999f, '!', pulseFloorButtons: true, fullSpin: true);
		});
		buttonAuto.onClick.AddListener(delegate
		{
			ToggleAuto();
		});
		buttonNextPage.onClick.AddListener(delegate
		{
			ShowNextPage();
		});
		buttonPrevPage.onClick.AddListener(delegate
		{
			ShowPrevPage();
		});
		buttonNoFail.onClick.AddListener(delegate
		{
			ToggleNoFail();
		});
		buttonUnlockKeyLimiter.onClick.AddListener(delegate
		{
			ToggleUnlockKeyLimiter();
		});
		floorButtonArbitrary.onEndEdit.AddListener(delegate
		{
			bool successful;
			float arbitraryAngleFromField = GetArbitraryAngleFromField(out successful);
			floorButtonArbitrary.text = (successful ? arbitraryAngleFromField.ToString() : "");
		});
		popupSaveOk.onClick.AddListener(delegate
		{
			ShowPopup(show: false);
		});
		popupSaveSaveAs.onClick.AddListener(delegate
		{
			SaveLevelAs();
			ShowPopup(show: false);
		});
		popupURLDownload.onClick.AddListener(delegate
		{
			StartLevelDownload();
		});
		popupURLCancel.onClick.AddListener(delegate
		{
			CancelDownload();
		});
		popupCopyrightAccept.onClick.AddListener(delegate
		{
			AcceptAgreement();
		});
		popupCopyrightReturn.onClick.AddListener(delegate
		{
			DeclineAgreement();
		});
		popupParamsCancel.onClick.AddListener(delegate
		{
			ShowPopup(show: false, PopupType.MissingExportParams);
		});
		popupMissingFilesCancel.onClick.AddListener(delegate
		{
			ShowPopup(show: false, PopupType.MissingFiles);
		});
		popupOggCancel.onClick.AddListener(delegate
		{
			ShowPopup(show: false);
		});
		popupOggConvert.onClick.AddListener(delegate
		{
			StartCoroutine(ConvertSoundToOggCo(soundConversionCallback));
		});
		popupOkOk.onClick.AddListener(delegate
		{
			if (popupOkCallback == null)
			{
				ShowPopup(show: false);
			}
			else
			{
				popupOkCallback();
			}
		});
		popupLargeOkOk.onClick.AddListener(delegate
		{
			if (popupOkCallback == null)
			{
				ShowPopup(show: false);
			}
			else
			{
				popupOkCallback();
			}
		});
		popupSpoilerOk.onClick.AddListener(delegate
		{
			if (popupOkCallback == null)
			{
				ShowPopup(show: false, PopupType.Spoiler);
			}
			else
			{
				popupOkCallback();
			}
		});
		popupConfirmOk.onClick.AddListener(delegate
		{
			ShowPopup(show: false);
			popupConfirmCallback?.Invoke();
			popupConfirmCallback = null;
		});
		popupConfirmCancel.onClick.AddListener(delegate
		{
			ShowPopup(show: false);
		});
		popupUnsavedChangesCancel.onClick.AddListener(delegate
		{
			ShowPopup(show: false);
		});
		popupUnsavedChangesDiscard.onClick.AddListener(delegate
		{
			DoQuitAction();
		});
		popupUnsavedChangesSave.onClick.AddListener(delegate
		{
			SaveAndQuit();
		});
		popupSteamDeckContinue.onClick.AddListener(delegate
		{
			steamDeckWarningPassed = true;
			Persistence.passedSteamDeckWarning = true;
			if (!Persistence.acceptedAgreement)
			{
				ShowPopup(show: true, PopupType.CopyrightWarning);
			}
			else
			{
				ShowPopup(show: false);
			}
		});
		popupSteamDeckExit.onClick.AddListener(delegate
		{
			QuitToMenu();
		});
		floorConnectors = new GameObject();
		floorConnectors.name = "Floor Connector Lines";
		lineMaterial.DOFloat(lineMaterial.GetFloat("_Time0") + 10f, "_Time0", 10f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental)
			.SetUpdate(isIndependentUpdate: true);
		RefreshFilenameText();
		if (RDC.runningOnSteamDeck && !Persistence.passedSteamDeckWarning)
		{
			ShowPopup(show: true, PopupType.SteamDeckWarning, skipAnim: true);
		}
		else if (!Persistence.acceptedAgreement)
		{
			ShowPopup(show: true, PopupType.CopyrightWarning, skipAnim: true);
		}
		DiscordController.instance?.UpdatePresence();
		GCS.difficulty = Persistence.GetDefaultDifficulty();
		ADOBase.uiController.difficultyContainer.gameObject.SetActive(value: false);
		editorDifficultySelector.gameObject.SetActive(value: true);
		autoImage.gameObject.SetActive(value: true);
		buttonNoFail.gameObject.SetActive(value: true);
		if (Persistence.showUnlockKeyLimiterButton)
		{
			buttonUnlockKeyLimiter.gameObject.SetActive(value: true);
			SetUnlockKeyLimiter(enable: true, byUser: false);
		}
		initialized = true;
		ShowEventPicker(show: false);
		if (!string.IsNullOrEmpty(levelToOpenOnLoad))
		{
			StartCoroutine(OpenLevelCo(levelToOpenOnLoad));
			levelToOpenOnLoad = null;
		}
		if (fromScnGame)
		{
			SwitchToEditMode(clsToEditor: true);
			SceneManager.SetActiveScene(SceneManager.GetSceneByName("scnEditor"));
		}
		scrCamera.instance.SetupRTCam(enable: false);
		static void LoadLevelCategorySprites()
		{
			if (GCS.eventCategoryIcons != null)
			{
				return;
			}
			GCS.eventCategoryIcons = new Dictionary<LevelEventCategory, Sprite>();
			foreach (object value in Enum.GetValues(typeof(LevelEventCategory)))
			{
				Sprite sprite = Resources.Load<Sprite>("LevelEditor/EventCategories/" + value);
				if (sprite != null)
				{
					GCS.eventCategoryIcons.Add((LevelEventCategory)value, sprite);
				}
			}
		}
		static void LoadLevelEventSprites()
		{
			if (GCS.levelEventIcons != null)
			{
				return;
			}
			GCS.levelEventIcons = new Dictionary<LevelEventType, Sprite>();
			foreach (object value2 in Enum.GetValues(typeof(LevelEventType)))
			{
				Sprite sprite = Resources.Load<Sprite>("LevelEditor/LevelEvents/" + value2);
				if (sprite != null)
				{
					GCS.levelEventIcons.Add((LevelEventType)value2, sprite);
				}
			}
		}
	}

	private void SetupHelpMenu()
	{
		foreach (KeyValuePair<EditorKeybind, List<EditorAction>> item2 in keybindManager)
		{
			EditorKeybind key = item2.Key;
			foreach (EditorAction item3 in item2.Value)
			{
				EditorTabKey sectionKey = item3.sectionKey;
				string description = item3.descriptionKey;
				if (item3.sectionKey != EditorTabKey.None && !string.IsNullOrEmpty(description))
				{
					List<EditorConstants.EditorKeyShortcut> list = shortcutsDictionary[sectionKey];
					int num = list.FindIndex((EditorConstants.EditorKeyShortcut e) => e.key == description);
					if (num == -1)
					{
						KeyModifier modifierMask = key.modifierMask;
						EditorConstants.EditorKeyShortcut item = new EditorConstants.EditorKeyShortcut(description, key.key, KeyCode.None, modifierMask.HasFlag(KeyModifier.Shift), modifierMask.HasFlag(KeyModifier.Control), modifierMask.HasFlag(KeyModifier.Alt), KeyModifier.None, key.ctrlIsCmd);
						list.Add(item);
					}
					else if (list[num].keyCode != key.key)
					{
						EditorConstants.EditorKeyShortcut value = list[num];
						value.otherKeyCode = key.key;
						value.otherKeyModifierMask = key.modifierMask;
						list[num] = value;
					}
				}
			}
		}
		shortcutsDictionary[EditorTabKey.Gameplay].Add(new EditorConstants.EditorKeyShortcut("ExitPlayMode", KeyCode.Escape, KeyCode.None, usingShift: false, usingCtrl: false, usingAlt: false));
		shortcutsDictionary[EditorTabKey.Gameplay].Add(new EditorConstants.EditorKeyShortcut("ExitPlayModeAtTile", KeyCode.Escape, KeyCode.None, usingShift: true, usingCtrl: false, usingAlt: false));
		shortcutsDictionary[EditorTabKey.SelectionAndDeletion].Add(new EditorConstants.EditorKeyShortcut("MultiselectTiles", KeyCode.Mouse0, KeyCode.None, usingShift: true, usingCtrl: false, usingAlt: false));
		shortcutsDictionary[EditorTabKey.AdvancedEditing].Add(new EditorConstants.EditorKeyShortcut("PositionTrack", KeyCode.Mouse0, KeyCode.None, usingShift: false, usingCtrl: false, usingAlt: true));
		shortcutsDictionary[EditorTabKey.AdvancedEditing].Add(new EditorConstants.EditorKeyShortcut("RotateTileMouse", KeyCode.Mouse1, KeyCode.None, usingShift: false, usingCtrl: false, usingAlt: false));
		shortcutsDictionary[EditorTabKey.AdvancedEditing].Add(new EditorConstants.EditorKeyShortcut("RotateTileMouseFine", KeyCode.Mouse1, KeyCode.None, usingShift: true, usingCtrl: false, usingAlt: false));
		shortcutsDictionary[EditorTabKey.BasicEditing].Add(new EditorConstants.EditorKeyShortcut("Zoom", (KeyCode)(-1), KeyCode.None, usingShift: false, usingCtrl: false, usingAlt: false));
		shortcutsDictionary[EditorTabKey.BasicEditing].Add(new EditorConstants.EditorKeyShortcut("SpeedIndicatorScroll", (KeyCode)(-1), KeyCode.None, usingShift: false, usingCtrl: true, usingAlt: false));
		shortcutsDictionary[EditorTabKey.BasicEditing].Add(new EditorConstants.EditorKeyShortcut("SpeedIndicatorScrollSmall", (KeyCode)(-1), KeyCode.None, usingShift: true, usingCtrl: true, usingAlt: false));
		shortcutsDictionary[EditorTabKey.Gameplay].Add(new EditorConstants.EditorKeyShortcut("TogglePause", KeyCode.Space, KeyCode.None, usingShift: false, usingCtrl: false, usingAlt: false));
		shortcutTitle.text = RDString.Get("editor.shortcuts.KeyboardShortcuts");
		int num2 = 0;
		foreach (KeyValuePair<EditorTabKey, List<EditorConstants.EditorKeyShortcut>> item4 in shortcutsDictionary)
		{
			EditorTabKey key2 = item4.Key;
			List<EditorConstants.EditorKeyShortcut> value2 = item4.Value;
			GameObject gameObject = UnityEngine.Object.Instantiate(shortcutTabPrefab);
			gameObject.transform.SetParent(shortcutTabsContainer, worldPositionStays: false);
			gameObject.name = key2.ToString();
			gameObject.GetComponentInChildren<TMP_Text>().text = RDString.Get($"editor.shortcuts.{key2}");
			int copyIndex = num2;
			gameObject.GetComponent<Button>().onClick.AddListener(delegate
			{
				ShowShortcutTab(copyIndex);
			});
			shortcutTabs.Add(gameObject);
			GameObject gameObject2 = UnityEngine.Object.Instantiate(shortcutContentPrefab);
			gameObject2.transform.SetParent(shortcutContentContainer, worldPositionStays: false);
			gameObject2.name = key2.ToString();
			shortcutContent.Add(gameObject2);
			Transform child = gameObject2.transform.GetChild(0).GetChild(0);
			if (RDString.isCJK)
			{
				child.GetComponent<VerticalLayoutGroup>().spacing = 10f;
			}
			foreach (EditorConstants.EditorKeyShortcut item5 in value2)
			{
				GameObject obj = UnityEngine.Object.Instantiate(shortcutTextPrefab);
				obj.transform.SetParent(child.transform, worldPositionStays: false);
				obj.name = item5.key;
				string text = RDEditorUtils.KeyComboToString(item5.usingCtrl, item5.usingShift, item5.usingAlt, item5.keyCode, item5.ctrlIsCmd);
				string text2 = string.Empty;
				if (item5.otherKeyCode != KeyCode.None)
				{
					EditorConstants.EditorKeyShortcut editorKeyShortcut = item5;
					editorKeyShortcut.usingCtrl = editorKeyShortcut.otherKeyModifierMask.HasFlag(KeyModifier.Control);
					editorKeyShortcut.usingShift = editorKeyShortcut.otherKeyModifierMask.HasFlag(KeyModifier.Shift);
					editorKeyShortcut.usingAlt = editorKeyShortcut.otherKeyModifierMask.HasFlag(KeyModifier.Alt);
					text2 = " " + RDString.Get("editor.shortcuts.or") + " " + RDEditorUtils.KeyComboToString(editorKeyShortcut.usingCtrl, editorKeyShortcut.usingShift, editorKeyShortcut.usingAlt, editorKeyShortcut.otherKeyCode, editorKeyShortcut.ctrlIsCmd);
				}
				obj.GetComponent<TMP_Text>().text = "<b>" + text + text2 + ":</b> " + RDString.Get("editor.shortcuts." + item5.key);
			}
			Canvas.ForceUpdateCanvases();
			num2++;
		}
		foreach (GameObject item6 in shortcutContent)
		{
			item6.gameObject.SetActive(value: false);
		}
	}

	private void UpdateSelectedFloor()
	{
		if (selectedFloors.Count != lastSelectedFloorsCount || (SelectionIsSingle() && lastSelectedFloor != selectedFloors[0]))
		{
			if (SelectionIsSingle())
			{
				lastSelectedFloor = selectedFloors[0];
			}
			lastSelectedFloorsCount = selectedFloors.Count;
			OnSelectedFloorChange();
		}
	}

	public void UpdateDecorationObjects()
	{
		customLevel.UpdateDecorationObjects();
		refreshDecSprites = false;
	}

	public void UpdateBackgroundSprites()
	{
		customLevel.UpdateBackgroundSprites();
		refreshBgSprites = false;
	}

	public void UpdateDecorationObject(LevelEvent e)
	{
		if (e.isFake)
		{
			e.ApplyPropertiesToRealEvents();
			return;
		}
		scrDecoration scrDecoration2 = allDecorations.Find((scrDecoration d) => d.sourceLevelEvent == e);
		if (scrDecoration2 != null)
		{
			scrDecoration2.Setup(e, out var _);
			scrDecoration2.UpdateHitbox();
			scrDecoration2.ShowHitboxBorders(show: true);
		}
	}

	public void GoToDecoration(LevelEvent levelEvent)
	{
		scrDecoration decoration = scrDecorationManager.GetDecoration(levelEvent);
		Vector2 v = decoration.transform.position.xy() - (ADOBase.controller.camy.transform.position.xy() - decoration.parallax.posCamAtStart.xy()) * decoration.parallax.multiplier;
		DoCameraJump(v.WithZ(-10f));
	}

	public void ShowPreferences()
	{
		CloseAllPanels();
		RectTransform obj = (RectTransform)prefsMenu.transform;
		obj.pivot = obj.pivot.WithY(-1f);
		prefsContainer.color = UnityEngine.Color.black.WithAlpha(0f);
		prefsContainer.gameObject.SetActive(value: true);
		prefsContainer.DOColor(UnityEngine.Color.black.WithAlpha(0.5f), 0.3f).SetUpdate(isIndependentUpdate: true);
		obj.DOPivotY(0.5f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(isIndependentUpdate: true);
	}

	public void HidePreferences()
	{
		prefsContainer.DOColor(UnityEngine.Color.black.WithAlpha(0f), 0.3f).SetUpdate(isIndependentUpdate: true).OnComplete(delegate
		{
			prefsContainer.gameObject.SetActive(value: false);
		});
		((RectTransform)prefsMenu.transform).DOPivotY(-1f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(isIndependentUpdate: true);
	}

	public void ShowParticleEditor(LevelEvent targetEvent)
	{
		ShowFileActionsPanel(show: false);
		particleEditor.SetEvent(targetEvent);
		RectTransform obj = (RectTransform)particleEditor.transform;
		obj.pivot = obj.pivot.WithY(-1f);
		particleEditorContainer.color = UnityEngine.Color.black.WithAlpha(0f);
		particleEditorContainer.gameObject.SetActive(value: true);
		particleEditorContainer.DOColor(UnityEngine.Color.black.WithAlpha(0.5f), 0.3f).SetUpdate(isIndependentUpdate: true);
		obj.DOPivotY(0.5f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(isIndependentUpdate: true);
	}

	public void HideParticleEditor()
	{
		particleEditorContainer.DOColor(UnityEngine.Color.black.WithAlpha(0f), 0.3f).SetUpdate(isIndependentUpdate: true).OnComplete(delegate
		{
			particleEditorContainer.gameObject.SetActive(value: false);
		});
		((RectTransform)particleEditor.transform).DOPivotY(-1f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(isIndependentUpdate: true);
	}

	public void UpdateImageLoadResult(string name, LoadResult loadResult)
	{
		if (isLoading)
		{
			if (!isUnauthorizedAccess && loadResult == LoadResult.UnauthorizedAccess)
			{
				isUnauthorizedAccess = true;
			}
			if (loadResult == LoadResult.UnauthorizedAccess || loadResult == LoadResult.MissingFile || loadResult == LoadResult.Error)
			{
				errorImageResult.Add(name, loadResult.ToString());
			}
		}
	}

	private void ShowImageLoadResult()
	{
		if (errorImageResult.Count == 0)
		{
			return;
		}
		string text = RDString.Get("editor.dialog.imageMessage") + " \n";
		string text2 = "";
		foreach (KeyValuePair<string, string> item in errorImageResult)
		{
			text2 = text2 + RDString.Get("editor.dialog.image" + item.Value) + ": " + item.Key + " \n";
		}
		text += text2;
		if (isUnauthorizedAccess)
		{
			text += RDString.Get("editor.dialog.imageUnauthorizedMessage");
		}
		ShowNotificationPopup(text);
	}

	private (PropertyControl_Toggle, PropertyControl_Text) GetFindPanelProps()
	{
		return ((PropertyControl_Toggle)findType.control, (PropertyControl_Text)findValue.control);
	}

	private void SetupFindFloorPanel()
	{
		var (typeControl, valueControl) = GetFindPanelProps();
		if (typeControl.buttonsDict != null)
		{
			return;
		}
		findFloorPanelTitle.text = RDString.Get("editor.findFloor.title");
		Property[] array = new Property[2] { findType, findValue };
		foreach (Property property in array)
		{
			property.label.text = RDString.Get("editor.findFloor." + property.key);
		}
		typeControl.buttonsDict = new Dictionary<string, Button>();
		Button[] componentsInChildren = findType.controlContainer.GetComponentsInChildren<Button>();
		for (int j = 0; j < componentsInChildren.Length; j++)
		{
			FindFloorType findFloorType = (FindFloorType)j;
			string curEnumStr = findFloorType.ToString();
			componentsInChildren[j].GetComponentInChildren<TMP_Text>().text = RDString.Get("enum.FindFloorType." + curEnumStr);
			typeControl.buttonsDict.Add(curEnumStr, componentsInChildren[j]);
			componentsInChildren[j].onClick.AddListener(delegate
			{
				typeControl.text = curEnumStr;
				typeControl.selected = curEnumStr;
			});
		}
		valueControl.onEndEdit.AddListener(ValidateFloorValue);
		findButton.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.findFloor");
		findButton.onClick.AddListener(delegate
		{
			SelectBookmarkOrFloor();
		});
		void ValidateFloorValue(string str)
		{
			int result = 0;
			int.TryParse(str, out result);
			valueControl.text = result.ToString();
		}
	}

	private void LateUpdate()
	{
		levelEditorCanvas.enabled = !playMode && !ADOBase.controller.pauseMenu.gameObject.activeSelf;
		scrUIController.instance.canvas.enabled = playMode;
		playPause.interactable = floors.Count != 1;
		playPauseIcon.sprite = ((!playMode) ? playButtonIcon : pauseButtonIcon);
		buttonToggleAngleInput.gameObject.SetActive(!isOldLevel);
		if (showingFileActions && controlPressChanged)
		{
			UpdateOpenRecentLabel();
		}
		FloorMesh.UpdateAllRequired();
	}

	private void Update()
	{
		if (scnGame.instance == null || ADOBase.controller.pauseMenu.gameObject.activeSelf)
		{
			return;
		}
		if (ADOBase.controller.paused && AsyncInputManager.isActive)
		{
			ADOBase.controller.UpdateInput();
		}
		thumbnailMaker.gameObject.SetActive(value: true);
		if (RDC.runningOnSteamDeck && !steamDeckWarningPassed && RDInput.cancelPress)
		{
			QuitToMenu();
		}
		UpdateSelectedFloor();
		OttoUpdate();
		if (refreshBgSprites)
		{
			UpdateBackgroundSprites();
		}
		if (refreshDecSprites)
		{
			UpdateDecorationObjects();
		}
		if (Input.GetKeyDown(KeyCode.Escape) && playMode)
		{
			SwitchToEditMode();
			return;
		}
		if (RDC.auto && Input.GetKeyDown(KeyCode.Space) && playMode && !Persistence.disableLevelEditorPause)
		{
			pausedInPlayMode = !ADOBase.controller.paused;
			buttonAuto.interactable = !pausedInPlayMode;
			blinkTimer?.TogglePause();
			ADOBase.controller.TogglePauseGame();
			return;
		}
		if (Time.unscaledTime > backupTimer + backupInterval)
		{
			backupTimer = Time.unscaledTime;
			SaveBackup();
		}
		if (eventSystem.currentInputModule is CustomStandaloneInputModule customStandaloneInputModule)
		{
			PointerEventData pointerData = customStandaloneInputModule.GetPointerData();
			bool flag = false;
			if (pointerData != null && pointerData.pointerCurrentRaycast.module != null)
			{
				GameObject gameObject = pointerData.pointerCurrentRaycast.gameObject;
				if (gameObject != null)
				{
					Transform parent = gameObject.transform;
					do
					{
						if (parent.TryGetComponent<ScrollRect>(out var _))
						{
							flag = true;
							break;
						}
						parent = parent.parent;
					}
					while (parent != null);
				}
			}
			if (prefsContainer.gameObject.activeInHierarchy || particleEditorContainer.gameObject.activeInHierarchy)
			{
				flag = true;
			}
			if (!flag)
			{
				Vector2 mouseScrollDelta = RDInput.mouseScrollDelta;
				if (Mathf.Abs(mouseScrollDelta.y) > 0.05f)
				{
					if (holdingControl)
					{
						if (mouseScrollDelta.y > 0f)
						{
							speedIndicator.MoreSpeed();
						}
						else if (mouseScrollDelta.y < 0f)
						{
							speedIndicator.LessSpeed();
						}
					}
					else
					{
						ZoomCamera(mouseScrollDelta.y, !Persistence.editorUseLegacyZoom);
					}
				}
			}
		}
		if (pausedInPlayMode)
		{
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2))
			{
				mousePosition0 = Input.mousePosition;
				cameraPositionAtDragStart = ADOBase.controller.camy.transform.position;
			}
			if (Input.GetMouseButton(0) || Input.GetMouseButton(2))
			{
				Vector3 vector = (Input.mousePosition - mousePosition0) / Screen.height * camera.orthographicSize * 2f;
				Vector3 delta = cameraPositionAtDragStart - vector;
				DragCamera(delta);
			}
		}
		if (playMode && shouldHideEditorUIForAutoplay)
		{
			controlsTip.text = "";
			ottoCanvas.gameObject.SetActive(value: false);
		}
		else
		{
			if (RDC.auto && !Persistence.disableLevelEditorPause)
			{
				controlsTip.text = (pausedInPlayMode ? RDString.Get("editor.ui.controlTip.paused") : RDString.Get("editor.ui.controlTip.unpaused"));
			}
			else
			{
				controlsTip.text = "";
			}
			ottoCanvas.gameObject.SetActive(value: true);
		}
		ADOBase.controller.UpdateErrorMeterVisibility();
		selectingFloorIDText.gameObject.SetActive(selectingFloorID);
		if (userIsEditingAnInputField)
		{
			selectingFloorID = false;
		}
		if (!selectingFloorIDTextMoving)
		{
			selectingFloorIDRectTransform.DOAnchorPosY(112.12f, 0.3f).SetEase(Ease.OutExpo).SetUpdate(isIndependentUpdate: true);
			selectingFloorIDTextMoving = true;
		}
		if (!selectingFloorID)
		{
			selectingFloorIDRectTransform.PositionY(0f);
		}
		if (!playMode)
		{
			HandleKeyboardActions();
			HandleMouseActions();
		}
	}

	public void ZoomCamera(float delta, bool anchorAtPointer = true, bool instant = false)
	{
		scrCamera cam = scrCamera.instance;
		float value = camUserSizeMultiplier - delta * scrollSpeed;
		camUserSizeMultiplier = Mathf.Clamp(value, 0.5f, 15f);
		if (playMode || !anchorAtPointer)
		{
			if (instant)
			{
				cam.userSizeMultiplier = camUserSizeMultiplier;
				return;
			}
			DOTween.To(() => cam.userSizeMultiplier, delegate(float x)
			{
				cam.userSizeMultiplier = x;
			}, camUserSizeMultiplier, 0.1f).SetEase(Ease.OutQuad).SetUpdate(isIndependentUpdate: true);
			return;
		}
		float transitionValue = 0f;
		Vector3 startMousePos = Input.mousePosition;
		float startSizeMultiplier = cam.userSizeMultiplier;
		_anchorZoomTween?.Kill();
		if (instant)
		{
			UpdateZoom(1f);
			return;
		}
		_anchorZoomTween = DOTween.To(() => transitionValue, UpdateZoom, 1f, 0.1f).SetEase(Ease.OutQuad).SetUpdate(isIndependentUpdate: true);
		void UpdateZoom(float progress)
		{
			Vector3 vector = cam.camobj.ScreenToWorldPoint(startMousePos);
			transitionValue = progress;
			cam.userSizeMultiplier = (camUserSizeMultiplier - startSizeMultiplier) * progress + startSizeMultiplier;
			cam.UpdateSize();
			Transform obj = cam.transform;
			Vector3 position = obj.position;
			position += vector - cam.camobj.ScreenToWorldPoint(startMousePos);
			startMousePos = Input.mousePosition;
			mousePosition0 = startMousePos;
			cameraPositionAtDragStart = position;
			obj.position = position;
		}
	}

	private void HandleKeyboardActions()
	{
		if (showingPopup)
		{
			if (new EditorKeybind(KeyModifier.None, KeyCode.Escape).IsPressed())
			{
				ShowPopup(show: false);
			}
		}
		else if (!userIsEditingAnInputField && !prefsContainer.gameObject.activeSelf && !particleEditorContainer.gameObject.activeSelf)
		{
			keybindManager.ExecutePressedActions();
		}
	}

	private void CycleObjectSelection()
	{
		if (ObjectsAtMouse() == null)
		{
			DeselectFloors();
			DeselectAllDecorations();
			return;
		}
		Transform transform = SmartObjectSelect().transform;
		Transform transform2 = transform;
		do
		{
			if (transform.TryGetComponent<scrFloor>(out var component))
			{
				if (holdingShift && !SelectionIsEmpty())
				{
					if (SelectionIsSingle())
					{
						MultiSelectFloors(selectedFloors[0], component, setSelectPoint: true);
						return;
					}
					_ = selectedFloors[0];
					selectedFloors.Last();
					if (component == multiSelectPoint)
					{
						SelectFloor(component);
					}
					else
					{
						MultiSelectFloors(component, multiSelectPoint);
					}
				}
				else if (selectingFloorID && !SelectionDecorationIsEmpty())
				{
					foreach (LevelEvent selectedDecoration in selectedDecorations)
					{
						if ((DecPlacementType)selectedDecoration["relativeTo"] == DecPlacementType.Tile)
						{
							selectedDecoration.floor = component.seqID;
						}
					}
					levelEventsPanel.UpdatePropertyText(selectedDecorations[0], "floor");
					selectingFloorID = false;
				}
				else
				{
					SelectFloor(component);
				}
				return;
			}
			if (!transform.parent.TryGetComponent<scrDecoration>(out var component2) || component2 is scrPrefabDecoration)
			{
				break;
			}
			LevelEvent sourceLevelEvent = component2.sourceLevelEvent;
			if (component2.GetVisible() && !sourceLevelEvent.locked && !component2.forceLock)
			{
				SelectDecoration(component2.sourceLevelEvent);
				return;
			}
			transform = SmartObjectSelect().transform;
		}
		while (transform2 != transform);
		DeselectFloors();
		DeselectAllDecorations();
	}

	private void HandleMouseActions()
	{
		bool key = Input.GetKey(KeyCode.BackQuote);
		if (Input.mouseScrollDelta != Vector2.zero)
		{
			ShowPropertyHelp(show: false);
		}
		bool flag = RDEditorUtils.CheckPointerInObject(fileActionsPanel);
		if (!showingFileActions && hoveringFileActions != flag)
		{
			(float, float) tuple = ((hoveringFileActions = flag) ? (1f, 0.5f) : (0.5f, 1f));
			fileIcon.DOFade(tuple.Item1, 0.25f).From(tuple.Item2).SetUpdate(isIndependentUpdate: true);
			fileArrow.DOFade(tuple.Item1, 0.25f).From(tuple.Item2).SetUpdate(isIndependentUpdate: true);
		}
		flag = RDEditorUtils.CheckPointerInObject(findFloorPanel);
		if (hoveringFindFloorPanel != flag)
		{
			(float, float) tuple2 = ((hoveringFindFloorPanel = flag) ? (1f, 0.5f) : (0.5f, 1f));
			findArrow.DOFade(tuple2.Item1, 0.25f).From(tuple2.Item2).SetUpdate(isIndependentUpdate: true);
		}
		floorButtonLeftRightCanvas.gameObject.SetActive((!holdingShift || !key) && !freeAngleMode);
		floorButtonExtraCanvas.gameObject.SetActive(holdingShift && !key && !freeAngleMode);
		floorButtonExtraBackQuoteCanvas.gameObject.SetActive(holdingShift && key && !freeAngleMode);
		floorButtonPrimaryCanvas.gameObject.SetActive(!holdingShift && !freeAngleMode);
		tabIndicator.SetKeyCode(holdingShift ? KeyCode.Space : KeyCode.Tab);
		bool flag2 = EventSystem.current.IsPointerOverGameObject() || (object)draggedEvIndicator != null;
		if (!userIsEditingAnInputField && !showingPopup && !holdingAlt && !holdingShift)
		{
			speedIndicator.gameObject.SetActive(holdingControl);
		}
		if (Input.GetMouseButtonUp(0))
		{
			if (draggedEvIndicator != null && draggedEvIndicator.editable)
			{
				if (dragging)
				{
					float num = (float)draggedEvIndicator.floor.entryangle;
					float num2 = (0f - draggedEvIndicator.transform.rotation.eulerAngles.z) * (MathF.PI / 180f);
					double angleMoved = scrMisc.GetAngleMoved(num, num2, !draggedEvIndicator.floor.isCCW);
					draggedEvIndicator.evnt["angleOffset"] = Mathf.Round((float)angleMoved * 57.29578f) % 360f;
					ApplyEventsToFloors();
					levelEventsPanel.ShowPanelOfEvent(draggedEvIndicator.evnt);
				}
				draggedEvIndicator.circle.color = UnityEngine.Color.white;
				draggedEvIndicator = null;
			}
			if (dragging)
			{
				if (pointerDownObjectType == PointerDownObjectType.Floor)
				{
					_ = Vector3.zero;
					if (!SelectionIsEmpty())
					{
						if (SelectionIsSingle())
						{
							bool num3 = selectedFloors[0].seqID == 0;
							LevelEvent levelEvent = new LevelEvent(selectedFloors[0].seqID, LevelEventType.PositionTrack);
							events.RemoveAll((LevelEvent levelEvent4) => levelEvent4.floor == selectedFloors[0].seqID && levelEvent4.eventType == LevelEventType.PositionTrack);
							Vector3 vector = Vector3.zero;
							if (!num3)
							{
								vector = floors[selectedFloors[0].seqID - 1].transform.position - floors[selectedFloors[0].seqID - 1].startPos;
							}
							Vector3 vector2 = floors[selectedFloors[0].seqID].transform.position - floors[selectedFloors[0].seqID].startPos - vector;
							if (!holdingShift)
							{
								dragGridSize = (holdingControl ? 0.707f : 0.5f);
								vector2 /= dragGridSize * ADOBase.controller.tileSize;
								vector2 = new Vector3(Mathf.Round(vector2.x), Mathf.Round(vector2.y), Mathf.Round(vector2.z));
								vector2 *= dragGridSize * ADOBase.controller.tileSize;
							}
							levelEvent["positionOffset"] = new Vector2(vector2.x / ADOBase.controller.tileSize, vector2.y / ADOBase.controller.tileSize);
							levelEvent.disabled["positionOffset"] = false;
							levelEvent["justThisTile"] = true;
							events.Add(levelEvent);
							int seqID = selectedFloors[0].seqID;
							RemakePath();
							Vector3 position = selectedFloors[0].transform.position;
							floorButtonCanvas.transform.position = new Vector2(position.x, position.y);
							levelEventsPanel.ShowInspector(show: true);
							levelEventsPanel.ShowPanel(levelEvent.eventType);
							SelectFloor(floors[seqID]);
						}
						else
						{
							new Vector2(0f, 0f);
							new Vector2(0f, 0f);
							bool num4 = selectedFloors.First().seqID == 0;
							bool flag3 = selectedFloors.Last().seqID == floors.Count - 1;
							LevelEvent levelEvent2 = new LevelEvent(selectedFloors.First().seqID, LevelEventType.PositionTrack);
							LevelEvent levelEvent3 = new LevelEvent(selectedFloors.Last().seqID + 1, LevelEventType.PositionTrack);
							events.RemoveAll((LevelEvent levelEvent4) => levelEvent4.floor == selectedFloors.First().seqID && levelEvent4.eventType == LevelEventType.PositionTrack);
							events.RemoveAll((LevelEvent levelEvent4) => levelEvent4.floor == selectedFloors.Last().seqID + 1 && levelEvent4.eventType == LevelEventType.PositionTrack);
							Vector3 vector3 = Vector3.zero;
							if (!num4)
							{
								vector3 = floors[selectedFloors.First().seqID - 1].transform.position - floors[selectedFloors.First().seqID - 1].startPos;
							}
							Vector3 vector4 = floors[selectedFloors.First().seqID].transform.position - floors[selectedFloors.First().seqID].startPos;
							Vector3 vector5 = Vector3.zero;
							Vector3 vector6 = Vector3.zero;
							if (!flag3)
							{
								vector5 = floors[selectedFloors.Last().seqID].transform.position - floors[selectedFloors.Last().seqID].startPos;
								vector6 = floors[selectedFloors.Last().seqID + 1].transform.position - floors[selectedFloors.Last().seqID + 1].startPos;
							}
							Vector3 vector7 = vector4 - vector3;
							if (!holdingShift)
							{
								dragGridSize = (holdingControl ? 0.707f : 0.5f);
								vector7 /= dragGridSize * ADOBase.controller.tileSize;
								vector7 = new Vector3(Mathf.Round(vector7.x), Mathf.Round(vector7.y), Mathf.Round(vector7.z));
								vector7 *= dragGridSize * ADOBase.controller.tileSize;
							}
							levelEvent2["positionOffset"] = new Vector2(vector7.x / ADOBase.controller.tileSize, vector7.y / ADOBase.controller.tileSize);
							levelEvent2.disabled["positionOffset"] = false;
							if (!flag3)
							{
								Vector3 vector8 = vector6 - vector5;
								if (!holdingShift)
								{
									dragGridSize = (holdingControl ? 0.707f : 0.5f);
									vector8 /= dragGridSize * ADOBase.controller.tileSize;
									vector8 = new Vector3(Mathf.Round(vector8.x), Mathf.Round(vector8.y), Mathf.Round(vector8.z));
									vector8 *= dragGridSize * ADOBase.controller.tileSize;
								}
								levelEvent3["positionOffset"] = new Vector2(vector8.x / ADOBase.controller.tileSize, vector8.y / ADOBase.controller.tileSize);
								levelEvent3.disabled["positionOffset"] = false;
							}
							events.Add(levelEvent2);
							if (!flag3)
							{
								events.Add(levelEvent3);
							}
							int seqID2 = selectedFloors.First().seqID;
							int seqID3 = selectedFloors.Last().seqID;
							RemakePath();
							MultiSelectFloors(floors[seqID2], floors[seqID3]);
							multiSelectPoint = floors[seqID2];
						}
					}
				}
				pointerDownObjectType = PointerDownObjectType.None;
			}
			else if (!flag2)
			{
				if (freeAngleMode)
				{
					CreateFloor(freeAngle * 57.29578f);
				}
				else
				{
					CycleObjectSelection();
				}
			}
			if (ADOBase.isEditingLevel)
			{
				Analytics.editorMakingTime += Time.unscaledDeltaTime;
			}
			else
			{
				Analytics.editorPlayingTime += Time.unscaledDeltaTime;
			}
			if (Time.unscaledTime > lastTimeStatsUploaded + 120f)
			{
				Analytics.UploadStatsToSteam();
				lastTimeStatsUploaded = Time.unscaledTime;
			}
		}
		bool flag4 = !SelectionIsEmpty() && SelectionIsSingle() && !isOldLevel;
		freeAngleMode = Input.GetMouseButton(1) && flag4;
		if (freeAngleMode)
		{
			scrFloor scrFloor2 = selectedFloors[0];
			float orthographicSize = camera.orthographicSize;
			float x = camera.aspect * orthographicSize;
			Vector3 to = Input.mousePosition / Screen.height * camera.orthographicSize * 2f + camera.transform.position.WithZ(0f) - new Vector3(x, orthographicSize) - scrFloor2.transform.position;
			freeAngle = Vector3.Angle(Vector3.up, to);
			if (to.x < 0f)
			{
				freeAngle = 360f - freeAngle;
			}
			if (!holdingShift)
			{
				freeAngle = Mathf.Round(freeAngle / 15f) * 15f;
			}
			freeAngle = (MathF.PI / 2f - freeAngle * (MathF.PI / 180f)) % (MathF.PI * 2f);
			float entryAngle = (MathF.PI / 2f - (float)scrFloor2.entryangle) % (MathF.PI * 2f);
			scrFloor2.floorRenderer.SetAngle(entryAngle, freeAngle);
			if (scrFloor2.seqID < floors.Count - 1)
			{
				scrFloor scrFloor3 = floors[scrFloor2.seqID + 1];
				float entryAngle2 = (freeAngle + MathF.PI) % (MathF.PI * 2f);
				float exitAngle = (MathF.PI / 2f - (float)scrFloor3.exitangle) % (MathF.PI * 2f);
				scrFloor3.floorRenderer.SetAngle(entryAngle2, exitAngle);
				Vector3 vector9 = new Vector3(ADOBase.controller.tileSize * Mathf.Cos(freeAngle), ADOBase.controller.tileSize * Mathf.Sin(freeAngle)) + scrFloor2.startPos - scrFloor3.startPos;
				for (int num5 = scrFloor2.seqID + 1; num5 < floors.Count; num5++)
				{
					scrFloor scrFloor4 = floors[num5];
					scrFloor4.transform.position = scrFloor4.startPos + scrFloor4.offsetPos + vector9;
				}
			}
		}
		if (Input.GetMouseButtonUp(1) && flag4)
		{
			ADOBase.lm.RefreshAngles();
			ApplyEventsToFloors();
		}
		if (playMode)
		{
			return;
		}
		if (Input.GetMouseButtonDown(2))
		{
			mousePosition0 = Input.mousePosition;
			cameraPositionAtDragStart = ADOBase.controller.camy.transform.position;
		}
		if (Input.GetMouseButtonUp(0))
		{
			ShowPropertyHelp(show: false);
			draggingGizmo?.holder.DragEnd();
		}
		if (Input.GetMouseButtonDown(0))
		{
			pointerDownObjectType = PointerDownObjectType.None;
			draggingGizmo = lastHoveredGizmo;
			mousePosition0 = Input.mousePosition;
			cameraPositionAtDragStart = ADOBase.controller.camy.transform.position;
			if (draggingGizmo == null)
			{
				GameObject[] array = ObjectsAtMouse();
				Transform transform = ((array != null) ? SmartObjectSelect(dontIncrement: true).transform : null);
				if (!flag2 && transform != null)
				{
					if (holdingAlt)
					{
						scrFloor component = (levelData.isOldLevel ? transform.parent : transform).GetComponent<scrFloor>();
						if (component != null && !SelectionIsEmpty())
						{
							if (SelectionIsSingle())
							{
								if (component.seqID == selectedFloors[0].seqID)
								{
									DragTilesStart();
								}
							}
							else if (component.seqID >= selectedFloors.First().seqID && component.seqID <= selectedFloors.Last().seqID)
							{
								DragTilesStart();
							}
						}
					}
					else
					{
						GameObject[] array2 = array;
						for (int num6 = 0; num6 < array2.Length; num6++)
						{
							Transform parent = array2[num6].transform.parent;
							parent.TryGetComponent<scrDecoration>(out var component2);
							if (component2 == null && parent.parent != null)
							{
								parent.parent.TryGetComponent<scrDecoration>(out component2);
							}
							if (component2 != null && !SelectionDecorationIsEmpty() && selectedDecorations.Contains(component2.sourceLevelEvent))
							{
								DragDecorationsStart();
								break;
							}
						}
					}
				}
				DragEventIndicatorStart(array);
			}
			else
			{
				DragTransformHandlesStart(draggingGizmo);
				DragDecorationsStart();
			}
			if ((flag2 && (draggingGizmo == null || !draggingGizmo.holder.forUI)) || freeAngleMode)
			{
				cancelDrag = true;
			}
			if (!RDEditorUtils.CheckPointerInObject(fileActionsPanel))
			{
				ShowFileActionsPanel(show: false);
			}
			if (!RDEditorUtils.CheckPointerInObject(shortcutsPanel))
			{
				ShowShortcutsPanel(show: false);
			}
			if (!RDEditorUtils.CheckPointerInObject(findFloorPanel) || RDEditorUtils.CheckPointerInObject(findArrow))
			{
				ShowFindFloorPanel(show: false);
			}
			if (!RDEditorUtils.CheckPointerInObject(findCommentPanel.gameObject) || RDEditorUtils.CheckPointerInObject(findCommentPanel.findArrow))
			{
				ShowFindCommentPanel(show: false);
			}
		}
		else if (Input.GetMouseButton(0))
		{
			Vector3 vector10 = Input.mousePosition - mousePosition0;
			Vector3 vector11 = vector10 / Screen.height * camera.orthographicSize * 2f;
			Vector3 vector12 = Vector3.zero;
			if (!cancelDrag)
			{
				vector12 = cameraPositionAtDragStart - vector11;
				if (draggedEvIndicator == null)
				{
					if (draggingGizmo == null)
					{
						if (pointerDownObjectType == PointerDownObjectType.Floor)
						{
							DragTiles(vector11);
						}
						else if (pointerDownObjectType == PointerDownObjectType.Decoration)
						{
							DragDecorations(vector11);
						}
						else if (pointerDownObjectType == PointerDownObjectType.None)
						{
							DragCamera(vector12);
						}
					}
					else
					{
						DragTransformHandles(vector11, vector10);
					}
				}
				else if (draggedEvIndicator.editable)
				{
					DragEventIndicator(vector11);
				}
			}
			if (vector12 != cameraPositionAtDragStart && !freeAngleMode)
			{
				dragging = true;
			}
		}
		else if (Input.GetMouseButton(2))
		{
			if (!cancelDrag)
			{
				Vector3 vector13 = (Input.mousePosition - mousePosition0) / Screen.height * camera.orthographicSize * 2f;
				Vector3 delta = cameraPositionAtDragStart - vector13;
				DragCamera(delta);
			}
		}
		else
		{
			dragging = false;
			cancelDrag = false;
		}
	}

	public void ChangeCursor()
	{
	}

	private int GetBookmarkInDirection(int seqID, bool isDirectionLeft = false, int moveAmount = 1)
	{
		List<int> list = (from e in levelData.levelEvents
			where e.eventType == LevelEventType.Bookmark && e.active
			select e.floor).ToList();
		list.Sort();
		if (!list.Contains(0))
		{
			list.Insert(0, 0);
		}
		if (!list.Contains(floors.Count - 1))
		{
			list.Add(floors.Count - 1);
		}
		if (!list.Contains(seqID))
		{
			int index = Math.Max(list.FindLastIndex((int b) => b < seqID) + 1, 0);
			list.Insert(index, seqID);
		}
		int index2 = Math.Max(Math.Min(list.FindIndex((int b) => b == seqID) + (isDirectionLeft ? (-moveAmount) : moveAmount), list.Count - 1), 0);
		return list[index2];
	}

	public void SelectBookmark(int bookmarkIndex, bool selectRelative)
	{
		int index = ((bookmarkIndex >= 0) ? (floors.Count - 1) : 0);
		if (selectRelative)
		{
			if (!SelectionIsEmpty())
			{
				int moveAmount = Math.Abs(bookmarkIndex);
				int seqID = selectedFloors[0].seqID;
				if (!SelectionIsSingle())
				{
					seqID = ((bookmarkIndex < 0) ? selectedFloors[0] : selectedFloors.Last()).seqID;
				}
				index = GetBookmarkInDirection(seqID, bookmarkIndex < 0, moveAmount);
			}
		}
		else
		{
			index = GetBookmarkInDirection(0, isDirectionLeft: false, bookmarkIndex);
		}
		SelectFloor(floors[index]);
	}

	private void ShowFindFloorPanel(bool show)
	{
		showingFindFloorPanel = show;
		RectTransform rt = findFloorPanel.GetComponent<RectTransform>();
		float endValue = (show ? findFloorPanelOpenHeight : findFloorPanelCloseHeight);
		rt.DOKill();
		rt.DOAnchorPosY(endValue, UIPanelEaseDur).SetUpdate(isIndependentUpdate: true).SetEase(UIPanelEaseMode)
			.OnComplete(delegate
			{
				if (!show)
				{
					rt.gameObject.SetActive(value: false);
				}
			});
		if (show)
		{
			rt.gameObject.SetActive(value: true);
			ShowShortcutsPanel(show: false);
		}
	}

	public void ShowFindCommentPanel(bool show)
	{
		showingFindCommentPanel = show;
		RectTransform component = findCommentPanel.gameObject.GetComponent<RectTransform>();
		GameObject obj = findCommentPanel.gameObject;
		float endValue = (show ? findCommentPanelOpenHeight : findCommentPanelCloseHeight);
		component.DOKill();
		component.DOAnchorPosY(endValue, UIPanelEaseDur).SetUpdate(isIndependentUpdate: true).SetEase(UIPanelEaseMode)
			.OnComplete(delegate
			{
				if (!show)
				{
					obj.SetActive(value: false);
				}
			});
		if (show)
		{
			obj.SetActive(value: true);
			((PropertyControl_Text)findCommentPanel.findValue.control).inputField.Select();
			ShowShortcutsPanel(show: false);
		}
	}

	public void ToggleFindFloorPanel()
	{
		ShowFindFloorPanel(!showingFindFloorPanel);
	}

	public void ToggleFindCommentPanel()
	{
		ShowFindCommentPanel(!showingFindCommentPanel);
	}

	public void CloseAllPanels(GameObject excludedPanel = null)
	{
		if (excludedPanel != fileActionsPanel)
		{
			ShowFileActionsPanel(show: false);
		}
		if (excludedPanel != shortcutsPanel)
		{
			ShowShortcutsPanel(show: false);
		}
		if (excludedPanel != findFloorPanel)
		{
			ShowFindFloorPanel(show: false);
		}
		if (excludedPanel != findCommentPanel.gameObject)
		{
			ShowFindCommentPanel(show: false);
		}
	}

	public void CloseAllInspectors()
	{
		settingsPanel.ShowInspector(show: false);
		levelEventsPanel.ShowInspector(show: false);
	}

	private void SelectBookmarkOrFloor()
	{
		(PropertyControl_Toggle, PropertyControl_Text) findPanelProps = GetFindPanelProps();
		PropertyControl_Toggle item = findPanelProps.Item1;
		PropertyControl_Text item2 = findPanelProps.Item2;
		int num = 0;
		if (int.TryParse(item2.text, out var result))
		{
			num = Math.Max(0, Math.Min(result, floors.Count - 1));
		}
		switch (Enum.Parse<FindFloorType>(item.text))
		{
		case FindFloorType.Floor:
			SelectFloor(floors[num]);
			break;
		case FindFloorType.Bookmark:
			SelectBookmark(num, selectRelative: false);
			break;
		}
	}

	public void EnableEvent(LevelEvent e, bool enabled)
	{
		e.active = enabled;
		ApplyEventsToFloors();
	}

	public void ShowEvent(LevelEvent e, bool visible)
	{
		e.visible = visible;
		UpdateEventVisibility(e);
	}

	public void ForceHideEvent(scrDecoration dec, bool forceHide)
	{
		dec.forceHide = forceHide;
		UpdateEventVisibility(dec.sourceLevelEvent);
	}

	public void LockEvent(LevelEvent e, bool locked)
	{
		e.locked = locked;
	}

	public void ForceLockEvent(scrDecoration dec, bool forceLock)
	{
		dec.forceLock = forceLock;
	}

	private void UpdateEventVisibility(LevelEvent e)
	{
		scrDecoration scrDecoration2 = allDecorations.Find((scrDecoration d) => d.sourceLevelEvent == e);
		scrDecoration2.SetVisible(e.visible && !scrDecoration2.forceHide);
	}

	public void SwitchToEditMode(bool clsToEditor = false)
	{
		GCS.speedTrialMode = false;
		GCS.editorQuickPitchedPlaying = false;
		GCS.practiceMode = false;
		GCS.currentSpeedTrial = 1f;
		pausedInPlayMode = false;
		buttonAuto.interactable = true;
		controlsTip.enabled = false;
		lastCustomFrameRate = 0f;
		wasUsingCustomFrameRate = false;
		inStrictlyEditingMode = true;
		mpWarned = false;
		scrFlash.FlashKill();
		ADOBase.conductor.KillAllSounds();
		ADOBase.conductor.gameObject.SetActive(value: false);
		ADOBase.conductor.song.pitch = (int)levelData.songSettings["pitch"] / 100;
		int num = Math.Min(ADOBase.controller.currentSeqID, floors.Count - 1);
		ADOBase.controller.camy.GetComponent<Grayscale>().enabled = false;
		scrUIController.instance.SetToTransparent();
		ADOBase.uiController.difficultyContainer.gameObject.SetActive(value: false);
		ADOBase.uiController.modifiersContainer.gameObject.SetActive(value: false);
		ADOBase.uiController.HideEndscreenLanterns();
		TogglePause(clsToEditor);
		ClearFloorGlows();
		int index = (holdingShift ? num : selectedFloorCached);
		if (decorationWasSelected)
		{
			using (new SaveStateScope(this))
			{
				for (int i = 0; i < lastSelectedDecorations.Count; i++)
				{
					SelectDecoration(lastSelectedDecorations[i], i == lastSelectedDecorations.Count - 1, showPanel: true, ignoreDeselection: true);
				}
			}
		}
		else
		{
			SelectFloor(floors[index], cameraJump: false);
		}
		ADOBase.controller.gameworld = true;
		DrawHolds(unfillHolds: true);
		DrawFloorOffsetLines();
		DrawFloorNums();
		DrawMultiPlanet();
		lineMaterial.DOFloat(lineMaterial.GetFloat("_Time0") + 10f, "_Time0", 10f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental)
			.SetUpdate(isIndependentUpdate: true);
		foreach (PlanetRenderer dummyPlanet in scrController.instance.dummyPlanets)
		{
			dummyPlanet.DisableParticles();
		}
		scrFloor scrFloor2 = floors[num];
		if (Vector2.Distance(camera.transform.position, scrFloor2.transform.position) > 3f)
		{
			Vector3 endValue = scrFloor2.transform.position.WithZ(-10f);
			camera.transform.DOKill();
			camera.transform.DOMove(endValue, 0.6f).SetUpdate(isIndependentUpdate: true).SetEase(Ease.OutExpo);
		}
		editorDifficultySelector.gameObject.SetActive(!RDC.noHud);
		editorDifficultySelector.SetChangeable(changeable: true);
		autoImage.gameObject.SetActive(!RDC.noHud);
		buttonNoFail.gameObject.SetActive(!RDC.noHud);
		buttonNoFail.interactable = true;
		buttonNoFail.GetComponent<RectTransform>().DOScale(Vector3.one, 0.25f).SetEase(Ease.OutQuad)
			.SetUpdate(isIndependentUpdate: true);
		buttonNoFail.GetComponent<UnityEngine.UI.Image>().color = (ADOBase.controller.noFail ? UnityEngine.Color.white : grayColor);
		buttonUnlockKeyLimiter.gameObject.SetActive(Persistence.showUnlockKeyLimiterButton);
		buttonUnlockKeyLimiter.interactable = true;
		scrDecorationManager obj = scrDecorationManager.instance;
		obj.ShowEmptyDecorations(show: true);
		obj.ToggleClickableBoxColliderForLevelEditor(value: true);
		Resources.UnloadUnusedAssets();
		scrController.checkpointsUsed = 0;
		ADOBase.controller.mistakesManager.Reset();
		scrController.instance.maximumUsedKeys = 0;
		ADOBase.playerManager.deadPlayersQueue.Clear();
		ADOBase.controller.endLevelInfo.endLevelType = EndLevelType.None;
		ADOBase.controller.endLevelInfo.newBestType = NewBestType.None;
		cacheSelectedEventIndex = 0;
		if (!Cursor.visible)
		{
			Cursor.visible = true;
		}
		ADOBase.controller.EnableHallOfMirrors(homEnabled: false);
		scrCamera.instance.SetupRTCam(enable: false);
	}

	private GameObject[] ObjectsAtMouse()
	{
		if (Time.frameCount != lastFrameUpdated)
		{
			lastFrameUpdated = Time.frameCount;
			Vector3 mousePosition = Input.mousePosition;
			Vector2 vector = camera.ScreenToWorldPoint(mousePosition).xy();
			float magnitude = scrFloor.LongDimensions.magnitude;
			List<Collider2D> list = new List<Collider2D>();
			List<GameObject> list2 = new List<GameObject>();
			foreach (scrFloor floor in floors)
			{
				if (Vector2.Distance(floor.transform.position.xy(), vector) <= magnitude)
				{
					if (floor.TryGetComponent<FloorMesh>(out var component))
					{
						component.GenerateCollider();
						component.polygonCollider.enabled = true;
						list.Add(component.polygonCollider);
					}
					if (floor.TryGetComponent<FloorSpriteRenderer>(out var _))
					{
						GameObject gameObject = floor.GenerateCollider();
						gameObject.name = "Parent";
						list.Add(gameObject.GetComponent<Collider2D>());
						list2.Add(gameObject);
					}
				}
			}
			RaycastHit2D[] first = Physics2D.RaycastAll(vector, Vector2.zero, 0f, foregroundMask);
			RaycastHit2D[] second = Physics2D.RaycastAll(vector, Vector2.zero, 0f, floorLayerMask);
			RaycastHit2D[] array = first.Concat(second).ToArray();
			if (array.Length == 0 || array == null)
			{
				foundObjects = null;
			}
			else
			{
				foundObjects = new GameObject[array.Length];
				for (int i = 0; i < array.Length; i++)
				{
					Collider2D collider = array[i].collider;
					foundObjects[i] = ((collider.name == "Parent") ? collider.transform.parent.gameObject : collider.gameObject);
				}
				Array.Sort(foundObjects, delegate(GameObject x, GameObject y)
				{
					Renderer componentInParent = x.GetComponentInParent<Renderer>();
					Renderer componentInParent2 = y.GetComponentInParent<Renderer>();
					Canvas componentInParent3 = x.GetComponentInParent<Canvas>();
					Canvas componentInParent4 = y.GetComponentInParent<Canvas>();
					int value = ((componentInParent != null) ? componentInParent.sortingOrder : ((!(componentInParent3 != null)) ? x.layer : componentInParent3.sortingOrder));
					return ((componentInParent2 != null) ? componentInParent2.sortingOrder : ((!(componentInParent4 != null)) ? y.layer : componentInParent4.sortingOrder)).CompareTo(value);
				});
			}
			foreach (Collider2D item in list)
			{
				item.enabled = false;
			}
			foreach (GameObject item2 in list2)
			{
				UnityEngine.Object.DestroyImmediate(item2);
			}
		}
		return foundObjects;
	}

	private TransformGizmo GizmoAtMouse()
	{
		Vector3 mousePosition = Input.mousePosition;
		RaycastHit2D[] array = Physics2D.RaycastAll(camera.ScreenToWorldPoint(mousePosition).xy(), Vector2.zero, 0f, handlesLayerMask);
		TransformGizmo component = null;
		if (array.Length != 0)
		{
			array?[0].collider.TryGetComponent<TransformGizmo>(out component);
		}
		return component;
	}

	private GameObject SmartObjectSelect(bool dontIncrement = false)
	{
		GameObject[] array = ObjectsAtMouse();
		if (dontIncrement)
		{
			if (array.Length == 0)
			{
				return null;
			}
			return array[0];
		}
		bool flag = false;
		if (previouslyFoundObjects != null)
		{
			if (previouslyFoundObjects.Length == array.Length)
			{
				for (int i = 0; i < array.Length; i++)
				{
					if (previouslyFoundObjects[i] != array[i])
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
			selectedObjectIndexOfBunch = 0;
			previouslyFoundObjects = array;
		}
		previouslyFoundObjects = array;
		GameObject result = array[selectedObjectIndexOfBunch];
		selectedObjectIndexOfBunch = (selectedObjectIndexOfBunch + 1) % array.Length;
		return result;
	}

	public void FilterEventType(LevelEventType eventType)
	{
		foreach (KeyValuePair<LevelEventCategory, List<LevelEventButton>> eventButton in eventButtons)
		{
			foreach (LevelEventButton item in eventButton.Value)
			{
				if (item.type != eventType)
				{
					item.ShowAsFiltered(filtered: false);
				}
			}
		}
		if (filteredEventType != eventType)
		{
			filteredEventType = eventType;
		}
		else
		{
			filteredEventType = LevelEventType.None;
		}
		GCS.filteredEvent = filteredEventType;
		ApplyEventsToFloors();
	}

	public void OnSelectedFloorChange()
	{
		if (SelectionIsSingle())
		{
			scrFloor scrFloor2 = selectedFloors[0];
			levelEventsPanel.ShowTabsForFloor(scrFloor2.seqID);
			UpdateFloorDirectionButtons(active: true);
			ShowEventPicker(show: true);
			ShowEventIndicators(scrFloor2);
		}
		else
		{
			if (SelectionIsEmpty() && !playMode)
			{
				DeselectFloors();
			}
			levelEventsPanel.ShowInspector(show: false);
			levelEventsPanel.HideAllInspectorTabs();
			ShowEventPicker(show: false);
			UpdateFloorDirectionButtons(active: false);
			DestroyEventIndicators();
			selectedObjectIndexOfBunch = 0;
		}
		ClearPopupBlocker();
	}

	public void ShowEventIndicators(scrFloor floor)
	{
		if (floor == null)
		{
			return;
		}
		DestroyEventIndicators();
		bool flag = false;
		int num = 0;
		foreach (LevelEvent item in events.FindAll((LevelEvent x) => x.floor == floor.seqID))
		{
			if (item.ContainsKey("angleOffset"))
			{
				UnityEngine.Object.Instantiate(ADOBase.gc.prefab_eventIndicator, floor.transform).GetComponent<EventIndicator>().Init(item, floor, num);
				flag = true;
				num++;
			}
		}
		if (flag)
		{
			EventCircle.gameObject.SetActive(value: true);
			EventCircle.fillClockwise = !floor.isCCW;
			EventCircle.transform.rotation = Quaternion.Euler(0f, 0f, (0f - (float)floor.entryangle) * 57.29578f);
			double angleMoved = scrMisc.GetAngleMoved((float)floor.entryangle, (float)floor.exitangle, !floor.isCCW);
			if (Mathf.Abs((float)angleMoved) <= Mathf.Pow(10f, -6f))
			{
				EventCircle.fillAmount = 1f;
			}
			else
			{
				EventCircle.fillAmount = (float)angleMoved / (MathF.PI * 2f);
			}
		}
	}

	private void DestroyEventIndicators()
	{
		EventCircle.gameObject.SetActive(value: false);
		GameObject[] array = GameObject.FindGameObjectsWithTag("EventIndicator");
		for (int i = 0; i < array.Length; i++)
		{
			UnityEngine.Object.Destroy(array[i]);
		}
	}

	public void ShowFileActionsPanel(bool show)
	{
		showingFileActions = show;
		float alpha = (show ? 1f : 0.6f);
		fileIcon.DOColor(UnityEngine.Color.white.WithAlpha(alpha), UIPanelEaseDur).SetUpdate(isIndependentUpdate: true);
		fileArrow.DOColor(UnityEngine.Color.white.WithAlpha(alpha), UIPanelEaseDur).SetUpdate(isIndependentUpdate: true);
		RectTransform component = fileActionsPanel.GetComponent<RectTransform>();
		float endValue = (show ? filePanelOpenWidth : filePanelCloseWidth);
		component.DOKill();
		component.DOAnchorPosY(endValue, UIPanelEaseDur).SetUpdate(isIndependentUpdate: true).SetEase(UIPanelEaseMode);
		fileArrow.sprite = (show ? fileSpriteUp : fileSpriteDown);
		if (show)
		{
			UpdateOpenRecentLabel();
		}
	}

	private void UpdateOpenRecentLabel()
	{
		string lastOpenedLevel = Persistence.GetLastOpenedLevel();
		string text2;
		if (File.Exists(lastOpenedLevel))
		{
			string text = Path.GetFileNameWithoutExtension(lastOpenedLevel);
			if (text == "main" || text == "level" || text.StartsWith("sub"))
			{
				string empty = string.Empty;
				try
				{
					empty = new DirectoryInfo(Path.GetDirectoryName(lastOpenedLevel)).Name;
				}
				catch (Exception arg)
				{
					Debug.Log($"Most recent name error: {arg}");
				}
				if (!empty.IsNullOrEmpty())
				{
					text = Path.Combine(empty, text);
				}
			}
			string value = "<color=#6495ED>" + text + "</color>";
			text2 = RDString.Get(controlPressed ? "editor.showRecent" : "editor.openRecent", new Dictionary<string, object> { { "file", value } });
		}
		else
		{
			text2 = RDString.Get("editor.noRecentFile");
		}
		string text3 = " <color=#00000077>(" + RDEditorUtils.KeyComboToString(control: true, shift: true, KeyCode.O) + ")</color>";
		buttonOpenRecent.GetComponentInChildren<TMP_Text>().text = text2 + text3;
	}

	public void ShowShortcutsPanel(bool show)
	{
		showingShortcuts = show;
		RectTransform component = shortcutsPanel.GetComponent<RectTransform>();
		float endValue = (show ? shortcutsPanelOpenHeight : shortcutsPanelCloseHeight);
		component.DOKill();
		component.DOAnchorPosY(endValue, UIPanelEaseDur).SetUpdate(isIndependentUpdate: true).SetEase(UIPanelEaseMode);
		if (show)
		{
			CloseAllPanels(shortcutsPanel);
			ShowEventPicker(show: false);
			CloseAllInspectors();
			ShowShortcutTab(0);
		}
	}

	public void ShowShortcutTab(int index)
	{
		for (int i = 0; i < shortcutTabs.Count; i++)
		{
			bool flag = i == index;
			GameObject obj = shortcutTabs[i];
			UnityEngine.UI.Image component = obj.GetComponent<UnityEngine.UI.Image>();
			TMP_Text componentInChildren = obj.GetComponentInChildren<TMP_Text>();
			component.color = (flag ? UnityEngine.Color.white : UnityEngine.Color.white.WithAlpha(0.13f));
			componentInChildren.color = (flag ? UnityEngine.Color.black : UnityEngine.Color.white);
			GameObject obj2 = shortcutContent[i];
			obj2.SetActive(flag);
			obj2.transform.GetChild(1).GetChild(0).GetComponent<Scrollbar>()
				.value = 1f;
		}
	}

	public void ToggleFileActionsPanel()
	{
		ShowFileActionsPanel(!showingFileActions);
	}

	public void ToggleShortcutsPanel()
	{
		ShowShortcutsPanel(!showingShortcuts);
	}

	private void ShowSelectedFloorsAsDeselected()
	{
		foreach (scrFloor selectedFloor in selectedFloors)
		{
			if (selectedFloor != null)
			{
				ShowDeselectedColor(selectedFloor);
			}
		}
	}

	public void DeselectFloors(bool skipSaving = false)
	{
		if (SelectionIsEmpty())
		{
			return;
		}
		using (new SaveStateScope(this, clearRedo: true, dataHasChanged: false, skipSaving))
		{
			DOTween.Kill("selectedColorTween");
			ShowSelectedFloorsAsDeselected();
			levelEventsPanel.HideAllInspectorTabs();
			selectedFloors.Clear();
			SelectFloorInfo();
			UpdateSelectedFloor();
		}
	}

	private void FloorWasCreatedOrDeleted(LevelEvent e, int floorID, bool created)
	{
		int offset = (created ? 1 : (-1));
		HandleKey("startTile");
		HandleKey("endTile");
		void HandleKey(string key)
		{
			if (e.TryGet<Tuple<int, TileRelativeTo>>(key, out var output))
			{
				if (output.Item2 == TileRelativeTo.Start && output.Item1 > floorID)
				{
					e[key] = new Tuple<int, TileRelativeTo>(output.Item1 + offset, TileRelativeTo.Start);
				}
				else if (output.Item2 == TileRelativeTo.End && output.Item1 <= floorID)
				{
					e[key] = new Tuple<int, TileRelativeTo>(output.Item1 - offset, TileRelativeTo.End);
				}
			}
		}
	}

	private bool DeleteFloor(int sequenceIndex, bool remakePath = true)
	{
		if (lockPathEditing)
		{
			return false;
		}
		using (new SaveStateScope(this))
		{
			int num = sequenceIndex - 1;
			int sequenceID = sequenceIndex;
			bool result = false;
			bool flag = num < levelData.pathData.Length;
			bool flag2 = num < levelData.angleData.Count;
			if (num >= 0 && ((isOldLevel && flag) || (!isOldLevel && flag2)))
			{
				foreach (LevelEvent item in events.FindAll((LevelEvent x) => x.floor == sequenceID))
				{
					if (EventHasBackgroundSprite(item))
					{
						refreshBgSprites = true;
					}
					if (item.IsDecoration)
					{
						refreshDecSprites = true;
					}
					FloorWasCreatedOrDeleted(item, sequenceID, created: false);
				}
				events.RemoveAll((LevelEvent x) => x.floor == sequenceID);
				OffsetFloorIDsInEvents(sequenceID, -1);
				if (isOldLevel)
				{
					RemoveCharFloor(num);
				}
				else
				{
					RemoveFloatFloor(num);
				}
				if (remakePath)
				{
					RemakePath();
				}
				result = true;
			}
			UpdateSelectedFloor();
			return result;
		}
	}

	private void RemoveCharFloor(int charIndex)
	{
		levelData.pathData = levelData.pathData.Remove(charIndex, 1);
	}

	private void RemoveFloatFloor(int floatIndex)
	{
		levelData.angleData.RemoveAt(floatIndex);
	}

	private void UpdateFloorDirectionButtons(bool active)
	{
		if (active)
		{
			PreviousFloor(selectedFloors[0]);
			double num = selectedFloors[0].entryangle * 57.295780181884766;
			float oppositeAngle = Mathf.Abs(450f - (float)num) % 360f;
			FloorDirectionButton[] array = floorDirectionButtons;
			foreach (FloorDirectionButton btn in array)
			{
				UpdateDirectionButton(btn, oppositeAngle);
			}
			floorButtonCanvas.transform.position = selectedFloors[0].transform.position;
		}
		floorButtonCanvas.gameObject.SetActive(active);
	}

	private void OffsetFloorIDsInEvents(int startFloorID, int offset)
	{
		List<LevelEvent>[] array = new List<LevelEvent>[2] { events, decorations };
		for (int i = 0; i < array.Length; i++)
		{
			foreach (LevelEvent item in array[i])
			{
				if (item.floor > startFloorID)
				{
					item.floor += offset;
				}
			}
		}
		refreshDecSprites = true;
	}

	private void UpdateDirectionButton(FloorDirectionButton btn, float oppositeAngle)
	{
		if (!(btn == null))
		{
			int num = 0;
			bool flag = false;
			switch (btn.btnType)
			{
			case FloorDirectionButtonType.D:
				num = 0;
				break;
			case FloorDirectionButtonType.BackQuoteJ:
				num = 15;
				break;
			case FloorDirectionButtonType.J:
				num = 30;
				break;
			case FloorDirectionButtonType.E:
				num = 45;
				break;
			case FloorDirectionButtonType.Y:
				num = 60;
				break;
			case FloorDirectionButtonType.BackQuoteY:
				num = 75;
				break;
			case FloorDirectionButtonType.W:
				num = 90;
				break;
			case FloorDirectionButtonType.BackQuoteT:
				num = 105;
				break;
			case FloorDirectionButtonType.T:
				num = 120;
				break;
			case FloorDirectionButtonType.Q:
				num = 135;
				break;
			case FloorDirectionButtonType.H:
				num = 150;
				break;
			case FloorDirectionButtonType.BackQuoteH:
				num = 165;
				break;
			case FloorDirectionButtonType.A:
				num = 180;
				break;
			case FloorDirectionButtonType.BackQuoteN:
				num = 195;
				break;
			case FloorDirectionButtonType.N:
				num = 210;
				break;
			case FloorDirectionButtonType.Z:
				num = 225;
				break;
			case FloorDirectionButtonType.V:
				num = 240;
				break;
			case FloorDirectionButtonType.BackQuoteV:
				num = 255;
				break;
			case FloorDirectionButtonType.S:
				num = 270;
				break;
			case FloorDirectionButtonType.BackQuoteB:
				num = 285;
				break;
			case FloorDirectionButtonType.B:
				num = 300;
				break;
			case FloorDirectionButtonType.C:
				num = 315;
				break;
			case FloorDirectionButtonType.M:
				num = 330;
				break;
			case FloorDirectionButtonType.BackQuoteM:
				num = 345;
				break;
			case FloorDirectionButtonType.Space:
				flag = true;
				break;
			case FloorDirectionButtonType.Tab:
				flag = true;
				break;
			}
			btn.delete = Mathf.Approximately(oppositeAngle, num) && !flag && !FloorIsMidspinOr360(selectedFloors[0]);
			btn.gameObject.SetActive(!btn.delete || selectedFloors[0].seqID != 0);
			btn.Init();
		}
	}

	private bool FloorPointsBackwards(char floorType)
	{
		if (floorType == '6' || floorType == '5' || floorType == '8' || floorType == '7')
		{
			return false;
		}
		PreviousFloor(selectedFloors[0]);
		FloorHelper.TryGetPathInDegrees(floorType, out var angle);
		angle %= 360f;
		double num = selectedFloors[0].entryangle * 57.295780181884766;
		return Mathf.Approximately(Mathf.Abs(450f - (float)num) % 360f, angle);
	}

	private bool FloorPointsBackwards(float floorAngle)
	{
		PreviousFloor(selectedFloors[0]);
		float b = floorAngle % 360f;
		double num = selectedFloors[0].entryangle * 57.295780181884766;
		return Mathf.Approximately(Mathf.Abs(450f - (float)num) % 360f, b);
	}

	private bool FloorIsMidspinOr360(scrFloor floor)
	{
		if (!scrMisc.ApproximatelyFloor(scrMisc.GetAngleMoved(floor.entryangle, floor.exitangle, !floor.isCCW), 6.2831854820251465))
		{
			return scrMisc.ApproximatelyFloor(floor.entryangle, floor.exitangle);
		}
		return true;
	}

	private void MoveCameraToFloor(scrFloor floor)
	{
		Vector3 endValue = floor.transform.localPosition.WithZ(-10f);
		camera.transform.DOMove(endValue, cameraSelectDuration).SetUpdate(isIndependentUpdate: true);
	}

	public void MoveCameraToDecoration(LevelEvent levelEvent)
	{
		scrDecoration decoration = scrDecorationManager.GetDecoration(levelEvent);
		Vector2 v = decoration.transform.position.xy() - (ADOBase.controller.camy.transform.position.xy() - decoration.parallax.posCamAtStart.xy()) * decoration.parallax.multiplier;
		DoCameraJump(v.WithZ(-10f));
	}

	private void DoCameraJump(Vector3 targetPos)
	{
		camera.transform.DOKill();
		camera.transform.DOMove(targetPos, 0.4f).SetUpdate(isIndependentUpdate: true).SetEase(Ease.OutCubic);
	}

	private void CreateFloor(char floorType, bool pulseFloorButtons = true, bool fullSpin = false)
	{
		if (!SelectionIsSingle())
		{
			return;
		}
		scrFloor scrFloor2 = selectedFloors[0];
		if (fullSpin && scrFloor2.seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			int seqID = scrFloor2.seqID;
			scrFloor scrFloor3 = PreviousFloor(scrFloor2);
			double num = scrFloor2.entryangle * 57.295780181884766;
			_ = Mathf.Abs(450f - (float)num) % 360f;
			if (FloorPointsBackwards(floorType) && !fullSpin && !FloorIsMidspinOr360(scrFloor2))
			{
				if (scrFloor3 != null)
				{
					int seqID2 = scrFloor2.seqID;
					if (DeleteFloor(seqID2))
					{
						SelectFloor(floors[seqID2 - 1]);
					}
					scrFloor floor = floors[seqID2 - 1];
					MoveCameraToFloor(floor);
				}
				return;
			}
			foreach (LevelEvent @event in events)
			{
				FloorWasCreatedOrDeleted(@event, seqID, created: true);
			}
			OffsetFloorIDsInEvents(seqID, 1);
			InsertCharFloor(seqID, floorType);
			scrFloor scrFloor4 = floors[seqID + 1];
			SelectFloor(scrFloor4);
			MoveCameraToFloor(scrFloor4);
			if (pulseFloorButtons)
			{
				Button button = null;
				switch (floorType)
				{
				case 'R':
					button = buttonD;
					break;
				case 'U':
					button = buttonW;
					break;
				case 'L':
					button = buttonA;
					break;
				case 'D':
					button = buttonS;
					break;
				case 'E':
					button = buttonE;
					break;
				case 'Q':
					button = buttonQ;
					break;
				case 'Z':
					button = buttonZ;
					break;
				case 'C':
					button = buttonC;
					break;
				case 'Y':
					button = buttonG;
					break;
				case 'T':
					button = buttonT;
					break;
				case 'V':
					button = buttonF;
					break;
				case 'B':
					button = buttonB;
					break;
				case 'H':
					button = buttonH;
					break;
				case 'J':
					button = buttonJ;
					break;
				case 'M':
					button = buttonM;
					break;
				case 'N':
					button = buttonN;
					break;
				}
				if (button != null)
				{
					Vector3 endValue = new Vector3(1f, 1f);
					button.transform.DOKill();
					button.transform.ScaleXY(floorButtonPulseSize);
					button.transform.DOScale(endValue, floorButtonPulseDuration).SetUpdate(isIndependentUpdate: true).SetEase(Ease.OutQuad);
				}
			}
		}
	}

	private void CreateFloor(float floorAngle, bool pulseFloorButtons = true, bool fullSpin = false)
	{
		if (!SelectionIsSingle())
		{
			return;
		}
		scrFloor scrFloor2 = selectedFloors[0];
		if (fullSpin && scrFloor2.seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			int seqID = scrFloor2.seqID;
			scrFloor scrFloor3 = PreviousFloor(scrFloor2);
			_ = floorAngle % 360f;
			double num = scrFloor2.entryangle * 57.295780181884766;
			_ = Mathf.Abs(450f - (float)num) % 360f;
			if (FloorPointsBackwards(floorAngle) && !fullSpin && !FloorIsMidspinOr360(scrFloor2))
			{
				if (scrFloor3 != null)
				{
					int seqID2 = scrFloor2.seqID;
					if (DeleteFloor(seqID2))
					{
						SelectFloor(floors[seqID2 - 1]);
					}
					scrFloor floor = floors[seqID2 - 1];
					MoveCameraToFloor(floor);
				}
			}
			else
			{
				OffsetFloorIDsInEvents(seqID, 1);
				InsertFloatFloor(seqID, floorAngle);
				scrFloor scrFloor4 = floors[seqID + 1];
				SelectFloor(scrFloor4);
				MoveCameraToFloor(scrFloor4);
				if (pulseFloorButtons)
				{
					Button button = null;
					if (floorAngle <= 180f)
					{
						if (floorAngle <= 60f)
						{
							if (floorAngle <= 30f)
							{
								if (floorAngle != 0f)
								{
									if (floorAngle == 30f)
									{
										button = buttonJ;
									}
								}
								else
								{
									button = buttonD;
								}
							}
							else if (floorAngle != 45f)
							{
								if (floorAngle == 60f)
								{
									button = buttonT;
								}
							}
							else
							{
								button = buttonE;
							}
						}
						else if (floorAngle <= 135f)
						{
							if (floorAngle != 90f)
							{
								if (floorAngle == 135f)
								{
									button = buttonQ;
								}
							}
							else
							{
								button = buttonW;
							}
						}
						else if (floorAngle != 150f)
						{
							if (floorAngle == 180f)
							{
								button = buttonA;
							}
						}
						else
						{
							button = buttonH;
						}
					}
					else if (floorAngle <= 270f)
					{
						if (floorAngle <= 225f)
						{
							if (floorAngle != 210f)
							{
								if (floorAngle == 225f)
								{
									button = buttonZ;
								}
							}
							else
							{
								button = buttonN;
							}
						}
						else if (floorAngle != 255f)
						{
							if (floorAngle == 270f)
							{
								button = buttonS;
							}
						}
						else
						{
							button = buttonF;
						}
					}
					else if (floorAngle <= 300f)
					{
						if (floorAngle != 285f)
						{
							if (floorAngle == 300f)
							{
								button = buttonB;
							}
						}
						else
						{
							button = buttonG;
						}
					}
					else if (floorAngle != 315f)
					{
						if (floorAngle == 330f)
						{
							button = buttonM;
						}
					}
					else
					{
						button = buttonC;
					}
					if (button != null)
					{
						Vector3 endValue = new Vector3(1f, 1f);
						button.transform.DOKill();
						button.transform.ScaleXY(floorButtonPulseSize);
						button.transform.DOScale(endValue, floorButtonPulseDuration).SetUpdate(isIndependentUpdate: true).SetEase(Ease.OutQuad);
					}
				}
			}
		}
		UpdateDecorationObjects();
	}

	public void CreateFloorWithCharOrAngle(float angle, char chara, bool pulseFloorButtons = true, bool fullSpin = false)
	{
		if (!lockPathEditing)
		{
			float angle2;
			if (isOldLevel && chara != '?')
			{
				CreateFloor(chara, pulseFloorButtons, fullSpin);
			}
			else if (FloorHelper.TryGetPathInDegrees(chara, out angle2))
			{
				CreateFloor(angle2);
			}
			else
			{
				CreateFloor(angle, pulseFloorButtons, fullSpin);
			}
		}
	}

	public void CreateArbitraryFloor()
	{
		if (isOldLevel)
		{
			return;
		}
		float arbitraryAngleFromField = GetArbitraryAngleFromField(out var successful);
		floorButtonArbitrary.text = (successful ? arbitraryAngleFromField.ToString(CultureInfo.InvariantCulture) : "");
		if (successful && (!successful || !(Mathf.Abs(arbitraryAngleFromField) < 0.01f) || arbitraryAngleFromField == 0f))
		{
			arbitraryAngleFromField = 180f - arbitraryAngleFromField;
			if (selectedFloors[0].isCCW)
			{
				arbitraryAngleFromField *= -1f;
			}
			char chara = '£';
			if (!useAbsoluteArbitraryAngle)
			{
				arbitraryAngleFromField += selectedFloors[0].floatDirection;
			}
			CreateFloorWithCharOrAngle(arbitraryAngleFromField, chara);
		}
	}

	private float GetArbitraryAngleFromField(out bool successful)
	{
		float result = 1f;
		if (!float.TryParse(floorButtonArbitrary.text, out result))
		{
			DataTable dataTable = new DataTable();
			try
			{
				result = RDEditorUtils.DecodeFloat(dataTable.Compute(floorButtonArbitrary.text, ""));
			}
			catch
			{
				successful = false;
				return 0f;
			}
		}
		result %= 360f;
		successful = true;
		return result;
	}

	public void ToggleAngleInputMode()
	{
		if (floorButtonContainer.activeSelf)
		{
			floorButtonContainer.SetActive(value: false);
			floorButtonArbitraryContainer.SetActive(value: true);
			buttonToggleAngleInput.GetComponent<UnityEngine.UI.Image>().sprite = show8DirectionSprite;
		}
		else
		{
			floorButtonContainer.SetActive(value: true);
			floorButtonArbitraryContainer.SetActive(value: false);
			buttonToggleAngleInput.GetComponent<UnityEngine.UI.Image>().sprite = showArbitraryAngleSprite;
		}
	}

	public void SwitchArbitraryMode()
	{
		useAbsoluteArbitraryAngle = !useAbsoluteArbitraryAngle;
	}

	public void CreateArbitraryMidspin()
	{
		if (isOldLevel)
		{
			return;
		}
		float arbitraryAngleFromField = GetArbitraryAngleFromField(out var successful);
		floorButtonArbitrary.text = (successful ? arbitraryAngleFromField.ToString() : "");
		if (successful && (!successful || !(Mathf.Abs(arbitraryAngleFromField) < 0.01f) || arbitraryAngleFromField == 0f))
		{
			arbitraryAngleFromField = 180f - arbitraryAngleFromField;
			if (selectedFloors[0].isCCW)
			{
				arbitraryAngleFromField *= -1f;
			}
			char chara = '£';
			if (!useAbsoluteArbitraryAngle)
			{
				arbitraryAngleFromField = ((selectedFloors[0].floatDirection != 999f) ? (arbitraryAngleFromField + selectedFloors[0].floatDirection) : (arbitraryAngleFromField + floors[Math.Max(0, selectedFloors[0].seqID - 1)].floatDirection));
			}
			if (Mathf.Abs(arbitraryAngleFromField) > 0.01f || arbitraryAngleFromField == 0f)
			{
				CreateFloorWithCharOrAngle(arbitraryAngleFromField, chara);
				CreateFloorWithCharOrAngle(999f, '!', pulseFloorButtons: true, fullSpin: true);
			}
		}
	}

	public void InsertCharFloor(int sequenceID, char floorType)
	{
		levelData.pathData = levelData.pathData.Insert(sequenceID, floorType.ToString());
		RemakePath();
	}

	public void InsertFloatFloor(int sequenceID, float floorAngle)
	{
		levelData.angleData.Insert(sequenceID, floorAngle);
		RemakePath();
	}

	public void RemakePath(bool applyEventsToFloors = true, bool remakeLevel = true)
	{
		customLevel.RemakePath(applyEventsToFloors, remakeLevel);
		DrawFloorOffsetLines();
		DrawHolds(!remakeLevel);
		DrawFloorNums();
		DrawMultiPlanet();
	}

	private void DrawFloorNums()
	{
		foreach (scrFloor floor in floors)
		{
			if (floor.enabled)
			{
				floor.editorNumText.gameObject.SetActive(showFloorNums && !playMode && !floor.isFake);
			}
		}
	}

	private void DrawHolds(bool unfillHolds = false)
	{
		customLevel.levelMaker.DrawHolds(unfillHolds);
	}

	private void DrawMultiPlanet()
	{
		if (customLevel.levelMaker.DrawMultiPlanet() > 3 && !mpWarned)
		{
			ShowPopup(show: true, PopupType.Spoiler, skipAnim: true);
			mpWarned = true;
		}
	}

	private void DrawFloorOffsetLines()
	{
		foreach (GameObject floorConnectorGO in floorConnectorGOs)
		{
			UnityEngine.Object.Destroy(floorConnectorGO);
		}
		floorConnectorGOs.Clear();
		int num = -1;
		int num2 = -2;
		Vector3 vector = Vector3.zero;
		foreach (LevelEvent @event in events)
		{
			if (@event.eventType != LevelEventType.PositionTrack || @event.floor <= 0)
			{
				continue;
			}
			num = @event.floor;
			if ((floors[num].prevfloor != null && floors[num].prevfloor.holdLength > -1) || @event.Get("justThisTile", defaultValue: false))
			{
				continue;
			}
			if (num != num2)
			{
				vector = new Vector2(0f, 0f);
			}
			Vector3 vector2 = @event.Get<Vector2>("positionOffset") * ADOBase.controller.tileSize;
			scrFloor obj = floors[num - 1];
			Vector3 position = obj.transform.position;
			float f = (float)obj.exitangle + MathF.PI / 2f;
			float x = ADOBase.controller.baseFloorDimensions.x;
			Vector2 vector3 = new Vector2((0f - x) * Mathf.Cos(f), x * Mathf.Sin(f));
			Vector3 vector4 = position + new Vector3(vector.x + vector3.x, vector.y + vector3.y, 0f);
			Vector3 vector5 = new Vector3(vector4.x + vector2.x, vector4.y + vector2.y, floors[num].transform.position.z);
			if (!(Vector3.Distance(vector4, vector5) < 0.05f))
			{
				GameObject gameObject = new GameObject();
				LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
				lineRenderer.positionCount = 2;
				lineRenderer.material = lineMaterial;
				lineRenderer.textureMode = LineTextureMode.Tile;
				if (@event.GetBool("editorOnly"))
				{
					lineRenderer.startColor = lineGreen;
					lineRenderer.endColor = lineYellow;
				}
				else
				{
					lineRenderer.startColor = linePurple;
					lineRenderer.endColor = lineBlue;
				}
				lineRenderer.SetPosition(0, vector4);
				lineRenderer.SetPosition(1, vector5);
				lineRenderer.startWidth = 0.1f;
				lineRenderer.endWidth = 0.1f;
				lineRenderer.name = "Floor connector";
				lineRenderer.transform.parent = floorConnectors.transform;
				floorConnectorGOs.Add(gameObject);
				Vector2 vector6 = (Vector2)@event["positionOffset"] * ADOBase.controller.tileSize;
				vector += new Vector3(vector6.x, vector6.y, 0f);
				num2 = num;
			}
		}
	}

	private void ClearAllFloorOffsets()
	{
		foreach (GameObject floorConnectorGO in floorConnectorGOs)
		{
			UnityEngine.Object.Destroy(floorConnectorGO);
		}
		floorConnectorGOs.Clear();
	}

	public void SelectFloor(scrFloor floorToSelect, bool cameraJump = true)
	{
		if (floorToSelect == null)
		{
			return;
		}
		using (new SaveStateScope(this, clearRedo: true, dataHasChanged: false))
		{
			DeselectAllFloors();
			DOTween.Kill("selectedColorTween");
			ShowSelectedFloorsAsDeselected();
			if ((bool)lastSelectedFloor)
			{
				ShowDeselectedColor(lastSelectedFloor);
			}
			selectedFloors.Clear();
			selectedFloors.Add(floorToSelect);
			SelectFloorInfo(floorToSelect);
			ShowSelectedColor(floorToSelect);
			if (cameraJump && Vector2.Distance(camera.transform.position, floorToSelect.transform.position) > 3f)
			{
				Vector3 targetPos = floorToSelect.transform.position.WithZ(-10f);
				DoCameraJump(targetPos);
			}
			UpdateSelectedFloor();
			selectingFloorID = false;
		}
	}

	public int[] SearchByComment(string text)
	{
		text = text.ToLowerInvariant();
		return (from i in (from e in levelData.levelEvents
				where e.eventType == LevelEventType.EditorComment && e.active && ((string)e["comment"]).ToLowerInvariant().Contains(text)
				select e.floor).Distinct()
			orderby i
			select i).ToArray();
	}

	public scrFloor SelectFirstFloor()
	{
		scrFloor scrFloor2 = floors[0];
		SelectFloor(scrFloor2);
		return scrFloor2;
	}

	public scrFloor NextFloor(scrFloor floor)
	{
		List<scrFloor> list = floors;
		int num = list.IndexOf(floor) + 1;
		if (num >= list.Count)
		{
			return null;
		}
		return list[num];
	}

	public scrFloor PreviousFloor(scrFloor floor)
	{
		List<scrFloor> list = floors;
		int num = list.IndexOf(floor) - 1;
		if (num < 0)
		{
			return null;
		}
		return list[num];
	}

	public void MultiSelectFloors(scrFloor startFloor, scrFloor endFloor, bool setSelectPoint = false)
	{
		DeselectAllFloors();
		if (floors.Count == 1)
		{
			return;
		}
		using (new SaveStateScope(this, clearRedo: true, dataHasChanged: false))
		{
			int num = floors.IndexOf(startFloor);
			int num2 = floors.IndexOf(endFloor);
			int num3;
			int num4;
			if (num2 - num > 0)
			{
				num3 = num;
				num4 = num2;
			}
			else
			{
				if (num2 - num >= 0)
				{
					SelectFloor(startFloor);
					UpdateSelectedFloor();
					return;
				}
				num3 = num2;
				num4 = num;
			}
			if (setSelectPoint)
			{
				multiSelectPoint = startFloor;
			}
			for (int i = num3; i <= num4; i++)
			{
				selectedFloors.Add(floors[i]);
			}
			SelectFloorInfo(selectedFloors);
			DOTween.Kill("selectedColorTween");
			if (selectedFloors.Count == 1)
			{
				DeselectAllFloors();
			}
			else
			{
				foreach (scrFloor selectedFloor in selectedFloors)
				{
					ShowSelectedColor(selectedFloor);
				}
			}
			UpdateSelectedFloor();
		}
	}

	private void DeselectAllFloors()
	{
		ShowSelectedFloorsAsDeselected();
		selectedFloors.Clear();
	}

	public bool SelectionIsEmpty()
	{
		return selectedFloors.Count == 0;
	}

	public bool SelectionIsSingle()
	{
		if (selectedFloors.Count == 1)
		{
			return selectedFloors[0] != null;
		}
		return false;
	}

	private void SelectFloorInfo(scrFloor floor)
	{
		SelectFloorInfo(new List<scrFloor> { floor });
	}

	private void SelectFloorInfo(List<scrFloor> floors = null)
	{
		if (floors == null)
		{
			floors = new List<scrFloor>();
		}
		findFloorSelectedInfo.text = RDString.Get("editor.findFloor.currFloor", new Dictionary<string, object>
		{
			{
				"seqID",
				(floors.Count > 0) ? floors[0].seqID.ToString() : "None"
			},
			{ "floorCount", floors.Count }
		});
	}

	public void SelectDecoration(int itemIndex, bool jumpToDecoration = true, bool showPanel = true, bool ignoreDeselection = false, bool ignoreAdjustRect = false)
	{
		LevelEvent sourceLevelEvent = scrDecorationManager.GetDecoration(itemIndex).sourceLevelEvent;
		if (sourceLevelEvent != null)
		{
			SelectDecoration(sourceLevelEvent, jumpToDecoration, showPanel, ignoreDeselection, ignoreAdjustRect);
		}
	}

	public void SelectDecoration(LevelEvent levelEvent, bool jumpToDecoration = true, bool showPanel = true, bool ignoreDeselection = false, bool ignoreAdjustRect = false)
	{
		using (new SaveStateScope(this, clearRedo: false, dataHasChanged: false))
		{
			bool flag = selectedDecorations.Contains(levelEvent);
			if (flag && holdingControl && !ignoreDeselection)
			{
				DeselectDecoration(levelEvent);
				return;
			}
			if (!holdingShift && !holdingControl && !ignoreDeselection)
			{
				DeselectAllDecorations();
				DeselectFloors();
				flag = false;
			}
			if (!(scrDecorationManager.GetDecoration(levelEvent) == null))
			{
				if (!flag)
				{
					selectedDecorations.Add(levelEvent);
				}
				if (jumpToDecoration && !Persistence.disableCameraDecorationFocus)
				{
					ADOBase.editor.MoveCameraToDecoration(levelEvent);
				}
				scrDecorationManager.instance.ShowSelectionBorders(levelEvent);
				bool enable = SelectionDecorationIsSingle();
				decPivot.UpdatePivotCrossImage(enable);
				if (selectedDecorations.Count <= 1)
				{
					decTransformGizmo.Setup(levelEvent);
				}
				else
				{
					decTransformGizmo.UpdateGizmosVisibility();
				}
				int decorationIndex = scrDecorationManager.GetDecorationIndex(levelEvent);
				if (showPanel)
				{
					levelEventsPanel.ShowInspector(show: true, forceAction: true);
					levelEventsPanel.ShowPanel(levelEvent.eventType);
				}
				propertyControlDecorationsList.lastSelectedIndex = decorationIndex;
				propertyControlDecorationsList.RefreshItemsList();
				if (!ignoreAdjustRect)
				{
					propertyControlDecorationsList.RefreshScrollRectPosition(levelEvent);
				}
				if (propertyControlDecorationsList.OnItemSelected != null)
				{
					propertyControlDecorationsList.OnItemSelected(levelEvent);
				}
				selectingFloorID = false;
			}
		}
	}

	public void DeselectDecoration(LevelEvent levelEvent)
	{
		using (new SaveStateScope(this))
		{
			if (selectedDecorations.Count <= 1)
			{
				DeselectAllDecorations();
			}
			else if (!(scrDecorationManager.GetDecoration(levelEvent) == null))
			{
				scrDecorationManager.instance.ShowSelectionBorders(levelEvent, show: false);
				decTransformGizmo.UpdateGizmosVisibility();
				selectedDecorations.Remove(levelEvent);
				LevelEvent levelEvent2 = selectedDecorations[selectedDecorations.Count - 1];
				SelectDecoration(levelEvent2, jumpToDecoration: false, showPanel: true, ignoreDeselection: true);
			}
		}
	}

	public void DeselectAllDecorations()
	{
		if (SelectionDecorationIsEmpty())
		{
			return;
		}
		using (new SaveStateScope(this, clearRedo: false, dataHasChanged: false))
		{
			levelEventsPanel.ShowInspector(show: false);
			scrDecorationManager.instance.ClearDecorationBorders();
			int count = selectedDecorations.Count;
			selectedDecorations.Clear();
			propertyControlDecorationsList.RefreshItemsList();
			decPivot.UpdatePivotCrossImage(enable: false);
			decTransformGizmo.UpdateGizmosVisibility();
			if (count > 0)
			{
				levelEventsPanel.HideAllInspectorTabs();
				if (propertyControlDecorationsList.OnAllItemsDeselected != null)
				{
					propertyControlDecorationsList.OnAllItemsDeselected();
				}
			}
			selectingFloorID = false;
		}
	}

	public bool SelectionDecorationIsEmpty()
	{
		return selectedDecorations.Count == 0;
	}

	public bool SelectionDecorationIsSingle()
	{
		if (selectedDecorations.Count == 1)
		{
			return selectedDecorations[0] != null;
		}
		return false;
	}

	public void FlipFloor(scrFloor floor, bool horizontal = true, bool remakePath = true)
	{
		int seqID = floor.seqID;
		if (seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			if (isOldLevel)
			{
				char c = ((!horizontal) ? FloorHelper.Transform(floor.stringDirection, FloorHelper.TransformOperation.VerticalFlip) : FloorHelper.Transform(floor.stringDirection, FloorHelper.TransformOperation.HorizontalFlip));
				levelData.pathData = levelData.pathData.Remove(floor.seqID - 1, 1).Insert(floor.seqID - 1, c.ToString());
			}
			else
			{
				float item = ((!horizontal) ? FloorHelper.Transform(floor.floatDirection, FloorHelper.TransformOperation.VerticalFlip) : FloorHelper.Transform(floor.floatDirection, FloorHelper.TransformOperation.HorizontalFlip));
				levelData.angleData.RemoveAt(floor.seqID - 1);
				levelData.angleData.Insert(floor.seqID - 1, item);
			}
			if (remakePath)
			{
				RemakePath();
				SelectFloor(floors[seqID]);
			}
		}
	}

	public void FlipSelection(bool horizontal = true)
	{
		int seqID = selectedFloors[0].seqID;
		int seqID2 = selectedFloors.Last().seqID;
		int seqID3 = multiSelectPoint.seqID;
		if (seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			foreach (scrFloor selectedFloor in selectedFloors)
			{
				FlipFloor(selectedFloor, horizontal, remakePath: false);
			}
			RemakePath();
			MultiSelectFloors(floors[seqID], floors[seqID2]);
			multiSelectPoint = floors[seqID3];
		}
	}

	public void RotateFloor(scrFloor floor, bool CW = true, bool remakePath = true)
	{
		if (lockPathEditing)
		{
			return;
		}
		int seqID = floor.seqID;
		if (seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			if (isOldLevel)
			{
				char c = FloorHelper.Transform(floor.stringDirection, (!CW) ? FloorHelper.TransformOperation.ClockwiseRotation270 : FloorHelper.TransformOperation.ClockwiseRotation90);
				levelData.pathData = levelData.pathData.Remove(floor.seqID - 1, 1);
				levelData.pathData = levelData.pathData.Insert(floor.seqID - 1, c.ToString());
			}
			else
			{
				float item = FloorHelper.Transform(floor.floatDirection, (!CW) ? FloorHelper.TransformOperation.ClockwiseRotation270 : FloorHelper.TransformOperation.ClockwiseRotation90);
				levelData.angleData.RemoveAt(floor.seqID - 1);
				levelData.angleData.Insert(floor.seqID - 1, item);
			}
			if (remakePath)
			{
				RemakePath();
				SelectFloor(floors[seqID]);
			}
		}
	}

	public void RotateSelection(bool CW = true)
	{
		int seqID = selectedFloors[0].seqID;
		int seqID2 = selectedFloors.Last().seqID;
		int seqID3 = multiSelectPoint.seqID;
		if (seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			foreach (scrFloor selectedFloor in selectedFloors)
			{
				RotateFloor(selectedFloor, CW, remakePath: false);
			}
			RemakePath();
			MultiSelectFloors(floors[seqID], floors[seqID2]);
			multiSelectPoint = floors[seqID3];
		}
	}

	public void RotateFloor180(scrFloor floor, bool remakePath = true)
	{
		int seqID = floor.seqID;
		if (seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			if (isOldLevel)
			{
				char c = FloorHelper.Transform(floor.stringDirection, FloorHelper.TransformOperation.ClockwiseRotation180);
				levelData.pathData = levelData.pathData.Remove(floor.seqID - 1, 1);
				levelData.pathData = levelData.pathData.Insert(floor.seqID - 1, c.ToString());
			}
			else
			{
				float item = FloorHelper.Transform(floor.floatDirection, FloorHelper.TransformOperation.ClockwiseRotation180);
				levelData.angleData.RemoveAt(floor.seqID - 1);
				levelData.angleData.Insert(floor.seqID - 1, item);
			}
			if (remakePath)
			{
				RemakePath();
				SelectFloor(floors[seqID]);
			}
		}
	}

	public void RotateSelection180()
	{
		int seqID = selectedFloors[0].seqID;
		int seqID2 = selectedFloors.Last().seqID;
		int seqID3 = multiSelectPoint.seqID;
		if (seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			foreach (scrFloor selectedFloor in selectedFloors)
			{
				RotateFloor180(selectedFloor, remakePath: false);
			}
			RemakePath();
			MultiSelectFloors(floors[seqID], floors[seqID2]);
			multiSelectPoint = floors[seqID3];
		}
	}

	private FloorData CopyOfFloor(scrFloor floor, bool selectedEventOnly = false, bool allSameTypeEvents = false)
	{
		char stringDir;
		float angle;
		if (floor.seqID == 0)
		{
			stringDir = 'R';
			angle = 0f;
		}
		else if (isOldLevel)
		{
			stringDir = floor.stringDirection;
			FloorHelper.TryGetPathInDegrees(floor.stringDirection, out angle);
		}
		else
		{
			stringDir = 'R';
			angle = floor.floatDirection;
		}
		List<LevelEvent> list = new List<LevelEvent>();
		List<LevelEvent> list2 = new List<LevelEvent>();
		if (selectedEventOnly && levelEventsPanel.selectedEventType != LevelEventType.None)
		{
			if (allSameTypeEvents)
			{
				list2 = events.FindAll((LevelEvent x) => x.floor == floor.seqID && x.eventType == levelEventsPanel.selectedEventType);
			}
			else
			{
				list2.Add(levelEventsPanel.selectedEvent);
			}
		}
		else
		{
			list2 = events.FindAll((LevelEvent x) => x.floor == floor.seqID);
		}
		foreach (LevelEvent item in list2)
		{
			list.Add(CopyEvent(item));
		}
		List<LevelEvent> list3 = new List<LevelEvent>();
		if (!selectedEventOnly)
		{
			foreach (scrDecoration allDecoration in allDecorations)
			{
				LevelEvent sourceLevelEvent = allDecoration.sourceLevelEvent;
				if ((DecPlacementType)sourceLevelEvent["relativeTo"] == DecPlacementType.Tile && sourceLevelEvent.floor == floor.seqID)
				{
					list3.Add(CopyEvent(sourceLevelEvent, sourceLevelEvent.floor));
				}
			}
		}
		return new FloorData(stringDir, angle, list, list3);
	}

	private LevelEvent CopyEvent(LevelEvent eventToCopy, int floor = -1)
	{
		LevelEvent levelEvent = eventToCopy.CopyShallow();
		levelEvent.floor = floor;
		return levelEvent;
	}

	public void CopyTrackColor(int floor)
	{
		List<LevelEvent> list = events.FindAll((LevelEvent x) => x.floor == floor && x.eventType == LevelEventType.ColorTrack);
		if (list.Any())
		{
			copiedTrackColor = CopyEvent(list[0]);
			ShowNotification("Copied Track Color");
		}
		else
		{
			ShowNotification("No Track Color To Copy");
		}
	}

	public void PasteTrackColor(int id)
	{
		if (id == 0 || copiedTrackColor == null)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			events.RemoveAll((LevelEvent x) => x.floor == id && x.eventType == LevelEventType.ColorTrack);
			events.Add(CopyEvent(copiedTrackColor, id));
			ApplyEventsToFloors();
			scrFloor scrFloor2 = floors[id];
			SelectFloor(scrFloor2);
			levelEventsPanel.ShowTabsForFloor(selectedFloors[0].seqID);
			levelEventsPanel.selectedEventType = LevelEventType.ColorTrack;
			levelEventsPanel.ShowPanel(LevelEventType.ColorTrack);
			ShowEventIndicators(scrFloor2);
			ShowNotification("Paste Track Color");
		}
	}

	public void PasteTrackColorSingleTile(int id)
	{
		if (id == 0 || copiedTrackColor == null)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			List<LevelEvent> list = new List<LevelEvent>();
			int i;
			for (i = id; i > 0; i--)
			{
				list = events.FindAll((LevelEvent x) => x.floor == i && x.eventType == LevelEventType.ColorTrack);
				if (list.Any())
				{
					break;
				}
			}
			if (list.Any())
			{
				previousTrackColor = CopyEvent(list[0]);
				events.RemoveAll((LevelEvent x) => x.floor == id && x.eventType == LevelEventType.ColorTrack);
				events.Add(CopyEvent(copiedTrackColor, id));
				if (id < floors.Count - 2 && !events.FindAll((LevelEvent x) => x.floor == id + 1 && x.eventType == LevelEventType.ColorTrack).Any())
				{
					events.Add(CopyEvent(previousTrackColor, id + 1));
				}
				ApplyEventsToFloors();
				scrFloor scrFloor2 = floors[id];
				SelectFloor(scrFloor2);
				levelEventsPanel.ShowTabsForFloor(selectedFloors[0].seqID);
				levelEventsPanel.selectedEventType = LevelEventType.ColorTrack;
				levelEventsPanel.ShowPanel(LevelEventType.ColorTrack);
				ShowEventIndicators(scrFloor2);
				ShowNotification("Paste Track Color (single tile)");
			}
		}
	}

	public void CopyHitSound(int floor)
	{
		List<LevelEvent> list = events.FindAll((LevelEvent x) => x.floor == floor && x.eventType == LevelEventType.SetHitsound);
		if (list.Any())
		{
			copiedHitsound = CopyEvent(list[0]);
			ShowNotification("Copied Hitsound");
		}
		else
		{
			ShowNotification("No Hitsound To Copy");
		}
	}

	public void PasteHitsoundSingleTile(int id)
	{
		if (id == 0 || copiedHitsound == null)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			List<LevelEvent> list = new List<LevelEvent>();
			int i;
			for (i = id; i > 0; i--)
			{
				list = events.FindAll((LevelEvent x) => x.floor == i && x.eventType == LevelEventType.SetHitsound);
				if (list.Any())
				{
					break;
				}
			}
			if (list.Any())
			{
				previousHitsound = CopyEvent(list[0]);
				events.RemoveAll((LevelEvent x) => x.floor == id && x.eventType == LevelEventType.SetHitsound);
				events.Add(CopyEvent(copiedHitsound, id));
				if (id < floors.Count - 2 && !events.FindAll((LevelEvent x) => x.floor == id + 1 && x.eventType == LevelEventType.SetHitsound).Any())
				{
					events.Add(CopyEvent(previousHitsound, id + 1));
				}
				ApplyEventsToFloors();
				scrFloor scrFloor2 = floors[id];
				SelectFloor(scrFloor2);
				levelEventsPanel.ShowTabsForFloor(selectedFloors[0].seqID);
				levelEventsPanel.selectedEventType = LevelEventType.SetHitsound;
				levelEventsPanel.ShowPanel(LevelEventType.SetHitsound);
				ShowEventIndicators(scrFloor2);
				ShowNotification("Paste Hitsound (single tile)");
			}
		}
	}

	public void CopyFloor(scrFloor toCopy, bool clearClipboard = true, bool cut = false, bool selectedEventOnly = false, bool allSameTypeEvents = false)
	{
		if (clearClipboard)
		{
			clipboard.Clear();
		}
		clipboard.Add(CopyOfFloor(toCopy, selectedEventOnly, allSameTypeEvents));
		clipboardContent = ClipboardContent.Floors;
		if (!cut)
		{
			FlashTile(toCopy);
		}
	}

	public void MultiCopyFloors(bool cut = false)
	{
		clipboard.Clear();
		foreach (scrFloor selectedFloor in selectedFloors)
		{
			CopyFloor(selectedFloor, clearClipboard: false);
		}
	}

	public void CopyDecoration(LevelEvent toCopy, bool clearClipboard = true, bool cut = false)
	{
		if (!dragging)
		{
			if (clearClipboard)
			{
				clipboard.Clear();
			}
			clipboard.Add(toCopy.Copy());
			clipboardContent = ClipboardContent.Decorations;
		}
	}

	public void MultiCopyDecorations()
	{
		clipboard.Clear();
		foreach (LevelEvent item in selectedDecorations.OrderBy((LevelEvent x) => levelData.decorations.IndexOf(x)).ToList())
		{
			CopyDecoration(item, clearClipboard: false);
		}
	}

	public void DuplicateDecorations()
	{
		if (dragging)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			MultiCopyDecorations();
			PasteDecorations(duplicating: true);
		}
	}

	public void CutFloor(scrFloor toCut, bool clearClipboard = true, bool selectedEventOnly = false, bool allSameTypeEvents = false)
	{
		using (new SaveStateScope(this))
		{
			int id = toCut.seqID;
			CopyFloor(toCut, clearClipboard, cut: true, selectedEventOnly, allSameTypeEvents);
			List<LevelEvent> list = new List<LevelEvent>();
			if (selectedEventOnly && levelEventsPanel.selectedEventType != LevelEventType.None)
			{
				if (allSameTypeEvents)
				{
					list = events.FindAll((LevelEvent x) => x.floor == id && x.eventType == levelEventsPanel.selectedEventType);
				}
				else
				{
					list.Add(levelEventsPanel.selectedEvent);
				}
			}
			else
			{
				list = events.FindAll((LevelEvent x) => x.floor == id);
				list.AddRange(decorations.FindAll((LevelEvent x) => x.floor == id));
			}
			RemoveEvents(list);
			ApplyEventsToFloors();
			levelEventsPanel.ShowTabsForFloor(id);
			ShowEventIndicators(toCut);
		}
	}

	public void MultiCutFloors()
	{
		using (new SaveStateScope(this))
		{
			MultiCopyFloors(cut: true);
			DeleteMultiSelection();
		}
	}

	public void PasteFloors(bool alsoPasteDecorations = true)
	{
		List<int> list = new List<int>();
		if (!clipboard.Any() || clipboardContent != ClipboardContent.Floors || !SelectionIsSingle())
		{
			return;
		}
		int num = selectedFloors[0].seqID;
		if (isOldLevel)
		{
			char stringDirection = ((FloorData)clipboard[0]).stringDirection;
			if (FloorPointsBackwards(stringDirection))
			{
				return;
			}
		}
		else
		{
			float floatDirection = ((FloorData)clipboard[0]).floatDirection;
			if (FloorPointsBackwards(floatDirection))
			{
				return;
			}
		}
		using (new SaveStateScope(this))
		{
			OffsetFloorIDsInEvents(num, clipboard.Count);
			for (int i = 0; i < clipboard.Count(); i++)
			{
				FloorData floorData = (FloorData)clipboard[i];
				List<LevelEvent> levelEventData = floorData.levelEventData;
				if (isOldLevel)
				{
					char stringDirection2 = floorData.stringDirection;
					levelData.pathData = levelData.pathData.Insert(num, stringDirection2.ToString());
				}
				else
				{
					float floatDirection2 = floorData.floatDirection;
					levelData.angleData.Insert(num, floatDirection2);
				}
				list.Add(num);
				num++;
				if (levelEventData.Any())
				{
					foreach (LevelEvent item in levelEventData)
					{
						events.Add(CopyEvent(item, num));
						if (EventHasBackgroundSprite(item))
						{
							refreshBgSprites = true;
						}
						if (item.IsDecoration)
						{
							refreshDecSprites = true;
						}
					}
				}
				if (!alsoPasteDecorations)
				{
					continue;
				}
				foreach (LevelEvent attachedDecoration in floorData.attachedDecorations)
				{
					LevelEvent dec = CopyEvent(attachedDecoration, num);
					AddDecoration(dec);
					refreshDecSprites = true;
				}
			}
			RemakePath();
			SelectFloor(floors[num]);
			MoveCameraToFloor(floors[num]);
			foreach (int item2 in list)
			{
				FlashTile(floors[item2]);
			}
			FlashTile(floors[selectedFloors[0].seqID]);
		}
	}

	public void CutDecoration(LevelEvent toCut)
	{
		if (dragging)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			CopyDecoration(toCut);
			RemoveEvent(toCut);
		}
	}

	public void MultiCutDecorations()
	{
		if (dragging)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			MultiCopyDecorations();
			DeleteMultiSelectionDecorations();
		}
	}

	public void PasteDecorations(bool duplicating = false)
	{
		if (!clipboard.Any() || clipboardContent != ClipboardContent.Decorations || dragging)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			List<LevelEvent> list = new List<LevelEvent>();
			if (!SelectionIsEmpty())
			{
				foreach (scrFloor selectedFloor in selectedFloors)
				{
					if (!(selectedFloor != null))
					{
						continue;
					}
					foreach (LevelEvent item2 in clipboard)
					{
						LevelEvent levelEvent = item2.Copy();
						levelEvent["relativeTo"] = DecPlacementType.Tile;
						levelEvent.floor = selectedFloor.seqID;
						list.Add(levelEvent);
					}
				}
			}
			else
			{
				foreach (LevelEvent item3 in clipboard)
				{
					LevelEvent item = item3.Copy();
					list.Add(item);
				}
			}
			bool flag = SelectionDecorationIsEmpty();
			int num = -1;
			List<LevelEvent> list2 = new List<LevelEvent>();
			if (!flag)
			{
				num = selectedDecorations.Max((LevelEvent x) => levelData.decorations.IndexOf(x));
				list2 = selectedDecorations.OrderBy((LevelEvent x) => levelData.decorations.IndexOf(x)).ToList();
			}
			for (int num2 = 0; num2 < list.Count; num2++)
			{
				int index = (flag ? (-1) : (duplicating ? levelData.decorations.IndexOf(list2[num2]) : (num + num2)));
				LevelEvent dec = list[num2];
				AddDecoration(dec, index);
			}
			DeselectAllDecorations();
			foreach (LevelEvent item4 in list)
			{
				SelectDecoration(item4, jumpToDecoration: false, showPanel: false, ignoreDeselection: true);
			}
			LevelEvent levelEvent2 = list.Last();
			levelEventsPanel.ShowInspector(show: true);
			levelEventsPanel.ShowPanel(levelEvent2.eventType);
			propertyControlDecorationsList.RefreshScrollRectPosition(levelEvent2);
			list.Clear();
		}
	}

	private void FlashTile(scrFloor floor)
	{
		string text = "copyFlashTween" + floor.seqID;
		DOTween.Kill(text);
		floor.floorRenderer.SetFlash(1f);
		floor.floorRenderer.TweenFlash(0f, 0.3f, text);
	}

	private void FlashDecoration(scrDecoration deco)
	{
	}

	private void ShowSelectedColor(scrFloor floor, float opacity = 1f)
	{
		FloorRenderer floorRenderer = floor.floorRenderer;
		UnityEngine.Color[] array = SelectedColors(floorRenderer.deselectedColor);
		UnityEngine.Color color = array[0];
		UnityEngine.Color color2 = array[1];
		if (opacity != 1f)
		{
			color2.a = opacity;
			floorRenderer.color = GetOverlayColor(floorRenderer.deselectedColor, color);
			color2 = GetOverlayColor(floorRenderer.deselectedColor, color2);
		}
		else
		{
			floorRenderer.color = color;
		}
		DOTween.To(() => floorRenderer.color, delegate(UnityEngine.Color x)
		{
			floorRenderer.color = x;
		}, color2, 1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
			.SetUpdate(isIndependentUpdate: true)
			.SetId("selectedColorTween");
		static UnityEngine.Color GetOverlayColor(UnityEngine.Color a, UnityEngine.Color b)
		{
			a.r = GetOverlayValue(a.r, b.r);
			a.g = GetOverlayValue(a.g, b.g);
			a.b = GetOverlayValue(a.b, b.b);
			a.a = GetOverlayValue(a.a, b.a);
			return a;
		}
		static float GetOverlayValue(float a, float b)
		{
			if (a < 0.5f)
			{
				return 2f * a * b;
			}
			return 1f - 2f * (1f - a) * (1f - b);
		}
	}

	private void ShowDeselectedColor(scrFloor floor)
	{
		floor.SetColor(floor.floorRenderer.deselectedColor);
	}

	public void ShowSelectedColorForLastSelectedFloor()
	{
		if ((bool)lastSelectedFloor)
		{
			if (DOTween.TweensById("selectedColorTween") != null)
			{
				DOTween.Kill("selectedColorTween");
			}
			ShowSelectedColor(lastSelectedFloor, 0.5f);
		}
	}

	private void OnDecorationSelected(LevelEvent decorationEvent)
	{
		if (!SelectionIsEmpty())
		{
			DeselectFloors();
		}
		ShowSelectedColorForLastSelectedFloor();
	}

	private void OnDecorationAllItemsDeselected()
	{
		if ((bool)lastSelectedFloor && SelectionIsEmpty())
		{
			DOTween.Kill("selectedColorTween");
			ShowDeselectedColor(lastSelectedFloor);
		}
	}

	public void PasteEvents(scrFloor targetFloor, FloorData floorData, bool alsoPasteDecorations = true, bool overwrite = true, bool selectAfterward = true, bool updateFloors = true)
	{
		List<LevelEvent> levelEventData = floorData.levelEventData;
		int id = targetFloor.seqID;
		List<LevelEvent> attachedDecorations = floorData.attachedDecorations;
		if (!levelEventData.Any() && !attachedDecorations.Any())
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			if (overwrite)
			{
				events.RemoveAll((LevelEvent x) => x.floor == id);
			}
			foreach (LevelEvent ev in levelEventData)
			{
				if ((!selectedFirstFloor || ev.info.allowFirstFloorCheck) && (!EditorConstants.soloTypes.Contains(ev.eventType) || !events.Exists((LevelEvent x) => x.floor == id && x.eventType == ev.eventType)) && (!EditorConstants.soloTypes.Contains(ev.eventType) || ((ev.eventType != LevelEventType.Hold || !events.Exists((LevelEvent x) => x.floor == id && x.eventType == LevelEventType.Pause)) && (ev.eventType != LevelEventType.Pause || !events.Exists((LevelEvent x) => x.floor == id && x.eventType == LevelEventType.Hold)))) && (!EditorConstants.soloTypes.Contains(ev.eventType) || ((ev.eventType != LevelEventType.FreeRoam || !events.Exists((LevelEvent x) => x.floor == id && x.eventType == LevelEventType.Twirl)) && (ev.eventType != LevelEventType.Twirl || !events.Exists((LevelEvent x) => x.floor == id && x.eventType == LevelEventType.FreeRoam)))))
				{
					if (EventHasBackgroundSprite(ev))
					{
						refreshBgSprites = true;
					}
					if (ev.IsDecoration)
					{
						refreshDecSprites = true;
					}
					events.Add(CopyEvent(ev, id));
				}
			}
			if (updateFloors)
			{
				ApplyEventsToFloors();
			}
			if (alsoPasteDecorations)
			{
				foreach (LevelEvent item in attachedDecorations)
				{
					LevelEvent dec = CopyEvent(item, id);
					AddDecoration(dec);
					refreshDecSprites = true;
				}
			}
			if (selectAfterward)
			{
				if (updateFloors)
				{
					SelectFloor(targetFloor);
				}
				if (levelEventData.Any() && updateFloors)
				{
					LevelEventType eventType = levelEventData[0].eventType;
					levelEventsPanel.ShowTabsForFloor(selectedFloors[0].seqID);
					levelEventsPanel.selectedEventType = eventType;
					levelEventsPanel.ShowPanel(eventType);
					ShowEventIndicators(targetFloor);
				}
			}
		}
	}

	public void DeselectAnyUIGameObject()
	{
		EventSystem.current.SetSelectedGameObject(null);
	}

	private void TogglePause(bool clsToEditor = false)
	{
		ResetScene(clsToEditor);
	}

	private void ResetScene(bool clsToEditor = false)
	{
		autoFailed = false;
		if (clsToEditor)
		{
			levelEventsPanel.HideAllInspectorTabs();
			levelEventsPanel.ShowInspector(show: false);
			UpdateSongAndLevelSettings();
		}
		customLevel.ResetScene(isResetCustomLevel: true);
		refreshDecSprites = false;
		propertyControlDecorationsList.ClearDragCache();
		DeselectAnyUIGameObject();
	}

	private void ClearFloorGlows()
	{
		foreach (scrFloor floor in floors)
		{
			if ((bool)floor.bottomGlow)
			{
				floor.bottomGlow.gameObject.SetActive(value: false);
			}
			floor.topGlow.gameObject.SetActive(value: false);
		}
	}

	public void UpdateSongAndLevelSettings()
	{
		int num = 0;
		foreach (PropertiesPanel panels in settingsPanel.panelsList)
		{
			panels.SetProperties(num switch
			{
				0 => levelData.levelSettings, 
				1 => levelData.songSettings, 
				2 => levelData.trackSettings, 
				3 => levelData.backgroundSettings, 
				4 => levelData.cameraSettings, 
				5 => levelData.miscSettings, 
				6 => levelData.eventSettings, 
				7 => levelData.decorationSettings, 
				_ => levelData.songSettings, 
			}, checkIfEnabled: false);
			num++;
		}
		settingsPanel.ShowPanel(LevelEventType.SongSettings);
	}

	public bool EventHasBackgroundSprite(LevelEvent evnt)
	{
		if (evnt.eventType == LevelEventType.CustomBackground)
		{
			return !string.IsNullOrEmpty(evnt["bgImage"].ToString());
		}
		return false;
	}

	private void PauseIfUnpaused()
	{
		if (playMode)
		{
			TogglePause();
		}
	}

	public void OpenLevel()
	{
		CheckUnsavedChanges(delegate
		{
			PauseIfUnpaused();
			StartCoroutine(OpenLevelCo());
		});
	}

	public void OpenLevel(string filePath)
	{
		PauseIfUnpaused();
		StartCoroutine(OpenLevelCo(filePath));
		ShowPopup(show: false);
	}

	private string SanitizeLevelPath(string path)
	{
		return Uri.UnescapeDataString(path.Replace("file:", ""));
	}

	private IEnumerator OpenLevelCo(string definedLevelPath = null)
	{
		while (stallFileDialog)
		{
			yield return null;
		}
		ClearAllFloorOffsets();
		redoStates.Clear();
		undoStates.Clear();
		bool num = definedLevelPath == null;
		string lastLevelPath = customLevel.levelPath;
		if (num)
		{
			string levelPath = FileBrowser.PickFile(Persistence.GetLastUsedFolder(), RDString.Get("editor.dialog.adofaiLevelDescription"), GCS.levelExtensions, RDString.Get("editor.dialog.openFile"));
			yield return null;
			if (string.IsNullOrEmpty(levelPath))
			{
				yield break;
			}
			string text = SanitizeLevelPath(levelPath);
			string text2 = Path.GetExtension(text).ToLower();
			string value = text2.Substring(1, text2.Length - 1);
			if (GCS.levelZipExtensions.Contains(value))
			{
				string availableDirectoryName = RDUtils.GetAvailableDirectoryName(Path.Combine(Path.GetDirectoryName(text), Path.GetFileNameWithoutExtension(text)));
				RDDirectory.CreateDirectory(availableDirectoryName);
				try
				{
					ZipUtils.Unzip(text, availableDirectoryName);
				}
				catch (Exception ex)
				{
					ShowNotificationPopup(RDString.Get("editor.notification.unzipFailed"));
					Debug.LogError("Unzip failed: " + ex);
					Directory.Delete(availableDirectoryName, recursive: true);
					yield break;
				}
				string text3 = FindAdofaiLevelOnDirectory(availableDirectoryName);
				if (text3 == null)
				{
					ShowNotificationPopup(RDString.Get("editor.notification.levelNotFound"));
					Directory.Delete(availableDirectoryName, recursive: true);
					yield break;
				}
				customLevel.levelPath = text3;
			}
			else
			{
				customLevel.levelPath = text;
			}
		}
		else
		{
			customLevel.levelPath = definedLevelPath;
		}
		scrController.deaths = 0;
		string customLevelId = GCS.customLevelId;
		GCS.customLevelId = null;
		Persistence.UpdateLastUsedFolder(ADOBase.levelPath);
		Persistence.UpdateLastOpenedLevel(ADOBase.levelPath);
		bool flag = false;
		LoadResult status = LoadResult.Error;
		string text4 = "";
		isLoading = true;
		try
		{
			flag = customLevel.LoadLevel(ADOBase.levelPath, out status);
		}
		catch (Exception ex2)
		{
			text4 = "Error loading level file at " + ADOBase.levelPath + ": " + ex2.Message + ", Stacktrace:\n" + ex2.StackTrace;
			Debug.Log(text4);
		}
		if (flag)
		{
			errorImageResult.Clear();
			isUnauthorizedAccess = false;
			RemakePath();
			lastSelectedFloor = null;
			SelectFirstFloor();
			UpdateSongAndLevelSettings();
			customLevel.ReloadAssets(force: true, reloadDecorations: false);
			UpdateDecorationObjects();
			DiscordController.instance?.UpdatePresence();
			ShowNotification(RDString.Get("editor.notification.levelLoaded"));
			unsavedChanges = false;
		}
		else
		{
			customLevel.levelPath = lastLevelPath;
			GCS.customLevelId = customLevelId;
			ShowNotificationPopup(text4, new NotificationAction[2]
			{
				new NotificationAction(RDString.Get("editor.notification.copyText"), delegate
				{
					notificationPopupContent.text.CopyToClipboard();
					ShowNotification(RDString.Get("editor.notification.copiedText"));
				}),
				new NotificationAction(RDString.Get("editor.ok"), delegate
				{
					CloseNotificationPopup();
				})
			}, RDString.Get($"editor.notification.loadingFailed.{status}"));
		}
		isLoading = false;
		CloseAllPanels();
		yield return null;
		ShowImageLoadResult();
	}

	public void OpenRecent(bool checkCtrl = false)
	{
		string recentLevel = Persistence.GetLastOpenedLevel();
		if (File.Exists(recentLevel) && (!checkCtrl || !OpenDirectory(recentLevel)))
		{
			CheckUnsavedChanges(delegate
			{
				StartCoroutine(OpenLevelCo(recentLevel));
			});
		}
	}

	private bool OpenDirectory(string path)
	{
		if (!controlPressed)
		{
			return false;
		}
		RDEditorUtils.RevealInExplorer(Path.GetDirectoryName(path));
		return true;
	}

	private string GetDataPathFromURL(string url)
	{
		string path = new Uri(url).Segments[^1];
		string text = Persistence.DataPath + "/Temp";
		if (!Directory.Exists(text))
		{
			RDDirectory.CreateDirectory(text);
		}
		return Path.Combine(text, path).Replace("?", "").Replace("=", "");
	}

	private string FindAdofaiLevelOnDirectory(string path)
	{
		string[] files = Directory.GetFiles(path, "*.adofai", SearchOption.AllDirectories);
		if (files.Length == 0)
		{
			return null;
		}
		string text = null;
		for (int i = 0; i < files.Length; i++)
		{
			if (!(Path.GetFileName(files[i]) == "backup.adofai") && !Path.GetFileName(files[i]).StartsWith("."))
			{
				text = files[i];
				MonoBehaviour.print("selected file: " + text);
				break;
			}
		}
		if (text == null)
		{
			MonoBehaviour.print("was null");
			return null;
		}
		return text;
	}

	private void StartLevelDownload()
	{
		if (!levelLinkInput.text.IsNullOrEmpty())
		{
			downloadCancellation?.Cancel();
			downloadCancellation = new CancellationTokenSource();
			OpenLevelFromURL(downloadCancellation.Token).Forget();
		}
	}

	private void CancelDownload()
	{
		if (downloadingLevel)
		{
			downloadingLevel = false;
			downloadCancellation?.Cancel();
			if (www != null)
			{
				www.Dispose();
			}
			popupURLDownload.interactable = true;
			popupURLDownload.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.dialog.download");
		}
		else
		{
			ShowPopup(show: false);
		}
	}

	public async UniTask OpenLevelFromURL(CancellationToken ct)
	{
		string text = levelLinkInput.text;
		downloadingLevel = true;
		popupURLDownload.interactable = false;
		popupURLDownload.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.dialog.downloading");
		text = await RDEditorUtils.ParseLevelURL(text);
		bool num = text.StartsWith("file://");
		if (num)
		{
			text = WebUtility.UrlDecode(new Uri(text).AbsolutePath);
		}
		string filePath = GetDataPathFromURL(text);
		if (num)
		{
			FileAttributes attributes = File.GetAttributes(text);
			bool num2 = attributes.HasFlag(FileAttributes.Directory);
			string filePath2 = filePath;
			if (num2)
			{
				string text2 = FindAdofaiLevelOnDirectory(text);
				if (text2 == null)
				{
					ShowNotificationPopup(RDString.Get("editor.notification.levelNotFound"));
					return;
				}
				filePath2 = text2;
				if (RDFile.Exists(filePath))
				{
					RDFile.Delete(filePath);
				}
				RDDirectory.Copy(text, filePath);
			}
			else
			{
				File.Copy(text, filePath, overwrite: true);
			}
			OpenLevel(filePath2);
		}
		else
		{
			www = UnityWebRequest.Get(text);
			await www.SendWebRequest();
			if (www.HasConnectionError())
			{
				ShowNotificationPopup(RDString.Get("editor.notification.downloadFailed"));
			}
			if (RDFile.Exists(filePath))
			{
				RDFile.Delete(filePath);
			}
			byte[] data = www.downloadHandler.data;
			bool flag = Encoding.Default.GetString(data[..10]).Split("\n")[0].Contains("{");
			Debug.Log($"isLevelFile: {flag}");
			RDFile.WriteAllBytes(filePath, data);
			www.Dispose();
			if (!flag)
			{
				string text3 = filePath + "_unzip";
				if (Directory.Exists(text3))
				{
					Directory.Delete(text3, recursive: true);
				}
				RDDirectory.CreateDirectory(text3);
				bool flag2 = false;
				try
				{
					ZipUtils.Unzip(filePath, text3);
					flag2 = true;
				}
				catch (Exception ex)
				{
					ShowNotificationPopup(RDString.Get("editor.notification.unzipFailed"));
					Debug.LogError("Unzip failed: " + ex.ToString());
				}
				if (!flag2)
				{
					Directory.Delete(text3, recursive: true);
					RDFile.Delete(filePath);
					return;
				}
				string text4 = FindAdofaiLevelOnDirectory(text3);
				if (text4 != null)
				{
					PauseIfUnpaused();
					StartCoroutine(OpenLevelCo(text4));
					ShowPopup(show: false);
				}
				else
				{
					ShowNotificationPopup(RDString.Get("editor.notification.levelNotFound"));
					Directory.Delete(text3, recursive: true);
					RDFile.Delete(filePath);
				}
			}
			else
			{
				OpenLevel(filePath);
			}
		}
		popupURLDownload.interactable = true;
		popupURLDownload.GetComponentInChildren<TMP_Text>().text = RDString.Get("editor.dialog.download");
		downloadingLevel = false;
	}

	private void OnApplicationQuit()
	{
		string path = Persistence.DataPath + "/Temp";
		if (Directory.Exists(path))
		{
			Directory.Delete(path, recursive: true);
		}
	}

	private void ExportInternalLevel()
	{
		if (!levelData.isOldLevel)
		{
			return;
		}
		string text = levelData.pathData;
		int num = 0;
		foreach (LevelEvent @event in events)
		{
			if (@event.eventType == LevelEventType.Twirl)
			{
				text = text.Insert(@event.floor + num, "/");
				num++;
			}
			if (@event.eventType == LevelEventType.SetSpeed)
			{
				scrFloor scrFloor2 = floors[@event.floor];
				float num2 = ((scrFloor2.seqID > 0) ? floors[scrFloor2.seqID - 1].speed : 1f);
				string value = ">";
				float speed = scrFloor2.speed;
				if (Mathf.Approximately(speed, num2 * 2f))
				{
					value = ">";
				}
				else if (Mathf.Approximately(speed, num2 / 0.75f))
				{
					value = "_";
				}
				else if (Mathf.Approximately(speed, num2 * 4f))
				{
					value = "*";
				}
				else if (Mathf.Approximately(speed, num2 / 2f))
				{
					value = "<";
				}
				else if (Mathf.Approximately(speed, num2 * 0.75f))
				{
					value = "-";
				}
				else if (Mathf.Approximately(speed, num2 / 4f))
				{
					value = "%";
				}
				text = text.Insert(@event.floor + num, value);
				num++;
			}
		}
	}

	public void SaveLevel()
	{
		if (!string.IsNullOrEmpty(ADOBase.levelPath))
		{
			try
			{
				string data = levelData.Encode();
				RDFile.WriteAllText(ADOBase.levelPath, data);
				ShowNotification(RDString.Get("editor.notification.levelSaved"));
				unsavedChanges = false;
			}
			catch (Exception ex)
			{
				Debug.LogError(ex);
				ShowNotificationPopup(RDString.Get("editor.notification.savingFailed"));
				Debug.Log("Failed saving at path " + ADOBase.levelPath + ": " + ex.Message);
				return;
			}
		}
		else
		{
			SaveLevelAs();
		}
		CloseAllPanels();
		if (Application.isEditor)
		{
			ExportInternalLevel();
		}
	}

	public void SaveLevelAs(bool newLevel = false, string path = null)
	{
		StartCoroutine(SaveLevelAsCo(newLevel, path));
	}

	private IEnumerator SaveLevelAsCo(bool newLevel = false, string path = null)
	{
		while (stallFileDialog)
		{
			yield return null;
		}
		string filename = ((newLevel || string.IsNullOrEmpty(customLevel.levelPath)) ? "level" : Path.GetFileNameWithoutExtension(customLevel.levelPath));
		if (string.IsNullOrEmpty(path))
		{
			string value = FileBrowser.SaveFile(Persistence.GetLastUsedFolder(), filename, RDString.Get("editor.dialog.adofaiLevelDescription"), GCS.levelTextExtensions, RDString.Get("editor.dialog.saveLevel"));
			if (!string.IsNullOrEmpty(value))
			{
				Save(value);
			}
		}
		else
		{
			Save(path);
		}
		void Save(string levelPath)
		{
			if (!string.IsNullOrEmpty(levelPath))
			{
				string text = SanitizeLevelPath(levelPath);
				if (!text.EndsWith(".adofai"))
				{
					text += ".adofai";
				}
				customLevel.levelPath = text;
				RefreshFilenameText();
				Persistence.UpdateLastUsedFolder(levelPath);
				Persistence.UpdateLastOpenedLevel(levelPath);
				DiscordController.instance?.UpdatePresence();
				SaveLevel();
			}
		}
	}

	public void NewLevel()
	{
		string text = FileBrowser.SaveFile(Persistence.GetLastUsedFolder(), "level.adofai", RDString.Get("editor.dialog.adofaiLevelDescription"), GCS.levelTextExtensions, RDString.Get("editor.dialog.saveLevel"));
		if (!string.IsNullOrEmpty(text))
		{
			ClearAllFloorOffsets();
			DeselectAnyUIGameObject();
			DeselectFloors();
			DeselectAllDecorations();
			SaveLevelAs(newLevel: true, text);
			if (text.Length != 0)
			{
				levelData.Setup();
				events.Clear();
				scrDecorationManager.instance.ClearDecorations();
				levelData.decorations.Clear();
				selectedDecorations.Clear();
				UpdateSongAndLevelSettings();
				customLevel.ReloadAssets();
				UpdateDecorationObjects();
				RemakePath();
				customLevel.ReloadSong();
				undoStates.Clear();
				redoStates.Clear();
				ShowNotification(RDString.Get("editor.notification.levelReset"));
			}
			CloseAllPanels();
		}
	}

	public void ShowPopup(bool show, PopupType popupType = PopupType.SaveBeforeSongImport, bool skipAnim = false)
	{
		if (popupIsAnimating && show)
		{
			return;
		}
		showingPopup = show;
		if (show)
		{
			foreach (Transform item in popupWindow.transform)
			{
				item.gameObject.SetActive(value: false);
			}
			switch (popupType)
			{
			case PopupType.SaveBeforeSongImport:
			case PopupType.SaveBeforeImageImport:
			case PopupType.SaveBeforeVideoImport:
			case PopupType.SaveBeforeLevelExport:
			{
				string key = "";
				switch (popupType)
				{
				case PopupType.SaveBeforeSongImport:
					key = "editor.dialog.saveBeforeImportingSounds";
					break;
				case PopupType.SaveBeforeImageImport:
					key = "editor.dialog.saveBeforeImportingImages";
					break;
				case PopupType.SaveBeforeVideoImport:
					key = "editor.dialog.saveBeforeImportingVideos";
					break;
				case PopupType.SaveBeforeLevelExport:
					key = "editor.dialog.saveBeforeLevelExport";
					break;
				}
				string text4 = RDString.Get(key);
				savePopupContainer.SetActive(value: true);
				savePopupText.text = text4;
				break;
			}
			case PopupType.OpenURL:
				urlPopupContainer.SetActive(value: true);
				break;
			case PopupType.ExportLevel:
				publishWindow.windowContainer.SetActive(value: true);
				publishWindow.Init();
				ShowEventPicker(show: false);
				CloseAllInspectors();
				break;
			case PopupType.CopyrightWarning:
			{
				ShowEventPicker(show: false);
				CloseAllInspectors();
				copyrightPopupContainer.SetActive(value: true);
				string text3 = RDString.Get("editor.agreement").Replace("[[editorPermissionGuidelinesLink]]", RDString.Get("editor.permissionGuidelinesLink")).Replace("[[editorPermissionTemplateLink]]", RDString.Get("editor.permissionTemplateLink"))
					.Replace("[[editorVerifiedArtistsLink]]", RDString.Get("editor.verifiedArtistsLink"))
					.Replace("[[editorDiscordLink]]", RDString.Get("editor.discordLink"))
					.Replace("[[editorLincenseAgreementLink]]", RDString.Get("editor.licenseAgreementLink"))
					.Replace("[[editorUserGeneratedContentRulesLink]]", RDString.Get("editor.userGeneratedContentRules"));
				copyrightText.text = text3;
				break;
			}
			case PopupType.Spoiler:
				ShowEventPicker(show: false);
				CloseAllInspectors();
				spoilerPopupContainer.SetActive(value: true);
				spoilerPopupText.text = "Planets more than 3 works, but is an unreleased feature right now. If you're reading this, please do not release a mod to enable it or share footage, so we can keep the spoiler!";
				break;
			case PopupType.MissingExportParams:
			{
				paramsPopupContainer.SetActive(value: true);
				string text = "";
				List<string> missingParams = levelData.GetMissingParams();
				string text2 = levelData.artist.Trim();
				if (!text2.IsNullOrEmpty() && (!GCS.isDev || !RDInput.holdingShift))
				{
					ApprovalLevel approvalLevel = ApprovalLevelForArtist(text2);
					bool flag = levelData.artistPermission != "" || levelData.specialArtistType != SpecialArtistType.None;
					if (approvalLevel == ApprovalLevel.Declined)
					{
						missingParams.Add("editor.artistDisclaimer.artistDeclinedRequirement");
					}
					else if (approvalLevel == ApprovalLevel.ListingRejected && !flag)
					{
						missingParams.Add("editor.artistDisclaimer.artistListingRejectedRequirement");
					}
					else if (approvalLevel == ApprovalLevel.Pending && !flag)
					{
						missingParams.Add("editor.artistDisclaimer.artistNewRequirement");
					}
				}
				foreach (string item2 in missingParams)
				{
					text = text + "- " + RDString.Get(item2, new Dictionary<string, object> { 
					{
						"artist",
						"<b>" + levelData.artist + "</b>"
					} }) + "\n";
				}
				text = text.Replace("[artist]", "<b>" + levelData.artist + "</b>");
				paramsPopupText.text = text;
				break;
			}
			case PopupType.MissingFiles:
			{
				missingFilesPopupContainer.SetActive(value: true);
				List<string> missingFiles = GetMissingFiles();
				StringBuilder stringBuilder = new StringBuilder();
				foreach (string item3 in missingFiles)
				{
					stringBuilder.Append("- ").Append(item3).Append('\n');
				}
				missingFilesPopupText.text = stringBuilder.ToString();
				break;
			}
			case PopupType.OggEncode:
				oggPopupContainer.SetActive(value: true);
				popupOggCancel.interactable = true;
				popupOggConvert.interactable = true;
				oggConversionBar.gameObject.SetActive(value: false);
				oggConversionBarText.text = RDString.Get("editor.dialog.convert");
				break;
			case PopupType.ConversionSuccessful:
				okPopupContainer.SetActive(value: true);
				okPopupText.text = RDString.Get("editor.dialog.conversionSuccessful");
				break;
			case PopupType.ConversionError:
				okPopupContainer.SetActive(value: true);
				okPopupText.text = RDString.Get("editor.dialog.conversionSongNotFound");
				break;
			case PopupType.UnsavedChanges:
				unsavedChangesPopupContainer.SetActive(value: true);
				break;
			case PopupType.MacAppStoreFolderRestriction:
				okPopupContainer.SetActive(value: true);
				stallFileDialog = true;
				okPopupText.text = RDString.Get("editor.open.macAppStoreWarning");
				popupOkCallback = delegate
				{
					stallFileDialog = false;
					popupOkCallback = null;
					ShowPopup(show: false);
					popupIsAnimating = false;
				};
				break;
			case PopupType.MacAppStoreFileOutsideDownloads:
				okPopupContainer.SetActive(value: true);
				okPopupText.text = RDString.Get("editor.open.fileOutsideDownloads");
				break;
			case PopupType.SteamDeckWarning:
				steamDeckPopupContainer.SetActive(value: true);
				break;
			case PopupType.BetaBranchesWorkshopPublish:
				largeOkPopupContainer.SetActive(value: true);
				largeOkPopupText.text = RDString.Get("editor.dialog.betaBranchesWorkshopPublish", new Dictionary<string, object> { 
				{
					"branch",
					GCS.steamBranchName
				} });
				break;
			case PopupType.Confirm:
				confirmPopupContainer.SetActive(value: true);
				break;
			case PopupType.WorkshopLevelReupload:
				okPopupContainer.SetActive(value: true);
				okPopupText.text = RDString.Get("editor.dialog.workshopLevelReupload");
				break;
			}
		}
		popupPanel.SetActive(value: true);
		UnityEngine.UI.Image component = popupPanel.GetComponent<UnityEngine.UI.Image>();
		RectTransform component2 = popupWindow.GetComponent<RectTransform>();
		float alpha = (show ? 0.5f : 0f);
		float num = ((popupType == PopupType.ExportLevel || popupType == PopupType.MissingExportParams || popupType == PopupType.Spoiler || popupType == PopupType.SteamDeckWarning) ? 450f : 200f);
		float num2 = 20f;
		float num3 = (show ? num2 : num);
		component2.DOKill();
		component.DOKill();
		if (show)
		{
			component2.SetAnchorPosY(num);
			component.color = UnityEngine.Color.black.WithAlpha(0f);
		}
		if (skipAnim)
		{
			component.color = UnityEngine.Color.black.WithAlpha(alpha);
			component2.anchoredPosition = new Vector2(component2.anchoredPosition.x, num3);
			CloseAllPanels();
			if (!show)
			{
				popupPanel.SetActive(value: false);
			}
			return;
		}
		component.DOColor(UnityEngine.Color.black.WithAlpha(alpha), 0.25f).SetUpdate(isIndependentUpdate: true).SetEase(Ease.Linear);
		popupIsAnimating = true;
		component2.DOAnchorPosY(num3, 0.5f).SetUpdate(isIndependentUpdate: true).SetEase(show ? panelShowEase : panelHideEase)
			.OnComplete(delegate
			{
				popupIsAnimating = false;
				if (!show)
				{
					popupPanel.SetActive(value: false);
				}
			});
		CloseAllPanels();
	}

	public void ConfirmPopup(string text, TweenCallback callback)
	{
		ShowPopup(show: true, PopupType.Confirm);
		confirmPopupText.text = text;
		popupConfirmCallback = callback;
	}

	public void ShowNotification(string text, UnityEngine.Color? textColor = null, float delayDuration = 1.25f)
	{
		RectTransform rt = notificationText.GetComponent<RectTransform>();
		float duration = 0.5f;
		float hidePos = -135f;
		float endValue = 165f;
		if (notificationSeq != null && notificationSeq.active)
		{
			notificationSeq.Kill();
		}
		TMP_Text component = notificationText.GetComponent<TMP_Text>();
		component.text = text;
		component.color = textColor ?? UnityEngine.Color.white;
		notificationSeq = DOTween.Sequence().SetUpdate(isIndependentUpdate: true).Append(rt.DOAnchorPosX(hidePos, 0f))
			.Append(rt.DOAnchorPosX(endValue, duration).SetUpdate(isIndependentUpdate: true).SetEase(Ease.OutBack))
			.AppendInterval(delayDuration)
			.SetUpdate(isIndependentUpdate: true)
			.Append(rt.DOAnchorPosX(hidePos, duration).SetUpdate(isIndependentUpdate: true).SetEase(Ease.InQuad))
			.OnKill(delegate
			{
				rt.AnchorPosX(hidePos);
			});
	}

	private void ShowNotificationPopupBase(string text, string title = null)
	{
		notificationPopupContent.gameObject.GetComponent<ContentSizeFitter>().enabled = true;
		notificationPopupContent.text = text;
		notificationPopupContainer.SetActive(value: true);
		Canvas.ForceUpdateCanvases();
		int num = 630;
		float height = notificationPopupContent.rectTransform.rect.height;
		float num2 = height + 170f;
		if (!string.IsNullOrEmpty(title))
		{
			notificationPopupScrollview.gameObject.SetActive(value: false);
			notificationPopupTitle.text = title;
			notificationPopupTitle.gameObject.SetActive(value: true);
		}
		else
		{
			num += 60;
			num2 -= 60f;
			notificationPopupScrollview.gameObject.SetActive(value: true);
			notificationPopupTitle.gameObject.SetActive(value: false);
		}
		notificationPopupScrollviewContent.SizeDeltaY(height);
		if (height > (float)num)
		{
			height = num;
			num2 = 800f;
			notificationPopupScrollview.GetComponent<ScrollRect>().enabled = true;
		}
		else
		{
			notificationPopupScrollview.GetComponent<ScrollRect>().enabled = false;
			notificationPopupScrollviewVertical.SetActive(value: false);
			notificationPopupScrollviewHorizontal.SetActive(value: false);
		}
		notificationPopupWindow.SizeDeltaY(num2);
		notificationPopupContainer.SetActive(value: true);
		UnityEngine.UI.Image component = notificationPopupContainer.GetComponent<UnityEngine.UI.Image>();
		component.color = UnityEngine.Color.black.WithAlpha(0f);
		notificationPopupWindow.anchoredPosition = new Vector2(0f, 450f);
		component.DOColor(UnityEngine.Color.black.WithAlpha(0.5f), filePanelMoveDuration / 2f).SetUpdate(isIndependentUpdate: true).SetEase(Ease.Linear);
		notificationPopupWindow.DOAnchorPosY(20f, filePanelMoveDuration).SetUpdate(isIndependentUpdate: true).SetEase(panelShowEase);
		foreach (Button notificationPopupAction in notificationPopupActions)
		{
			UnityEngine.Object.Destroy(notificationPopupAction.gameObject);
		}
		notificationPopupActions.Clear();
	}

	public void ShowNotificationPopup(string text, string title = null, Action callbackAction = null)
	{
		ShowNotificationPopupBase(text, title);
		notificationPopupActionsContainer.gameObject.SetActive(value: false);
		notificationOkButton.gameObject.SetActive(value: true);
		notificationOkButton.onClick.AddListener(delegate
		{
			callbackAction?.Invoke();
			notificationOkButton.onClick.RemoveAllListeners();
			CloseNotificationPopup();
		});
	}

	public void ShowNotificationPopup(string text, NotificationAction[] notificationActions, string title = null)
	{
		ShowNotificationPopupBase(text, title);
		notificationPopupActionsContainer.gameObject.SetActive(value: true);
		notificationOkButton.gameObject.SetActive(value: false);
		notificationPopupActions.Clear();
		for (int i = 0; i < notificationActions.Length; i++)
		{
			NotificationAction notificationAction = notificationActions[i];
			GameObject obj = UnityEngine.Object.Instantiate(notificationOkButton.gameObject);
			Button component = obj.GetComponent<Button>();
			TMP_Text componentInChildren = obj.GetComponentInChildren<TMP_Text>();
			obj.name = notificationAction.text;
			componentInChildren.text = notificationAction.text;
			obj.transform.SetParent(notificationPopupActionsContainer);
			obj.transform.ScaleXY(1f);
			obj.SetActive(value: true);
			notificationPopupActions.Add(component);
			component.onClick.AddListener(delegate
			{
				notificationAction.action?.Invoke();
			});
		}
	}

	private void CloseNotificationPopup()
	{
		notificationPopupWindow.DOAnchorPosY(450f, filePanelMoveDuration).SetUpdate(isIndependentUpdate: true).SetEase(panelHideEase)
			.OnComplete(delegate
			{
				notificationPopupContainer.SetActive(value: false);
			});
	}

	public void ShowEventsPage(int pageNum)
	{
		foreach (LevelEventButton item in eventButtons[currentCategory])
		{
			bool flag = selectedFirstFloor && !item.info.allowFirstFloorCheck;
			item.gameObject.SetActive(item.page == pageNum);
			item.enableButton = !flag;
		}
		UpdateCategoryVisibility();
	}

	public void RepositionEventButtons()
	{
		int num = 0;
		foreach (LevelEventButton item in eventButtons[currentCategory])
		{
			if (item.gameObject.activeSelf)
			{
				RectTransform component = item.GetComponent<RectTransform>();
				float x = 0f + (0f + component.sizeDelta.x) * (float)(num++ % 11);
				component.SetAnchorPosX(x);
			}
		}
	}

	public void UpdateCategoryVisibility()
	{
		foreach (CategoryTab categoryTab in categoryTabs)
		{
			bool active = categoryTab.levelEventCategory == LevelEventCategory.Favorites || eventButtons[categoryTab.levelEventCategory].Any((LevelEventButton x) => !selectedFirstFloor || x.info.allowFirstFloorCheck);
			categoryTab.gameObject.SetActive(active);
		}
		if (!categoryTabs.Find((CategoryTab x) => x.levelEventCategory == currentCategory).gameObject.activeSelf)
		{
			CycleEventsPage(forward: false);
		}
	}

	private void CycleEventsPage(bool forward, bool moveAllTheWay = false)
	{
		Array values = Enum.GetValues(typeof(LevelEventCategory));
		int num = Array.IndexOf(values, currentCategory);
		if (moveAllTheWay)
		{
			num = (forward ? (values.Length - 1) : 0);
		}
		else
		{
			int num2 = (forward ? 1 : (-1));
			LevelEventCategory levelEventCategory;
			do
			{
				num += num2;
				num = (Persistence.disableEventsPageRepeat ? Mathf.Clamp(num, 0, values.Length - 1) : ((int)Mathf.Repeat(num, values.Length)));
				levelEventCategory = (LevelEventCategory)num;
			}
			while (!eventButtons[levelEventCategory].Any((LevelEventButton x) => !selectedFirstFloor || x.info.allowFirstFloorCheck) && levelEventCategory != LevelEventCategory.Favorites);
		}
		SetCategory((LevelEventCategory)num);
	}

	public void ShowNextPage(bool moveToLast = false)
	{
		CycleEventsPage(forward: true, moveToLast);
	}

	public void ShowPrevPage(bool moveToFirst = false)
	{
		CycleEventsPage(forward: false, moveToFirst);
	}

	public void SetCategory(LevelEventCategory eventCategory, bool changedFavorites = false)
	{
		LevelEventButton[] componentsInChildren = levelEventsBar.GetComponentsInChildren<LevelEventButton>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].gameObject.SetActive(value: false);
		}
		currentCategory = eventCategory;
		maxPage = (eventButtons[eventCategory].Count - 1) / 11;
		if (changedFavorites)
		{
			currentPage = Math.Min(currentPage, maxPage);
		}
		else
		{
			currentPage = 0;
		}
		ShowEventsPage(currentPage);
		ShowEventPicker(show: true);
	}

	public void AddFavoriteEvent(LevelEventType type)
	{
		if (!favoriteEvents.Contains(type))
		{
			favoriteEvents.Add(type);
			Persistence.SetFavoriteEditorEvents(favoriteEvents);
			SetupFavoritesCategory();
		}
	}

	public void RemoveFavoriteEvent(LevelEventType type)
	{
		if (favoriteEvents.Contains(type))
		{
			favoriteEvents.Remove(type);
			Persistence.SetFavoriteEditorEvents(favoriteEvents);
			SetupFavoritesCategory();
		}
	}

	public void SetupFavoritesCategory(bool firstTime = false)
	{
		if (firstTime)
		{
			favoriteEvents = Persistence.favoriteEditorEvents;
		}
		else
		{
			foreach (LevelEventButton item in eventButtons[LevelEventCategory.Favorites])
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			eventButtons[LevelEventCategory.Favorites].Clear();
		}
		int num = 0;
		foreach (LevelEventInfo value in GCS.levelEventsInfo.Values)
		{
			LevelEventType levelEventType = RDUtils.ParseEnum(value.name, LevelEventType.None);
			if (value.isActive && levelEventType != LevelEventType.ChangeTrack && !value.isDecoration && favoriteEvents.Contains(levelEventType))
			{
				GameObject obj = UnityEngine.Object.Instantiate(prefab_levelEventButton, levelEventsBarButtons);
				RectTransform component = obj.GetComponent<RectTransform>();
				float x = 0f + (0f + component.sizeDelta.x) * (float)(num % 11);
				component.SetAnchorPosX(x);
				LevelEventButton component2 = obj.GetComponent<LevelEventButton>();
				component2.Init(levelEventType, num / 11, num % 11 + 1);
				eventButtons[LevelEventCategory.Favorites].Add(component2);
				num++;
			}
		}
		if (!firstTime)
		{
			SetCategory(currentCategory, changedFavorites: true);
		}
	}

	public void DecideInspectorTabsAtSelected()
	{
		if (SelectionIsSingle())
		{
			levelEventsPanel.ShowTabsForFloor(selectedFloors[0].seqID);
		}
	}

	public void AddEventAtSelected(LevelEventType eventType)
	{
		if (!SelectionIsSingle() || (lockPathEditing && !whitelistedEvents.Contains(eventType)))
		{
			return;
		}
		int sequenceID = selectedFloors[0].seqID;
		if ((eventType == LevelEventType.Hold && events.Exists((LevelEvent x) => x.eventType == LevelEventType.Pause && x.floor == sequenceID)) || (eventType == LevelEventType.Pause && events.Exists((LevelEvent x) => x.eventType == LevelEventType.Hold && x.floor == sequenceID)))
		{
			ShowNotification(RDString.Get("editor.errorHeldBeatPausedBeat"));
			return;
		}
		if ((eventType == LevelEventType.FreeRoam && events.Exists((LevelEvent x) => x.eventType == LevelEventType.Twirl && x.floor == sequenceID)) || (eventType == LevelEventType.Twirl && events.Exists((LevelEvent x) => x.eventType == LevelEventType.FreeRoam && x.floor == sequenceID)))
		{
			ShowNotification(RDString.Get("editor.errorFreeroamTwirl"));
			return;
		}
		if (eventType == LevelEventType.FreeRoam && selectedFloors[0].nextfloor == null)
		{
			ShowNotification(RDString.Get("editor.errorFreeroamAtEnd"));
			return;
		}
		using (new SaveStateScope(this))
		{
			LevelEvent levelEvent = events.Find((LevelEvent x) => x.eventType == eventType && x.floor == sequenceID);
			bool flag = EditorConstants.toggleableTypes.Contains(eventType);
			if (levelEvent != null && EditorConstants.soloTypes.Contains(eventType) && !flag)
			{
				return;
			}
			if (eventType == LevelEventType.FreeRoam && !events.Exists((LevelEvent x) => x.eventType == LevelEventType.PositionTrack && x.floor == sequenceID + 1))
			{
				LevelEvent levelEvent2 = new LevelEvent(selectedFloors[0].seqID + 1, LevelEventType.PositionTrack);
				levelEvent2["positionOffset"] = new Vector2(4f * Mathf.Sin((float)selectedFloors[0].exitangle), 4f * Mathf.Cos((float)selectedFloors[0].exitangle));
				levelEvent2.disabled["positionOffset"] = false;
				levelEvent2.disabled["opacity"] = false;
				events.Add(levelEvent2);
				LevelEvent levelEvent3 = new LevelEvent(selectedFloors[0].seqID + 1, LevelEventType.MoveCamera);
				levelEvent3.disabled["relativeTo"] = false;
				levelEvent3["relativeTo"] = CamMovementType.Player;
				events.Add(levelEvent3);
			}
			if (flag && levelEvent != null)
			{
				RemoveEvent(levelEvent);
				DecideInspectorTabsAtSelected();
			}
			else
			{
				AddEvent(sequenceID, eventType);
				levelEventsPanel.selectedEventType = eventType;
				int count = events.FindAll((LevelEvent x) => x.eventType == eventType && x.floor == sequenceID).Count;
				if (count == 1)
				{
					DecideInspectorTabsAtSelected();
					levelEventsPanel.ShowPanel(eventType);
				}
				else
				{
					levelEventsPanel.ShowPanel(eventType, count - 1);
				}
			}
			ApplyEventsToFloors();
			ShowEventIndicators(selectedFloors[0]);
		}
	}

	private void AddEvent(int floorID, LevelEventType eventType)
	{
		LevelEvent newLevelEvent = new LevelEvent(floorID, eventType);
		LevelEvent selectedEvent = levelEventsPanel.selectedEvent;
		if (!Persistence.disableAutoAngleOffset && selectedEvent != null && selectedEvent.ContainsKey("angleOffset") && newLevelEvent.ContainsKey("angleOffset") && newLevelEvent.eventType != LevelEventType.SetSpeed)
		{
			newLevelEvent["angleOffset"] = selectedEvent["angleOffset"];
		}
		events.Add(newLevelEvent);
		if (eventType == LevelEventType.SetHitsound)
		{
			List<LevelEvent> list = events.FindAll((LevelEvent e) => e.eventType == LevelEventType.SetHitsound).FindAll((LevelEvent e) => e["gameSound"] == newLevelEvent["gameSound"]);
			list.Sort((LevelEvent a, LevelEvent b) => a.floor.CompareTo(b.floor));
			LevelEvent levelEvent = list.FindLast((LevelEvent e) => e.floor < floorID);
			newLevelEvent["hitsoundVolume"] = ((levelEvent != null) ? levelEvent["hitsoundVolume"] : ((object)100));
		}
		if (eventType == LevelEventType.SetFilterAdvanced)
		{
			newLevelEvent["isNewlyAdded"] = true;
		}
	}

	public LevelEvent AddDecoration(LevelEventType eventType, int index = -1)
	{
		LevelEvent levelEvent = CreateDecoration(eventType);
		AddDecoration(levelEvent, index);
		return levelEvent;
	}

	public void AddDecoration(LevelEvent dec, int index = -1)
	{
		using (new SaveStateScope(this))
		{
			int index2 = ((index == -1) ? levelData.decorations.Count : (index + 1));
			levelData.decorations.Insert(index2, dec);
			scrDecorationManager.instance.CreateDecoration(dec, out var _, index);
		}
	}

	private LevelEvent CreateDecoration(LevelEventType eventType)
	{
		LevelEvent levelEvent = new LevelEvent(-1, eventType);
		Vector3 position = Camera.main.transform.position;
		if (selectedFloors.Count == 1)
		{
			levelEvent["relativeTo"] = DecPlacementType.Tile;
			levelEvent.floor = selectedFloors[0].seqID;
			levelEvent["position"] = Vector2.zero;
		}
		else
		{
			levelEvent["position"] = new Vector2(position.x, position.y) / ADOBase.controller.tileSize;
		}
		return levelEvent;
	}

	public void RemoveEvent(LevelEvent evnt, bool skipDecorationUpdate = false)
	{
		if (evnt == null)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			if (evnt.IsDecoration)
			{
				decorations.Remove(evnt);
				selectedDecorations.Remove(evnt);
				if (!skipDecorationUpdate)
				{
					decTransformGizmo.UpdateGizmosVisibility();
					UpdateDecorationObjects();
					levelEventsPanel.ShowPanel(LevelEventType.None);
					levelEventsPanel.HideAllInspectorTabs();
					refreshBgSprites = true;
					if (decorations.Count == 0)
					{
						decPivot.UpdatePivotCrossImage(enable: false);
					}
				}
			}
			else
			{
				events.Remove(evnt);
			}
			if (EventHasBackgroundSprite(evnt))
			{
				refreshBgSprites = true;
			}
		}
	}

	public void RemoveEvents(List<LevelEvent> events)
	{
		if (events == null || events.Count == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			for (int i = 0; i < events.Count; i++)
			{
				RemoveEvent(events[i], i != events.Count - 1);
			}
		}
	}

	public void RemoveEventAtSelected(LevelEventType eventType)
	{
		if (eventType == LevelEventType.None)
		{
			return;
		}
		if (GCS.levelEventsInfo[eventType.ToString()].isDecoration)
		{
			DeleteMultiSelectionDecorations();
			return;
		}
		int num = levelEventsPanel.EventNumOfTab(eventType);
		List<LevelEvent> selectedFloorEvents = GetSelectedFloorEvents(eventType);
		if ((lockPathEditing && !whitelistedEvents.Contains(eventType)) || selectedFloorEvents == null || num >= selectedFloorEvents.Count)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			RemoveEvent(selectedFloorEvents[num]);
			int num2 = selectedFloorEvents.Count - 1;
			if (num2 > 0)
			{
				num = Mathf.Clamp(num, 0, num2 - 1);
				levelEventsPanel.ShowPanel(eventType, num);
			}
			else
			{
				DecideInspectorTabsAtSelected();
			}
			ApplyEventsToFloors();
			ShowEventIndicators(selectedFloors[0]);
			floorButtonCanvas.transform.position = selectedFloors[0].transform.position;
			if (eventType == LevelEventType.Hold)
			{
				RemakePath();
			}
		}
	}

	private void OnGUI()
	{
		if (RDC.debug)
		{
			VideoPlayer videoBG = scrVfxPlus.instance.videoBG;
			string arg = ((videoBG != null) ? videoBG.time.ToString() : "none");
			string text = $"song.time: {ADOBase.conductor.song.time}\nvideoBg.time: {arg}";
			text += $"\n {videoBG != null} && {videoBG.gameObject.activeSelf} && {!videoBG.isPlaying} && {!scrVfxPlus.instance.hasPlayed} && {videoBG.isPrepared}";
			GUI.Label(new Rect(0f, 300f, 200f, 200f), text);
			Rect position = new Rect(200f, 0f, 1000f, 1000f);
			string text2 = "scnEditor: ";
			text2 = text2 + "\n" + customLevel.imgHolder.ToString();
			GUI.Label(position, text2);
		}
	}

	public void LoadEditorProperties()
	{
		LevelEventType[] source = new LevelEventType[5]
		{
			LevelEventType.ChangeTrack,
			LevelEventType.AddDecoration,
			LevelEventType.AddText,
			LevelEventType.AddObject,
			LevelEventType.AddParticle
		};
		Dictionary<LevelEventCategory, int> dictionary = new Dictionary<LevelEventCategory, int>();
		foreach (LevelEventCategory value in Enum.GetValues(typeof(LevelEventCategory)))
		{
			dictionary[value] = 0;
			eventButtons[value] = new List<LevelEventButton>();
		}
		foreach (LevelEventInfo value2 in GCS.levelEventsInfo.Values)
		{
			LevelEventType levelEventType = RDUtils.ParseEnum(value2.name, LevelEventType.None);
			if (source.Contains(levelEventType) || !value2.isActive)
			{
				continue;
			}
			foreach (LevelEventCategory category in value2.categories)
			{
				int num = dictionary[category];
				if (levelEventType != LevelEventType.FreeRoamWarning)
				{
					GameObject obj = UnityEngine.Object.Instantiate(prefab_levelEventButton, levelEventsBarButtons);
					RectTransform component = obj.GetComponent<RectTransform>();
					float x = 0f + (0f + component.sizeDelta.x) * (float)(num % 11);
					component.SetAnchorPosX(x);
					LevelEventButton component2 = obj.GetComponent<LevelEventButton>();
					component2.Init(levelEventType, num / 11, num % 11 + 1);
					eventButtons[category].Add(component2);
					dictionary[category]++;
				}
			}
		}
		int num2 = 0;
		foreach (LevelEventCategory value3 in Enum.GetValues(typeof(LevelEventCategory)))
		{
			if (eventButtons[value3].Count == 0 && value3 != LevelEventCategory.Favorites)
			{
				num2++;
				continue;
			}
			GameObject obj2 = UnityEngine.Object.Instantiate(prefab_eventCategoryTab, levelEventsBarCategories);
			obj2.GetComponent<RectTransform>().SetAnchorPosY(55f);
			CategoryTab component3 = obj2.GetComponent<CategoryTab>();
			component3.Init(value3);
			categoryTabs.Add(component3);
			num2++;
		}
		SetupFavoritesCategory(firstTime: true);
		SetCategory(currentCategory);
		settingsPanel.Init(GCS.settingsInfo, floorPanel: false);
		levelEventsPanel.Init(GCS.levelEventsInfo, floorPanel: true);
	}

	public void ToggleAuto()
	{
		RDC.auto = !RDC.auto;
		autoFailed = false;
		if (RDC.auto)
		{
			ottoAudioSrc.clip = ADOBase.gc.soundEffects[highBPM ? 6 : 5];
			ShowNotification(RDString.Get("editor.notification.autoplayEnabled"));
		}
		else
		{
			ottoAudioSrc.clip = ADOBase.gc.soundEffects[7];
			ShowNotification(RDString.Get("editor.notification.autoplayDisabled"));
		}
		ottoAudioSrc.Play();
		if (playMode && ADOBase.controller.unlockKeyLimiter)
		{
			buttonUnlockKeyLimiter.gameObject.SetActive(!RDC.auto);
		}
	}

	private void OttoUpdate()
	{
		if (RDEditorUtils.CheckPointerInObject(buttonAuto))
		{
			OttoPetUpdate();
		}
		else
		{
			autoPetTime = 0f;
		}
		UnityEngine.Color color = (highBPM ? UnityEngine.Color.red : UnityEngine.Color.white);
		if (isOttoBlinking)
		{
			return;
		}
		if (RDC.auto)
		{
			if (!autoFailed)
			{
				if (autoPetTime < 1.5f)
				{
					autoImage.sprite = ((highBPM && !playMode) ? autoSprites[7] : autoSprites[1]);
				}
				else
				{
					autoImage.sprite = autoSprites[9];
				}
			}
			else
			{
				autoImage.sprite = autoSprites[6];
			}
			autoImage.color = color;
		}
		else
		{
			autoImage.sprite = ((highBPM && !playMode) ? autoSprites[8] : autoSprites[0]);
			autoImage.color = grayColor * color;
		}
	}

	public void OttoBlink()
	{
		float autoBlinkDuration = ADOBase.controller.GetAutoBlinkDuration();
		int num = ((ottoBlinkCounter % 2 != 0) ? (RDC.auto ? 2 : 4) : (RDC.auto ? 3 : 5));
		ottoBlinkCounter++;
		autoImage.sprite = autoSprites[num];
		if (blinkTimer != null && blinkTimer.active)
		{
			blinkTimer.Kill();
		}
		blinkTimer = DOTween.Sequence().AppendInterval(autoBlinkDuration).OnComplete(delegate
		{
			isOttoBlinking = false;
		})
			.SetUpdate(isIndependentUpdate: true)
			.OnKill(delegate
			{
				isOttoBlinking = false;
			});
		isOttoBlinking = true;
	}

	private void OttoPetUpdate()
	{
		float unscaledTime = Time.unscaledTime;
		if (RDC.auto)
		{
			if (lastOttoPetPosition != Input.mousePosition)
			{
				autoPetTime += Time.unscaledDeltaTime;
				lastOttoPetTime = unscaledTime;
				lastOttoPetPosition = Input.mousePosition;
			}
		}
		else
		{
			autoPetTime = 0f;
		}
	}

	public void ToggleNoFail()
	{
		ADOBase.controller.noFail = !ADOBase.controller.noFail;
		if (ADOBase.controller.noFail)
		{
			buttonNoFail.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.white;
			interfaceAudioSrc.PlayOneShot(ADOBase.gc.soundEffects[10]);
			ShowNotification(RDString.Get("editor.notification.noFailEnabled"));
		}
		else
		{
			buttonNoFail.GetComponent<UnityEngine.UI.Image>().color = grayColor;
			interfaceAudioSrc.PlayOneShot(ADOBase.gc.soundEffects[11]);
			ShowNotification(RDString.Get("editor.notification.noFailDisabled"));
		}
	}

	private void ToggleUnlockKeyLimiter()
	{
		SetUnlockKeyLimiter(!ADOBase.controller.unlockKeyLimiter);
	}

	private void SetUnlockKeyLimiter(bool enable, bool byUser = true)
	{
		ADOBase.controller.unlockKeyLimiter = enable;
		unlockKeyLimiterImage.color = (enable ? UnityEngine.Color.white : grayColor);
		if (byUser)
		{
			interfaceAudioSrc.PlayOneShot(ADOBase.gc.soundEffects[enable ? 10 : 11]);
			ShowNotification(RDString.Get("editor.notification.unlockKeyLimiter" + (enable ? "Enabled" : "Disabled")));
		}
	}

	public void DeleteSubsequentFloors()
	{
		if (selectedFloors.Count == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			int seqID = selectedFloors[0].seqID;
			for (int num = floors.Count() - 2; num > seqID; num--)
			{
				DeleteFloor(seqID + 1, remakePath: false);
			}
			DeleteFloor(seqID + 1);
			SelectFloor(floors[seqID]);
		}
	}

	public void DeletePrecedingFloors()
	{
		if (selectedFloors.Count == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			int num = selectedFloors[0].seqID;
			if (num != 0)
			{
				while (num > 1)
				{
					DeleteFloor(1, remakePath: false);
					num--;
				}
				DeleteFloor(1);
				SelectFloor(floors[0]);
			}
		}
	}

	public void DeleteSingleSelection(bool backspace = true)
	{
		if (lockPathEditing)
		{
			return;
		}
		int seqID = selectedFloors[0].seqID;
		if ((backspace && seqID == 0) || (!backspace && seqID == floors.Count - 1))
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			if (backspace)
			{
				if (DeleteFloor(seqID))
				{
					SelectFloor(floors[seqID - 1]);
				}
			}
			else if (DeleteFloor(seqID + 1))
			{
				SelectFloor(floors[seqID]);
			}
		}
	}

	public void DeleteMultiSelection(bool backspace = true)
	{
		if (lockPathEditing || selectedFloors[0].seqID == 0)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			int num = selectedFloors[0].seqID;
			int num2 = selectedFloors.Last().seqID;
			DeselectAllFloors();
			while (num < num2)
			{
				if (DeleteFloor(num, remakePath: false))
				{
					num2--;
				}
				else
				{
					num++;
				}
			}
			DeleteFloor(num);
			if (backspace)
			{
				SelectFloor(floors[num - 1]);
			}
			else if (num == floors.Count())
			{
				SelectFloor(floors[num - 1]);
			}
			else
			{
				SelectFloor(floors[num]);
			}
		}
	}

	public void DeleteMultiSelectionDecorations()
	{
		if (!SelectionDecorationIsEmpty())
		{
			List<LevelEvent> list = new List<LevelEvent>(selectedDecorations);
			RemoveEvents(list);
			decTransformGizmo.UpdateGizmosVisibility();
		}
	}

	public List<LevelEvent> GetSelectedFloorEvents(LevelEventType eventType)
	{
		return GetFloorEvents(selectedFloors[0].seqID, eventType);
	}

	public List<LevelEvent> GetFloorEvents(int floorID, LevelEventType eventType)
	{
		if (eventType.IsSetting())
		{
			return null;
		}
		List<LevelEvent> list = new List<LevelEvent>();
		foreach (LevelEvent @event in events)
		{
			if (floorID == @event.floor && @event.eventType == eventType)
			{
				list.Add(@event);
			}
		}
		return list;
	}

	private void ShowEventPicker(bool show)
	{
		float endValue = (show ? 0f : (0f - levelEventsBar.sizeDelta.y - levelEventsBarCategories.sizeDelta.y - 5f));
		levelEventsBar.DOAnchorPosY(endValue, 0.25f).SetUpdate(isIndependentUpdate: true).SetEase(UIPanelEaseMode);
		foreach (CategoryTab categoryTab in categoryTabs)
		{
			categoryTab.SetSelected(show && currentCategory == categoryTab.levelEventCategory);
		}
		if (show)
		{
			ShowEventsPage(currentPage);
			return;
		}
		eventPickerText.text = "";
		categoryText.text = "";
	}

	private void SaveBackup()
	{
		if (playMode || string.IsNullOrEmpty(ADOBase.levelPath) || saveBackupLastFrame == saveStateLastFrame || !unsavedChanges)
		{
			return;
		}
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(ADOBase.levelPath);
		string text = "backup";
		if (fileNameWithoutExtension == text)
		{
			text = "backup2";
		}
		string data = levelData.Encode();
		try
		{
			RDFile.WriteAllText(Path.GetDirectoryName(ADOBase.levelPath) + "/" + text + ".adofai", data);
		}
		catch (Exception)
		{
		}
	}

	private void OpenDiscord()
	{
		ADOBase.platformHelper.OpenURL("https://discord.gg/rhythmdr");
	}

	private UnityEngine.Color[] SelectedColors(UnityEngine.Color color)
	{
		UnityEngine.Color[] array = new UnityEngine.Color[2];
		new UnityEngine.Color(1f - color.r, 1f - color.g, 1f - color.b);
		UnityEngine.Color.RGBToHSV(color, out var H, out var S, out var V);
		H = (H + 0.25f) % 1f;
		V = Mathf.Clamp01(V * 1.2f);
		float s = Mathf.Clamp01(S + 0.5f);
		float v = ((V > 0.75f) ? (V - 0.25f) : (V + 0.25f));
		array[0] = UnityEngine.Color.HSVToRGB(H, S, V);
		array[1] = UnityEngine.Color.HSVToRGB(H, s, v);
		return array;
	}

	public void Undo()
	{
		UndoOrRedo(redo: false);
	}

	public void Redo()
	{
		UndoOrRedo(redo: true);
	}

	public void UndoOrRedo(bool redo)
	{
		if (changingState != 0)
		{
			return;
		}
		List<LevelState> list = (redo ? redoStates : undoStates);
		if (list.Count <= 0)
		{
			return;
		}
		bool dataHasChanged = list.Count > 0 && list.Last().data != null;
		using (new SaveStateScope(this, clearRedo: false, dataHasChanged))
		{
			if (!redo)
			{
				redoStates.Add(undoStates.Pop());
			}
			LevelState levelState = list.Last();
			int[] selectedDecorationIndices = levelState.selectedDecorationIndices;
			if (levelState.data != null)
			{
				customLevel.levelData = levelState.data;
			}
			DeselectFloors();
			RemakePath();
			DeselectAllDecorations();
			UpdateDecorationObjects();
			int[] array = selectedDecorationIndices;
			foreach (int num in array)
			{
				if (customLevel.levelData.decorations.Count > num)
				{
					LevelEvent levelEvent = customLevel.levelData.decorations[num];
					SelectDecoration(levelEvent, jumpToDecoration: false, showPanel: false, ignoreDeselection: true);
				}
			}
			if (!SelectionDecorationIsEmpty())
			{
				LevelEvent levelEvent2 = selectedDecorations[selectedDecorations.Count - 1];
				levelEventsPanel.ShowInspector(show: true, forceAction: true);
				levelEventsPanel.ShowPanel(levelEvent2.eventType);
			}
			propertyControlDecorationsList.RefreshItemsList(forceRefreshAll: true);
			List<int> list2 = levelState.selectedFloors;
			if (list2.Count > 1)
			{
				MultiSelectFloors(floors[list2[0]], floors[list2[list2.Count - 1]]);
			}
			else if (list2.Count == 1)
			{
				int index = list2[0];
				SelectFloor(floors[index]);
				levelEventsPanel.ShowPanel(levelState.floorEventType, levelState.floorEventTypeIndex);
			}
			settingsPanel.ShowPanel(levelState.settingsEventType);
			if (particleEditor.gameObject.activeSelf && particleEditor.SelectedEvent != null)
			{
				if (selectedDecorations.Count == 0)
				{
					HideParticleEditor();
				}
				else
				{
					ParticleEditor obj = particleEditor;
					List<LevelEvent> list3 = selectedDecorations;
					obj.SetEvent(list3[list3.Count - 1]);
				}
			}
			list.RemoveAt(list.Count - 1);
		}
	}

	public void SaveState(bool clearRedo = true, bool dataHasChanged = true)
	{
		if (changingState != 0 || !initialized)
		{
			return;
		}
		List<int> list = new List<int>();
		if (!SelectionIsEmpty())
		{
			if (SelectionIsSingle())
			{
				list.Add(selectedFloors[0].seqID);
			}
			else
			{
				foreach (scrFloor selectedFloor in selectedFloors)
				{
					list.Add(selectedFloor.seqID);
				}
			}
		}
		LevelData data = levelData.Copy();
		int[] array = new int[selectedDecorations.Count];
		int num = 0;
		foreach (LevelEvent selectedDecoration in selectedDecorations)
		{
			array[num] = scrDecorationManager.GetDecorationIndex(selectedDecoration);
			num++;
		}
		LevelState levelState = new LevelState(data, list, array, dataHasChanged);
		levelState.settingsEventType = settingsPanel.selectedEventType;
		levelState.floorEventType = levelEventsPanel.selectedEventType;
		levelState.floorEventTypeIndex = levelEventsPanel.EventNumOfTab(levelState.floorEventType);
		undoStates.Add(levelState);
		if (clearRedo)
		{
			redoStates.Clear();
		}
		if (undoStates.Count > 100)
		{
			undoStates.RemoveAt(0);
		}
		if (dataHasChanged)
		{
			unsavedChanges = true;
		}
		saveStateLastFrame = Time.frameCount;
	}

	public void ApplyEventsToFloors()
	{
		customLevel.ApplyEventsToFloors(floors);
		DrawFloorOffsetLines();
		DrawHolds();
		DrawMultiPlanet();
		refreshDecSprites = true;
	}

	public void Play()
	{
		if (floors.Count == 1)
		{
			return;
		}
		ADOBase.playerManager.UnlockAllPlayerInput();
		controlsTip.enabled = true;
		ottoBlinkCounter = 0;
		GCS.editorQuickPitchedPlaying = holdingControl;
		playbackSpeed = (holdingControl ? ((float)Persistence.shortcutPlaySpeed / 100f) : 1f);
		decorationWasSelected = false;
		int num;
		if (SelectionIsSingle())
		{
			num = selectedFloors[0].seqID;
			cacheSelectedEventIndex = levelEventsPanel.cacheEventIndex;
		}
		else if (!SelectionDecorationIsEmpty() && (bool)lastSelectedFloor)
		{
			num = lastSelectedFloor.seqID;
			decorationWasSelected = true;
			lastSelectedDecorations = new List<LevelEvent>(selectedDecorations);
		}
		else
		{
			num = 0;
		}
		selectedFloorCached = num;
		DeselectAnyUIGameObject();
		DeselectFloors(skipSaving: true);
		DeselectAllDecorations();
		ClearPopupBlocker();
		ClearFloorGlows();
		ADOBase.conductor.gameObject.SetActive(value: true);
		RemakePath(applyEventsToFloors: false);
		foreach (GameObject floorConnectorGO in floorConnectorGOs)
		{
			UnityEngine.Object.Destroy(floorConnectorGO);
		}
		floorConnectorGOs.Clear();
		foreach (scrFloor floor in floors)
		{
			floor.editorNumText.gameObject.SetActive(value: false);
		}
		ADOBase.controller.currentSeqID = 0;
		GCS.checkpointNum = num;
		customLevel.ReloadAssets();
		customLevel.Play(num);
		DrawHolds();
		DrawMultiPlanet();
		editorDifficultySelector.SetChangeable(changeable: false);
		buttonNoFail.interactable = false;
		buttonUnlockKeyLimiter.interactable = false;
		if (!Persistence.showUnlockKeyLimiterButton && ADOBase.controller.unlockKeyLimiter)
		{
			SetUnlockKeyLimiter(enable: false, byUser: false);
		}
		if (RDC.auto || !ADOBase.controller.unlockKeyLimiter)
		{
			buttonUnlockKeyLimiter.gameObject.SetActive(value: false);
		}
		scrDecorationManager obj = scrDecorationManager.instance;
		obj.ShowEmptyDecorations(show: false);
		obj.ToggleClickableBoxColliderForLevelEditor(value: false);
		if (Persistence.GetHideCursorWhilePlaying())
		{
			Cursor.visible = false;
		}
	}

	public void ShowExportWindow(int state)
	{
		if (!GCS.isDev && !GCS.steamBranchName.IsNullOrEmpty() && !GCNS.stableBranches.Contains(GCS.steamBranchName))
		{
			ShowPopup(show: true, PopupType.BetaBranchesWorkshopPublish);
		}
		if (GCS.customLevelId != null)
		{
			ShowPopup(show: true, PopupType.WorkshopLevelReupload);
		}
		else if (ADOBase.levelPath.IsNullOrEmpty())
		{
			ShowPopup(show: true, PopupType.SaveBeforeLevelExport);
		}
		else if (levelData.GetMissingParams().Count > 0)
		{
			ShowPopup(show: true, PopupType.MissingExportParams);
		}
		else if (!levelData.songFilename.IsNullOrEmpty() && levelData.songFilename.Split('.').Last() == "mp3")
		{
			soundToConvert = Path.Combine(Path.GetDirectoryName(ADOBase.levelPath), levelData.songFilename);
			ShowPopup(show: true, PopupType.OggEncode);
		}
		else if (GetMissingFiles().Count > 0)
		{
			ShowPopup(show: true, PopupType.MissingFiles);
		}
		else
		{
			ShowPopup(show: true, PopupType.ExportLevel);
		}
	}

	private string GetExportLevelTempDirectory()
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
		return RDUtils.GetAvailableDirectoryName(Path.Combine(Persistence.DataPath, "Temp", fileNameWithoutExtension));
	}

	private List<string> GetExportLevelFiles(string tempDir)
	{
		bool uploadable;
		DLCManager[] requiredDLC;
		return GetExportLevelFiles(tempDir, out uploadable, out requiredDLC);
	}

	private List<string> GetExportLevelFiles(string tempDir, out bool uploadable, out DLCManager[] requiredDLC)
	{
		uploadable = true;
		string data = this.levelData.Encode();
		RDFile.WriteAllText(ADOBase.levelPath, data);
		List<string> includedFiles = new List<string>();
		List<string> list = new List<string>();
		List<DLCManager> list2 = new List<DLCManager>();
		string levelDir = Path.GetDirectoryName(ADOBase.levelPath);
		if (!Directory.Exists(tempDir))
		{
			RDDirectory.CreateDirectory(tempDir);
		}
		string text = Path.Combine(tempDir, "main.adofai");
		File.Copy(ADOBase.levelPath, text, overwrite: true);
		list.Add(text);
		string[] worldPaths = scnGame.GetWorldPaths(ADOBase.levelPath, excludeMain: true);
		string[] array = new string[worldPaths.Length];
		int num = 1;
		string[] array2 = worldPaths;
		foreach (string sourceFileName in array2)
		{
			string text2 = Path.Combine(tempDir, "sub" + num + ".adofai");
			File.Copy(sourceFileName, text2, overwrite: true);
			array[num - 1] = text2;
			num++;
		}
		list.AddRange(array);
		includedFiles.AddRange(list);
		AddFile(this.levelData.previewImage);
		AddFile(this.levelData.previewIcon);
		AddFile(this.levelData.artistPermission);
		foreach (string item in list)
		{
			Dictionary<string, object> dictionary = Json.Deserialize(RDFile.ReadAllText(item)) as Dictionary<string, object>;
			LevelData levelData = new LevelData();
			if (dictionary == null)
			{
				continue;
			}
			levelData.Decode(dictionary, out var status);
			if (status == LoadResult.ModRequired)
			{
				uploadable = false;
			}
			levelData.RefreshRequiredDLC();
			list2.AddMany(levelData.requiredDLC);
			AddFile(levelData.bgImage);
			AddFile(levelData.songFilename);
			AddFile(levelData.bgVideo);
			foreach (LevelEvent levelEvent in levelData.levelEvents)
			{
				switch (levelEvent.eventType)
				{
				case LevelEventType.CustomBackground:
					AddFile(levelEvent.GetString("bgImage"));
					break;
				case LevelEventType.ColorTrack:
				case LevelEventType.RecolorTrack:
					AddFile(levelEvent.GetString("trackTexture"));
					break;
				case LevelEventType.MoveDecorations:
				{
					string text3 = levelEvent.GetString("decorationImage");
					if (!levelEvent.disabled["decorationImage"] && !text3.IsNullOrEmpty())
					{
						AddFile(text3);
					}
					break;
				}
				}
			}
			foreach (LevelEvent decoration in levelData.decorations)
			{
				LevelEventType eventType = decoration.eventType;
				if (eventType == LevelEventType.AddDecoration || eventType == LevelEventType.AddParticle)
				{
					AddFile(decoration.GetString("decorationImage"));
				}
			}
		}
		requiredDLC = list2.Distinct().ToArray();
		return includedFiles;
		void AddFile(string filename)
		{
			if (!filename.IsNullOrEmpty())
			{
				includedFiles.Add(Path.Combine(levelDir, filename));
			}
		}
	}

	public string MakeThumbnail(DLCManager[] requiredDLC = null)
	{
		string path = Path.Combine(Path.GetDirectoryName(ADOBase.levelPath), levelData.previewImage);
		Texture2D texture2D = ADOBase.gc.sprite_defaultPortal.texture;
		if (File.Exists(path))
		{
			byte[] data = File.ReadAllBytes(path);
			texture2D = new Texture2D(2, 2);
			texture2D.LoadImage(data);
		}
		return thumbnailMaker.MakeThumbnail(texture2D, levelData.previewIconColor, requiredDLC);
	}

	public void ExportLevel(bool uploadToSteam = false)
	{
		if (!uploadToSteam)
		{
			string text = FileBrowser.SaveFile(Persistence.GetLastUsedFolder(), "level.adozip", RDString.Get("editor.dialog.adofaizipDescription"), new string[1] { "adozip" }, RDString.Get("editor.dialog.exportLevel"));
			if (!string.IsNullOrEmpty(text))
			{
				if (!text.EndsWith(".adozip", StringComparison.OrdinalIgnoreCase))
				{
					text += ".adozip";
				}
				string data = levelData.Encode();
				RDFile.WriteAllText(ADOBase.levelPath, data);
				string exportLevelTempDirectory = GetExportLevelTempDirectory();
				List<string> exportLevelFiles = GetExportLevelFiles(exportLevelTempDirectory);
				ZipUtils.Zip(text, exportLevelFiles.Distinct().ToArray());
				ShowNotification(RDString.Get("editor.notification.levelExported"));
			}
			else
			{
				Debug.Log("Export cancelled!");
			}
		}
		else
		{
			PublishToSteam(null).Forget();
		}
	}

	public async UniTask PublishToSteam(PublishedFileId? updateId)
	{
		string exportLevelTempDirectory = GetExportLevelTempDirectory();
		bool uploadable;
		DLCManager[] requiredDLC;
		List<string> exportLevelFiles = GetExportLevelFiles(exportLevelTempDirectory, out uploadable, out requiredDLC);
		if (!uploadable)
		{
			return;
		}
		string previewImagePath = MakeThumbnail(requiredDLC);
		publishWindow.UploadInProgress.Value = true;
		foreach (string item3 in exportLevelFiles)
		{
			if (!RDFile.Exists(item3))
			{
				ShowNotification(RDString.Get("editor.notification.exportFailed"));
				return;
			}
			string text = Path.Combine(exportLevelTempDirectory, Path.GetFileName(item3));
			if (!RDFile.Exists(text) || new FileInfo(item3).LastWriteTimeUtc > new FileInfo(text).LastWriteTimeUtc)
			{
				RDFile.Copy(item3, text, overwrite: true);
			}
		}
		List<string> list = new List<string> { "Level" };
		list.AddRange(levelData.levelTags.Split(','));
		int difficulty = levelData.difficulty;
		string item = ((difficulty <= 3) ? "Easy" : ((difficulty <= 6) ? "Medium" : ((difficulty <= 9) ? "Tough" : "Very Tough")));
		list.Add(item);
		list.AddRange(requiredDLC.Select((DLCManager x) => x.steamWorkshopTag));
		ApprovalLevel approvalLevel = ApprovalLevelForArtist(levelData.artist);
		SpecialArtistType specialArtistType = levelData.specialArtistType;
		if (levelData.seizureWarning)
		{
			list.Add("Seizure Warning");
		}
		if (scnGame.GetWorldPaths(ADOBase.levelPath, excludeMain: true).Length != 0)
		{
			list.Add("World");
		}
		if (approvalLevel != ApprovalLevel.Allowed && approvalLevel != ApprovalLevel.PartiallyDeclined)
		{
			if (specialArtistType == SpecialArtistType.AuthorIsArtist)
			{
				list.Add("Composed by me");
			}
			if (specialArtistType == SpecialArtistType.PublicLicense)
			{
				list.Add("Public License");
			}
		}
		bool flag = specialArtistType == SpecialArtistType.None;
		if (approvalLevel == ApprovalLevel.Pending && flag)
		{
			list.Add("New Artist");
		}
		if (approvalLevel == ApprovalLevel.ListingRejected && flag)
		{
			list.Add("One-time Approval");
		}
		try
		{
			PublishResult publishResult = await SteamWorkshop.UploadToWorkshop(levelData.fullCaption, levelData.levelDesc, previewImagePath, exportLevelTempDirectory, list.Distinct().ToArray(), updateId, requiredDLC, delegate(float v)
			{
				publishWindow.UploadProgress.Value = v;
			});
			Item item2 = new Item(publishResult.FileId);
			publishWindow.UploadInProgress.Value = false;
			publishWindow.SwitchToResultPage(publishResult.NeedsWorkshopAgreement);
			await item2.Subscribe();
			ShowNotification(RDString.Get("editor.notification.levelExported"));
		}
		catch (SteamWorkshop.WorkshopError workshopError)
		{
			SteamWorkshop.WorkshopErrorMessage[] messages = workshopError.Messages;
			for (int num = 0; num < messages.Length; num++)
			{
				_ = messages[num];
			}
			ShowNotification(RDString.Get("editor.notification.exportFailed"));
		}
	}

	private void AcceptAgreement()
	{
		ShowPopup(show: false, PopupType.CopyrightWarning, skipAnim: true);
		Persistence.acceptedAgreement = true;
		Persistence.Save();
		ShowEventPicker(show: true);
		settingsPanel.ShowInspector(show: true);
	}

	private void DeclineAgreement()
	{
		ADOBase.controller.QuitToMainMenu();
	}

	public void ShowPropertyHelp(bool show, Transform location = null, string helpText = "", string buttonText = null, string buttonURL = null)
	{
		if (animatingPropertyHelp || show == showingPropertyHelp)
		{
			return;
		}
		animatingPropertyHelp = true;
		showingPropertyHelp = show;
		float duration = 0.15f;
		bool active = !string.IsNullOrEmpty(buttonURL);
		propertyHelpURLButton.gameObject.SetActive(active);
		RectTransform component = propertyHelpText.GetComponent<RectTransform>();
		if (show)
		{
			propertyHelpText.text = helpText;
			propertyHelpContainer.position = location.position;
			propertyHelpContainer.AnchorPosY(propertyHelpContainer.anchoredPosition.y - 27f);
			RectTransform component2 = propertyHelpImage.GetComponent<RectTransform>();
			propertyHelpText.GetComponent<ContentSizeFitter>().enabled = true;
			Canvas.ForceUpdateCanvases();
			float y = component.rect.height + 20f;
			component2.SizeDeltaY(y);
			if (propertyHelpURLButton.gameObject.activeSelf)
			{
				propertyHelpURLButtonText.text = buttonText;
				propertyHelpURLButton.onClick.RemoveAllListeners();
				propertyHelpURLButton.onClick.AddListener(delegate
				{
					Application.OpenURL(buttonURL);
				});
			}
		}
		bool flag = propertyHelpContainer.position.x + component.rect.width > (float)Screen.width;
		Vector3 vector = Vector3.one.WithX((!flag) ? 1 : (-1));
		propertyHelpText.transform.localScale = vector;
		propertyHelpURLButton.transform.localScale = vector;
		if (show && flag)
		{
			propertyHelpContainer.position = propertyHelpContainer.position.WithX(location.position.x - location.GetComponent<RectTransform>().rect.width);
		}
		Vector3 endValue = (show ? vector : Vector3.zero);
		Ease ease = (show ? Ease.OutCubic : Ease.InCubic);
		propertyHelpImage.DOScale(endValue, duration).SetEase(ease).SetUpdate(isIndependentUpdate: true)
			.OnComplete(delegate
			{
				animatingPropertyHelp = false;
			});
	}

	private List<string> GetMissingFiles()
	{
		string exportLevelTempDirectory = GetExportLevelTempDirectory();
		bool uploadable;
		DLCManager[] requiredDLC;
		List<string> exportLevelFiles = GetExportLevelFiles(exportLevelTempDirectory, out uploadable, out requiredDLC);
		List<string> list = new List<string>();
		foreach (string item in exportLevelFiles)
		{
			if (!RDFile.Exists(item))
			{
				list.Add(Path.GetFileName(item));
			}
		}
		return list;
	}

	public ApprovalLevel ApprovalLevelForArtist(string artistName)
	{
		artistName = artistName.ToLower().Trim();
		artistName = new Regex("<.*?>").Replace(artistName, "");
		ArtistData[] artists = EditorWebServices.artists;
		foreach (ArtistData artistData in artists)
		{
			if (artistName == artistData.nameLowercase)
			{
				return artistData.approvalLevel;
			}
		}
		return ApprovalLevel.Pending;
	}

	public void oggConCallback(float percent)
	{
		oggConversionBar.value = percent / 100f;
		oggConversionBarText.text = string.Format("{0}  {1:F0}%", RDString.Get("editor.dialog.converting"), percent);
	}

	public void SetLevelSong(string resultName)
	{
		levelData.songFilename = resultName;
		UpdateSongAndLevelSettings();
		customLevel.ReloadSong();
	}

	private IEnumerator ConvertSoundToOggCo(Action<string> successfulCallback)
	{
		if (successfulCallback == null)
		{
			Debug.Log("successfulCallback was not set");
			yield break;
		}
		popupOggCancel.interactable = false;
		popupOggConvert.interactable = false;
		oggConversionBar.gameObject.SetActive(value: true);
		oggConversionBar.value = 0f;
		oggConversionBarText.text = RDString.Get("editor.dialog.converting") + "  0%";
		string directoryName = Path.GetDirectoryName(ADOBase.levelPath);
		string soundName = soundToConvert;
		string path = Path.Combine(directoryName, soundName);
		string resultName = Path.GetFileNameWithoutExtension(soundName) + ".ogg";
		string resultPath = Path.Combine(directoryName, resultName);
		if (File.Exists(resultPath))
		{
			Debug.Log(resultPath + " already exists! defaulting to that...");
			successfulCallback(resultName);
			ShowNotificationPopup(RDString.Get("editor.notification.oggAlreadyExists"));
			ShowPopup(show: false);
			yield break;
		}
		yield return ADOBase.audioManager.FindOrLoadAudioClipExternal(path, mp3Streaming: false);
		string songKey = soundName + "*external";
		if (ADOBase.audioManager.audioLib.ContainsKey(songKey))
		{
			AudioClip audioClip = ADOBase.audioManager.audioLib[songKey];
			yield return AudioclipToOggEncoder.EncodeToOgg(audioClip, 0f, audioClip.length, resultPath, oggConCallback);
			ADOBase.audioManager.audioLib.Remove(songKey);
			if (ADOBase.audioManager.audioLibHandles.TryGetValue(songKey, out var value))
			{
				Addressables.Release(value);
				ADOBase.audioManager.audioLibHandles.Remove(songKey);
			}
			oggConversionBarText.text = RDString.Get("editor.dialog.convert");
			successfulCallback(resultName);
			ShowPopup(show: true, PopupType.ConversionSuccessful, skipAnim: true);
		}
		else
		{
			ShowPopup(show: true, PopupType.ConversionError, skipAnim: true);
		}
	}

	public int PushPopupBlocker(Action onClickAction)
	{
		int result = _currentPopupSortOrder + 1;
		popupBlocker.gameObject.SetActive(value: true);
		Button component = popupBlocker.GetComponent<Button>();
		component.onClick.RemoveAllListeners();
		component.onClick.AddListener(PopPopupBlocker);
		popupBlocker.GetComponent<Canvas>().sortingOrder = _currentPopupSortOrder;
		_popupStack.Add(onClickAction);
		_currentPopupSortOrder += 2;
		return result;
	}

	public void ClearPopupBlocker()
	{
		while (_popupStack.Count > 0)
		{
			PopPopupBlocker();
		}
	}

	public void PopPopupBlocker()
	{
		if (_popupStack.Count != 0)
		{
			List<Action> popupStack = _popupStack;
			Action action = popupStack[popupStack.Count - 1];
			_popupStack.Remove(action);
			action();
			if (_popupStack.Count == 0)
			{
				popupBlocker.gameObject.SetActive(value: false);
			}
			_currentPopupSortOrder -= 2;
			popupBlocker.GetComponent<Canvas>().sortingOrder = _currentPopupSortOrder - 2;
		}
	}

	public void ZoomOutUI()
	{
		float height = Persistence.editorScale + 50f;
		UpdateCanvasScalerResolution(height);
	}

	public void ZoomInUI()
	{
		float height = Persistence.editorScale - 50f;
		UpdateCanvasScalerResolution(height);
	}

	public void ToggleShortcutsLock()
	{
		LockPathEditing(!lockPathEditing);
	}

	public void LockPathEditing(bool locked)
	{
		lockPathEditing = locked;
		lockBackground.color = (lockPathEditing ? shortcutsLockColor : defaultLockColor);
		lockIcon.color = (lockPathEditing ? shortcutsLockIconColor : defaultLockIconColor);
		lockIcon.sprite = (lockPathEditing ? lockSpriteOn : lockSpriteOff);
		floorButtonContainer.SetActive(!lockPathEditing);
	}

	private void DragCamera(Vector3 delta)
	{
		camera.transform.DOKill();
		camera.transform.position = new Vector3(delta.x, delta.y, -10f);
	}

	private void DragTilesStart()
	{
		floorPositionsAtDragStart.Clear();
		if (SelectionIsSingle())
		{
			floorPositionsAtDragStart[selectedFloors[0]] = selectedFloors[0].transform.position;
		}
		else
		{
			foreach (scrFloor selectedFloor in selectedFloors)
			{
				floorPositionsAtDragStart[selectedFloor] = selectedFloor.transform.position;
			}
		}
		pointerDownObjectType = PointerDownObjectType.Floor;
	}

	private void DragTiles(Vector3 translation)
	{
		if (SelectionIsSingle())
		{
			scrFloor scrFloor2 = selectedFloors[0];
			Vector3 vector = floorPositionsAtDragStart[scrFloor2] + translation;
			scrFloor2.transform.position = new Vector3(vector.x, vector.y, scrFloor2.transform.position.z);
			return;
		}
		foreach (scrFloor selectedFloor in selectedFloors)
		{
			Vector3 vector2 = floorPositionsAtDragStart[selectedFloor] + translation;
			selectedFloor.transform.position = new Vector3(vector2.x, vector2.y, selectedFloor.transform.position.z);
		}
	}

	private void DragDecorationsStart()
	{
		if (lockPathEditing)
		{
			return;
		}
		using (new SaveStateScope(this))
		{
			decorationPositionsAtDragStart.Clear();
			foreach (LevelEvent selectedDecoration in selectedDecorations)
			{
				scrDecoration decoration = scrDecorationManager.GetDecoration(selectedDecoration);
				Vector2 output;
				Vector2 vector = (selectedDecoration.TryGet<Vector2>("parallaxOffset", out output) ? (output * ADOBase.controller.tileSize) : Vector2.zero);
				if (selectedDecoration.TryGet<Vector2>("parallax", out var output2) && output2 == Vector2.zero)
				{
					vector = Vector2.zero;
				}
				decorationPositionsAtDragStart[decoration] = decoration.transform.position.xy() - (ADOBase.controller.camy.transform.position.xy() - decoration.parallax.posCamAtStart.xy()) * decoration.parallax.multiplier - vector;
			}
		}
		pointerDownObjectType = PointerDownObjectType.Decoration;
	}

	public void DragDecorations(Vector3 translation, bool ignoreModifiers = false)
	{
		bool flag = Mathf.Abs(translation.x) + addXDragCache > Mathf.Abs(translation.y) + addYDragCache;
		addXDragCache = (flag ? 1f : 0f);
		addYDragCache = (flag ? 0f : 1f);
		foreach (LevelEvent selectedDecoration in selectedDecorations)
		{
			scrDecoration decoration = scrDecorationManager.GetDecoration(selectedDecoration);
			if (!(decoration == null) && !selectedDecoration.locked && !decoration.forceLock && decorationPositionsAtDragStart.ContainsKey(decoration))
			{
				Vector2 decorationDragDelta = GetDecorationDragDelta(translation.xy(), decoration);
				Vector2 vector = selectedDecoration.Get<Vector2>("parallax");
				if (vector.x == 100f)
				{
					decorationDragDelta.x = 0f;
				}
				if (vector.y == 100f)
				{
					decorationDragDelta.y = 0f;
				}
				Vector2 vector2 = decorationPositionsAtDragStart[decoration];
				Vector2 vector3 = vector2 + decorationDragDelta;
				float x = ((!holdingShift || ignoreModifiers) ? vector3.x : (flag ? vector3.x : vector2.x));
				float y = ((!holdingShift || ignoreModifiers) ? vector3.y : (flag ? vector2.y : vector3.y));
				Vector2 vector4 = new Vector2(x, y);
				Vector2 vector5 = vector4;
				if ((DecPlacementType)selectedDecoration["relativeTo"] == DecPlacementType.Tile)
				{
					int index = Mathf.Clamp(selectedDecoration.floor, 0, floors.Count - 1);
					vector5 -= scrLevelMaker.instance.listFloors[index].transform.position.xy();
				}
				vector5 /= ADOBase.controller.tileSize;
				selectedDecoration["position"] = vector5;
				decoration.SetPosition(vector4, decoration.pivotOffsetVec);
			}
		}
		if (SelectionDecorationIsSingle())
		{
			levelEventsPanel.UpdatePropertyText(selectedDecorations[0], "position");
		}
	}

	private void DragEventIndicatorStart(GameObject[] objects)
	{
		bool flag = false;
		if (objects != null)
		{
			for (int i = 0; i < objects.Length; i++)
			{
				EventIndicator component = objects[i].transform.parent.GetComponent<EventIndicator>();
				if (component != null)
				{
					draggedEvIndicator = component;
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			draggedEvIndicator = null;
		}
		if (!(draggedEvIndicator != null))
		{
			return;
		}
		if (holdingControl)
		{
			LevelEvent evnt = draggedEvIndicator.evnt;
			EnableEvent(evnt, !evnt.active);
			cancelDrag = true;
			return;
		}
		evIndPosAtDragStart = draggedEvIndicator.circle.transform.position;
		levelEventsPanel.ShowPanelOfEvent(draggedEvIndicator.evnt);
		if (draggedEvIndicator.editable)
		{
			draggedEvIndicator.circle.color = new UnityEngine.Color(0.9f, 0.9f, 0.9f);
		}
	}

	private void DragEventIndicator(Vector3 translation)
	{
		Vector3 to = evIndPosAtDragStart + translation - draggedEvIndicator.gameObject.transform.position;
		float num = Vector3.Angle(Vector3.up, to);
		if (to.x < 0f)
		{
			num = 360f - num;
		}
		num *= MathF.PI / 180f;
		scrFloor floor = draggedEvIndicator.floor;
		double num2 = scrMisc.GetAngleMoved((float)floor.entryangle, num, !floor.isCCW);
		double num3 = scrMisc.GetAngleMoved((float)floor.entryangle, (float)floor.exitangle, !floor.isCCW);
		if (Mathf.Abs((float)num3) <= Mathf.Pow(10f, -6f))
		{
			num3 = 6.2831854820251465;
			if (floor.midSpin)
			{
				num2 = 0.0;
			}
		}
		double num4 = num2 / num3;
		if (num4 > 1.0)
		{
			double num5 = (Math.PI * 2.0 - num3) / 2.0;
			double num6 = (num3 + num5 + Math.PI) % (Math.PI * 2.0);
			double num7 = num3 + Math.PI;
			double num8 = (num2 + Math.PI) % (Math.PI * 2.0);
			num4 = ((!(num8 > num6) || !(num8 < num7)) ? 1.0 : 0.0);
		}
		double entryangle = floor.entryangle;
		double num9 = floor.entryangle + num3 * (double)(floor.isCCW ? 1 : (-1));
		float num10 = Mathf.Lerp((float)entryangle, (float)num9, (float)num4);
		num10 = (num10 - 2f * (float)floor.entryangle) * 57.29578f;
		if (!holdingShift)
		{
			num10 = (float)Mathf.RoundToInt(num10 / 15f) * 15f;
		}
		draggedEvIndicator.gameObject.transform.rotation = Quaternion.Euler(0f, 0f, num10);
	}

	private void DragTransformHandlesStart(TransformGizmo handle)
	{
		handle.holder.DragStart(handle);
	}

	private void DragTransformHandles(Vector2 mouseTranslation, Vector2 mouseDelta)
	{
		draggingGizmo.holder.Drag(mouseTranslation, mouseDelta);
	}

	public Vector2 GetDecorationDragDelta(Vector2 translation, scrDecoration dec, bool parallax = true)
	{
		if (!parallax)
		{
			return translation;
		}
		Vector2 vector = Vector2.one - dec.parallax.multiplier;
		if (vector.x == 0f)
		{
			vector.x = 1f;
		}
		if (vector.y == 0f)
		{
			vector.y = 1f;
		}
		return translation / vector;
	}

	public void DrawX(Vector2 position, UnityEngine.Color color)
	{
		float num = 0.3f;
		Debug.DrawLine(position + new Vector2(0f - num, 0f - num), position + new Vector2(num, num), color);
		Debug.DrawLine(position + new Vector2(0f - num, num), position + new Vector2(num, 0f - num), color);
	}
}
