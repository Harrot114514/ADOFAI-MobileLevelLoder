using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PauseMedals : ADOBase
{
	public GameObject medalPrefab;

	public Image currentMedal;

	public RectTransform hidePosition;

	[NonSerialized]
	public int medalIndex;

	public RectTransform medalsContainer;

	public RectTransform medalSelect;

	private int[] displayedMedals;

	private Vector3[] originalPositions;

	private List<MenuMedal> medals = new List<MenuMedal>();

	private float medalScale = 16f;

	private int curSection;

	public bool transition;

	private bool init;

	private bool inited;

	private float clickTimer;

	private int lastClicked;

	private const bool requireDoubleClick = false;

	public Transform currentMedalTransform => medals[medalIndex].transform;

	public PauseLevelButton currentMedalButton => medals[medalIndex].GetComponent<PauseLevelButton>();

	public int medalsLength => displayedMedals.Length;

	public bool isExpanded => medalsContainer.gameObject.activeSelf;

	private PauseMenu pauseMenu => scrController.instance.pauseMenu;

	public bool SectionIsUnlocked(int id)
	{
		return displayedMedals[id] != 0;
	}

	private void Update()
	{
		if (init && clickTimer > 0f)
		{
			clickTimer -= Time.unscaledDeltaTime;
		}
		if (init)
		{
			Scale();
		}
	}

	public void OnClick(int id)
	{
		if (!transition && SectionIsUnlocked(id))
		{
			WarpToSection(id);
			transition = true;
		}
	}

	public void WarpToSection(int id)
	{
		int num = GCS.pauseMedalFloors[id] - 1;
		if (num < 0)
		{
			num = 0;
		}
		while ((scrLevelMaker.instance.listFloors[num].midSpin || scrLevelMaker.instance.listFloors[num].freeroam) && num > 0)
		{
			num--;
		}
		GCS.checkpointNum = num;
		Persistence.DeleteSavedProgress();
		scrController.instance.Restart();
	}

	public void Init()
	{
		bool flag = (bool)ADOBase.controller.currFloor && (ADOBase.controller.currFloor.freeroam || ADOBase.controller.currFloor.freeroamGenerated);
		bool flag2 = ADOBase.controller.gameworld || flag;
		bool num = ADOBase.sceneName.IsTaro() && !ADOBase.controller.isPuzzleRoom && ADOBase.controller.isbosslevel;
		pauseMenu.vignetteMaterial.SetFloat("_VignetteRadius", 0.3f);
		if (!num || !flag2 || GCS.speedTrialMode || GCS.practiceMode)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		if (!inited)
		{
			inited = true;
			currentMedal.GetComponent<Button>().onClick.AddListener(delegate
			{
				scrController.instance.pauseMenu.ShowMedals(instant: false);
			});
		}
		base.gameObject.SetActive(value: true);
		pauseMenu.vignetteMaterial.SetFloat("_VignetteRadius", 1f);
		if (TaroBGScript.instance != null && GCS.pauseMedalStatsCurrent != null)
		{
			displayedMedals = TaroBGScript.instance.SaveMedals(scrController.currentWorldString, GCS.pauseMedalStatsCurrent);
			if ((bool)ADOBase.controller.currFloor)
			{
				for (int num2 = 0; num2 < GCS.pauseMedalFloors.Count && ADOBase.controller.currFloor.seqID >= GCS.pauseMedalFloors[num2] - 1; num2++)
				{
					curSection = num2;
				}
			}
			init = true;
			medalIndex = curSection;
			Scale();
			foreach (MenuMedal medal in medals)
			{
				UnityEngine.Object.Destroy(medal.gameObject);
			}
			medals.Clear();
			originalPositions = new Vector3[displayedMedals.Length];
			for (int num3 = 0; num3 < displayedMedals.Length; num3++)
			{
				MenuMedal component = UnityEngine.Object.Instantiate(medalPrefab, medalsContainer).GetComponent<MenuMedal>();
				RectTransform component2 = component.GetComponent<RectTransform>();
				component2.anchoredPosition = new Vector3(((float)num3 - (float)displayedMedals.Length / 2f + 0.5f) * medalScale, (num3 % 2 == 0) ? (medalScale * 0.5f) : ((0f - medalScale) * 0.5f), 0f);
				component.id = num3;
				component.SetState(displayedMedals[num3]);
				originalPositions[num3] = component2.transform.position;
				if (curSection == num3)
				{
					component.TintBack(Color.white);
					currentMedal.sprite = component.medalFront.sprite;
				}
				else
				{
					component.TintBack(Color.black);
				}
				medals.Add(component);
			}
			RectTransform component3 = medals[curSection].GetComponent<RectTransform>();
			medalSelect.position = component3.transform.position;
			medalSelect.anchoredPosition = new Vector2(medalSelect.anchoredPosition.x, medalSelect.anchoredPosition.y + 13f);
		}
		else
		{
			base.gameObject.SetActive(value: false);
		}
	}

	private void Scale()
	{
		float num = (float)Screen.width / (float)Screen.height;
		if (num < 1.77f)
		{
			medalsContainer.localScale = Vector3.one * Mathf.Sqrt(num / 1.77f);
		}
		if (displayedMedals.Length >= 11)
		{
			base.transform.ScaleXY(0.8f);
		}
	}

	public void Show(Sequence sequence, bool instant = false)
	{
		instant = instant || isExpanded;
		medalIndex = curSection;
		medalsContainer.gameObject.SetActive(value: true);
		currentMedal.gameObject.SetActive(value: false);
		Vector3 zero = Vector3.zero;
		Vector3 zero2 = Vector3.zero;
		Vector3 zero3 = Vector3.zero;
		for (int i = 0; i < displayedMedals.Length; i++)
		{
			MenuMedal menuMedal = medals[i];
			zero = menuMedal.transform.position;
			zero2 = originalPositions[i];
			zero3 = (zero + zero2) / 2f;
			zero3 += Vector3.up * 100f;
			sequence.Insert(0f, menuMedal.transform.DOPath(new Vector3[3] { zero, zero3, zero2 }, instant ? 0f : 0.5f, PathType.CatmullRom));
		}
		sequence.Insert(0f, pauseMenu.vignetteMaterial.DOFloat(1f, "_VignetteRadius", instant ? 0f : 0.5f));
		sequence.InsertCallback(instant ? 0f : 0.5f, delegate
		{
			medalSelect.gameObject.SetActive(value: true);
		});
	}

	public void Hide(Sequence sequence, bool instant = false)
	{
		medalIndex = curSection;
		medalSelect.gameObject.SetActive(value: false);
		currentMedal.transform.position = hidePosition.position;
		medals[medalIndex].transform.SetAsLastSibling();
		Vector3 zero = Vector3.zero;
		Vector3 zero2 = Vector3.zero;
		Vector3 zero3 = Vector3.zero;
		for (int i = 0; i < displayedMedals.Length; i++)
		{
			MenuMedal menuMedal = medals[i];
			zero = menuMedal.transform.position;
			zero2 = hidePosition.position;
			zero3 = (zero + zero2) / 2f;
			zero3 += Vector3.up * 100f;
			sequence.Insert(0f, menuMedal.transform.DOPath(new Vector3[3] { zero, zero3, zero2 }, instant ? 0f : 0.5f, PathType.CatmullRom));
		}
		sequence.Insert(0f, pauseMenu.vignetteMaterial.DOFloat(0.3f, "_VignetteRadius", instant ? 0f : 0.5f));
		sequence.InsertCallback(instant ? 0f : 0.5f, delegate
		{
			medalsContainer.gameObject.SetActive(value: false);
			currentMedal.gameObject.SetActive(value: true);
		});
	}
}
