using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PauseLevelButton : GeneralPauseButton
{
	public RectTransform linkMask;

	public Image link;

	public Image background;

	public TMP_Text label;

	public TMP_Text restartLabel;

	public Image icon;

	public Button button;

	public CanvasGroup canvasGroup;

	[Header("CLS")]
	public RawImage clsIcon;

	public scrBlur blur;

	[NonSerialized]
	public string levelName;

	[NonSerialized]
	public bool useIconColorForLabel;

	[NonSerialized]
	public bool useSpriteForFill;

	public override void Select()
	{
	}

	public override void SetFocus(bool focus)
	{
		if (background != null && !useSpriteForFill)
		{
			background.color = (focus ? base.pauseMenu.selectedFillColor : base.pauseMenu.unselectedFillColor);
		}
		if (background != null && useSpriteForFill)
		{
			background.sprite = (focus ? base.pauseMenu.clsSelectedFillSprite : base.pauseMenu.clsUnselectedFillSprite);
		}
		if (icon != null)
		{
			icon.color = (focus ? base.pauseMenu.selectedIconColor : base.pauseMenu.unselectedIconColor);
		}
		if (clsIcon != null)
		{
			clsIcon.color = (focus ? base.pauseMenu.selectedIconColor : base.pauseMenu.unselectedIconColor);
		}
		if (label != null)
		{
			label.color = ((!focus) ? (useIconColorForLabel ? base.pauseMenu.unselectedIconColor : base.pauseMenu.unselectedLabelColor) : (useIconColorForLabel ? base.pauseMenu.selectedIconColor : base.pauseMenu.selectedLabelColor));
		}
		if (restartLabel != null)
		{
			restartLabel.gameObject.SetActive(focus);
		}
	}
}
