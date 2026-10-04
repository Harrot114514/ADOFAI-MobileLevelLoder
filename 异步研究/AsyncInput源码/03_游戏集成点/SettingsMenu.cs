using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using GDMiniJSON;
using SA.GoogleDoc;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : ADOBase
{
	public enum Interaction
	{
		Refresh = 0,
		Activate = 1,
		ActivateInfo = 2,
		Increment = 3,
		Decrement = 4
	}

	[Header("Components")]
	public GameObject buttonPrefab;

	public GameObject tabButtonPrefab;

	public RectTransform settingsContainer;

	public RectTransform settingsScrollRectContent;

	public ScrollRect scrollRect;

	public Transform tabButtonsContainer;

	public CanvasScaler canvasScaler;

	public RectTransform canvasRectTransform;

	public SettingsGoat goat;

	private int confirmationProgress;

	private int selectedTab;

	private int selectedIndex;

	private List<List<PauseSettingButton>> settingsTabs;

	private List<SettingsTabButton> tabButtons;

	private PauseSettingButton offsetButton;

	[NonSerialized]
	public int settingsThatRequireRestart;

	[NonSerialized]
	public bool isSelectingTab;

	private const int NotYetKnown = -100;

	private int resolutionIndex = -100;

	private int _lastModificationFrame = -1;

	public KeysSetting selectedKeysSetting;

	private List<PauseSettingButton> currentSettings => settingsTabs[selectedTab];

	private PauseSettingButton selectedSetting => currentSettings[selectedIndex];

	private Resolution[] resolutions => (from r in Screen.resolutions
		group r by (width: r.width, height: r.height) into g
		select g.First()).ToArray();

	private PauseMenu pauseMenu => scrController.instance.pauseMenu;

	public bool editingKeys => selectedKeysSetting != null;

	private void Awake()
	{
		GenerateSettings();
		tabButtonsContainer.gameObject.SetActive(value: false);
		goat.description.GetComponent<Button>().onClick.AddListener(delegate
		{
			UpdateSelectedSetting(Interaction.ActivateInfo);
		});
	}

	public void Show()
	{
		isSelectingTab = true;
		base.gameObject.SetActive(value: true);
		SetDescription("");
		foreach (PauseSettingButton currentSetting in currentSettings)
		{
			currentSetting.SetFocus(false);
		}
		selectedIndex = -1;
		SelectTab(0, force: true);
		settingsContainer.DOKill();
		settingsContainer.pivot = settingsContainer.pivot.WithY(1.5f);
		settingsContainer.DOPivotY(0f, 0.5f).SetEase(Ease.OutQuint).SetUpdate(isIndependentUpdate: true);
		scrSfx.instance.PlaySfx(SfxSound.MenuStonePanelOpen, MixerGroup.InterfaceParent);
	}

	private void GenerateSettings()
	{
		string text = Resources.Load<TextAsset>("PauseMenuSettings").text;
		settingsTabs = new List<List<PauseSettingButton>>();
		tabButtons = new List<SettingsTabButton>();
		foreach (KeyValuePair<string, object> item in Json.Deserialize(text) as Dictionary<string, object>)
		{
			List<object> obj = item.Value as List<object>;
			string key = item.Key;
			List<PauseSettingButton> list = new List<PauseSettingButton>();
			foreach (object item2 in obj)
			{
				Dictionary<string, object> dictionary = item2 as Dictionary<string, object>;
				string text2 = dictionary["name"] as string;
				if (dictionary.ContainsKey("exclude") || (ADOBase.isExpo && dictionary.TryGetValue("excludeExpo", out var value) && value is bool && (bool)value) || (!GCS.isDev && dictionary.TryGetValue("devOnly", out value) && value is bool && (bool)value))
				{
					continue;
				}
				if (dictionary.TryGetValueAs<string, object, List<object>>("platform", out var valueAs))
				{
					bool flag = false;
					foreach (object item3 in valueAs)
					{
						Platform platform = ADOBase.platform;
						bool flag2 = (platform == Platform.Mac || platform == Platform.Windows || platform == Platform.Linux) && !RDC.isSteamDeckOnSteamOS;
						string text3 = (string)item3;
						if ((text3 == "iOS" && ADOBase.platform == Platform.iOS) || (text3 == "android" && ADOBase.platform == Platform.Android) || (text3 == "mobile" && ADOBase.isMobile) || (text3 == "switch" && ADOBase.isSwitch) || (text3 == "desktop" && flag2) || (text3 == "steamDeck" && RDC.isSteamDeckOnSteamOS))
						{
							flag = true;
							break;
						}
						if (Enum.TryParse<Platform>(text3, out var result) && result == ADOBase.platform)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						continue;
					}
				}
				if (dictionary.TryGetValueAs<string, object, string>("requiresDLC", out var valueAs2))
				{
					bool flag3 = false;
					if (!(valueAs2 == "neoCosmos"))
					{
						if (valueAs2 == "vega")
						{
							flag3 = base.vegaDLCManager.installed;
						}
					}
					else
					{
						flag3 = base.neoCosmosManager.installed;
					}
					if (!flag3)
					{
						continue;
					}
				}
				if (text2 == "vibration" && !GameServices.Instance.HasVibrator())
				{
					continue;
				}
				PauseSettingButton component = UnityEngine.Object.Instantiate(buttonPrefab, settingsScrollRectContent).GetComponent<PauseSettingButton>();
				component.name = text2;
				list.Add(component);
				component.type = (string)dictionary["type"];
				string type = component.type;
				if (type == "Action" || type == "Keys")
				{
					component.leftArrow.transform.ScaleXY(0f, 0f);
					component.rightArrow.transform.ScaleXY(0f, 0f);
					if (component.type == "Action")
					{
						RectTransform rectTransform = component.label.rectTransform;
						rectTransform.offsetMax = new Vector2(0f - rectTransform.offsetMin.x, rectTransform.offsetMax.y);
						component.label.alignment = TextAnchor.MiddleCenter;
					}
				}
				dictionary.TryGetValueAs("min", out component.minInt, 0);
				dictionary.TryGetValueAs("max", out component.maxInt, 0);
				dictionary.TryGetValueAs<string, object, string>("unit", out component.unit);
				dictionary.TryGetValueAs("changeBy", out component.changeBy, 0);
				dictionary.TryGetValueAs("multiplyBy", out component.multiplyBy, 0);
				dictionary.TryGetValueAs("deferUpdate", out component.deferUpdate, _default: false);
				if (!dictionary.TryGetValueAs("changeBySmall", out component.changeBySmall, 0))
				{
					component.changeBySmall = component.changeBy;
				}
				dictionary.TryGetValueAs("restartOnChange", out component.restartOnChange, _default: false);
				if (text2 == "mobileTargetFrameRate")
				{
					component.maxInt = GameServices.Instance.FrameRates.Count - 1;
				}
				component.hasRange = component.minInt != component.maxInt;
				bool exists = false;
				if (ADOBase.isMobile)
				{
					component.descriptionKey = component.descriptionKey + "pauseMenu.settings.info." + text2 + ".mobile";
					RDString.GetWithCheck(component.descriptionKey, out exists);
				}
				if (!exists)
				{
					component.descriptionKey = "pauseMenu.settings.info." + text2;
					RDString.GetWithCheck(component.descriptionKey, out exists);
				}
				bool flag4 = text2 == "inputOffset" && ADOBase.isGamepad;
				component.hasDescription = !flag4 && exists;
				UpdateSetting(component, Interaction.Refresh);
				component.SetFocus(false);
				component.label.text = RDString.Get("pauseMenu.settings." + component.name);
				component.label.SetLocalizedFont();
				component.valueLabel.SetLocalizedFont();
			}
			if (list.Count > 0)
			{
				SettingsTabButton component2 = UnityEngine.Object.Instantiate(tabButtonPrefab, tabButtonsContainer).GetComponent<SettingsTabButton>();
				tabButtons.Add(component2);
				component2.index = tabButtons.Count - 1;
				component2.SetFocus(false);
				component2.name = key;
				component2.label.text = RDString.Get("pauseMenu.settings.category." + key);
				component2.label.SetLocalizedFont();
				component2.icon.sprite = Resources.Load<Sprite>("SettingsTabIcon/" + key);
				settingsTabs.Add(list);
			}
		}
	}

	public void UpdateSelectedSetting(Interaction action)
	{
		if (selectedIndex >= 0 && selectedIndex < currentSettings.Count)
		{
			UpdateSetting(selectedSetting, action);
		}
	}

	private void Update()
	{
		if (RDInput.cancelPress)
		{
			if (editingKeys)
			{
				StopEditingKeys();
			}
			else
			{
				if (ADOBase.isLevelEditor)
				{
					pauseMenu.Hide();
				}
				else
				{
					pauseMenu.PlayMenuSfx(SfxSound.MenuBack);
				}
				Persistence.Save();
			}
		}
		if (isSelectingTab)
		{
			if (RDInput.leftPress)
			{
				SelectPreviousTab();
			}
			else if (RDInput.rightPress)
			{
				SelectNextTab();
			}
			else if (RDInput.confirmPress || RDInput.downPress)
			{
				isSelectingTab = false;
				Select(0, force: true);
			}
		}
		else if (editingKeys)
		{
			List<object> list = (from x in RDInput.GetMainPressKeys()
				select x.value).ToList();
			if (RDEditorUtils.CheckForKeyCombo(control: true, shift: false, KeyCode.Backspace))
			{
				selectedKeysSetting.Clear();
				UpdateSelectedSetting(Interaction.Refresh);
			}
			else if (list.Count > 0)
			{
				if (list.First() is AsyncKeyCode)
				{
					foreach (object item in list)
					{
						selectedKeysSetting.Toggle(((AsyncKeyCode)item).key);
					}
				}
				else
				{
					foreach (object item2 in list)
					{
						selectedKeysSetting.Toggle((KeyCode)item2);
					}
				}
				UpdateSelectedSetting(Interaction.Refresh);
			}
		}
		else if (RDInput.confirmPress)
		{
			UpdateSelectedSetting(Interaction.Activate);
		}
		else if (RDInput.downPress)
		{
			SelectNextOption();
		}
		else if (RDInput.upPress)
		{
			SelectPreviousOption();
		}
		else if (RDInput.leftPress)
		{
			if (Time.frameCount - _lastModificationFrame > 2)
			{
				UpdateSelectedSetting(Interaction.Decrement);
			}
			_lastModificationFrame = Time.frameCount;
		}
		else if (RDInput.rightPress)
		{
			if (Time.frameCount - _lastModificationFrame > 2)
			{
				UpdateSelectedSetting(Interaction.Increment);
			}
			_lastModificationFrame = Time.frameCount;
		}
		else if (RDInput.confirmPress)
		{
			UpdateSelectedSetting(Interaction.Activate);
		}
		ArrangeTabButtons();
		settingsContainer.SizeDeltaY(canvasRectTransform.rect.height - 19f);
	}

	private void StartEditingKeys(KeysSetting setting)
	{
		selectedKeysSetting = setting;
		SetDescription("<color=orange>" + RDString.Get("pauseMenu.settings.exitInfo.keyLimiter", new Dictionary<string, object> { 
		{
			"removeAll",
			RDEditorUtils.KeyComboToString(control: true, shift: false, KeyCode.Backspace)
		} }) + "</color>");
	}

	private void StopEditingKeys()
	{
		selectedKeysSetting = null;
		SetDescription(selectedSetting.GetDescriptionText());
	}

	public void UpdateSetting(PauseSettingButton setting, Interaction action)
	{
		bool flag = action == Interaction.Increment;
		bool flag2 = action == Interaction.Decrement;
		bool refresh = action == Interaction.Refresh;
		string type = setting.type;
		bool flag3 = type == "Action" || type == "Keys";
		bool holdingShift = RDInput.holdingShift;
		bool flag4 = false;
		object obj = null;
		Action<object> action2 = null;
		Action action3 = null;
		Action action4 = null;
		if (setting.type == "Keys" && (flag || flag2))
		{
			return;
		}
		if (action == Interaction.Refresh)
		{
			setting.CachedValue = null;
		}
		switch (setting.name)
		{
		case "volume":
			obj = scrController.volume;
			action2 = delegate(object newVolume)
			{
				scrController.volume = (int)newVolume;
				ADOBase.controller.UpdateListenerVolume();
			};
			break;
		case "musicVolume":
			obj = Persistence.musicVolume;
			action2 = delegate(object newVolume)
			{
				Persistence.musicVolume = (int)newVolume;
				RDUtils.SetMixerVolume("MusicVolume", (float)(int)newVolume / 10f);
				if ((int)newVolume > 0)
				{
					scrSfx.instance.PlayMusicVolumePreview();
				}
			};
			break;
		case "hitsoundVolume":
			obj = Persistence.hitSoundVolume;
			action2 = delegate(object newVolume)
			{
				Persistence.hitSoundVolume = (int)newVolume;
				RDUtils.SetMixerVolume("HitsoundsVolume", (float)(int)newVolume / 10f);
				if ((int)newVolume > 0)
				{
					scrSfx.instance.PlaySfx(HitSound.Shaker, MixerGroup.ConductorHitsounds);
				}
			};
			break;
		case "sfxVolume":
			obj = Persistence.sfxVolume;
			action2 = delegate(object newVolume)
			{
				Persistence.sfxVolume = (int)newVolume;
				RDUtils.SetMixerVolume("SfxVolume", (float)(int)newVolume / 10f);
				if ((int)newVolume > 0)
				{
					scrSfx.instance.PlaySfx(SfxSound.PlanetPreExplosion, MixerGroup.SfxParent, 0.5f);
				}
			};
			break;
		case "interfaceVolume":
			obj = Persistence.interfaceVolume;
			action2 = delegate(object newVolume)
			{
				Persistence.interfaceVolume = (int)newVolume;
				RDUtils.SetMixerVolume("InterfaceVolume", (float)(int)newVolume / 10f);
				if ((int)newVolume > 0)
				{
					scrSfx.instance.PlaySfx(SfxSound.OttoActivate, MixerGroup.InterfaceParent);
				}
			};
			break;
		case "fullscreen":
			obj = Screen.fullScreen;
			action2 = delegate(object fullscreen)
			{
				Screen.fullScreenMode = (((bool)fullscreen) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
			};
			break;
		case "resolution":
			obj = ((resolutionIndex == -100) ? NearestResolution(Screen.width, Screen.height) : resolutions[resolutionIndex]);
			action2 = delegate(object obj2)
			{
				resolutionIndex = Array.IndexOf(resolutions, (Resolution)obj2);
				Resolution resolution2 = resolutions[resolutionIndex];
				Screen.SetResolution(resolution2.width, resolution2.height, Screen.fullScreenMode);
			};
			break;
		case "mobileResolution":
			obj = GetCurrentResolutionPercent();
			action2 = delegate(object percent)
			{
				int width = Mathf.RoundToInt((float)(int)percent * 0.01f * (float)ADOBase.GetDisplayWidth());
				int height = Mathf.RoundToInt((float)(int)percent * 0.01f * (float)ADOBase.GetDisplayHeight());
				Screen.SetResolution(width, height, fullscreen: true);
			};
			break;
		case "targetFramerate":
			obj = Math.Clamp(RDUtils.targetFrameRate, setting.minInt, setting.maxInt);
			if (refresh)
			{
				flag4 = true;
			}
			action2 = delegate(object frameRate)
			{
				bool flag9 = (int)frameRate == setting.maxInt;
				if (!refresh)
				{
					Persistence.targetFrameRate = (flag9 ? 10000 : ((int)frameRate));
				}
				if (flag9)
				{
					setting.valueLabel.text = RDString.Get("pauseMenu.settings.noFramerateLimit");
				}
			};
			break;
		case "mobileTargetFrameRate":
		{
			int targetFrameRateMobile = Persistence.targetFrameRateMobile;
			targetFrameRateMobile = ((targetFrameRateMobile == -1) ? setting.maxInt : targetFrameRateMobile);
			obj = Math.Clamp(targetFrameRateMobile, setting.minInt, setting.maxInt);
			if (refresh)
			{
				flag4 = true;
			}
			action2 = delegate(object frameRateMobile)
			{
				int num7 = (int)frameRateMobile;
				int targetFrameRate = GameServices.Instance.FrameRates[num7];
				Persistence.targetFrameRateMobile = num7;
				Persistence.targetFrameRate = targetFrameRate;
				setting.valueLabel.text = setting.valueLabel.text.Replace(frameRateMobile.ToString(), targetFrameRate.ToString());
			};
			break;
		}
		case "visualEffects":
			obj = Persistence.realVisualEffects;
			action2 = delegate(object effects)
			{
				Persistence.visualEffects = (VisualEffects)effects;
			};
			break;
		case "visualQuality":
			obj = Persistence.visualQuality;
			action2 = delegate(object quality)
			{
				Persistence.visualQuality = (VisualQuality)quality;
			};
			break;
		case "animateSpeedChanges":
			obj = Persistence.animateSpeedChange;
			action2 = delegate(object animate)
			{
				Persistence.animateSpeedChange = (bool)animate;
			};
			break;
		case "antiAliasing":
			obj = QualitySettings.antiAliasing;
			action2 = delegate(object samples)
			{
				QualitySettings.antiAliasing = (int)samples;
				Persistence.antiAliasing = (int)samples;
			};
			break;
		case "vibration":
			obj = Persistence.vibration;
			action2 = delegate(object vibration)
			{
				Persistence.vibration = (bool)vibration;
			};
			break;
		case "inputOffset":
			offsetButton = setting;
			obj = Mathf.RoundToInt(scrConductor.calibration_i * 1000f);
			action2 = delegate(object offset)
			{
				scrConductor.currentPreset.inputOffset = (int)offset;
				scrConductor.SaveCurrentPreset();
			};
			if (ADOBase.isMobile)
			{
				action4 = delegate
				{
					Persistence.Save();
					ADOBase.GoToCalibration();
				};
			}
			break;
		case "audioBufferSize":
			obj = AudioSettings.GetConfiguration().dspBufferSize;
			action2 = delegate(object v)
			{
				if (action != Interaction.Refresh)
				{
					Persistence.audioBufferSize = (int)v;
				}
			};
			break;
		case "calibration":
			action3 = delegate
			{
				ADOBase.GoToCalibration();
			};
			break;
		case "language":
			obj = RDUtils.ParseEnum(Persistence.language.ToString(), SystemLanguage.English);
			action2 = delegate(object obj2)
			{
				pauseMenu.PlayMenuSfx(SfxSound.MobileButtonEnter);
				Persistence.language = (SystemLanguage)obj2;
				RDString.ChangeLanguage((SystemLanguage)obj2);
				Persistence.Save();
				ADOBase.RestartScene();
			};
			action4 = action3;
			break;
		case "vsync":
			obj = QualitySettings.vSyncCount > 0;
			action2 = delegate(object vsync)
			{
				int num7 = (((bool)vsync) ? 1 : 0);
				QualitySettings.vSyncCount = ((!ADOBase.isMobile) ? num7 : 0);
				Persistence.vSync = num7;
			};
			break;
		case "hitMarginLimit":
			obj = GCS.hitMarginLimit;
			action2 = delegate(object hitMarginLimitObj)
			{
				Persistence.hitMarginLimit = (GCS.hitMarginLimit = (HitMarginLimit)hitMarginLimitObj);
			};
			break;
		case "showXAccuracy":
			obj = Persistence.showXAccuracy;
			action2 = delegate(object showXAccuracy)
			{
				Persistence.showXAccuracy = (bool)showXAccuracy;
			};
			break;
		case "hitErrorMeterSize":
			obj = Persistence.hitErrorMeterSize;
			action2 = delegate(object meterSizeObj)
			{
				ErrorMeterSize errorMeterSize = (ErrorMeterSize)meterSizeObj;
				ErrorMeterShape hitErrorMeterShape = Persistence.hitErrorMeterShape;
				if ((bool)ADOBase.controller.errorMeter)
				{
					ADOBase.controller.errorMeter.UpdateLayout(errorMeterSize, hitErrorMeterShape);
				}
				Persistence.hitErrorMeterSize = errorMeterSize;
			};
			break;
		case "hitErrorMeterShape":
			obj = Persistence.hitErrorMeterShape;
			action2 = delegate(object meterShapeObj)
			{
				ErrorMeterSize hitErrorMeterSize = Persistence.hitErrorMeterSize;
				ErrorMeterShape errorMeterShape = (ErrorMeterShape)meterShapeObj;
				if ((bool)ADOBase.controller.errorMeter)
				{
					ADOBase.controller.errorMeter.UpdateLayout(hitErrorMeterSize, errorMeterShape);
				}
				Persistence.hitErrorMeterShape = errorMeterShape;
			};
			break;
		case "showDetailedResults":
			obj = scrController.showDetailedResults;
			action2 = delegate(object detailedResults)
			{
				scrController.showDetailedResults = (bool)detailedResults;
				Persistence.showDetailedResults = (bool)detailedResults;
			};
			break;
		case "hideCursorWhilePlaying":
			obj = Persistence.GetHideCursorWhilePlaying();
			action2 = delegate(object hideCursorWhilePlaying)
			{
				Persistence.SetHideCursorWhilePlaying((bool)hideCursorWhilePlaying);
			};
			break;
		case "skipIntroBehavior":
			obj = Persistence.skipIntroBehavior;
			action2 = delegate(object skipIntroAfterFirstTry)
			{
				Persistence.skipIntroBehavior = (SkipIntroBehavior)skipIntroAfterFirstTry;
			};
			break;
		case "multitapTileBehavior":
			obj = Persistence.multiTapTileBehavior;
			action2 = delegate(object behavior)
			{
				Persistence.multiTapTileBehavior = (MultitapTileBehavior)behavior;
			};
			break;
		case "holdBehavior":
			obj = Persistence.holdBehavior;
			action2 = delegate(object behavior)
			{
				scrController.instance.strictHoldsSaved = (int)behavior == 0;
				scrController.instance.strictHolds = scrController.instance.strictHoldsSaved;
				scrController.instance.requireHolding = (int)behavior < 2;
				Persistence.holdBehavior = (HoldBehavior)behavior;
			};
			break;
		case "unlockAllLevels":
			obj = Persistence.unlockAllLevels;
			action2 = delegate(object unlockAllLevels)
			{
				RDC.forceUnlockAllLevels = (bool)unlockAllLevels;
				Persistence.unlockAllLevels = (bool)unlockAllLevels;
			};
			break;
		case "freeroamInvuln":
			obj = Persistence.freeroamInvulnerability;
			action2 = delegate(object freeroamInvulnerability)
			{
				scrController.instance.freeroamInvulnerability = (bool)freeroamInvulnerability;
				Persistence.freeroamInvulnerability = (bool)freeroamInvulnerability;
			};
			break;
		case "shortcutPitchedPlaySpeed":
			obj = Persistence.shortcutPlaySpeed;
			action2 = delegate(object shortcutPitchedPlaySpeed)
			{
				Persistence.shortcutPlaySpeed = (int)shortcutPitchedPlaySpeed;
			};
			break;
		case "recoverDataSteam":
		case "recoverDataFromAchievements":
			action3 = delegate
			{
				Persistence.RecoverSaveDataFromAchievements();
				ADOBase.GoToLevelSelect();
			};
			break;
		case "clearData":
			action3 = delegate
			{
				ClearDataButtonWasActivated();
			};
			break;
		case "clearDlc":
			action3 = delegate
			{
				ClearDlcButtonWasActivated();
			};
			break;
		case "useAsynchronousInput":
			obj = AsyncInputManager.isActive;
			action2 = delegate(object enabled)
			{
				bool flag9 = (bool)enabled;
				try
				{
					RDC.useAsyncInput = flag9;
					Persistence.SetChosenAsynchronousInput(flag9);
					SetDescription(RDString.Get("pauseMenu.settings.info.useAsynchronousInput"));
					setting.valueLabel.text = RDString.Get(AsyncInputManager.isActive ? "pauseMenu.settings.on" : "pauseMenu.settings.off");
				}
				catch (Exception ex)
				{
					Debug.LogError(ex);
					SetDescription("<color=#ff3b3b>" + RDString.Get((ADOBase.platform == Platform.Mac) ? "pauseMenu.settings.useAsynchronousInput.toggleError.mac" : "pauseMenu.settings.useAsynchronousInput.toggleError", new Dictionary<string, object> { { "error", ex.Message } }) + "</color>");
					setting.valueLabel.text = RDString.Get(AsyncInputManager.isActive ? "pauseMenu.settings.on" : "pauseMenu.settings.off");
				}
				foreach (List<PauseSettingButton> settingsTab in settingsTabs)
				{
					foreach (PauseSettingButton item in settingsTab)
					{
						if (item.type == "Keys")
						{
							UpdateSetting(item, Interaction.Refresh);
						}
					}
				}
			};
			break;
		case "hideRichPresenceDetails":
			obj = Persistence.hideRichPresenceDetails;
			action2 = delegate(object hideRichPresenceDetails)
			{
				Persistence.hideRichPresenceDetails = (bool)hideRichPresenceDetails;
				DiscordController.instance?.UpdatePresence();
			};
			break;
		case "enableProEvents":
			obj = Persistence.enableProEvents;
			action2 = delegate(object enableProEvents)
			{
				Persistence.enableProEvents = (bool)enableProEvents;
			};
			break;
		case "forceVisualSettings":
			obj = Persistence.forceVisualSettings;
			action2 = delegate(object forceVisualSettings)
			{
				Persistence.forceVisualSettings = (bool)forceVisualSettings;
			};
			break;
		case "keyLimiter":
			obj = Persistence.keyLimiterKeys;
			action3 = delegate
			{
				StartEditingKeys(Persistence.keyLimiterKeys);
			};
			break;
		}
		if (setting.deferUpdate && setting.CachedValue != null)
		{
			obj = setting.CachedValue;
		}
		bool flag5 = false;
		if (!flag3)
		{
			if (obj == null)
			{
				Debug.LogError("No getter for " + setting.name + "!");
				return;
			}
			if (action2 == null)
			{
				Debug.LogError("No setter for " + setting.name + "!");
				return;
			}
			if (action != Interaction.Refresh)
			{
				flag5 = setting.initialValue.Equals(obj);
			}
		}
		bool flag6 = false;
		if (action != Interaction.Activate)
		{
			int num = ((setting.changeBy <= 0) ? 1 : (holdingShift ? setting.changeBySmall : setting.changeBy));
			if (setting.type == "Int")
			{
				int num2 = (int)obj;
				if (action != Interaction.Refresh)
				{
					if (setting.multiplyBy > 0)
					{
						obj = ((!flag) ? ((object)((int)obj / setting.multiplyBy)) : ((object)((int)obj * setting.multiplyBy)));
					}
					else if (flag)
					{
						obj = (int)obj + num;
					}
					else if (flag2)
					{
						obj = (int)obj - num;
					}
				}
				if (setting.hasRange)
				{
					obj = Mathf.Clamp((int)obj, setting.minInt, setting.maxInt);
					setting.leftArrow.transform.localScale = (((int)obj == setting.minInt) ? Vector3.zero : Vector3.one);
					setting.rightArrow.transform.localScale = (((int)obj == setting.maxInt) ? Vector3.zero : Vector3.one);
				}
				if ((int)obj != num2)
				{
					flag6 = true;
				}
				if (flag6 || refresh)
				{
					setting.valueLabel.text = obj?.ToString() ?? "";
					if (!setting.unit.IsNullOrEmpty())
					{
						bool exists = false;
						string withCheck = RDString.GetWithCheck("editor.unit." + setting.unit, out exists);
						setting.valueLabel.text += (exists ? withCheck : setting.unit);
					}
				}
			}
			else if (setting.type == "Bool")
			{
				if (!refresh)
				{
					flag6 = true;
					obj = !(bool)obj;
				}
				setting.valueLabel.text = RDString.Get(((bool)obj) ? "pauseMenu.settings.on" : "pauseMenu.settings.off");
			}
			else if (setting.type == "Resolution")
			{
				Resolution value = (Resolution)obj;
				int num3 = Array.IndexOf(resolutions, value);
				if (flag2 && num3 > 0)
				{
					flag6 = true;
					num3--;
				}
				else if (flag && num3 < resolutions.Length - 1)
				{
					flag6 = true;
					num3++;
				}
				obj = resolutions[num3];
				Resolution resolution = (Resolution)obj;
				setting.valueLabel.text = $"{resolution.width}x{resolution.height}";
			}
			else if (setting.type == "Samples")
			{
				obj = Math.Max(1, (int)obj);
				int num4 = (int)obj;
				if (flag)
				{
					obj = (int)obj * 2;
				}
				else if (flag2)
				{
					obj = (int)obj / 2;
				}
				obj = Mathf.Clamp((int)obj, 1, 8);
				if ((int)obj != num4)
				{
					flag6 = true;
				}
				setting.valueLabel.text = (((int)obj == 1) ? RDString.Get("pauseMenu.settings.off") : (obj?.ToString() + "x"));
			}
			else if (setting.type.StartsWith("Enum:"))
			{
				string text = setting.type.Remove(0, 5);
				Type type2;
				int[] array;
				if (text == "Language")
				{
					type2 = typeof(SystemLanguage);
					array = Array.ConvertAll(RDString.AvailableLanguages, (SystemLanguage v) => (int)v);
				}
				else
				{
					type2 = Type.GetType(text);
					array = (int[])Enum.GetValues(type2);
				}
				int num5 = 0;
				for (int num6 = 0; num6 < array.Length; num6++)
				{
					if (array[num6] == (int)obj)
					{
						num5 = num6;
						break;
					}
				}
				if (flag)
				{
					flag6 = true;
					num5 = (num5 + 1) % array.Length;
				}
				else if (flag2)
				{
					flag6 = true;
					num5 = (num5 + array.Length - 1) % array.Length;
				}
				obj = Enum.ToObject(type2, array[num5]);
				if (text == "Language")
				{
					SystemLanguage language = (SystemLanguage)obj;
					FontData fontDataForLanguage = RDString.GetFontDataForLanguage(language);
					setting.valueLabel.text = GetNativeLanguageName(language);
					setting.valueLabel.font = fontDataForLanguage.font;
					setting.valueLabel.fontSize = Mathf.RoundToInt(fontDataForLanguage.fontScale * 9f);
				}
				else
				{
					setting.valueLabel.text = RDString.Get("pauseMenu.settings." + type2.ToString() + "." + type2.GetEnumName(obj));
				}
			}
			else if (setting.type == "Keys")
			{
				KeysSetting keysSetting = (KeysSetting)obj;
				setting.valueLabel.text = string.Join(", ", keysSetting.KeyLabels).NullIfEmpty() ?? RDString.Get("editor.none");
			}
		}
		if (action == Interaction.Refresh)
		{
			setting.initialValue = obj;
		}
		if (setting.deferUpdate)
		{
			bool flag7 = setting.initialValue.Equals(obj);
			setting.CachedValue = obj;
			setting.confirmText.gameObject.SetActive(!flag7);
		}
		if (flag6 || flag4)
		{
			if (!setting.deferUpdate)
			{
				action2(obj);
			}
			if (!flag3)
			{
				if (flag)
				{
					setting.PlayArrowAnimation(isRight: true);
					pauseMenu.PlayMenuSfx(SfxSound.MenuIncrement);
				}
				else if (flag2)
				{
					setting.PlayArrowAnimation(isRight: false);
					pauseMenu.PlayMenuSfx(SfxSound.MenuDecrement);
				}
			}
			if (!setting.deferUpdate && setting.restartOnChange)
			{
				bool flag8 = setting.initialValue.Equals(obj);
				if (flag5 != flag8)
				{
					if (flag8)
					{
						settingsThatRequireRestart--;
					}
					else
					{
						settingsThatRequireRestart++;
					}
				}
			}
		}
		if (action == Interaction.Activate)
		{
			if (action3 != null)
			{
				confirmationProgress++;
				action3();
				pauseMenu.PlayMenuSfx(SfxSound.MobileButtonEnter);
			}
			if (setting.deferUpdate && setting.initialValue != obj)
			{
				action2(obj);
				setting.initialValue = obj;
				setting.CachedValue = obj;
				setting.confirmText.gameObject.SetActive(value: false);
				if (setting.restartOnChange)
				{
					settingsThatRequireRestart++;
				}
			}
		}
		if (action == Interaction.ActivateInfo)
		{
			action4?.Invoke();
		}
	}

	public void Select(PauseSettingButton button)
	{
		Select(currentSettings.IndexOf(button));
	}

	private void Select(int index, bool force = false)
	{
		PauseSettingButton pauseSettingButton = currentSettings[index];
		if (selectedIndex != index || force)
		{
			if (selectedIndex >= 0)
			{
				currentSettings[selectedIndex].SetFocus(false);
			}
			tabButtons[selectedTab].SetFocus(focus: false, dontChangeSize: true);
			pauseSettingButton.SetFocus(true);
			pauseMenu.PlayMenuSfx(SfxSound.MenuNavigate);
			selectedIndex = index;
			StartCoroutine(UpdateContentY(index, pauseSettingButton));
		}
		else if (ADOBase.isMobile)
		{
			UpdateSetting(pauseSettingButton, Interaction.Activate);
		}
		scrSfx.instance.StopMusicVolumePreview();
	}

	private IEnumerator UpdateContentY(int _index, PauseSettingButton _newSetting)
	{
		yield return null;
		float a;
		if (_index == currentSettings.Count - 1)
		{
			a = settingsScrollRectContent.sizeDelta.y - settingsScrollRectContent.parent.GetComponent<RectTransform>().rect.height;
			a = Mathf.Max(a, 0f);
			_ = currentSettings[_index].hasDescription;
		}
		else
		{
			RectTransform rectTransform = _newSetting.rectTransform;
			float num = rectTransform.anchoredPosition.y - rectTransform.sizeDelta.y / (_newSetting.hasDescription ? 1f : 2f);
			float height = settingsScrollRectContent.parent.GetComponent<RectTransform>().rect.height;
			a = 0f - (num + height / 2f);
			float y = settingsScrollRectContent.sizeDelta.y;
			a = Mathf.Clamp(a, 0f, Mathf.Max(0f, y - height));
		}
		settingsScrollRectContent.DOKill();
		scrollRect.movementType = ScrollRect.MovementType.Unrestricted;
		scrollRect.inertia = false;
		settingsScrollRectContent.DOAnchorPosY(a, 0.15f).SetUpdate(isIndependentUpdate: true).OnComplete(delegate
		{
			scrollRect.inertia = true;
			scrollRect.movementType = ScrollRect.MovementType.Elastic;
		});
		confirmationProgress = 0;
	}

	private void SelectPreviousOption()
	{
		int index = (selectedIndex + currentSettings.Count - 1) % currentSettings.Count;
		Select(index);
	}

	private void SelectNextOption()
	{
		int index = (selectedIndex + 1) % currentSettings.Count;
		Select(index);
	}

	private void SelectPreviousTab()
	{
		int index = (selectedTab + tabButtons.Count - 1) % tabButtons.Count;
		SelectTab(index);
	}

	private void SelectNextTab()
	{
		int index = (selectedTab + 1) % tabButtons.Count;
		SelectTab(index);
	}

	private int GetCurrentResolutionPercent()
	{
		return Mathf.RoundToInt((float)Screen.width / (float)ADOBase.GetDisplayWidth() * 100f);
	}

	private string GetNativeLanguageName(SystemLanguage language)
	{
		return Localization.GetLocalizedString("pauseMenu.settings.myLanguage", language);
	}

	private Resolution NearestResolution(int width, int height)
	{
		int num = int.MaxValue;
		int num2 = 0;
		int num3 = 0;
		Resolution[] array = resolutions;
		for (int i = 0; i < array.Length; i++)
		{
			Resolution resolution = array[i];
			int num4 = Mathf.Abs(width - resolution.width) + Mathf.Abs(height - resolution.height);
			if (num4 < num)
			{
				num = num4;
				num2 = num3;
			}
			if (width == resolution.width && height == resolution.height)
			{
				num2 = num3;
				break;
			}
			num3++;
		}
		return resolutions[num2];
	}

	private void ClearDataButtonWasActivated()
	{
		UpdateConfirmationText(confirmationProgress);
		if (confirmationProgress != 3)
		{
			return;
		}
		DOVirtual.DelayedCall(0.25f, delegate
		{
			Persistence.ClearData();
			GameServices instance = GameServices.Instance;
			if (instance.Initialized && instance.IsLoadStatusComplete)
			{
				instance.DeleteGame();
			}
			ADOBase.GoToLevelSelect();
		});
	}

	private void ClearDlcButtonWasActivated()
	{
		UpdateConfirmationText(confirmationProgress);
		if (confirmationProgress == 3)
		{
			DOVirtual.DelayedCall(0.25f, delegate
			{
				Persistence.ResetTaroStoryProgress();
				ADOBase.GoToLevelSelect();
			});
		}
	}

	private void UpdateConfirmationText(int progress)
	{
		SetDescription(confirmationProgress switch
		{
			1 => RDString.Get("pauseMenu.settings.clearData2Left"), 
			2 => RDString.Get("pauseMenu.settings.clearData1Left"), 
			3 => RDString.Get("pauseMenu.settings.dataErased"), 
			_ => "", 
		});
	}

	public void StopBrowsingTab()
	{
		isSelectingTab = true;
		if (selectedIndex >= 0)
		{
			currentSettings[selectedIndex].SetFocus(false);
		}
		tabButtons[selectedTab].SetFocus(focus: true, dontChangeSize: true);
		selectedIndex = -1;
		scrSfx.instance.StopMusicVolumePreview();
		SetDescription("");
	}

	public void SelectTab(SettingsTabButton button)
	{
		SelectTab(tabButtons.IndexOf(button));
	}

	public void SelectTab(int index, bool force = false)
	{
		if (!(selectedTab != index || force))
		{
			return;
		}
		tabButtons[selectedTab].SetFocus(false);
		tabButtons[index].SetFocus(true);
		selectedTab = index;
		if (selectedIndex >= 0)
		{
			currentSettings[selectedIndex].SetFocus(false);
		}
		settingsScrollRectContent.AnchorPosY(0f);
		if (!force)
		{
			pauseMenu.PlayMenuSfx(SfxSound.MobileButton);
		}
		for (int i = 0; i < settingsTabs.Count; i++)
		{
			foreach (PauseSettingButton item in settingsTabs[i])
			{
				item.gameObject.SetActive(i == index);
			}
		}
	}

	private void ArrangeTabButtons()
	{
		tabButtonsContainer.gameObject.SetActive(value: true);
		float x = tabButtonsContainer.GetComponent<RectTransform>().sizeDelta.x;
		float num = 0f;
		for (int i = 0; i < tabButtons.Count; i++)
		{
			SettingsTabButton settingsTabButton = tabButtons[i];
			settingsTabButton.rectTransform.LocalMoveX(num);
			num += settingsTabButton.rectTransform.sizeDelta.x + 0f;
		}
		float num2 = (x - (num - 0f)) / 2f;
		foreach (SettingsTabButton tabButton in tabButtons)
		{
			tabButton.rectTransform.LocalMoveX(tabButton.rectTransform.localPosition.x + num2);
		}
	}

	public void AudioOutputWasUpdated()
	{
		if ((bool)offsetButton)
		{
			UpdateSetting(offsetButton, Interaction.Refresh);
		}
	}

	public void SetDescription(string text)
	{
		goat.SetDescription(text);
	}
}
