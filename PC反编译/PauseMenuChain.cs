using System;
using System.Collections.Generic;
using UnityEngine;

public class PauseMenuChain : MonoBehaviour
{
	[Serializable]
	public struct SineWave
	{
		public float amplitude;

		public float frequency;

		public float offset;

		public float speed;

		public bool anchorLeft;

		public bool anchorRight;
	}

	public int linkCount = 30;

	private const float linkLength = 19.8f;

	public SineWave[] sineWaves;

	public GameObject linkPrefab;

	public bool modifyHeight;

	public bool modifyLinks;

	public bool useXOffset;

	[NonSerialized]
	public List<PauseMenuChainLink> links;

	private float xOffset => (float)(-linkCount) * 19.8f / 2f;

	public float WaveFunction(float x)
	{
		float num = 0f;
		float num2 = x / 19.8f / (float)(linkCount + 1);
		SineWave[] array = sineWaves;
		for (int i = 0; i < array.Length; i++)
		{
			SineWave sineWave = array[i];
			float num3 = (sineWave.anchorLeft ? num2 : 1f);
			float num4 = (sineWave.anchorRight ? (1f - num2) : 1f);
			num += Mathf.Sin(x / sineWave.frequency + sineWave.offset + Time.realtimeSinceStartup * sineWave.speed) * sineWave.amplitude * num3 * num4;
		}
		return num;
	}

	private void Update()
	{
		if (links != null)
		{
			for (int i = 0; i < linkCount; i++)
			{
				PauseMenuChainLink pauseMenuChainLink = links[i];
				pauseMenuChainLink.gameObject.SetActive(value: true);
				float offset = (useXOffset ? xOffset : 0f);
				Vector2 vector = GetPosition(i, offset);
				Vector2 vector2 = GetPosition(i - 1, offset);
				Vector2 vector3 = vector - vector2;
				float num = Mathf.Atan2(vector3.y, vector3.x);
				pauseMenuChainLink.rectTransform.anchoredPosition = new Vector3(vector2.x, vector2.y, 10f);
				pauseMenuChainLink.rectTransform.eulerAngles = num * 57.29578f * Vector3.forward;
			}
		}
		Vector2 GetPosition(int num3, float num2 = 0f)
		{
			float x = num2 + 19.8f * (float)(num3 + 1);
			float y = WaveFunction(x);
			return new Vector2(x, y);
		}
	}

	private void LateUpdate()
	{
		if (modifyLinks)
		{
			UpdateLinks();
		}
	}

	public void UpdateHeight(RectTransform buttonsTransform)
	{
		if (modifyHeight)
		{
			scrController instance = scrController.instance;
			bool flag = (bool)instance.currFloor && (instance.currFloor.freeroam || instance.currFloor.freeroamGenerated);
			bool num = instance.gameworld || flag;
			bool flag2 = ADOBase.sceneName.IsTaro() && !instance.isPuzzleRoom && instance.isbosslevel;
			if (num)
			{
				Vector2 vector = new Vector2(0f, GCS.practiceMode ? 35 : ((GCS.speedTrialMode || flag2) ? 15 : 0));
				RectTransform component = GetComponent<RectTransform>();
				Vector2 anchoredPosition = (buttonsTransform.anchoredPosition = vector);
				component.anchoredPosition = anchoredPosition;
			}
		}
	}

	private void ModifyCount()
	{
		int num = (int)(Camera.main.aspect * 15f);
		linkCount = ((num > 30) ? num : 30);
	}

	public void UpdateLinks()
	{
		ModifyCount();
		if (links != null && links.Count == linkCount)
		{
			return;
		}
		if (links != null)
		{
			for (int i = 0; i < links.Count; i++)
			{
				UnityEngine.Object.Destroy(links[i].gameObject);
			}
		}
		InitLinks();
	}

	public void InitLinks()
	{
		links = new List<PauseMenuChainLink>();
		for (int i = 0; i < linkCount; i++)
		{
			PauseMenuChainLink component = UnityEngine.Object.Instantiate(linkPrefab, base.transform).GetComponent<PauseMenuChainLink>();
			float num = UnityEngine.Random.Range(0.75f, 1f);
			component.image.color = new Color(num, num, num, 1f);
			links.Insert(0, component);
		}
	}
}
