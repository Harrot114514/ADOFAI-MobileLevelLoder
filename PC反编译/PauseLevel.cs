using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ADOFAI;
using DG.Tweening;
using GDMiniJSON;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class PauseLevel : ADOBase
{
	[Header("References")]
	public PauseMenuChain pauseMenuChain;

	public RectTransform touchTransform;

	public RectTransform levelsParent;

	public RectTransform levelContainer;

	public RectTransform levelPosition;

	public GameObject pauseLevelButtonPrefab;

	[Header("Variables")]
	public Sprite[] lanterns;

	public Color[] lanternsLightsColors;

	[NonSerialized]
	public PauseLevelButton speedTrialButton;

	[NonSerialized]
	public RectTransform leftArrowTransform;

	[NonSerialized]
	public RectTransform rightArrowTransform;

	[NonSerialized]
	public static float defaultButtonWidth = 19f;

	[NonSerialized]
	public const float taroScale = 1.3f;

	[NonSerialized]
	public const float techScale = 1.1f;

	private List<PauseLevelButton> levels = new List<PauseLevelButton>();

	private List<CanvasGroup> lanternsAlpha = new List<CanvasGroup>();

	private bool inited;

	private float bossScale = 1.2f;

	private float scale = 1f;

	private float difference = 25f;

	private PauseLevelButton levelSelected;

	private int currentLevelIndex = -1;

	private bool expanded;

	private bool isTaro;

	private bool isTech;

	private Sequence moveToIndex;

	private Vector2 touchStartPos;

	private Vector2 origPos;

	private const float targetTouchRefreshRate = 60f;

	private bool dragging;

	private bool dragCanBegin;

	private bool wasPaused;

	private const float dragMinDistance = 5f;

	public PauseLevelButton currentLevelSelect => levelSelected;

	public bool isExpanded => expanded;

	public bool levelIsTaro => isTaro;

	public bool levelIsTech => isTech;

	public int levelsNumber => levels.Count;

	private PauseMenu pauseMenu => scrController.instance.pauseMenu;

	private bool speedTrial => GCS.speedTrialMode;

	private void Awake()
	{
		Sprite[] array = null;
		if (ADOBase.IsHalloweenWeek())
		{
			array = RDC.data.halloweenLanternSprites;
		}
		else if (ADOBase.IsCNY())
		{
			array = RDC.data.CNYLanternSprites;
		}
		if (array != null)
		{
			lanterns = array;
		}
	}

	public static Sprite GetOrLoadSpriteVariant(string spriteName, string variantKey)
	{
		if (variantKey != "")
		{
			spriteName = spriteName + "_" + variantKey;
		}
		return Resources.Load<Sprite>(spriteName);
	}

	public void Init()
	{
		bool flag = (bool)ADOBase.controller.currFloor && (ADOBase.controller.currFloor.freeroam || ADOBase.controller.currFloor.freeroamGenerated);
		if (!(ADOBase.controller.gameworld || flag) || GCS.practiceMode)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		base.gameObject.SetActive(value: true);
		if (inited)
		{
			MoveToCurrentIndex(instant: true);
			return;
		}
		inited = true;
		if (!ADOBase.editor)
		{
			if (ADOBase.isScnGame && !ADOBase.isOfficialLevel)
			{
				CustomLevels();
			}
			else
			{
				Levels();
			}
		}
		pauseMenu.vignetteMaterial.SetVector("_ScreenResolution", new Vector2(Camera.main.pixelWidth, Camera.main.pixelHeight));
	}

	private void InstantiantePauseLevels(int levelsCount, int levelIndex, int levelProgress, bool bossReached, bool isWorldComplete, bool isWorldPerfect, bool isWorldSpeedTrial, bool isCLS, string world = null, bool isCrown = false)
	{
		if (RDC.forceUnlockAllLevels)
		{
			bossReached = true;
		}
		int num = (bossReached ? levelsCount : (levelProgress + 1));
		float num2 = defaultButtonWidth;
		if (num >= 11)
		{
			num2 *= 0.88f;
			scale = 0.8f;
		}
		else if (num >= 10)
		{
			scale *= 0.88f;
		}
		string variantKey = (isTech ? "tech" : (isCrown ? "crown" : (isTaro ? "taro" : "")));
		Sprite orLoadSpriteVariant = GetOrLoadSpriteVariant("submenu_blank", variantKey);
		if (speedTrial)
		{
			PauseLevelButton component = UnityEngine.Object.Instantiate(pauseLevelButtonPrefab, levelsParent).GetComponent<PauseLevelButton>();
			component.rectTransform.anchoredPosition = new Vector2(0f * difference, 0f);
			component.link.material = pauseMenu.vignetteMaterial;
			component.background.material = pauseMenu.vignetteMaterial;
			component.icon.material = pauseMenu.vignetteMaterial;
			component.clsIcon.material = pauseMenu.vignetteMaterial;
			component.background.sprite = orLoadSpriteVariant;
			component.icon.sprite = Resources.Load<Sprite>("new/icon-previous");
			component.button.onClick.AddListener(delegate
			{
				pauseMenu.SelectVerticalFixed(1, -1);
				pauseMenu.ChangeLevel(next: false);
			});
			leftArrowTransform = component.icon.rectTransform;
			PauseLevelButton component2 = UnityEngine.Object.Instantiate(pauseLevelButtonPrefab, levelsParent).GetComponent<PauseLevelButton>();
			component2.link.material = pauseMenu.vignetteMaterial;
			component2.background.material = pauseMenu.vignetteMaterial;
			component2.icon.material = pauseMenu.vignetteMaterial;
			component2.clsIcon.material = pauseMenu.vignetteMaterial;
			component2.icon.gameObject.SetActive(value: false);
			component2.rectTransform.anchoredPosition = new Vector2(1f * difference, 0f);
			component2.background.rectTransform.localScale = Vector2.one * bossScale;
			component2.restartLabel.text = RDString.Get("pauseMenu.restart");
			component2.restartLabel.SetLocalizedFont();
			if (world != null)
			{
				component2.background.sprite = Resources.Load<Sprite>("boss" + world);
			}
			if (isCrown)
			{
				component2.background.DORainbow(10f, 0.5f, 1f, Ease.Linear).SetUpdate(isIndependentUpdate: true);
			}
			component2.button.onClick.AddListener(delegate
			{
				pauseMenu.SelectVerticalFixed(1, 0);
			});
			speedTrialButton = component2;
			pauseMenuChain.transform.SetParent(component2.rectTransform, worldPositionStays: false);
			pauseMenuChain.transform.SetAsFirstSibling();
			InstantiateChain(isWorldComplete, isWorldPerfect, isWorldSpeedTrial);
			PauseLevelButton component3 = UnityEngine.Object.Instantiate(pauseLevelButtonPrefab, levelsParent).GetComponent<PauseLevelButton>();
			component3.rectTransform.anchoredPosition = new Vector2(2f * difference, 0f);
			component3.link.material = pauseMenu.vignetteMaterial;
			component3.background.material = pauseMenu.vignetteMaterial;
			component3.icon.material = pauseMenu.vignetteMaterial;
			component3.clsIcon.material = pauseMenu.vignetteMaterial;
			component3.linkMask.gameObject.SetActive(value: false);
			component3.icon.sprite = Resources.Load<Sprite>("new/icon-next");
			component3.background.sprite = orLoadSpriteVariant;
			component3.button.onClick.AddListener(delegate
			{
				pauseMenu.SelectVerticalFixed(1, 1);
				pauseMenu.ChangeLevel(next: true);
			});
			rightArrowTransform = component3.icon.GetComponent<RectTransform>();
			if (isTaro)
			{
				component.icon.rectTransform.anchoredPosition = new Vector3(-0.7f, 0f);
				component3.icon.rectTransform.anchoredPosition = new Vector3(0.7f, 0f);
				Image icon = component.icon;
				Color color = (component3.icon.color = "D6D6D6".HexToColor());
				icon.color = color;
				RectTransform rectTransform = component.background.rectTransform;
				RectTransform rectTransform2 = component3.background.rectTransform;
				Vector2 vector = (component2.background.rectTransform.sizeDelta = num2 * 1.3f * Vector2.one);
				Vector2 sizeDelta = (rectTransform2.sizeDelta = vector);
				rectTransform.sizeDelta = sizeDelta;
			}
			levelsParent.anchoredPosition = new Vector2(1f * (0f - difference), 0f);
			return;
		}
		if (ADOBase.controller.isPuzzleRoom)
		{
			PauseLevelButton level = UnityEngine.Object.Instantiate(pauseLevelButtonPrefab, levelsParent).GetComponent<PauseLevelButton>();
			string text = ((levelIndex == levelsCount - 1) ? "X" : (levelIndex + 1).ToString());
			level.useIconColorForLabel = !isTaro;
			levels.Add(level);
			level.rectTransform.anchoredPosition = new Vector2(0f, 0f);
			level.link.material = pauseMenu.vignetteMaterial;
			level.background.material = pauseMenu.vignetteMaterial;
			level.icon.material = pauseMenu.vignetteMaterial;
			level.clsIcon.material = pauseMenu.vignetteMaterial;
			level.label.text = text;
			level.label.SetLocalizedFont();
			level.background.sprite = orLoadSpriteVariant;
			level.label.transform.localScale = Vector2.one * bossScale;
			level.background.transform.localScale = Vector2.one * bossScale;
			level.background.GetComponent<RectTransform>().sizeDelta = level.rectTransform.sizeDelta.WithX(num2);
			level.restartLabel.text = RDString.Get("pauseMenu.restart");
			level.restartLabel.SetLocalizedFont();
			level.icon.gameObject.SetActive(value: false);
			level.link.gameObject.SetActive(value: false);
			level.label.gameObject.SetActive(value: true);
			if (isTaro)
			{
				level.label.color = "D6D6D6".HexToColor();
				level.background.rectTransform.sizeDelta = num2 * 1.3f * Vector2.one;
			}
			level.levelName = world + "-" + text;
			level.button.onClick.AddListener(delegate
			{
				pauseMenu.UpdateLevelDescriptionAndReload(level.levelName, dragging, dragging);
			});
			currentLevelIndex = 0;
			levelSelected = levels[currentLevelIndex];
			MoveToCurrentIndex(instant: true);
			Switch(null, show: true);
			return;
		}
		for (int num3 = 0; num3 < levelsCount; num3++)
		{
			PauseLevelButton level2 = UnityEngine.Object.Instantiate(pauseLevelButtonPrefab, levelsParent).GetComponent<PauseLevelButton>();
			level2.useIconColorForLabel = !isTaro;
			levels.Add(level2);
			level2.rectTransform.anchoredPosition = new Vector2((float)num3 * difference, 0f);
			level2.link.material = pauseMenu.vignetteMaterial;
			level2.background.material = pauseMenu.vignetteMaterial;
			level2.icon.material = pauseMenu.vignetteMaterial;
			level2.clsIcon.material = pauseMenu.vignetteMaterial;
			level2.icon.gameObject.SetActive(value: false);
			bool flag = num3 == levelsCount - 1;
			string text2;
			if (flag)
			{
				text2 = "X";
				if (world != null)
				{
					level2.background.sprite = Resources.Load<Sprite>("boss" + world);
				}
				if (isCrown)
				{
					level2.background.DORainbow(10f, 0.5f, 1f, Ease.Linear).SetUpdate(isIndependentUpdate: true);
				}
				level2.link.gameObject.SetActive(value: false);
				level2.linkMask.sizeDelta = new Vector2(70f, 50f);
				RectTransform component4 = pauseMenuChain.GetComponent<RectTransform>();
				component4.anchorMin = new Vector2(0f, 0.5f);
				component4.anchorMax = new Vector2(0f, 0.5f);
				component4.AnchorPosX(0f);
				pauseMenuChain.transform.SetParent(level2.linkMask, worldPositionStays: false);
				pauseMenuChain.transform.SetAsFirstSibling();
			}
			else
			{
				text2 = (num3 + 1).ToString();
				level2.label.gameObject.SetActive(value: true);
				level2.label.text = text2;
				level2.label.SetLocalizedFont();
				level2.background.sprite = orLoadSpriteVariant;
				if (isCrown)
				{
					level2.label.color = "333333".HexToColor();
				}
				if (isTaro)
				{
					level2.label.color = "D6D6D6".HexToColor();
				}
			}
			bool flag2 = levelIndex == num3;
			if (flag2)
			{
				level2.restartLabel.text = RDString.Get("pauseMenu.restart");
				level2.restartLabel.SetLocalizedFont();
			}
			level2.label.transform.localScale = Vector2.one * (flag2 ? bossScale : 1f);
			level2.background.transform.localScale = Vector2.one * (flag2 ? bossScale : 1f);
			level2.background.GetComponent<RectTransform>().sizeDelta = level2.rectTransform.sizeDelta.WithX(num2);
			int num4 = num3;
			level2.levelName = (isCLS ? $"{num4}" : (world + "-" + text2));
			level2.button.onClick.AddListener(delegate
			{
				pauseMenu.UpdateLevelDescriptionAndReload(level2.levelName, dragging, dragging);
			});
			float num5 = ((isTech && flag) ? 1.1f : (isTaro ? 1.3f : 1f));
			level2.background.rectTransform.sizeDelta = num2 * num5 * Vector2.one;
		}
		if (levels != null && levelIndex < levels.Count)
		{
			levelSelected = levels[levelIndex];
		}
		currentLevelIndex = levelIndex;
		MoveToCurrentIndex(instant: true);
		InstantiateChain(isWorldComplete, isWorldPerfect, isWorldSpeedTrial);
		Switch(null, !isTaro || !ADOBase.isBossLevel);
	}

	private void Levels()
	{
		string text = ADOBase.currentLevel;
		if (text == "scnMinesweeper")
		{
			InstantiantePauseLevels(1, 0, 0, bossReached: true, isWorldComplete: false, isWorldPerfect: false, isWorldSpeedTrial: false, isCLS: false, ADOBase.currentLevel);
			return;
		}
		bool coopMode = scrController.coopMode;
		string[] array = text.Split('-');
		string text2 = array[0];
		int index = ADOBase.worldData[text2].index;
		int levelCount = ADOBase.worldData[text2].levelCount;
		int levelIndex = ((array[1] == "X") ? levelCount : int.Parse(array[1])) - 1;
		int levelTutorialProgress = Persistence.GetLevelTutorialProgress(ADOBase.worldData[text2].index);
		bool bossReached = Persistence.GetWorldAttempts(index, coop: false) > 0 || Persistence.GetWorldAttempts(index, coop: true) > 0 || levelTutorialProgress >= levelCount - 1;
		bool isCrown = text2.IsCrownWorld();
		isTech = text2.IsTechWorld();
		isTaro = text2.IsTaro();
		isTech = text2.IsTechWorld();
		if (RDC.forceUnlockAllLevels)
		{
			RDC.forceUnlockAllLevels = false;
		}
		bool flag = Persistence.IsWorldComplete(index);
		bool isWorldPerfect = Persistence.IsWorldPerfect(index, coopMode);
		bool isWorldSpeedTrial = Persistence.IsSpeedTrialComplete(index, coopMode) && flag;
		RDC.forceUnlockAllLevels = Persistence.unlockAllLevels;
		InstantiantePauseLevels(levelCount, levelIndex, levelTutorialProgress, bossReached, flag, isWorldPerfect, isWorldSpeedTrial, isCLS: false, text2, isCrown);
	}

	private void CustomLevels()
	{
		string text = GCS.customLevelPaths[^1];
		string json;
		if (ADOBase.isBundleLevel)
		{
			AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(text);
			json = handle.WaitForCompletion().text;
			Addressables.Release(handle);
		}
		else
		{
			json = RDFile.ReadAllText(text);
		}
		Dictionary<string, object> rootDict = Json.DeserializePartially(json, "actions") as Dictionary<string, object>;
		LevelDataCLS levelDataCLS = new LevelDataCLS();
		levelDataCLS.Decode(rootDict);
		string iconPath = Path.Combine(Path.GetDirectoryName(text), levelDataCLS.previewIcon);
		int num = GCS.customLevelPaths.Length;
		int customLevelIndex = GCS.customLevelIndex;
		string hash = levelDataCLS.Hash;
		int customWorldPlayIndex = Persistence.GetCustomWorldPlayIndex(hash);
		bool bossReached = Persistence.GetCustomWorldAttempts(hash) > 0 || customWorldPlayIndex >= num - 1;
		bool isWorldComplete = Persistence.GetCustomWorldCompletion(hash) * 1f >= 1f;
		bool customWorldIsHighestPossibleAcc = Persistence.GetCustomWorldIsHighestPossibleAcc(hash);
		bool isWorldSpeedTrial = Persistence.GetCustomWorldSpeedTrial(hash) > 1f;
		InstantiantePauseLevels(num, customLevelIndex, customWorldPlayIndex, bossReached, isWorldComplete, customWorldIsHighestPossibleAcc, isWorldSpeedTrial, isCLS: true);
		PauseLevelButton levelButton;
		if (!speedTrial)
		{
			List<PauseLevelButton> list = levels;
			levelButton = list[list.Count - 1];
		}
		else
		{
			levelButton = speedTrialButton;
		}
		StartCoroutine(LoadIconCLS(iconPath, levelButton, levelDataCLS.previewIconColor, pauseMenu.unselectedIconColor));
	}

	private static void ShrinkImage(Texture2D tex2D, int maxSideSize)
	{
		if (tex2D.width > maxSideSize || tex2D.height > maxSideSize)
		{
			int num = Mathf.Max(tex2D.width, tex2D.height);
			float num2 = (float)maxSideSize * 1f / (float)num;
			new TextureScale().Bilinear(tex2D, Mathf.RoundToInt((float)tex2D.width * num2), Mathf.RoundToInt((float)tex2D.height * num2));
		}
	}

	private static void ProcessIcon(scrBlur blur, Texture2D icon, Color iconColor)
	{
		blur.baseTint = Color.white;
		blur.blurTint = Color.black;
		blur.UpdateTexture();
	}

	private static Color ProcessBackgroundColor(Color iconColor)
	{
		Color.RGBToHSV(iconColor, out var H, out var S, out var V);
		return Color.HSVToRGB(H, Math.Min(S, 0.8f), Math.Max(V, 0.6f));
	}

	public static IEnumerator LoadIconCLS(string iconPath, PauseLevelButton levelButton, Color iconColor, Color unselectedIconColor)
	{
		Texture2D texture2D;
		if (ADOBase.isBundleLevel)
		{
			AsyncOperationHandle<Texture2D> handle = Addressables.LoadAssetAsync<Texture2D>(iconPath.ToFeaturedDLCPath());
			texture2D = handle.WaitForCompletion();
			Addressables.Release(handle);
		}
		else
		{
			string uri = iconPath.ToFileUri();
			using UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(uri);
			yield return imageRequest.SendWebRequest();
			if (imageRequest.result == UnityWebRequest.Result.ConnectionError || imageRequest.result == UnityWebRequest.Result.ProtocolError)
			{
				yield break;
			}
			texture2D = ((DownloadHandlerTexture)imageRequest.downloadHandler).texture;
			ShrinkImage(texture2D, 128);
		}
		levelButton.useSpriteForFill = true;
		levelButton.background.color = ProcessBackgroundColor(iconColor);
		levelButton.clsIcon.texture = texture2D;
		levelButton.clsIcon.color = unselectedIconColor;
		levelButton.clsIcon.gameObject.SetActive(value: true);
		ProcessIcon(levelButton.blur, texture2D, iconColor);
	}

	private void InstantiateChain(bool isWorldComplete, bool isWorldPerfect, bool isWorldSpeedTrial)
	{
		if (!isWorldComplete)
		{
			return;
		}
		pauseMenuChain.gameObject.SetActive(value: true);
		pauseMenuChain.InitLinks();
		if (speedTrial)
		{
			pauseMenuChain.GetComponent<RectTransform>().anchoredPosition = (isTaro ? new Vector2(25f, 6f) : new Vector2(25f, 6f));
			pauseMenuChain.transform.eulerAngles = new Vector3(0f, 0f, 30f);
			foreach (PauseMenuChainLink link in pauseMenuChain.links)
			{
				link.image.rectTransform.localEulerAngles = new Vector3(0f, 0f, 30f);
			}
		}
		else if (isTaro)
		{
			pauseMenuChain.GetComponent<RectTransform>().anchoredPosition = new Vector2(-17f, 0f);
		}
		int linkCount = pauseMenuChain.linkCount;
		for (int i = 0; i < linkCount; i++)
		{
			PauseMenuChainLink pauseMenuChainLink = pauseMenuChain.links[i];
			pauseMenuChainLink.image.material = pauseMenu.vignetteMaterial;
			pauseMenuChainLink.lantern.material = pauseMenu.vignetteMaterial;
			if (i == linkCount - 1 && isWorldComplete)
			{
				lanternsAlpha.Add(pauseMenuChainLink.canvasGroup);
				pauseMenuChainLink.lanternLight.color = lanternsLightsColors[0];
				pauseMenuChainLink.lantern.sprite = lanterns[0];
				pauseMenuChainLink.lantern.gameObject.SetActive(value: true);
				RectTransform rectTransform = pauseMenuChainLink.lantern.rectTransform;
				Vector2 anchorMin = (pauseMenuChainLink.lantern.rectTransform.anchorMax = new Vector2(1f, 0.5f));
				rectTransform.anchorMin = anchorMin;
				pauseMenuChainLink.lantern.rectTransform.anchoredPosition = new Vector2(18f, -1.5f);
				pauseMenuChainLink.lantern.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
			}
			else if (i == linkCount - 2 && isWorldPerfect)
			{
				lanternsAlpha.Add(pauseMenuChainLink.canvasGroup);
				pauseMenuChainLink.lanternLight.color = lanternsLightsColors[1];
				pauseMenuChainLink.lantern.sprite = lanterns[1];
				pauseMenuChainLink.lantern.gameObject.SetActive(value: true);
				RectTransform rectTransform2 = pauseMenuChainLink.lantern.rectTransform;
				Vector2 anchorMin = (pauseMenuChainLink.lantern.rectTransform.anchorMax = new Vector2(0.5f, 1f));
				rectTransform2.anchorMin = anchorMin;
				pauseMenuChainLink.lantern.rectTransform.anchoredPosition = new Vector2(2f, 12f);
				pauseMenuChainLink.lantern.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
			}
			else if (i == linkCount - 3 && isWorldSpeedTrial)
			{
				lanternsAlpha.Add(pauseMenuChainLink.canvasGroup);
				pauseMenuChainLink.lanternLight.color = lanternsLightsColors[2];
				pauseMenuChainLink.lantern.sprite = lanterns[2];
				pauseMenuChainLink.lantern.gameObject.SetActive(value: true);
				RectTransform rectTransform3 = pauseMenuChainLink.lantern.rectTransform;
				Vector2 anchorMin = (pauseMenuChainLink.lantern.rectTransform.anchorMax = new Vector2(0f, 1f));
				rectTransform3.anchorMin = anchorMin;
				pauseMenuChainLink.lantern.rectTransform.anchoredPosition = new Vector2(-6.5f, 10f);
				pauseMenuChainLink.lantern.rectTransform.localEulerAngles = new Vector3(0f, 0f, 210f);
			}
			else
			{
				pauseMenuChainLink.lantern.gameObject.SetActive(value: false);
			}
		}
	}

	private void Switch(Sequence sequence, bool show, bool instant = false)
	{
		expanded = show;
		if (sequence == null)
		{
			instant = true;
			sequence = DOTween.Sequence();
			sequence.SetUpdate(isIndependentUpdate: true);
		}
		if (ADOBase.currentLevel.Split('-')[0].IsTaro() && ADOBase.isBossLevel && !speedTrial)
		{
			if (!expanded)
			{
				sequence.Insert(0f, currentLevelSelect.background.DOColor(pauseMenu.selectedFillColor, (currentLevelSelect == levels[currentLevelIndex] || instant) ? 0f : 0.5f));
			}
			pauseMenuChain.gameObject.SetActive(show);
			for (int i = 0; i < levels.Count - 1; i++)
			{
				sequence.Insert(0f, levels[i].canvasGroup.DOFade(show ? 1 : 0, instant ? 0f : 1f));
			}
			sequence.Insert(0f, levelContainer.DOAnchorMin(new Vector2(show ? 0.5f : 0f, 0.5f), instant ? 0f : 0.5f));
			sequence.Insert(0f, levelContainer.DOAnchorMax(new Vector2(show ? 0.5f : 0f, 0.5f), instant ? 0f : 0.5f));
			sequence.Insert(0f, levelContainer.DOAnchorPos(new Vector2(show ? 0f : (-20f), 0f), instant ? 0f : 0.5f));
		}
	}

	public void Hide(Sequence sequence, bool instant = false)
	{
		Switch(sequence, show: false, instant);
	}

	public void Show(Sequence sequence, bool instant = false)
	{
		Switch(sequence, show: true, instant);
	}

	public void MoveToSpecificLevel(string currentLevel)
	{
		string[] array = currentLevel.Split('-');
		string text = array[0];
		int index = ((!(array[1] == "X")) ? int.Parse(array[1]) : ((text == "scnMinesweeper") ? 1 : ADOBase.worldData[text].levelCount)) - 1;
		MoveToSpecificIndex(index);
	}

	public void MoveToSpecificIndex(int index, bool instant = false)
	{
		if (inited && !(levelSelected == null) && levels != null && index >= 0 && index < levels.Count)
		{
			if (moveToIndex != null && moveToIndex.active)
			{
				moveToIndex.Complete();
			}
			moveToIndex = DOTween.Sequence();
			moveToIndex.SetUpdate(isIndependentUpdate: true);
			UpdateAlpha(index, instant);
			moveToIndex.Insert(0f, levelSelected.rectTransform.DOScale(Vector2.one, 0f));
			moveToIndex.Insert(0f, levelSelected.background.rectTransform.DOScale(Vector2.one, instant ? 0f : 0.15f).SetEase(Ease.OutBack));
			moveToIndex.Insert(0f, levelSelected.label.rectTransform.DOScale(Vector2.one, instant ? 0f : 0.15f).SetEase(Ease.OutBack));
			levelSelected = levels[index];
			moveToIndex.Insert(0f, levelSelected.background.rectTransform.DOScale(Vector2.one * bossScale, instant ? 0f : 0.15f).SetEase(Ease.OutBack));
			moveToIndex.Insert(0f, levelSelected.label.rectTransform.DOScale(Vector2.one * bossScale, instant ? 0f : 0.15f).SetEase(Ease.OutBack));
			moveToIndex.Insert(0f, levelsParent.DOAnchorPos(new Vector2((float)index * (0f - difference), 0f), instant ? 0f : 0.15f).SetEase(Ease.OutCirc));
		}
	}

	public void MoveToCurrentIndex(bool instant = false)
	{
		if (currentLevelIndex != -1)
		{
			MoveToSpecificIndex(currentLevelIndex, instant);
		}
	}

	private int CalculateSelectedIndex()
	{
		float num = levelsParent.anchoredPosition.x;
		if (num > 0f)
		{
			num = 0f;
		}
		else if (num < (0f - difference) * (float)(levels.Count - 1))
		{
			num = (0f - difference) * (float)(levels.Count - 1);
		}
		return Mathf.RoundToInt(num / (0f - difference));
	}

	private void HighlightSelectedIndex(bool instant = false)
	{
		int num = CalculateSelectedIndex();
		UpdateAlpha(num);
		levelSelected.rectTransform.DOScale(Vector2.one, 0f).SetUpdate(isIndependentUpdate: true);
		levelSelected.background.rectTransform.DOScale(Vector2.one, instant ? 0f : 0.15f).SetEase(Ease.OutBack).SetUpdate(isIndependentUpdate: true);
		levelSelected.label.rectTransform.DOScale(Vector2.one, instant ? 0f : 0.15f).SetEase(Ease.OutBack).SetUpdate(isIndependentUpdate: true);
		levelSelected = levels[num];
		levelSelected.background.rectTransform.DOScale(Vector2.one * bossScale, instant ? 0f : 0.15f).SetEase(Ease.OutBack).SetUpdate(isIndependentUpdate: true);
		levelSelected.label.rectTransform.DOScale(Vector2.one * bossScale, instant ? 0f : 0.15f).SetEase(Ease.OutBack).SetUpdate(isIndependentUpdate: true);
		string levelName = levelSelected.levelName;
		levelsParent.DOAnchorPos(new Vector2((float)num * (0f - difference), 0f), instant ? 0f : 0.15f).SetEase(Ease.OutCirc).OnComplete(delegate
		{
			pauseMenu.SelectPauseLevelButton(instant);
			pauseMenu.UpdateLevelDescriptionAndReload(levelName, ignoreRestart: true, ignoreSelect: true);
		})
			.SetUpdate(isIndependentUpdate: true);
	}

	private void UpdateAlpha(float targetPosition, bool instant = false)
	{
		float duration = (instant ? 0f : 0.15f);
		RectTransform component = GetComponent<RectTransform>();
		for (int i = 0; i < levels.Count; i++)
		{
			PauseLevelButton pauseLevelButton = levels[i];
			ShortcutExtensionsTMPText.DOFade(endValue: 1f - Mathf.InverseLerp(value: Math.Abs(pauseLevelButton.rectTransform.anchoredPosition.x + targetPosition), a: 0f, b: component.rect.size.x / 3.5f), target: pauseLevelButton.label, duration: duration).SetUpdate(isIndependentUpdate: true);
		}
		List<PauseLevelButton> list = levels;
		PauseLevelButton pauseLevelButton2 = list[list.Count - 1];
		for (int j = 0; j < lanternsAlpha.Count; j++)
		{
			CanvasGroup target = lanternsAlpha[j];
			float endValue = 1f - Mathf.InverseLerp(value: Math.Abs(pauseLevelButton2.rectTransform.anchoredPosition.x + 20f + (float)(15 * j) + targetPosition), a: 0f, b: component.rect.size.x / 2f);
			target.DOFade(endValue, duration).SetUpdate(isIndependentUpdate: true);
		}
	}

	private void UpdateAlpha(int targetIndex, bool instant = false)
	{
		float targetPosition = (float)targetIndex * (0f - difference);
		UpdateAlpha(targetPosition);
	}

	private void Update()
	{
		if (levels.Count > 1 && isExpanded)
		{
			UpdateScrollLevels();
		}
	}

	private void UpdateScrollLevels()
	{
		bool num = wasPaused && !ADOBase.controller.paused;
		wasPaused = ADOBase.controller.paused;
		Vector2 vector = Vector2.zero;
		TouchPhase touchPhase = TouchPhase.Stationary;
		bool flag = false;
		if (Input.touchCount >= 1)
		{
			Touch touch = Input.GetTouch(0);
			vector = touch.position;
			touchPhase = touch.phase;
			flag = touchPhase == TouchPhase.Moved;
		}
		else if (Input.mousePresent)
		{
			vector = Input.mousePosition;
			touchPhase = (Input.GetMouseButtonUp(0) ? TouchPhase.Ended : ((!Input.GetMouseButtonDown(0)) ? (Input.GetMouseButton(0) ? TouchPhase.Moved : TouchPhase.Stationary) : TouchPhase.Began));
			flag = touchPhase == TouchPhase.Moved;
		}
		if (num && !flag && dragging)
		{
			touchPhase = TouchPhase.Canceled;
		}
		if (touchPhase == TouchPhase.Began && RectTransformUtility.RectangleContainsScreenPoint(touchTransform, vector))
		{
			OnTouchStart(vector, touchingUI: false);
		}
		else if (touchPhase == TouchPhase.Ended || touchPhase == TouchPhase.Canceled)
		{
			OnTouchEnd(vector);
		}
		else if (flag)
		{
			OnTouchMove(vector);
		}
	}

	private void OnTouchStart(Vector2 pos, bool touchingUI)
	{
		dragCanBegin = !touchingUI;
		if (dragCanBegin)
		{
			touchStartPos = pos;
			origPos = levelsParent.anchoredPosition;
		}
	}

	private void OnTouchMove(Vector2 pos)
	{
		Vector2 vector = new Vector2(pos.x - touchStartPos.x, pos.y - touchStartPos.y);
		Vector2 vector2 = new Vector2(Mathf.Abs(vector.x), Mathf.Abs(vector.y));
		if ((vector2.x > 5f || vector2.y > 5f) && !dragging && dragCanBegin)
		{
			dragging = true;
			HighlightSelectedIndex(instant: true);
		}
		if (dragging)
		{
			Vector2 vector3 = new Vector2(0f - vector.x, 0f) / Screen.height * 25f * levels.Count;
			Vector2 anchoredPosition = levelsParent.anchoredPosition;
			Vector2 b = origPos - vector3;
			Vector2 vector4 = Vector2.Lerp(anchoredPosition, b, Time.unscaledDeltaTime * 60f);
			if (vector4.x <= difference / 2f && vector4.x >= (0f - difference) * (float)(levels.Count - 1) - difference / 2f)
			{
				levelsParent.anchoredPosition = new Vector2(vector4.x, levelsParent.anchoredPosition.y);
				UpdateAlpha(levelsParent.anchoredPosition.x);
			}
		}
	}

	private void OnTouchEnd(Vector2 pos)
	{
		if (dragging)
		{
			HighlightSelectedIndex();
		}
		dragging = false;
	}
}
