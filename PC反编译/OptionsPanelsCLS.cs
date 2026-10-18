using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ADOFAI.SteamIntegration;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OptionsPanelsCLS : ADOBase
{
	public enum OptionName
	{
		Find = 0,
		Difficulty = 1,
		LastPlayed = 2,
		Song = 3,
		Artist = 4,
		Author = 5,
		SpeedTrial = 6,
		NoFail = 7,
		UnlockKeyLimiter = 8,
		Delete = 9,
		OpenImportPanel = 10,
		OpenWorkshop = 11,
		Wave = 12
	}

	[Serializable]
	public class Option
	{
		private const float animDuration = 0.15f;

		private readonly OptionName[] sortings = new OptionName[6]
		{
			OptionName.Wave,
			OptionName.Difficulty,
			OptionName.LastPlayed,
			OptionName.Song,
			OptionName.Artist,
			OptionName.Author
		};

		public static bool initialized;

		[NonSerialized]
		public bool highlighted;

		[NonSerialized]
		public bool selected;

		public OptionName name;

		public Image image;

		public Text text;

		public bool canBeUnselected;

		private static OptionsPanelsCLS panelsCLS => scnCLS.instance.optionsPanels;

		public void SetState(bool _highlighted, bool _selected, bool changeOnlyMenu = false)
		{
			if (highlighted != _highlighted)
			{
				highlighted = _highlighted;
				image.DOKill();
				Color endValue = (highlighted ? panelsCLS.highlightedColor : panelsCLS.unhighlightedColor);
				image.DOColor(endValue, 0.15f);
			}
			bool flag = selected != _selected;
			bool flag2 = !selected && _selected;
			if (flag)
			{
				selected = _selected;
				text.DOKill();
				Color endValue2 = (selected ? panelsCLS.selectedColor : panelsCLS.unselectedColor);
				text.DOColor(endValue2, 0.15f);
			}
			if (name == OptionName.OpenImportPanel && flag2 && scnCLS.instance.importLevelPanel != null)
			{
				scnCLS.instance.OpenImportPanel();
				SetState(_highlighted: false, _selected: false);
			}
			if (name == OptionName.OpenWorkshop && flag2)
			{
				SteamWorkshop.OpenWorkshopOverlay();
				SetState(_highlighted: false, _selected: false);
				panelsCLS.ToggleRightPanel();
			}
			if ((!canBeUnselected && !flag2) || (canBeUnselected && !flag) || !initialized || changeOnlyMenu)
			{
				return;
			}
			if (Array.IndexOf(sortings, name) != -1)
			{
				foreach (Option leftPanelOption in panelsCLS.leftPanelOptions)
				{
					if (leftPanelOption.name != name && Array.IndexOf(sortings, leftPanelOption.name) != -1 && leftPanelOption.selected)
					{
						leftPanelOption.SetState(_highlighted: false, _selected: false);
					}
				}
				ADOBase.cls.UpdateSorting(name);
				panelsCLS.UpdateOrderText();
			}
			else if (name == OptionName.SpeedTrial)
			{
				panelsCLS.ToggleSpeedTrial();
			}
			else if (name == OptionName.NoFail)
			{
				panelsCLS.ToggleNoFail();
			}
			else if (name == OptionName.UnlockKeyLimiter)
			{
				panelsCLS.ToggleUnlockKeyLimiter();
			}
			else if (name == OptionName.Delete)
			{
				scnCLS.instance.DeleteLevel();
				panelsCLS.ToggleRightPanel();
			}
			else if (name == OptionName.Find && panelsCLS.searchAvailable)
			{
				panelsCLS.toggleSearchModeCoroutine = panelsCLS.ToggleSearchMode(selected);
				panelsCLS.StartCoroutine(panelsCLS.toggleSearchModeCoroutine);
			}
		}
	}

	private readonly OptionName[] sortings = new OptionName[6]
	{
		OptionName.Wave,
		OptionName.Difficulty,
		OptionName.LastPlayed,
		OptionName.Song,
		OptionName.Artist,
		OptionName.Author
	};

	[Header("Panel")]
	public EventSystem eventSystem;

	public Image fadeBackground;

	public RectTransform leftPanel;

	public RectTransform rightPanel;

	public List<Option> leftPanelOptions;

	public List<Option> rightPanelOptions;

	[Header("Search")]
	public InputField searchInputField;

	public SpriteRenderer bgSprite;

	public Color bgColor;

	public Color bgColorSpeedTrial;

	public Text currentOrderText;

	[Header("Color")]
	public Color unhighlightedColor = Color.clear;

	public Color highlightedColor = new Color(1f, 1f, 1f, 0.75f);

	public Color unselectedColor = new Color(1f, 1f, 1f, 0.75f);

	public Color selectedColor = new Color(1f, 0.9921569f, 63f / 85f, 1f);

	[NonSerialized]
	public bool searchMode;

	[NonSerialized]
	public bool justHidPanels;

	[NonSerialized]
	public bool showingLeftPanel;

	[NonSerialized]
	public bool showingRightPanel;

	private IEnumerator toggleSearchModeCoroutine;

	private int currentOptionIndex;

	private int searchDeselectedFrame;

	private int showPanelFrame;

	private bool openedPanelUsingShortcut;

	private Option openImportPanelOption;

	private int openImportPanelOptionIndex = -1;

	private Option openWorkshopOption;

	private int openWorkshopOptionIndex = -1;

	private Option waveSortOption;

	private int waveSortOptionIndex = -1;

	private Option deleteOption;

	private int deleteOptionIndex = -1;

	private Tween backgroundTween;

	private const float FinalAlpha = 0.4f;

	private const float PanelTweenDuration = 0.1f;

	private bool searchAvailable => !ADOBase.isSwitch;

	public bool speedTrial => GCS.speedTrialMode;

	private Option currentOption => (showingLeftPanel ? leftPanelOptions : rightPanelOptions)[currentOptionIndex];

	public bool showingAnyPanel
	{
		get
		{
			if (!showingLeftPanel)
			{
				return showingRightPanel;
			}
			return true;
		}
	}

	private void Awake()
	{
		foreach (Option leftPanelOption in leftPanelOptions)
		{
			if (ADOBase.cls.sortingMethod == leftPanelOption.name)
			{
				leftPanelOption.SetState(_highlighted: false, _selected: true);
			}
		}
		GCS.useUnlockKeyLimiter = Persistence.showUnlockKeyLimiterButton;
		foreach (Option rightPanelOption in rightPanelOptions)
		{
			bool num = GCS.speedTrialMode && OptionName.SpeedTrial == rightPanelOption.name;
			bool flag = GCS.useNoFail && OptionName.NoFail == rightPanelOption.name;
			bool flag2 = GCS.useUnlockKeyLimiter && OptionName.UnlockKeyLimiter == rightPanelOption.name;
			if (num || flag || flag2)
			{
				rightPanelOption.SetState(_highlighted: false, _selected: true, changeOnlyMenu: true);
			}
		}
		Option.initialized = true;
		searchInputField.onValueChanged.AddListener(delegate(string sub)
		{
			ADOBase.cls.SearchLevels(sub);
		});
		searchInputField.onEndEdit.AddListener(delegate
		{
			searchDeselectedFrame = Time.frameCount;
			currentOption.SetState(_highlighted: true, _selected: false);
		});
		if (!Persistence.showUnlockKeyLimiterButton)
		{
			RemoveOption(rightPanelOptions, OptionName.UnlockKeyLimiter);
		}
		if (!searchAvailable)
		{
			RemoveOption(leftPanelOptions, OptionName.Find);
		}
		if (ADOBase.isSwitch)
		{
			RemoveOption(rightPanelOptions, OptionName.Delete);
			RemoveOption(rightPanelOptions, OptionName.OpenImportPanel);
		}
		if (!ADOBase.isSteamworks || !SteamController.initialized)
		{
			RemoveOption(rightPanelOptions, OptionName.OpenWorkshop);
		}
		openImportPanelOptionIndex = rightPanelOptions.FindIndex((Option o) => o.name == OptionName.OpenImportPanel);
		if (openImportPanelOptionIndex >= 0)
		{
			openImportPanelOption = rightPanelOptions[openImportPanelOptionIndex];
		}
		openWorkshopOptionIndex = rightPanelOptions.FindIndex((Option o) => o.name == OptionName.OpenWorkshop);
		if (openWorkshopOptionIndex >= 0)
		{
			openWorkshopOption = rightPanelOptions[openWorkshopOptionIndex];
		}
		waveSortOptionIndex = leftPanelOptions.FindIndex((Option o) => o.name == OptionName.Wave);
		if (waveSortOptionIndex >= 0)
		{
			waveSortOption = leftPanelOptions[waveSortOptionIndex];
		}
		deleteOptionIndex = rightPanelOptions.FindIndex((Option o) => o.name == OptionName.Delete);
		if (deleteOptionIndex >= 0)
		{
			deleteOption = rightPanelOptions[deleteOptionIndex];
		}
	}

	public void RefreshOptionsForCategory()
	{
		bool featuredLevelsMode = ADOBase.cls.featuredLevelsMode;
		ToggleCachedOption(rightPanelOptions, openImportPanelOption, openImportPanelOptionIndex, !featuredLevelsMode);
		ToggleCachedOption(rightPanelOptions, openWorkshopOption, openWorkshopOptionIndex, !featuredLevelsMode);
		ToggleCachedOption(leftPanelOptions, waveSortOption, waveSortOptionIndex, featuredLevelsMode);
		foreach (Option leftPanelOption in leftPanelOptions)
		{
			if (Array.IndexOf(sortings, leftPanelOption.name) != -1)
			{
				leftPanelOption.SetState(_highlighted: false, leftPanelOption.name == ADOBase.cls.effectiveSortingMethod, changeOnlyMenu: true);
			}
		}
	}

	private void ToggleCachedOption(List<Option> list, Option option, int originalIndex, bool show)
	{
		if (option != null)
		{
			bool flag = list.Contains(option);
			if (show && !flag)
			{
				list.Insert(Mathf.Min(originalIndex, list.Count), option);
			}
			else if (!show && flag)
			{
				list.Remove(option);
			}
			option.image.gameObject.SetActive(show);
		}
	}

	private void RemoveOption(List<Option> options, OptionName optionName)
	{
		Option option = options.First((Option option2) => option2.name == optionName);
		options.Remove(option);
		option.image.gameObject.SetActive(value: false);
	}

	private void LateUpdate()
	{
		bool flag = showingLeftPanel || showingRightPanel;
		if (backgroundTween == null || !backgroundTween.IsActive())
		{
			if (flag && fadeBackground.color.a != 0.4f)
			{
				backgroundTween = fadeBackground.DOFade(0.4f, 0.1f);
			}
			else if (!flag && fadeBackground.color.a != 0f)
			{
				backgroundTween = fadeBackground.DOFade(0f, 0.1f);
				StartCoroutine(EnableInputCo());
			}
		}
		fadeBackground.raycastTarget = showingAnyPanel;
		fadeBackground.enabled = fadeBackground.color.a > 0f;
		bgSprite.color = (speedTrial ? bgColorSpeedTrial : bgColor);
	}

	public bool CheckInputs()
	{
		if (!base.gameObject.activeInHierarchy)
		{
			return false;
		}
		if (scnCLS.instance.importLevelPanel.gameObject.activeSelf)
		{
			return false;
		}
		if (justHidPanels)
		{
			justHidPanels = false;
		}
		if (RDInput.leftPress || RDInput.rightPress)
		{
			bool show = (RDInput.leftPress ? (!showingLeftPanel) : (!showingRightPanel));
			TogglePanel(RDInput.leftPress, show);
			return true;
		}
		if (showingAnyPanel)
		{
			if (RDInput.cancelPress)
			{
				if (showingLeftPanel)
				{
					TogglePanel(left: true, show: false);
				}
				else if (showingRightPanel)
				{
					TogglePanel(left: false, show: false);
				}
				justHidPanels = true;
				return true;
			}
			if (RDInput.downPress || RDInput.upPress)
			{
				ChangeOption(RDInput.downPress ? 1 : (-1));
				return true;
			}
			if (RDInput.confirmPress && searchDeselectedFrame != Time.frameCount && showPanelFrame != Time.frameCount)
			{
				bool selected = !currentOption.canBeUnselected || !currentOption.selected;
				currentOption.SetState(_highlighted: true, selected);
				return true;
			}
		}
		if (Input.GetKeyDown(KeyCode.F) && !searchMode && searchAvailable)
		{
			if (!showingLeftPanel)
			{
				TogglePanel(left: true, show: true);
			}
			SelectOption(0);
			openedPanelUsingShortcut = true;
			return true;
		}
		if (Input.GetKeyDown(KeyCode.S))
		{
			SelectOption(OptionName.SpeedTrial, leftOptions: false);
			return true;
		}
		if (Input.GetKeyDown(KeyCode.N))
		{
			SelectOption(OptionName.NoFail, leftOptions: false);
			return true;
		}
		if (Input.GetKeyDown(KeyCode.Delete))
		{
			ADOBase.cls.DeleteLevel();
			return true;
		}
		if (Input.GetKeyDown(KeyCode.O))
		{
			OptionName[] array = (ADOBase.cls.featuredLevelsMode ? sortings : sortings.Where((OptionName s) => s != OptionName.Wave).ToArray());
			int num = Array.IndexOf(array, ADOBase.cls.effectiveSortingMethod);
			num = ((num != array.Length - 1) ? (num + 1) : 0);
			ADOBase.cls.sortingMethod = array[num];
			SelectOption(ADOBase.cls.sortingMethod, leftOptions: true);
			return true;
		}
		if (openedPanelUsingShortcut)
		{
			TogglePanel(left: true, show: false);
			openedPanelUsingShortcut = false;
			return true;
		}
		if (showingAnyPanel)
		{
			return true;
		}
		return false;
	}

	public void HideAnyPanel()
	{
		if (showingLeftPanel)
		{
			ToggleLeftPanel();
		}
		else if (showingRightPanel)
		{
			ToggleRightPanel();
		}
	}

	public void ToggleLeftPanel()
	{
		TogglePanel(left: true, !showingLeftPanel);
	}

	public void ToggleRightPanel()
	{
		TogglePanel(left: false, !showingRightPanel);
	}

	public void SelectOption(int optionName)
	{
		openedPanelUsingShortcut = false;
		int num = (showingLeftPanel ? leftPanelOptions : rightPanelOptions).FindIndex((Option o) => o.name == (OptionName)optionName);
		if (num >= 0)
		{
			currentOption.SetState(_highlighted: false, currentOption.selected);
			currentOptionIndex = num;
			currentOption.SetState(_highlighted: true, currentOption.selected);
			bool selected = currentOption.name == OptionName.Find || !currentOption.canBeUnselected || !currentOption.selected;
			currentOption.SetState(_highlighted: true, selected);
		}
	}

	private void TogglePanel(bool left, bool show)
	{
		if (!left)
		{
			if (show)
			{
				ToggleCachedOption(rightPanelOptions, deleteOption, deleteOptionIndex, ADOBase.cls.currentLevelCanBeDeleted);
			}
			string key = (ADOBase.cls.currentLevelIsWorkshop ? "cls.unsubscribe" : "cls.delete");
			foreach (Option rightPanelOption in rightPanelOptions)
			{
				if (rightPanelOption.name == OptionName.Delete)
				{
					rightPanelOption.SetState(_highlighted: false, ADOBase.cls.levelDeleted);
					if (rightPanelOption.text != null)
					{
						rightPanelOption.text.text = RDString.Get(key);
					}
				}
			}
		}
		showPanelFrame = Time.frameCount;
		RectTransform panel = (left ? leftPanel : rightPanel);
		float num = 30f;
		float yPos = ((!show) ? 0f : (left ? (leftPanel.sizeDelta.y - num) : (rightPanel.sizeDelta.y - num)));
		if (!eventSystem.alreadySelecting)
		{
			eventSystem.SetSelectedGameObject(null);
		}
		if (!show && currentOptionIndex != 0)
		{
			currentOption.SetState(_highlighted: false, currentOption.selected);
		}
		if (left)
		{
			if (show && showingRightPanel)
			{
				TogglePanel(left: false, show: false);
			}
			showingLeftPanel = show;
		}
		else
		{
			if (show && showingLeftPanel)
			{
				TogglePanel(left: true, show: false);
			}
			showingRightPanel = show;
		}
		panel.DOKill();
		if (show)
		{
			panel.DOAnchorPosY(yPos + num, 0.12f).SetEase(Ease.OutQuad).OnComplete(delegate
			{
				panel.DOAnchorPosY(yPos, 0.08f).SetEase(Ease.InQuad);
			});
			ADOBase.playerManager.SetAllPlayerResponsive(responsive: false);
		}
		else
		{
			panel.DOAnchorPosY(yPos, 0.15f).SetEase(Ease.OutSine);
		}
		SfxSound sfxSound = (show ? SfxSound.MenuStonePanelOpen : SfxSound.MenuStonePanelClose);
		scrSfx.instance.PlaySfx(sfxSound, MixerGroup.InterfaceParent);
		if (show)
		{
			currentOptionIndex = 0;
			currentOption.SetState(_highlighted: true, currentOption.selected);
		}
	}

	private IEnumerator EnableInputCo()
	{
		yield return new WaitForEndOfFrame();
		if (!showingLeftPanel && !showingRightPanel && !scnCLS.instance.importLevelPanel.gameObject.activeSelf)
		{
			ADOBase.playerManager.SetAllPlayerResponsive(responsive: true);
		}
	}

	private void ChangeOption(int direction)
	{
		currentOption.SetState(_highlighted: false, currentOption.selected);
		List<Option> list = (showingLeftPanel ? leftPanelOptions : rightPanelOptions);
		currentOptionIndex = (int)Mathf.Repeat(currentOptionIndex + direction, list.Count);
		currentOption.SetState(_highlighted: true, currentOption.selected);
	}

	private void SelectOption(OptionName name, bool leftOptions)
	{
		bool num = (leftOptions ? showingLeftPanel : showingRightPanel);
		List<Option> list = (leftOptions ? leftPanelOptions : rightPanelOptions);
		if (num)
		{
			currentOption.SetState(_highlighted: false, currentOption.selected);
			currentOptionIndex = list.FindIndex((Option option) => option.name == name);
			bool selected = !currentOption.canBeUnselected || !currentOption.selected;
			currentOption.SetState(_highlighted: true, selected);
			return;
		}
		foreach (Option item in list)
		{
			if (item.name == name)
			{
				bool selected2 = !item.canBeUnselected || !item.selected;
				item.SetState(_highlighted: true, selected2);
			}
		}
	}

	public void UpdateOrderText()
	{
		string text = RDString.Get($"cls.orderBy.{ADOBase.cls.effectiveSortingMethod}");
		string text2 = RDString.Get("cls.shortcut.order") + " <color=lightblue><i>[" + text + "]</i></color>";
		currentOrderText.text = text2;
	}

	private void ToggleNoFail()
	{
		GCS.useNoFail = !GCS.useNoFail;
		scrFlash.Flash(GCS.useNoFail ? Color.green.WithAlpha(0.3f) : Color.gray.WithAlpha(0.7f), 0.5f);
		scrSfx.instance.PlaySfx(GCS.useNoFail ? SfxSound.ModifierActivate : SfxSound.ModifierDeactivate, MixerGroup.InterfaceParent);
	}

	private void ToggleSpeedTrial()
	{
		GCS.speedTrialMode = !GCS.speedTrialMode;
		scrFlash.Flash(Color.white, 0.5f);
		scnCLS.instance.ReloadDisplay();
	}

	private void ToggleUnlockKeyLimiter()
	{
		GCS.useUnlockKeyLimiter = !GCS.useUnlockKeyLimiter;
		scrFlash.Flash(GCS.useUnlockKeyLimiter ? Color.red.WithAlpha(0.3f) : Color.gray.WithAlpha(0.7f), 0.5f);
		scrSfx.instance.PlaySfx(GCS.useUnlockKeyLimiter ? SfxSound.ModifierActivate : SfxSound.ModifierDeactivate, MixerGroup.InterfaceParent);
	}

	public IEnumerator ToggleSearchMode(bool search)
	{
		searchMode = search;
		if (search && RDC.runningOnSteamDeck)
		{
			while (!SteamWorkshop.ShowTextInput())
			{
				yield return null;
			}
		}
		if (search)
		{
			searchInputField.ActivateInputField();
			yield break;
		}
		if (!eventSystem.alreadySelecting)
		{
			eventSystem.SetSelectedGameObject(null);
		}
		toggleSearchModeCoroutine = null;
	}
}
