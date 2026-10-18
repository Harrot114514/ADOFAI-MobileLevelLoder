using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PauseButton : GeneralPauseButton
{
	public PauseMenu.ButtonType buttonType;

	public Button button;

	public Image rectangle;

	public Image icon;

	public Image shadow;

	public TMP_Text label;

	public string rdString;

	public PauseMenuChain customChain;

	public Image controllerButtonIcon;

	private bool iconColorSet;

	private Tween fillTween;

	private Tween labelTween;

	private Tween iconTween;

	private Color selectedIconColor;

	private Color unselectedIconColor;

	private RectTransform shadowRT;

	private const Ease SelectAnimationEase = Ease.OutExpo;

	private const float SelectAnimationDuration = 0.6f;

	private const float SelectAnimationDelay = 0.05f;

	public void Awake()
	{
		rectangleRT = rectangle.GetComponent<RectTransform>();
		shadowRT = shadow.GetComponent<RectTransform>();
		controllerButtonIcon.enabled = false;
	}

	public override void SetFocus(bool focus)
	{
		if (fillTween != null)
		{
			fillTween.Kill();
			labelTween.Kill();
			iconTween.Kill();
		}
		label.color = (focus ? base.pauseMenu.selectedLabelColor : base.pauseMenu.unselectedLabelColor);
		if ((bool)icon)
		{
			icon.color = ((!focus) ? (iconColorSet ? unselectedIconColor : base.pauseMenu.unselectedIconColor) : (iconColorSet ? selectedIconColor : base.pauseMenu.selectedIconColor));
		}
		rectangle.color = (focus ? base.pauseMenu.selectedFillColor : base.pauseMenu.unselectedFillColor);
		controllerButtonIcon.enabled = focus;
	}

	public override void Select()
	{
		if (base.pauseMenu.enabled)
		{
			base.pauseMenu.Select(index, 0);
			base.pauseMenu.Choose();
		}
	}

	public void ShowAsSelected()
	{
		if (fillTween != null)
		{
			fillTween.Kill();
			labelTween.Kill();
			iconTween.Kill();
		}
		label.color = Color.white;
		iconTween = FlashIcon(icon);
		fillTween = rectangle.DOColor(base.pauseMenu.selectedFillColor, 0.6f).SetEase(Ease.OutExpo).SetUpdate(isIndependentUpdate: true)
			.SetDelay(0.05f);
		labelTween = label.DOColor(base.pauseMenu.selectedLabelColor, 0.6f).SetEase(Ease.OutExpo).SetUpdate(isIndependentUpdate: true)
			.SetDelay(0.05f);
	}

	public Tween FlashIcon(Image icon)
	{
		icon.DOKill();
		icon.color = Color.Lerp(base.pauseMenu.selectedIconColor, Color.white, 0.65f);
		return icon.DOColor(base.pauseMenu.selectedIconColor, 0.6f).SetEase(Ease.OutExpo).SetUpdate(isIndependentUpdate: true)
			.SetDelay(0.05f);
	}

	public void SetIconColors(Color selectedColor, Color unselectedColor)
	{
		iconColorSet = true;
		selectedIconColor = selectedColor;
		unselectedIconColor = unselectedColor;
	}

	public void DisableColorSet()
	{
		iconColorSet = false;
	}

	public void LateUpdate()
	{
		PauseMenuChain obj = ((customChain != null) ? customChain : base.pauseMenu.pauseMenuChain);
		float x = base.rectTransform.anchoredPosition.x;
		float num = obj.WaveFunction(x);
		float num2 = obj.WaveFunction(x + 0.1f);
		base.rectTransform.AnchorPosY(num);
		float num3 = Mathf.Atan2(num2 - num, 0.1f) * 57.29578f;
		RectTransform obj2 = shadowRT;
		Vector3 localEulerAngles = (rectangleRT.localEulerAngles = Vector3.forward * num3 * 0.4f);
		obj2.localEulerAngles = localEulerAngles;
	}
}
