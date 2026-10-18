using System;
using DG.Tweening;
using UnityEngine;

public class PausePlanets : MonoBehaviour
{
	public scrPlanet blue;

	public scrPlanet red;

	public Texture2D upArrowMask;

	public Texture2D downArrowMask;

	public Texture2D defaultMask;

	public float speed;

	public float radius;

	private RectTransform rectTransform;

	private bool clear;

	private bool instant;

	private bool instantScale;

	private bool updatePosition;

	private float scale = 1f;

	private float scaleFactor = 20f;

	private float pConstant = 4f;

	private float lastTimeUpdate;

	private const float interval = 1f;

	private void Awake()
	{
		rectTransform = GetComponent<RectTransform>();
		scale = base.transform.localScale.x;
		lastTimeUpdate = Time.unscaledTime;
	}

	private double M(double a, double b)
	{
		return a - Math.Floor(a / b) * b;
	}

	private float F(double x)
	{
		double num = Math.Floor(2.0 * x / Math.PI);
		return (float)Math.Pow(M(num + 1.0, 2.0) * 2.0 - 1.0, pConstant);
	}

	private float GetX(double x)
	{
		double num = Math.Cos(x);
		int num2 = Math.Sign(num);
		double num3 = Math.Pow(num, pConstant);
		double num4 = Math.Pow(Math.Sin(x), pConstant);
		double num5 = num3 + num4 * (double)F(x);
		double num6 = Math.Pow(num3 / num5, 1f / pConstant);
		return (float)((double)num2 * num6);
	}

	private float GetY(double y)
	{
		double num = Math.Sin(y);
		int num2 = Math.Sign(num);
		double num3 = Math.Pow(num, pConstant);
		double num4 = Math.Pow(Math.Cos(y), pConstant);
		double num5 = num3 + num4 * (double)F(y);
		double num6 = Math.Pow(num3 / num5, 1f / pConstant);
		return (float)((double)num2 * num6);
	}

	private void Update()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		float num = GetX((0f - realtimeSinceStartup) * speed) * radius;
		float num2 = GetY((0f - realtimeSinceStartup) * speed) * radius;
		blue.transform.localPosition = new Vector2(num, num2);
		red.transform.localPosition = new Vector2(0f - num, 0f - num2);
	}

	private void LateUpdate()
	{
		if (clear)
		{
			clear = false;
			rectTransform.DOKill();
			rectTransform.DOScale(scale * scaleFactor, instantScale ? 0f : 0.2f).SetUpdate(isIndependentUpdate: true);
			if (updatePosition)
			{
				rectTransform.DOAnchorPos(Vector2.zero, instant ? 0f : 0.2f).SetUpdate(isIndependentUpdate: true).OnComplete(delegate
				{
					UpdateParticles(show: true);
				});
			}
		}
		float num = Time.unscaledTime - lastTimeUpdate;
		if (num > 1f)
		{
			blue.planetRenderer.ChangeFace(pulse: true, ignoreTimescale: true);
			red.planetRenderer.ChangeFace(pulse: true, ignoreTimescale: true);
			blue.planetRenderer.scrSamurai.updateOnPause = true;
			red.planetRenderer.scrSamurai.updateOnPause = true;
			lastTimeUpdate = Time.unscaledTime + num % 1f;
		}
	}

	public void UpdateAnimation(Transform parent, float newScale, float newPConstant, bool instant, bool updatePosition = true)
	{
		blue.gameObject.SetActive(value: true);
		red.gameObject.SetActive(value: true);
		UpdateParticles(show: false);
		instantScale = instant || scale == newScale;
		scale = newScale;
		pConstant = newPConstant;
		this.instant = instant;
		this.updatePosition = updatePosition;
		base.transform.SetParent(parent, worldPositionStays: true);
		base.transform.SetAsFirstSibling();
		clear = true;
	}

	public void UpdatePlanets()
	{
		UpdatePlanet(blue);
		UpdatePlanet(red);
	}

	private void UpdatePlanet(scrPlanet planet)
	{
		planet.planetRenderer.LoadPlanetColor(planet.isRed);
		planet.planetRenderer.ring.gameObject.SetActive(value: false);
		planet.planetRenderer.deathExplosion.gameObject.SetActive(value: false);
	}

	private void UpdateParticles(bool show)
	{
		UpdateParticle(blue, show);
		UpdateParticle(red, show);
	}

	private void UpdateParticle(scrPlanet planet, bool show)
	{
		if (show)
		{
			planet.planetRenderer.PlayParticles();
		}
		else
		{
			planet.planetRenderer.DisableParticles();
		}
	}

	public void SetPlanetsToArrows(bool arrows)
	{
		red.planetRenderer.sprite.alphaMask = (arrows ? upArrowMask : defaultMask);
		blue.planetRenderer.sprite.alphaMask = (arrows ? downArrowMask : defaultMask);
	}
}
