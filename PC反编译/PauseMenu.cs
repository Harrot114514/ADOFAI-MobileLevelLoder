using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ADOFAI;
using ADOFAI.SteamIntegration;
using DG.Tweening;
using GDMiniJSON;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityFileDialog;

public class PauseMenu : ADOBase
{
	public enum ButtonType
	{
		Continue = 0,
		LevelEditor = 1,
		SteamWorkshop = 2,
		Refresh = 3,
		Practice = 4,
		Settings = 5,
		Calibration = 6,
		Discord = 7,
		QQ = 8,
		GameCenter = 9,
		PlayGames = 10,
		Quit = 11,
		LoadLevel = 12,
		ChangePlayers = 13
	}

	public enum Submenu
	{
		Main = 0,
		Settings = 1,
		PlayerSelect = 2
	}

	private enum PauseButtonsLayout
	{
		ShowPreviousAndNextButton = 0,
		HidePreviousButton = 1,
		HideNextButton = 2,
		HidePreviousAndNextButton = 3
	}

	private const float pauseButtonOffset = 58f;

	public const float pauseButtonWidth = 52f;

	private const float blinkAnimDuration = 0.5f;

	private const float timelineFirstInputDelay = 0.4f;

	private const float timelineInputUpdateTime = 0.02f;

	[Header("General UI")]
	[SerializeField]
	private RawImage background;

	public GameObject mainMenuContainer;

	[SerializeField]
	private GameObject pauseButton;

	[SerializeField]
	private GameObject pausePlanetsPrefab;

	public SettingsMenu settingsMenu;

	[SerializeField]
	private PlayerSelect playerSelect;

	[SerializeField]
	private RectTransform canvasRT;

	public CanvasScaler canvasScaler;

	[SerializeField]
	private PracticeTimeline practiceTimeline;

	[SerializeField]
	private PauseMedals pauseMedals;

	public PauseMenuChain pauseMenuChain;

	[SerializeField]
	private PauseLevel pauseLevel;

	public RawImage pausePlanetsImage;

	[SerializeField]
	private RenderTexture pausePlanetsRenderTexture;

	[SerializeField]
	private scrVersionText versionText;

	[SerializeField]
	private TakeScreenshot takeScreenshot;

	[SerializeField]
	private CanvasGroup mainMenuCanvasGroup;

	[Header("Button groups")]
	private Dictionary<ButtonType, PauseButton> buttonDictionary = new Dictionary<ButtonType, PauseButton>();

	[SerializeField]
	private RectTransform buttonsContainer;

	[SerializeField]
	private List<GeneralPauseButton> currentButtons;

	[Header("Individual buttons")]
	private PauseButton openInEditorButton;

	private PauseButton practiceButton;

	private PauseButton settingsButton;

	private PauseButton quitButton;

	private PauseButton changePlayersButton;

	public Text subtitle;

	[Header("Social network buttons")]
	public SocialPauseButton sbgButton;

	public SocialPauseButton blueskyButton;

	public SocialPauseButton twitterButton;

	public SocialPauseButton youtubeButton;

	[Header("Button Indicators")]
	public Button backButton;

	[Header("Variables")]
	public float animationDistance;

	public float animationTime = 2f;

	public Ease animationEase = Ease.InOutQuad;

	[Header("Colors")]
	public Color selectedIconColor;

	public Color unselectedIconColor;

	public Color selectedLabelColor;

	public Color unselectedLabelColor;

	public Color selectedFillColor;

	public Color unselectedFillColor;

	public Color unselectedBorderColor;

	[Header("CLS")]
	public Sprite clsSelectedFillSprite;

	public Sprite clsUnselectedFillSprite;

	[Header("Other")]
	public int lastInputSelected;

	public bool requireRestart;

	public Material vignetteMaterial;

	private Submenu submenu;

	private int lastFrameVolume;

	private int selectedIndex;

	private int selectedVerticalIndex;

	private int selectedSocialIndex;

	private GeneralPauseButton[] pauseButtons;

	private string currentPauseLevel;

	private int currentCustomPauseLevel;

	private string[] customLevelNames;

	private bool anyButtonPressed;

	private float timelineInputTimer;

	private float lastCanvasWidth;

	private bool showSocialMediaButtons;

	[NonSerialized]
	public bool transitioning;

	[NonSerialized]
	public PausePlanets pausePlanets;

	[NonSerialized]
	public GeneralPauseButton currentPauseButton;

	private List<PauseButton> allPauseButtons = new List<PauseButton>();

	private List<PauseButton> menuPauseButtons = new List<PauseButton>();

	private List<PauseButton> clsPauseButtons = new List<PauseButton>();

	private List<PauseButton> gamePauseButtons = new List<PauseButton>();

	private List<SocialPauseButton> socialPauseButtons = new List<SocialPauseButton>();

	private static bool addedSceneChangeEvent;

	private const int areEqual = 0;

	private const int isHigher = 1;

	private const int isLower = -1;

	private Tween delayedAction;

	private Action delayedActionLevelSkip;

	private Sequence switchSequence;

	private bool onSettingsMenu => submenu == Submenu.Settings;

	private bool responsive
	{
		get
		{
			if (ADOBase.controller.paused)
			{
				return !transitioning;
			}
			return false;
		}
	}

	public bool shouldUseGamePauseButtons
	{
		get
		{
			if (!ADOBase.controller.gameworld || ADOBase.isLevelEditor)
			{
				if (!ADOBase.controller.gameworld)
				{
					if (!ADOBase.controller.currFloor || !ADOBase.controller.currFloor.freeroamGenerated)
					{
						return ADOBase.controller.isPuzzleRoom;
					}
					return true;
				}
				return false;
			}
			return true;
		}
	}

