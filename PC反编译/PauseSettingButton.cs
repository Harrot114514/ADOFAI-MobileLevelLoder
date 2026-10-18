using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PauseSettingButton : GeneralPauseButton
{
	[Header("Components")]
	public Image rectangle;

	public Image fill;

	public Text label;

	public Text valueLabel;

	public Button rightArrow;

	public Button leftArrow;

	public RectTransform buttonContainer;

	public Text confirmText;

	[Header("Info")]
	public string type;

	public bool hasDescription;

	public string descriptionKey;

	public object initialValue;

	public bool restartOnChange;

	public int minInt;

	public int maxInt;

	public int changeBy;

	public int multiplyBy;

	public int changeBySmall;

	public string unit;

	public bool hasRange;

	public bool deferUpdate;

	public object CachedValue;

	private Vector2 buttonImageStartSize;

	private SettingsMenu settingsMenu => base.pauseMenu.settingsMenu;

	private void Awake()
	{
		rectangleRT = rectangle.GetComponent<RectTransform>();
		buttonImageStartSize = rightArrow.GetComponentInChildren<RectTransform>().sizeDelta;
		leftArrow.onClick.AddListener(delegate
		{
			settingsMenu.UpdateSelectedSetting(SettingsMenu.Interaction.Decrement);
		});
		rightArrow.onClick.AddListener(delegate
		{
			settingsMenu.UpdateSelectedSetting(SettingsMenu.Interaction.Increment);
		});
		confirmText.SetLocalizedFont();
		confirmText.text = RDString.Get("pauseMenu.confirmInstruction").Replace("[[button]]", "\ue0ab");
		confirmText.gameObject.SetActive(value: false);
	}

	public string GetDescriptionText()
	{
		string text = RDString.Get(descriptionKey);
		if (base.name == "keyLimiter")
		{
			text += "\n";
			int count = Persistence.keyLimiterKeys.Count;
			Dictionary<string, object> parameters = new Dictionary<string, object>
			{
				{ "maximumAllowedKeys", 1000 },
				{ "allowedKeysResetTime", 500f }
			};
			if (count <= 1000)
			{
				if (count == 0)
				{
				}
			}
			else
			{
				text += RDString.Get("pauseMenu.settings.info.keyLimiter.overAllowedKeys", parameters);
			}
		}
		if (ADOBase.isSwitch)
		{
			text = text.Replace("[[button]]", "\ue0ab");
		}
		return text;
	}

	public override void SetFocus(bool focus)
	{
		if (!focus && deferUpdate && CachedValue != null)
		{
			CachedValue = null;
			settingsMenu.UpdateSetting(this, SettingsMenu.Interaction.Refresh);
		}
		label.color = (focus ? base.pauseMenu.selectedLabelColor : base.pauseMenu.unselectedLabelColor);
		valueLabel.color = label.color;
		rectangle.color = (focus ? base.pauseMenu.selectedLabelColor : base.pauseMenu.unselectedBorderColor);
		fill.color = Color.clear;
		rightArrow.gameObject.SetActive(focus);
		leftArrow.gameObject.SetActive(focus);
		if (focus)
		{
			settingsMenu.SetDescription(hasDescription ? GetDescriptionText() : "");
			buttonContainer.DOKill(complete: true);
			buttonContainer.DOScale(1.02f, base.pauseMenu.animationTime).SetEase(base.pauseMenu.animationEase).SetUpdate(isIndependentUpdate: true)
				.OnComplete(delegate
				{
					buttonContainer.DOScale(1f, base.pauseMenu.animationTime).SetEase(base.pauseMenu.animationEase).SetUpdate(isIndependentUpdate: true);
				});
		}
	}

	public void PlayArrowAnimation(bool isRight)
	{
		RectTransform componentInChildren = (isRight ? rightArrow : leftArrow).GetComponentInChildren<RectTransform>();
		componentInChildren.DOComplete();
		componentInChildren.DOAnchorPosX(componentInChildren.anchoredPosition.x + base.pauseMenu.animationDistance * 0.25f * (float)(isRight ? 1 : (-1)), base.pauseMenu.animationTime).SetEase(base.pauseMenu.animationEase).SetUpdate(isIndependentUpdate: true)
			.SetLoops(2, LoopType.Yoyo);
	}

	public override void Select()
	{
		if (!settingsMenu.editingKeys)
		{
			settingsMenu.isSelectingTab = false;
			settingsMenu.Select(this);
		}
	}
}