	private void Awake()
	{
		vignetteMaterial = new Material(vignetteMaterial);
		GenerateButtons();
		base.gameObject.SetActive(value: false);
		if (ADOBase.isDesktop)
		{
			backButton.gameObject.SetActive(value: false);
		}
		showSocialMediaButtons = !ADOBase.controller.gameworld && !ADOBase.controller.isPuzzleRoom && !RDC.isSteamDeckOnSteamOS && !ADOBase.isSwitch && !ADOBase.isExpo;
		if (showSocialMediaButtons)
		{
			bool isChinese = RDString.isChinese;
			bool flag = showSocialMediaButtons && !isChinese;
			sbgButton.gameObject.SetActive(showSocialMediaButtons);
			if (showSocialMediaButtons)
			{
				sbgButton.action = delegate
				{
					Open7BG();
				};
				sbgButton.button.onClick.AddListener(delegate
				{
					selectedSocialIndex = 0;
					SelectVerticalFixed(1, 0);
					sbgButton.Select();
				});
				socialPauseButtons.Add(sbgButton);
				if (isChinese)
				{
					sbgButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-17f, 2f);
				}
			}
			twitterButton.gameObject.SetActive(flag);
			youtubeButton.gameObject.SetActive(flag);
			blueskyButton.gameObject.SetActive(flag);
			if (flag)
			{
				youtubeButton.action = delegate
				{
					OpenYoutube();
				};
				blueskyButton.action = delegate
				{
					OpenBluesky();
				};
				twitterButton.action = delegate
				{
					OpenTwitter();
				};
				youtubeButton.button.onClick.AddListener(delegate
				{
					selectedSocialIndex = 1;
					SelectVerticalFixed(1, 0);
					youtubeButton.Select();
				});
				blueskyButton.button.onClick.AddListener(delegate
				{
					selectedSocialIndex = 2;
					SelectVerticalFixed(1, 0);
					blueskyButton.Select();
				});
				twitterButton.button.onClick.AddListener(delegate
				{
					selectedSocialIndex = 3;
					SelectVerticalFixed(1, 0);
					twitterButton.Select();
				});
				socialPauseButtons.Add(youtubeButton);
				socialPauseButtons.Add(blueskyButton);
				socialPauseButtons.Add(twitterButton);
			}
		}
		else
		{
			sbgButton.gameObject.SetActive(value: false);
			youtubeButton.gameObject.SetActive(value: false);
			blueskyButton.gameObject.SetActive(value: false);
			twitterButton.gameObject.SetActive(value: false);
		}
		if (!addedSceneChangeEvent)
		{
			SceneManager.activeSceneChanged += OnSceneChanged;
			addedSceneChangeEvent = true;
		}
	}

	private void GenerateButtons()
	{
		buttonDictionary.Clear();
		allPauseButtons.Clear();
		menuPauseButtons.Clear();
		clsPauseButtons.Clear();
		gamePauseButtons.Clear();
		Dictionary<string, object> obj = Json.Deserialize(Resources.Load<TextAsset>("PauseMenuButtons").text) as Dictionary<string, object>;
		Dictionary<string, object> dictionary = obj["Buttons"] as Dictionary<string, object>;
		List<object> list = obj["CLSPause"] as List<object>;
		if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
		{
			customLevelNames = new string[GCS.customLevelPaths.Length];
			for (int i = 0; i < customLevelNames.Length; i++)
			{
				string path = GCS.customLevelPaths[i];
				customLevelNames[i] = LevelData.GetCustomLevelName(path);
			}
		}
		pausePlanets = UnityEngine.Object.Instantiate(pausePlanetsPrefab, Vector3.zero, Quaternion.identity, mainMenuContainer.transform).GetComponent<PausePlanets>();
		int num = 0;
		foreach (KeyValuePair<string, object> item in dictionary)
		{
			ButtonType buttonType = item.Key.ToEnum(ButtonType.Continue);
			Dictionary<string, object> dictionary2 = item.Value as Dictionary<string, object>;
			string rdString = dictionary2["rdString"] as string;
			string key = ((ADOBase.isSwitch && dictionary2.ContainsKey("iconSwitch")) ? "iconSwitch" : "icon");
			Sprite sprite = Resources.Load<Sprite>("new/" + dictionary2[key]);
			bool num2 = dictionary2.ContainsKey("forChina");
			bool flag = num2 && (bool)dictionary2["forChina"];
			bool flag2 = !num2 || flag == RDString.isChinese;
			bool flag3 = dictionary2.ContainsKey("forExpo");
			bool flag4 = flag3 && (bool)dictionary2["forExpo"];
			if (flag2)
			{
				flag2 = !flag3 || flag4 == ADOBase.isExpo;
			}
			if (!flag2)
			{
				continue;
			}
			GameObject obj2 = UnityEngine.Object.Instantiate(pauseButton, Vector3.zero, Quaternion.identity);
			PauseButton component = obj2.GetComponent<PauseButton>();
			obj2.transform.SetParent(buttonsContainer);
			component.icon.sprite = sprite;
			component.buttonType = buttonType;
			component.rdString = rdString;
			obj2.name = item.Key;
			component.transform.ScaleXY(1f, 1f);
			obj2.GetComponent<RectTransform>().anchoredPosition = new Vector2(58f * (float)num, 0f);
			buttonDictionary.Add(buttonType, component);
			allPauseButtons.Add(component);
			switch (buttonType)
			{
			case ButtonType.LevelEditor:
				openInEditorButton = component;
				break;
			case ButtonType.Practice:
				practiceButton = component;
				break;
			case ButtonType.Settings:
				settingsButton = component;
				break;
			case ButtonType.Quit:
				quitButton = component;
				break;
			case ButtonType.ChangePlayers:
				changePlayersButton = component;
				break;
			case ButtonType.LoadLevel:
				if (ADOBase.isUnityEditor || GCS.isDev)
				{
					menuPauseButtons.Add(component);
				}
				break;
			}
			bool flag5 = false;
			if (dictionary2.TryGetValue("platforms", out var value) && buttonType != ButtonType.LevelEditor)
			{
				foreach (string item2 in (List<object>)value)
				{
					if (item2.ToEnum(Platform.None) == ADOBase.platform)
					{
						if (buttonType == ButtonType.ChangePlayers)
						{
							flag5 = true;
						}
						menuPauseButtons.Add(component);
					}
				}
			}
			if (dictionary2.TryGetValue("features", out var _) && (buttonType != ButtonType.ChangePlayers || flag5))
			{
				AddButtonToFeatureList(gamePauseButtons, component);
			}
			num++;
		}
		foreach (object item3 in list)
		{
			ButtonType buttonType2 = (item3 as string).ToEnum(ButtonType.Continue);
			bool flag6 = buttonType2 == ButtonType.SteamWorkshop;
			bool flag7 = buttonType2 == ButtonType.Refresh;
			if ((!flag6 || (flag6 && ADOBase.isSteamworks && !ADOBase.isSwitch && !ADOBase.isExpo)) && (!flag7 || (flag7 && !ADOBase.isSwitch)))
			{
				clsPauseButtons.Add(buttonDictionary[buttonType2]);
			}
		}
	}

	private void AddButtonToFeatureList(List<PauseButton> featureButtons, PauseButton button)
	{
		if (button.buttonType != ButtonType.Practice || base.practiceAvailable)
		{
			featureButtons.Add(button);
		}
	}

	private static void OnSceneChanged(Scene _, Scene __)
	{
		UpdateCursorVisibility();
	}

	private static void UpdateCursorVisibility()
	{
		if (Persistence.GetHideCursorWhilePlaying() && !(scrController.instance == null))
		{
			Cursor.visible = !ADOBase.controller.gameworld || ADOBase.isEditingLevel || (bool)scnCLS.instance || GCS.lastVisitedScene == "scnEditor";
		}
	}

	private int CompareSpeedRunValues(float a, float b)
	{
		if (Mathf.Abs(a - b) < 0.01f)
		{
			return 0;
		}
		if (!(a > b))
		{
			return -1;
		}
		return 1;
	}

	private void RefreshLayout()
	{
		foreach (PauseButton allPauseButton in allPauseButtons)
		{
			allPauseButton.label.text = RDString.Get(allPauseButton.rdString);
			allPauseButton.label.SetLocalizedFont();
		}
		if (GCS.practiceMode)
		{
			practiceButton.label.text = RDString.Get("pauseMenu.endPractice");
			practiceButton.label.SetLocalizedFont();
			practiceButton.SetIconColors(Color.white, Color.white);
		}
		else
		{
			practiceButton.DisableColorSet();
		}
		if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
		{
			currentCustomPauseLevel = GCS.customLevelIndex;
		}
		else
		{
			currentPauseLevel = ADOBase.currentLevel;
		}
		if (shouldUseGamePauseButtons)
		{
			GeneralPauseButton[] array = gamePauseButtons.ToArray();
			pauseButtons = array;
			List<GeneralPauseButton> list = pauseButtons.ToList();
			if ((ADOBase.sceneName.IsTaro() || ADOBase.controller.isPuzzleRoom || GCS.practiceMode) && list.Contains(changePlayersButton))
			{
				list.Remove(changePlayersButton);
			}
			if (!GCS.practiceMode && ADOBase.isScnGame && !ADOBase.isOfficialLevel && !ADOBase.isSwitch && !ADOBase.isMobile && !ADOBase.isExpo)
			{
				list.Insert(list.Count - 1, openInEditorButton);
			}
			pauseButtons = list.ToArray();
		}
		else
		{
			List<PauseButton> list2 = ((ADOBase.cls != null) ? clsPauseButtons : menuPauseButtons);
			if (ADOBase.sceneName.IsTaroScene() && list2.Contains(changePlayersButton))
			{
				list2.Remove(changePlayersButton);
			}
			GeneralPauseButton[] array = list2.ToArray();
			pauseButtons = array;
		}
		pauseMenuChain.UpdateHeight(buttonsContainer);
	}

	private PauseButtonsLayout GetPauseButtonsLayout()
	{
		if (currentPauseLevel == null && !ADOBase.isScnGame)
		{
			return PauseButtonsLayout.HidePreviousAndNextButton;
		}
		if (currentPauseLevel == "scnMinesweeper")
		{
			return PauseButtonsLayout.HidePreviousAndNextButton;
		}
		bool flag = true;
		bool flag2 = true;
		if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
		{
			if (GCS.speedTrialMode)
			{
				flag2 = GCS.nextSpeedRun <= 0.5f;
				flag = false;
			}
			else
			{
				flag2 = currentCustomPauseLevel <= 0;
				flag = currentCustomPauseLevel >= GCS.customLevelPaths.Length - 1;
			}
		}
		else if (GCS.speedTrialMode)
		{
			if (CompareSpeedRunValues(GCS.nextSpeedRun, 0.5f) != 0)
			{
				flag2 = false;
			}
			flag = false;
		}
		else
		{
			if (scrController.currentWorldString == null || !ADOBase.worldData.ContainsKey(scrController.currentWorldString))
			{
				scrController.currentWorldString = "Template";
			}
			bool flag3 = currentPauseLevel.Contains("-X");
			bool flag4 = currentPauseLevel.EndsWith("-1");
			if (ADOBase.sceneName.StartsWith("TP"))
			{
				scrController.currentWorldString = "TP";
				bool num = Persistence.taroStoryProgress > 4;
				flag = !num || flag3;
				flag2 = !num || flag4;
			}
			else
			{
				bool flag5 = ADOBase.worldData[scrController.currentWorldString].levelCount == 1;
				flag2 = flag4 || flag5;
				flag = flag3;
			}
		}
		if (flag && flag2)
		{
			return PauseButtonsLayout.HidePreviousAndNextButton;
		}
		if (flag)
		{
			return PauseButtonsLayout.HideNextButton;
		}
		if (flag2)
		{
			return PauseButtonsLayout.HidePreviousButton;
		}
		return PauseButtonsLayout.ShowPreviousAndNextButton;
	}

	public void ShowMainMenu(int selectedItem = 0)
	{
		RefreshLayout();
		if (!Cursor.visible)
		{
			Cursor.visible = true;
		}
		base.enabled = true;
		submenu = Submenu.Main;
		base.gameObject.SetActive(value: true);
		mainMenuContainer.SetActive(value: true);
		settingsMenu.gameObject.SetActive(value: false);
		playerSelect.gameObject.SetActive(value: false);
		background.gameObject.SetActive(value: true);
		mainMenuCanvasGroup.DOFade(1f, 1f).SetUpdate(isIndependentUpdate: true);
		GCS.nextSpeedRun = GCS.currentSpeedTrial;
		currentButtons = new List<GeneralPauseButton>(pauseButtons);
		float num = (float)(pauseButtons.Length - 1) * 58f + 52f;
		buttonsContainer.sizeDelta = buttonsContainer.sizeDelta.WithX(num);
		pauseMenuChain.UpdateLinks();
		foreach (PauseButton allPauseButton in allPauseButtons)
		{
			allPauseButton.gameObject.SetActive(value: false);
		}
		int num2 = 0;
		GeneralPauseButton[] array = pauseButtons;
		for (int i = 0; i < array.Length; i++)
		{
			PauseButton obj = (PauseButton)array[i];
			obj.gameObject.SetActive(value: true);
			RectTransform component = obj.GetComponent<RectTransform>();
			component.anchoredPosition = ExtensionMethods.WithX(x: (float)num2 * 58f - (num - 52f) / 2f, v: component.anchoredPosition);
			num2++;
		}
		num2 = 0;
		foreach (GeneralPauseButton currentButton in currentButtons)
		{
			currentButton.SetFocus(value: false);
			currentButton.index = num2;
			num2++;
		}
		selectedIndex = ((lastInputSelected != -1) ? lastInputSelected : selectedItem);
		selectedVerticalIndex = 0;
		lastInputSelected = -1;
		selectedIndex = selectedItem;
		currentButtons[selectedIndex].SetFocus(value: true);
		SelectPauseButton(currentButtons[selectedIndex], 1f, 4f, instant: true, null, playSound: false);
		UpdateLevelDescription();
		UpdateButtonsContainer();
		pausePlanetsImage.enabled = true;
		UpdatePausePlanetRender();
		pausePlanets.UpdatePlanets();
		versionText.Init();
		practiceTimeline.Init();
		pauseMedals.Init();
		pauseLevel.Init();
		subtitle.SetLocalizedFont();
		subtitle.gameObject.SetActive(pauseLevel.gameObject.activeSelf);
		if (pauseMedals.gameObject.activeSelf)
		{
			ShowMedals(instant: true, updateVerticalIndex: false, updateCallback: false);
		}
	}

	public void Hide()
	{
		delayedAction?.Kill();
		delayedActionLevelSkip = null;
		scrSfx.instance.StopMusicVolumePreview();
		base.gameObject.SetActive(value: false);
		if (settingsMenu.settingsThatRequireRestart > 0 && (bool)ADOBase.editor)
		{
			ADOBase.editor.CheckUnsavedChanges(delegate
			{
				ADOBase.RestartScene();
			});
		}
		UpdateCursorVisibility();
		Persistence.Save(instant: true);
	}

	private void UpdateButtonsContainer()
	{
		float num = buttonsContainer.sizeDelta.x + 20f;
		float width = mainMenuContainer.GetComponent<RectTransform>().rect.width;
		float xy = Mathf.Min(1f, width / num);
		buttonsContainer.ScaleXY(xy);
	}

	private void OnGUI()
	{
		if (RDC.debug)
		{
			string text = "";
			text = text + "currentResolution Width: " + Screen.width + " height: " + Screen.height + " @ " + Math.Round(Screen.currentResolution.refreshRateRatio.value) + "hz\n";
			text = text + "fullscreen: " + Screen.fullScreen + "\n";
			text = text + "fullscreenMode: " + Screen.fullScreenMode.ToString() + "\n";
			Resolution[] resolutions = Screen.resolutions;
			foreach (Resolution r in resolutions)
			{
				text = text + ResolutionToString(r) + "\n";
			}
			GUI.Label(new Rect(0f, 20f, 300f, 200f), text);
		}
	}

	private string ResolutionToString(Resolution r)
	{
		return $"{r.width} x {r.height} @ {Math.Round(r.refreshRateRatio.value)}hz";
	}

	private void Update()
	{
		if (!responsive)
		{
			return;
		}
		if (AsyncInputManager.isActive)
		{
			ADOBase.controller.UpdateInput();
		}
		if (!settingsMenu.editingKeys)
		{
			if (RDEditorUtils.CheckForKeyCombo(control: true, shift: true, KeyCode.L))
			{
				RDEditorUtils.RevealInExplorer(RDEditorUtils.LogPath());
			}
			if (Input.GetKeyDown(KeyCode.C) && (!GCS.d_judges || RDC.debug) && (!GCS.d_boothDisablePossibleMessUpButtons || RDEditorUtils.CheckForKeyCombo(control: false, shift: true, KeyCode.C)))
			{
				ADOBase.GoToCalibration();
			}
		}
		anyButtonPressed = false;
		if (onSettingsMenu)
		{
			if (RDInput.cancelPress)
			{
				BackButtonAction();
			}
		}
		else if (submenu == Submenu.PlayerSelect)
		{
			playerSelect.InputUpdate();
		}
		else if (RDInput.rightIsPressed)
		{
			SelectHorizontal(1, RDInput.rightPress);
		}
		else if (RDInput.leftIsPressed)
		{
			SelectHorizontal(-1, RDInput.leftPress);
		}
		else if (RDInput.upPress)
		{
			SelectVertical(-1);
		}
		else if (RDInput.downPress)
		{
			SelectVertical(1);
		}
		else if (RDInput.quitPress)
		{
			quitButton.Select();
			anyButtonPressed = true;
		}
		else if (RDInput.cancelPress)
		{
			BackButtonAction();
			anyButtonPressed = true;
		}
		GameObject currentSelectedGameObject = EventSystem.current.currentSelectedGameObject;
		TMP_InputField component;
		bool flag = currentSelectedGameObject != null && currentSelectedGameObject.TryGetComponent<TMP_InputField>(out component);
		if (RDInput.confirmPress && !anyButtonPressed && !flag)
		{
			if (submenu != Submenu.PlayerSelect)
			{
				Select(selectedIndex, -1, playSound: true);
				Choose();
			}
			else
			{
				GeneralPauseButton[] buttons = playerSelect.buttons;
				int index = Array.IndexOf(buttons, currentPauseButton);
				playerSelect.Choose(index);
			}
		}
	}

	private void LateUpdate()
	{
		GameObject obj = pauseMenuChain.gameObject;
		Submenu submenu = this.submenu;
		obj.SetActive(submenu == Submenu.Main || submenu == Submenu.PlayerSelect);
		int volume = scrController.volume;
		if (lastFrameVolume != volume)
		{
			Persistence.globalVolume = (lastFrameVolume = volume);
		}
		if (lastCanvasWidth != canvasRT.sizeDelta.x)
		{
			lastCanvasWidth = canvasRT.sizeDelta.x;
			UpdateButtonsContainer();
		}
		UpdatePausePlanetRender();
		canvasScaler.referenceResolution = new Vector2(onSettingsMenu ? 425f : 352f, 198f);
		canvasScaler.screenMatchMode = (onSettingsMenu ? CanvasScaler.ScreenMatchMode.Expand : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight);
	}

	private void UpdatePausePlanetRender()
	{
		bool flag = pausePlanetsRenderTexture == null;
		Camera pausePlanetsCam = scrCamera.instance.PausePlanetsCam;
		Vector3 vector = new Vector3(Screen.width, Screen.height);
		if (pausePlanetsRenderTexture != null && (vector.x != (float)pausePlanetsRenderTexture.width || vector.y != (float)pausePlanetsRenderTexture.height))
		{
			pausePlanetsCam.targetTexture = null;
			pausePlanetsRenderTexture.Release();
			UnityEngine.Object.DestroyImmediate(pausePlanetsRenderTexture);
			pausePlanetsRenderTexture = null;
			flag = true;
		}
		if (flag)
		{
			if (pausePlanetsRenderTexture == null)
			{
				pausePlanetsRenderTexture = new RenderTexture((int)vector.x, (int)vector.y, 24, RenderTextureFormat.ARGB32);
			}
			pausePlanetsImage.texture = pausePlanetsRenderTexture;
			pausePlanetsCam.gameObject.SetActive(value: true);
			pausePlanetsCam.targetTexture = pausePlanetsRenderTexture;
			pausePlanetsCam.transform.SetParent(null);
			pausePlanetsCam.transform.localPosition = new Vector3(vector.x / 2f, vector.y / 2f, -20f);
			pausePlanetsCam.transform.localEulerAngles = Vector3.zero;
			pausePlanetsCam.orthographicSize = vector.y / 2f;
		}
	}

	public void SelectHorizontal(int direction, bool firstPress)
	{
		anyButtonPressed = true;
		bool activeSelf = pauseMedals.gameObject.activeSelf;
		bool selectingLeftBar;
		bool selectingRightBar;
		if (selectedVerticalIndex == 0)
		{
			if (firstPress)
			{
				int index = ((direction == 1) ? ((selectedIndex + 1) % currentButtons.Count) : ((selectedIndex + currentButtons.Count - 1) % currentButtons.Count));
				Select(index);
				if (!onSettingsMenu)
				{
					PlayMenuSfx(SfxSound.MenuNavigate);
				}
			}
		}
		else if (GCS.practiceMode)
		{
			selectingLeftBar = selectedVerticalIndex == 1;
			selectingRightBar = selectedVerticalIndex == 2;
			if (selectingLeftBar || selectingRightBar)
			{
				if (firstPress)
				{
					UpdateTimelineBar();
					timelineInputTimer = Time.unscaledTime + 0.4f;
				}
				else if (Time.unscaledTime > timelineInputTimer)
				{
					UpdateTimelineBar();
					timelineInputTimer = Time.unscaledTime + 0.02f;
				}
			}
			else if (firstPress)
			{
				practiceTimeline.ChangeSpeed(direction == 1);
			}
		}
		else if (scrController.instance.gameworld || ADOBase.isScnGame)
		{
			if (firstPress)
			{
				if (selectedVerticalIndex == 1 && activeSelf)
				{
					ChangeMedal(direction);
				}
				else if ((selectedVerticalIndex == 2 && activeSelf) || (selectedVerticalIndex == 1 && !activeSelf))
				{
					ChangeLevel(direction == 1);
				}
			}
		}
		else if (socialPauseButtons.Count != 0 && firstPress)
		{
			selectedSocialIndex += direction;
			selectedSocialIndex = Math.Clamp(selectedSocialIndex, 0, socialPauseButtons.Count - 1);
			SelectPauseButton(socialPauseButtons[selectedSocialIndex], 0.5f);
		}
		void UpdateTimelineBar()
		{
			if (selectingLeftBar)
			{
				practiceTimeline.practiceStart += direction;
			}
			else
			{
				practiceTimeline.practiceEnd += direction;
			}
			practiceTimeline.UpdatePositions(selectingRightBar, firstPress);
			if (direction == 1)
			{
				PlayMenuSfx(SfxSound.MenuIncrement);
			}
			else if (direction == -1)
			{
				PlayMenuSfx(SfxSound.MenuDecrement);
			}
		}
	}

	public void SelectVertical(int direction)
	{
		anyButtonPressed = true;
		delayedActionLevelSkip = null;
		bool activeSelf = pauseMedals.gameObject.activeSelf;
		int num = selectedVerticalIndex;
		selectedVerticalIndex += direction;
		int num2 = (GCS.practiceMode ? 3 : (activeSelf ? 2 : ((ADOBase.controller.gameworld || ADOBase.controller.isPuzzleRoom || showSocialMediaButtons) ? 1 : 0)));
		if (selectedVerticalIndex > num2)
		{
			selectedVerticalIndex = 0;
		}
		else if (selectedVerticalIndex < 0)
		{
			selectedVerticalIndex = num2;
		}
		if (num == selectedVerticalIndex)
		{
			return;
		}
		currentButtons[selectedIndex].SetFocus(value: false);
		GCS.nextSpeedRun = GCS.currentSpeedTrial;
		if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
		{
			currentCustomPauseLevel = GCS.customLevelIndex;
		}
		else
		{
			currentPauseLevel = ADOBase.currentLevel;
		}
		pauseLevel.MoveToCurrentIndex();
		if (selectedVerticalIndex == 0)
		{
			UpdateLevelDescription();
			currentButtons[selectedIndex].SetFocus(value: true);
			SelectPauseButton(currentButtons[selectedIndex]);
			practiceTimeline.PlaySong(play: false);
		}
		else if (selectedVerticalIndex == 1)
		{
			float newPConstant = (pauseLevel.levelIsTech ? 2f : (pauseLevel.levelIsTaro ? 1f : 4f));
			if (GCS.speedTrialMode)
			{
				SelectPauseButton(pauseLevel.speedTrialButton, 0.5f, newPConstant);
				UpdateLevelDescription();
			}
			else if (activeSelf)
			{
				UpdateLevelDescription();
				SelectPauseButton(pauseMedals.currentMedalButton, 0f);
				ShowMedals(direction == 1 && pauseMedals.isExpanded);
			}
			else if (GCS.practiceMode)
			{
				SelectPauseButton(practiceTimeline.startButton, 0.5f);
				practiceTimeline.PlayIfNotPlaying();
			}
			else if (scrController.instance.gameworld || ADOBase.isScnGame || ADOBase.controller.isPuzzleRoom)
			{
				SelectPauseButton(pauseLevel.currentLevelSelect, 0.5f, newPConstant, instant: false, pauseLevel.levelPosition);
				UpdateLevelDescription();
			}
			else if (socialPauseButtons.Count != 0)
			{
				SelectPauseButton(socialPauseButtons[0], 0.5f);
			}
		}
		else if (selectedVerticalIndex == 2)
		{
			if (activeSelf)
			{
				SelectPauseButton(pauseMedals.currentMedalButton, 0f);
				ShowLevels(direction == -1 && !pauseMedals.isExpanded);
			}
			else if (GCS.practiceMode)
			{
				SelectPauseButton(practiceTimeline.endButton, 0.5f);
				practiceTimeline.PlayIfNotPlaying();
			}
		}
		else if (selectedVerticalIndex == 3 && GCS.practiceMode)
		{
			SelectPauseButton(practiceTimeline.speedButton, 0.5f);
			UpdateLevelDescription(delegate
			{
				requireRestart = true;
				Unpause();
			});
		}
	}

	public void SelectVerticalFixed(int index, int direction)
	{
		anyButtonPressed = true;
		delayedActionLevelSkip = null;
		bool flag = selectedVerticalIndex == index;
		_ = pauseMedals.gameObject.activeSelf;
		selectedVerticalIndex = index;
		currentButtons[selectedIndex].SetFocus(value: false);
		if (selectedVerticalIndex == 0)
		{
			currentButtons[selectedIndex].SetFocus(value: true);
			SelectPauseButton(currentButtons[selectedIndex]);
		}
		else if (selectedVerticalIndex == 1)
		{
			float newPConstant = (pauseLevel.levelIsTech ? 2f : (pauseLevel.levelIsTaro ? 1f : 4f));
			if (GCS.speedTrialMode)
			{
				SelectPauseButton(pauseLevel.speedTrialButton, 0.5f, newPConstant);
				UpdateLevelDescription();
				if (flag && direction == 0)
				{
					delayedActionLevelSkip?.Invoke();
				}
			}
			else if (GCS.practiceMode)
			{
				SelectPauseButton(practiceTimeline.startButton, 0.5f, 4f, instant: false, null, !flag);
				practiceTimeline.PlayIfNotPlaying();
			}
			else if (scrController.instance.gameworld || ADOBase.isScnGame || ADOBase.controller.isPuzzleRoom)
			{
				SelectPauseButton(pauseLevel.currentLevelSelect, 0.5f, newPConstant, instant: false, pauseLevel.levelPosition);
				UpdateLevelDescription();
			}
			else if (socialPauseButtons.Count != 0)
			{
				SelectPauseButton(socialPauseButtons[selectedSocialIndex], 0.5f);
			}
		}
		else if (selectedVerticalIndex == 2)
		{
			if (GCS.practiceMode)
			{
				SelectPauseButton(practiceTimeline.endButton, 0.5f, 4f, instant: false, null, !flag);
				practiceTimeline.PlayIfNotPlaying();
			}
		}
		else if (selectedVerticalIndex == 3 && GCS.practiceMode)
		{
			SelectPauseButton(practiceTimeline.speedButton, 0.5f);
			UpdateLevelDescription(delegate
			{
				requireRestart = true;
				Unpause();
			});
			if (flag && direction == 0)
			{
				delayedActionLevelSkip?.Invoke();
			}
		}
	}

	public void Select(int index, int verticalIndex = -1, bool playSound = false)
	{
		if (verticalIndex != -1)
		{
			selectedVerticalIndex = verticalIndex;
		}
		if (selectedIndex != index)
		{
			currentButtons[selectedIndex].SetFocus(value: false);
			selectedIndex = index;
			currentButtons[index].SetFocus(value: true);
			SelectPauseButton(currentButtons[selectedIndex], 1f, 4f, instant: false, null, playSound);
		}
	}

	public void ChangeLevel(bool next)
	{
		PauseButtonsLayout pauseButtonsLayout = GetPauseButtonsLayout();
		PauseButtonsLayout pauseButtonsLayout2 = ((!next) ? PauseButtonsLayout.HidePreviousButton : PauseButtonsLayout.HideNextButton);
		if (pauseButtonsLayout != PauseButtonsLayout.HidePreviousAndNextButton && pauseButtonsLayout != pauseButtonsLayout2)
		{
			if (GCS.speedTrialMode)
			{
				RectTransform target = (next ? pauseLevel.rightArrowTransform : pauseLevel.leftArrowTransform);
				target.DOComplete(withCallbacks: true);
				target.DOPunchAnchorPos(Vector2.zero.WithX(1f * (float)(next ? 1 : (-1))), 0.2f, 0).SetUpdate(isIndependentUpdate: true);
				scrSfx.instance.PlaySfx(SfxSound.MenuNavigate, MixerGroup.InterfaceParent);
				GCS.nextSpeedRun += (next ? 0.1f : (-0.1f));
			}
			else if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
			{
				currentCustomPauseLevel += (next ? 1 : (-1));
				currentCustomPauseLevel = Mathf.Clamp(currentCustomPauseLevel, 0, pauseLevel.levelsNumber - 1);
				pauseLevel.MoveToSpecificIndex(currentCustomPauseLevel);
				SelectPauseButton(pauseLevel.currentLevelSelect, 0.5f, 4f, instant: true, pauseLevel.levelPosition);
			}
			else
			{
				float newPConstant = (pauseLevel.levelIsTech ? 2f : (pauseLevel.levelIsTaro ? 1f : 4f));
				currentPauseLevel = (next ? ADOBase.GetNextLevelName(currentPauseLevel) : ADOBase.GetPreviousLevelName(currentPauseLevel));
				pauseLevel.MoveToSpecificLevel(currentPauseLevel);
				SelectPauseButton(pauseLevel.currentLevelSelect, 0.5f, newPConstant, instant: true, pauseLevel.levelPosition);
			}
			UpdateLevelDescription();
		}
	}

	public void ChangeMedal(int direction)
	{
		bool playSound = true;
		pauseMedals.medalIndex += direction;
		if (pauseMedals.medalIndex < 0)
		{
			pauseMedals.medalIndex = 0;
			playSound = false;
		}
		else if (pauseMedals.medalIndex >= pauseMedals.medalsLength)
		{
			pauseMedals.medalIndex = pauseMedals.medalsLength - 1;
			playSound = false;
		}
		SelectPauseButton(pauseMedals.currentMedalButton, 2f, 1f, instant: false, null, playSound);
		delayedActionLevelSkip = null;
		if (pauseMedals.SectionIsUnlocked(pauseMedals.medalIndex))
		{
			delayedActionLevelSkip = delegate
			{
				base.enabled = false;
				pauseMedals.OnClick(pauseMedals.medalIndex);
			};
		}
	}

	public void ChangeMedalFixed(int index)
	{
		if (pauseMedals.medalIndex == index && selectedVerticalIndex == 1)
		{
			delayedActionLevelSkip?.Invoke();
		}
		selectedVerticalIndex = 1;
		pauseMedals.medalIndex = index;
		ChangeMedal(0);
	}

	public void Choose()
	{
		if (onSettingsMenu)
		{
			return;
		}
		if (selectedVerticalIndex == 0)
		{
			PauseButton pauseButton = currentButtons[selectedIndex] as PauseButton;
			ButtonType buttonType = pauseButton.buttonType;
			pauseButton.ShowAsSelected();
			if (delayedAction != null)
			{
				return;
			}
			switch (buttonType)
			{
			case ButtonType.GameCenter:
			case ButtonType.PlayGames:
				selectedVerticalIndex = 0;
				GameServices.Instance.ShowAchievements();
				break;
			case ButtonType.Discord:
				selectedVerticalIndex = 0;
				OpenDiscord();
				break;
			case ButtonType.QQ:
				selectedVerticalIndex = 0;
				OpenQQ();
				break;
			default:
			{
				base.enabled = false;
				selectedVerticalIndex = 0;
				scrController controller = scrController.instance;
				delayedAction = DOVirtual.DelayedCall(0.15f, delegate
				{
					switch (buttonType)
					{
					case ButtonType.Continue:
						Unpause();
						break;
					case ButtonType.Settings:
						base.enabled = true;
						ShowSettingsMenu();
						break;
					case ButtonType.ChangePlayers:
						base.enabled = true;
						ShowPlayerSelect();
						break;
					case ButtonType.Calibration:
						ADOBase.GoToCalibration();
						break;
					case ButtonType.LoadLevel:
						StartCoroutine(OpenLevelCo());
						base.enabled = true;
						break;
					case ButtonType.LevelEditor:
						SceneManager.LoadScene("scnEditor", LoadSceneMode.Additive);
						Hide();
						break;
					case ButtonType.Quit:
						if (ADOBase.cls != null)
						{
							ADOBase.cls.QuitPortal();
						}
						else if (controller.expoTutorial || controller.gameworld || (!controller.gameworld && (((bool)controller.currFloor && controller.currFloor.freeroamGenerated) || controller.isPuzzleRoom)))
						{
							controller.SaveProgress(save: true);
							controller.QuitToMainMenu();
						}
						else if (ADOBase.sceneName.StartsWith("scnTaro"))
						{
							controller.QuitToMainMenu();
						}
						else
						{
							Application.Quit();
						}
						break;
					case ButtonType.SteamWorkshop:
						SteamWorkshop.OpenWorkshopOverlay();
						break;
					case ButtonType.Refresh:
						Unpause();
						scnCLS.instance.Refresh();
						break;
					case ButtonType.Practice:
						if (ADOBase.isScnGame)
						{
							Unpause();
						}
						controller.SetPracticeMode(!GCS.practiceMode);
						break;
					default:
						base.enabled = true;
						break;
					}
					delayedAction = null;
				});
				break;
			}
			}
			if (buttonType == ButtonType.Practice)
			{
				ADOBase.conductor.song?.Stop();
				ADOBase.conductor.song2?.Stop();
				ADOBase.audioManager.StopAllSounds();
			}
			PlayMenuSfx(SfxSound.MobileButtonEnter);
		}
		else if (selectedVerticalIndex == 1 && socialPauseButtons.Count != 0)
		{
			socialPauseButtons[selectedSocialIndex].Select();
		}
		else
		{
			delayedActionLevelSkip?.Invoke();
		}
	}

	private IEnumerator OpenLevelCo()
	{
		string value = FileBrowser.PickFile(Persistence.GetLastUsedFolder(), "", new string[1] { "adofai" }, RDString.Get("editor.dialog.openFile"));
		if (string.IsNullOrEmpty(value))
		{
			Debug.Log("Level was not selected");
		}
		else
		{
			ADOBase.controller.LoadCustomLevel(value);
		}
		yield break;
	}

	private void ShowLevels(bool instant)
	{
		if (switchSequence != null && switchSequence.active)
		{
			switchSequence.Complete();
		}
		switchSequence = DOTween.Sequence();
		switchSequence.SetUpdate(isIndependentUpdate: true);
		pausePlanets.UpdateAnimation(pauseMedals.transform, 0f, 1f, instant: true, updatePosition: false);
		pauseMedals.Hide(switchSequence, instant);
		pauseLevel.Show(switchSequence, instant);
		switchSequence.InsertCallback(instant ? 0f : 0.5f, delegate
		{
			SelectPauseButton(pauseLevel.currentLevelSelect, 0.5f, 1f, instant: false, pauseLevel.levelPosition, playSound: false);
			UpdateLevelDescription();
		});
	}

	public void ShowMedals(bool instant, bool updateVerticalIndex = true, bool updateCallback = true)
	{
		if (updateVerticalIndex)
		{
			selectedVerticalIndex = 1;
		}
		if (switchSequence != null && switchSequence.active)
		{
			switchSequence.Complete();
		}
		switchSequence = DOTween.Sequence();
		switchSequence.SetUpdate(isIndependentUpdate: true);
		pauseMedals.Show(switchSequence, instant);
		pauseLevel.Hide(switchSequence, instant);
		if (!updateCallback)
		{
			return;
		}
		switchSequence.InsertCallback(instant ? 0f : 0.5f, delegate
		{
			SelectPauseButton(pauseMedals.currentMedalButton, 2f, 1f, instant: false, null, playSound: false);
			delayedActionLevelSkip = null;
			if (pauseMedals.SectionIsUnlocked(pauseMedals.medalIndex))
			{
				delayedActionLevelSkip = delegate
				{
					base.enabled = false;
					pauseMedals.OnClick(pauseMedals.medalIndex);
				};
			}
		});
	}

	public void UpdateLevelDescription(Action overrideAction = null)
	{
		string sceneName = "";
		string text = "";
		bool exists = true;
		if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
		{
			text = customLevelNames[currentCustomPauseLevel];
		}
		else
		{
			sceneName = currentPauseLevel;
			text = ((GCS.speedTrialMode && sceneName.Contains("EX") && sceneName.StartsWith("T")) ? ADOBase.GetLocalizedLevelNameWithCheck(sceneName.Replace("EX", ""), out exists).Replace("-X", "-EX") : ((!sceneName.StartsWith("TP")) ? ADOBase.GetLocalizedLevelNameWithCheck(sceneName, out exists) : sceneName));
		}
		if (GCS.speedTrialMode)
		{
			string text2 = RDString.Get("levelSelect.multiplier", new Dictionary<string, object> { 
			{
				"multiplier",
				GCS.nextSpeedRun.ToString("0.0")
			} });
			text = text + " (" + text2 + ")";
		}
		if (exists && ADOBase.levelSelect == null && !GCS.practiceMode)
		{
			subtitle.text = text;
			subtitle.enabled = true;
		}
		else
		{
			subtitle.enabled = false;
		}
		if (overrideAction != null)
		{
			delayedActionLevelSkip = overrideAction;
			return;
		}
		delayedActionLevelSkip = delegate
		{
			PlayMenuSfx(SfxSound.MobileButtonEnter);
			base.enabled = false;
			if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
			{
				GCS.customLevelIndex = currentCustomPauseLevel;
			}
			else if (sceneName.Contains("scnMinesweeper"))
			{
				GCS.sceneToLoad = "scnMinesweeper";
				GCS.internalLevelName = null;
			}
			else
			{
				string[] array = sceneName.Split('-');
				string key = array[0];
				WorldData obj = WorldData.dict[key];
				bool flag = array[1] == "X";
				LevelSource levelSource = obj.levelSource;
				if (levelSource == LevelSource.Scenes || (levelSource == LevelSource.Mixed && flag))
				{
					GCS.sceneToLoad = sceneName;
					GCS.internalLevelName = null;
				}
				else
				{
					GCS.sceneToLoad = "scnGame";
					GCS.internalLevelName = sceneName;
				}
			}
			ADOBase.controller.RestartProgress();
			ADOBase.controller.StartLoadingScene();
		};
	}

	private void LoadLevel(string sceneName)
	{
		base.enabled = false;
		if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
		{
			GCS.customLevelIndex = currentCustomPauseLevel;
		}
		else
		{
			string text = sceneName.Split('-')[0];
			WorldData obj = WorldData.dict[text];
			bool flag = text[1] == 'X';
			LevelSource levelSource = obj.levelSource;
			if (levelSource == LevelSource.Scenes || (levelSource == LevelSource.Mixed && flag))
			{
				GCS.sceneToLoad = sceneName;
				GCS.internalLevelName = null;
			}
			else
			{
				GCS.sceneToLoad = "scnGame";
				GCS.internalLevelName = sceneName;
			}
		}
		ADOBase.controller.RestartProgress();
		ADOBase.controller.StartLoadingScene();
	}

	public void UpdateLevelDescriptionAndReload(string newLevel, bool ignoreRestart = false, bool ignoreSelect = false)
	{
		bool activeSelf = pauseMedals.gameObject.activeSelf;
		bool flag = ADOBase.isScnGame && !ADOBase.isOfficialLevel;
		int num = -1;
		bool flag2;
		if (flag)
		{
			num = int.Parse(newLevel);
			flag2 = currentCustomPauseLevel == num;
			currentCustomPauseLevel = num;
		}
		else
		{
			flag2 = currentPauseLevel == newLevel;
			currentPauseLevel = newLevel;
		}
		if (!ignoreRestart && flag2 && selectedVerticalIndex == ((!activeSelf || !pauseLevel.levelIsTaro) ? 1 : 2))
		{
			delayedActionLevelSkip?.Invoke();
			return;
		}
		if (pauseLevel.levelIsTaro && !pauseLevel.isExpanded)
		{
			ShowLevels(instant: false);
		}
		selectedVerticalIndex = ((!activeSelf || !pauseLevel.levelIsTaro) ? 1 : 2);
		if (flag)
		{
			pauseLevel.MoveToSpecificIndex(num);
		}
		else
		{
			pauseLevel.MoveToSpecificLevel(currentPauseLevel);
		}
		if (!ignoreSelect)
		{
			float newPConstant = (pauseLevel.levelIsTech ? 2f : (pauseLevel.levelIsTaro ? 1f : 4f));
			SelectPauseButton(pauseLevel.currentLevelSelect, 0.5f, newPConstant, instant: true, pauseLevel.levelPosition);
		}
		UpdateLevelDescription();
	}

	public void SelectPauseButton(GeneralPauseButton pauseButton, float newScale = 1f, float newPConstant = 4f, bool instant = false, Transform overrideTransform = null, bool playSound = true, SfxSound sfxSound = SfxSound.MenuNavigate)
	{
		if (currentPauseButton != null)
		{
			currentPauseButton.SetFocus(value: false);
		}
		if (playSound)
		{
			PlayMenuSfx(sfxSound);
		}
		currentPauseButton = pauseButton;
		currentPauseButton.SetFocus(value: true);
		pausePlanets.UpdateAnimation((overrideTransform != null) ? overrideTransform : pauseButton.transform, newScale, newPConstant, instant);
	}

	public void SelectPauseLevelButton(bool instant = false)
	{
		SelectPauseButton(pauseLevel.currentLevelSelect, 0.5f, pauseLevel.levelIsTech ? 2f : (pauseLevel.levelIsTaro ? 1f : 4f), instant, pauseLevel.levelPosition);
	}

	private void Unpause()
	{
		practiceTimeline.SetPositions();
		if (requireRestart)
		{
			ADOBase.controller.Restart();
		}
		else
		{
			ADOBase.controller.TogglePauseGame();
		}
	}

	public void ShowSettingsMenu()
	{
		settingsMenu.Show();
		mainMenuContainer.SetActive(value: false);
		pausePlanetsImage.enabled = false;
		playerSelect.gameObject.SetActive(value: false);
		submenu = Submenu.Settings;
	}

	public void ShowPlayerSelect(bool instant = false)
	{
		settingsMenu.gameObject.SetActive(value: false);
		transitioning = true;
		if (!instant)
		{
			ShowFromPlayerSelect(show: false);
			pausePlanetsImage.enabled = false;
		}
		float duration = (instant ? 0f : 0.5f);
		DOVirtual.DelayedCall(duration, delegate
		{
			if (instant)
			{
				mainMenuContainer.SetActive(value: false);
			}
			else
			{
				mainMenuCanvasGroup.DOFade(0f, duration).SetUpdate(isIndependentUpdate: true).OnComplete(delegate
				{
					mainMenuContainer.SetActive(value: false);
				});
			}
			playerSelect.Show(instant);
		});
		submenu = Submenu.PlayerSelect;
		if (instant)
		{
			ShowBackButton(show: false, instant: true);
		}
	}

	public void ShowFromPlayerSelect(bool show, bool instant = false)
	{
		if (show)
		{
			mainMenuContainer.SetActive(value: true);
			mainMenuCanvasGroup.DOFade(1f, 0.5f).SetUpdate(isIndependentUpdate: true);
		}
		float num = (instant ? 0f : 1f);
		float num2 = (float)Screen.height / canvasScaler.referenceResolution.y * canvasScaler.referenceResolution.x;
		float num3 = (show ? num2 : (0f - num2));
		Ease ease = (show ? Ease.OutQuad : Ease.InQuad);
		SfxSound sfxSound = (show ? SfxSound.MenuChainSlideIn : SfxSound.MenuChainSlideOut);
		if (!instant)
		{
			foreach (GeneralPauseButton currentButton in currentButtons)
			{
				currentButton.rectTransform.DOAnchorPosX(currentButton.rectTransform.anchoredPosition.x + num3, num).SetEase(ease).SetUpdate(isIndependentUpdate: true);
			}
			scrSfx.instance.PlaySfx(sfxSound, MixerGroup.InterfaceParent);
		}
		if (show)
		{
			DOVirtual.DelayedCall(num, delegate
			{
				pausePlanetsImage.enabled = true;
				SelectPauseButton(currentButtons[selectedIndex], 1f, 4f, instant: true, null, playSound: false);
				transitioning = false;
				submenu = Submenu.Main;
			});
		}
	}

	public void HideSettingsMenu()
	{
		if (settingsMenu.settingsThatRequireRestart > 0)
		{
			ADOBase.controller.Restart();
		}
		int num = Array.IndexOf(pauseButtons, settingsButton);
		ShowMainMenu((num != -1) ? num : 0);
		submenu = Submenu.Main;
		settingsMenu.gameObject.SetActive(value: false);
		mainMenuContainer.transform.localScale = Vector3.one;
		mainMenuContainer.SetActive(value: true);
		scrSfx.instance.StopMusicVolumePreview();
		Persistence.Save();
	}

	public void Show(Submenu submenu = Submenu.Main, bool playSound = false)
	{
		ADOBase.controller.StartCoroutine(ShowPauseMenuCoroutine(submenu, playSound));
	}

	private IEnumerator ShowPauseMenuCoroutine(Submenu submenu, bool playSound = false)
	{
		if (takeScreenshot == null)
		{
			takeScreenshot = new TakeScreenshot();
		}
		yield return takeScreenshot.GetBlurredScreenshot(background);
		if (playSound)
		{
			PlayMenuSfx(SfxSound.MenuPause);
		}
		int selectedItem = ((lastInputSelected != -1) ? lastInputSelected : 0);
		ShowMainMenu(selectedItem);
		switch (submenu)
		{
		case Submenu.Settings:
			ShowSettingsMenu();
			break;
		case Submenu.PlayerSelect:
			ShowPlayerSelect(instant: true);
			break;
		}
	}

	public void ShowBackButton(bool show, bool instant = false)
	{
		float duration = (instant ? 0f : 0.25f);
		backButton.transform.parent.GetComponent<RectTransform>().DOPivotY(show ? 1 : 0, duration).SetEase(Ease.OutSine)
			.SetUpdate(isIndependentUpdate: true);
	}

	public void BackButtonAction(bool isUIButton = false)
	{
		if (!responsive)
		{
			return;
		}
		if (onSettingsMenu)
		{
			if (settingsMenu.editingKeys)
			{
				return;
			}
			if (!settingsMenu.isSelectingTab)
			{
				settingsMenu.StopBrowsingTab();
				if (!isUIButton)
				{
					return;
				}
			}
			if (ADOBase.isLevelEditor)
			{
				Hide();
				return;
			}
			HideSettingsMenu();
			PlayMenuSfx(SfxSound.MenuBack);
		}
		else if (submenu == Submenu.PlayerSelect)
		{
			playerSelect.BackButtonAction(isUIButton);
		}
		else
		{
			Unpause();
		}
	}

	public void GoToCalibrationScreen()
	{
		ADOBase.GoToCalibration();
	}

	public void OpenDiscord()
	{
		ADOBase.platformHelper.OpenURL("https://7thbe.at/discord");
	}

	public void OpenYoutube()
	{
		ADOBase.platformHelper.OpenURL("https://www.youtube.com/c/7thBeatGames?sub_confirmation=1");
	}

	public void OpenTwitter()
	{
		ADOBase.platformHelper.OpenURL("https://twitter.com/adofai");
	}

	public void Open7BG()
	{
		ADOBase.platformHelper.OpenURL("https://7thbe.at/");
	}

	public void OpenBluesky()
	{
		ADOBase.platformHelper.OpenURL("https://bsky.app/profile/did:plc:rkuyuj7djyvluh52trt4ndka");
	}

	public void OpenQQ()
	{
		ADOBase.platformHelper.OpenURL("https://jq.qq.com/?_wv=1027&k=5xO9aF4");
	}

	public void PlayMenuSfx(SfxSound sound, float volume = 1f)
	{
		scrSfx.instance.PlaySfx(sound, MixerGroup.InterfaceParent, volume);
	}
}
