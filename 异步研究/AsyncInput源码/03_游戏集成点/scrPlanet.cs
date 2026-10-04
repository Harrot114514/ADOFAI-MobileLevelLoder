using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class scrPlanet : ADOBase
{
	public const float basePlanetScale = 0.9f;

	public PlanetRenderer planetRenderer;

	public PlanetarySystem planetarySystem;

	public scrPlayer player;

	[Header("Mechanics")]
	public scrPlanet other;

	public int planetIndex = -1;

	[Header("Debug")]
	public bool shouldPrint;

	[NonSerialized]
	public float cosmeticRadius;

	[NonSerialized]
	public scrPlanet next;

	[NonSerialized]
	public scrPlanet prev;

	[NonSerialized]
	public bool toDelete;

	[NonSerialized]
	public double angle;

	[NonSerialized]
	public float cosmeticAngle;

	[NonSerialized]
	public float planetScale = 1f;

	[NonSerialized]
	public Tween scaleTween;

	[NonSerialized]
	public scrFloor currfloor;

	[NonSerialized]
	public bool isExtra;

	[NonSerialized]
	public Vector3 storedPosition;

	[NonSerialized]
	public Vector3 holdOffsetPos;

	[NonSerialized]
	public float iFrames;

	[NonSerialized]
	public bool isRed;

	[NonSerialized]
	public scrPlanet redPlanet;

	[NonSerialized]
	public scrPlanet bluePlanet;

	[NonSerialized]
	public double cachedAngle;

	private VisualQuality visualQuality;

	[NonSerialized]
	public bool onlyRing;

	[NonSerialized]
	public bool dead;

	[NonSerialized]
	public bool hittable;

	[NonSerialized]
	public bool dummyPlanets;

	private float aliveTime;

	private float endingTween;

	private float swirlTween;

	private float mpHoldTween;

	private Tween _cosmeticAngleTween;

	private double perc;

	private double easedPerc;

	private double dist;

	private double endpoint;

	private int easeParts;

	private EasePartBehavior easePartBehavior;

	private int loops;

	private float newStart;

	private Vector3 targetPosition;

	private Vector3 deathPos;

	private double deathAngle;

	private double initDeathAngle;

	private float randScatterAngle;

	private Vector3 addBob;

	private const float bobRadius = 0.1f;

	private bool printed;

	private bool handledPositionAlready;

	private Vector3 movingFloorHoldOffset;

	private Vector3 actualPos;

	private Vector3 actualPivotPos;

	private Vector3 actualMPPos;

	private Vector3 oppositePos;

	private Vector3 oppositePivotPos;

	private Vector3 oppositeMPPos;

	private Vector3 tempTransPos;

	private Vector3 tempMPPivotPos;

	private scrPlanet p;

	private scrPlanet movingToNext;

	private scrPlanet lastMovedPlanet;

	private float lockTime;

	public double targetExitAngle { get; private set; }

	public double snappedLastAngle { get; private set; }

	public new scrConductor conductor => scrConductor.instance;

	public new scrController controller => scrController.instance;

	public scrCamera camy => controller?.camy;

	public double visualAngle => angle + (double)scrConductor.calibration_v - (double)scrConductor.calibration_i;

	private float radius => scrController.instance.tileSize;

	public bool isChosen => planetarySystem?.chosenPlanet == this;

	private scrFloor conditionalFloor => controller.conditionalFloor;

	private bool foolSwirl => GCS.FOOL_SWIRL;

	public void PrintImportantVariables()
	{
		string text = "\n";
		_ = "planet: " + base.gameObject.name + text + "angle: " + angle + text + "snappedLastAngle: " + snappedLastAngle + text + "conductor.songposition_minusi: " + conductor.songposition_minusi + text + "lastHit: " + player.lastHit + text + "conductor.crotchet: " + conductor.crotchetAtStart + text + "planetarySystem.speed: " + planetarySystem.speed + text + "planetarySystem.isCW: " + planetarySystem.isCW;
	}

	public void Rewind()
	{
		Revive();
		cosmeticRadius = 0f;
		angle = 0.0;
		snappedLastAngle = 0.0;
		currfloor = null;
		aliveTime = 0f;
		planetRenderer.ringComp.Switch(chosen: false, instant: true);
		planetRenderer.ClearParticles();
		planetRenderer.SetLayer("Planet");
		Start();
	}

	private void Revive(bool liteRevive = false)
	{
		dead = false;
		planetRenderer.Revive(liteRevive);
		if (!liteRevive)
		{
			hittable = true;
		}
	}

	private void Start()
	{
		if (!isChosen)
		{
			cosmeticRadius = scrController.instance.tileSize;
		}
		endingTween = 0f;
		swirlTween = (foolSwirl ? 1 : 0);
		deathPos = Vector3.zero;
		holdOffsetPos = Vector3.zero;
		addBob = Vector3.zero;
		visualQuality = controller.visualQuality;
	}

	private void Awake()
	{
		isRed = base.name.Contains("Red");
		redPlanet = (isRed ? this : other);
		bluePlanet = (isRed ? other : this);
		onlyRing = false;
	}

	private void Update()
	{
		if (dummyPlanets)
		{
			return;
		}
		if (iFrames > 0f)
		{
			iFrames -= Time.deltaTime;
		}
		if (aliveTime < 60f)
		{
			aliveTime += Time.deltaTime;
		}
		if (hittable && !dead)
		{
			Collider2D[] array = Physics2D.OverlapCircleAll(base.transform.position, 0.3f, 2097152);
			for (int i = 0; i < array.Length; i++)
			{
				scrDecoration component = array[i].transform.parent.GetComponent<scrDecoration>();
				if (component.useHitbox && component.hitboxDetectTarget == HitboxDetectTarget.Planet && (component.hitboxTargetPlanet != HitboxTargetPlanet.Center || isChosen) && (component.hitboxTargetPlanet != HitboxTargetPlanet.Orbiting || !isChosen))
				{
					component.HitboxTriggerAction(this);
				}
			}
		}
		scrMisc.ScaleSymmetric2D(base.transform, 0.9f * planetScale);
		Update_RefreshAngles();
		ParticleSystem.EmissionModule emission = planetRenderer.coreParticles.emission;
		emission.enabled = isChosen;
		if (controller.state == States.PlayerControl)
		{
			holdOffsetPos = Vector3.zero;
			addBob = Vector3.zero;
		}
		if (controller.state == States.PlayerControl && currfloor != null && currfloor.nextfloor != null)
		{
			Vector3 vector = currfloor.nextfloor.transform.position - currfloor.transform.position;
			deathAngle = Mathf.Atan2(vector.x, vector.y);
			initDeathAngle = angle;
			randScatterAngle = UnityEngine.Random.Range(-MathF.PI / 6f, MathF.PI / 6f);
		}
		if (currfloor != null && currfloor.holdLength > -1 && isChosen)
		{
			States state = controller.state;
			if (state != States.Start && state != States.Won)
			{
				movingToNext = next;
				if (controller.gameworld)
				{
					state = controller.state;
					if (state != States.Fail && state != States.Fail2 && (bool)currfloor.nextfloor)
					{
						perc = (conductor.songposition_minusi - currfloor.entryTime) / (currfloor.nextfloor.entryTime - currfloor.entryTime);
					}
				}
				else
				{
					double entryTime = currfloor.entryTime;
					double num = entryTime + conductor.crotchetAtStart * (double)(currfloor.holdLength * 2 + 1);
					perc = planetarySystem.speed * (conductor.songposition_minusi - entryTime) / (num - entryTime);
				}
				if (!printed)
				{
					printed = true;
				}
				if (perc < 0.0)
				{
					perc = 0.0;
				}
				easedPerc = perc;
				endpoint = 1.0;
				if (controller.rotationEase != Ease.Linear && currfloor.planetEaseParts > 0)
				{
					easeParts = currfloor.planetEaseParts;
					easePartBehavior = currfloor.planetEasePartBehavior;
					easedPerc *= easeParts;
					endpoint /= easeParts;
					loops = Mathf.FloorToInt(Mathf.Abs((float)easedPerc));
					newStart = (float)loops / (float)easeParts;
					if (loops % 2 == 0 || easePartBehavior == EasePartBehavior.Repeat)
					{
						easedPerc = DOVirtual.EasedValue(newStart, newStart + (float)endpoint, (float)easedPerc - (float)loops, controller.rotationEase);
					}
					else
					{
						easedPerc = newStart + (float)endpoint - DOVirtual.EasedValue(0f, (float)endpoint, 1f - ((float)easedPerc - (float)loops), controller.rotationEase);
					}
				}
				if (!currfloor.nextfloor.stickToFloor)
				{
					targetPosition = currfloor.nextfloor.startPos - scrController.instance.tileSize * currfloor.nextfloor.radiusScale * ClockwiseAngleToVector(currfloor.exitangle);
					holdOffsetPos = (float)easedPerc * (targetPosition - currfloor.startPos) + deathPos;
				}
				else
				{
					targetPosition = currfloor.nextfloor.transform.position - scrController.instance.tileSize * currfloor.nextfloor.radiusScale * ClockwiseAngleToVector(currfloor.exitangle);
					holdOffsetPos = (float)easedPerc * (targetPosition - currfloor.transform.position) + deathPos + movingFloorHoldOffset;
					movingFloorHoldOffset = currfloor.transform.position - storedPosition;
				}
				currfloor.holdCompletion = (float)perc;
				currfloor.holdCompletionEased = (float)easedPerc;
				if (controller.state == States.PlayerControl && currfloor.holdDistance > 0f)
				{
					float num2 = Mathf.Sin((float)(angle - currfloor.exitangle));
					addBob = scrController.instance.tileSize * 0.1f * Vector2.one * num2;
				}
				if (controller.state == States.Fail && perc < 0.99)
				{
					double num3 = (currfloor.stickToFloor ? deathAngle : currfloor.exitangle);
					deathPos += Time.deltaTime * ((Mathf.Sin((float)initDeathAngle - (float)num3) < 0f) ? 8f : (-8f)) * ClockwiseAngleToVector((float)num3 + randScatterAngle - MathF.PI / 2f);
				}
				currfloor.holdRenderer.touchColor = next.planetRenderer.planetColor.ToRealColor();
				currfloor.holdRenderer.UpdateColor((float)easedPerc);
				base.transform.position = storedPosition + addBob + holdOffsetPos;
				if (camy.followMode)
				{
					state = controller.state;
					if (state != States.Fail && state != States.Fail2)
					{
						camy.SetHoldOffset(holdOffsetPos);
					}
				}
			}
		}
		handledPositionAlready = false;
	}

	private void LateUpdate()
	{
		if (currfloor != null && currfloor.stickToFloor && isChosen && !handledPositionAlready && currfloor.holdLength < 0)
		{
			base.transform.position = currfloor.transform.position + holdOffsetPos + addBob;
		}
		if (planetarySystem != null)
		{
			base.transform.position = base.transform.position.WithZ(planetarySystem.transform.position.z);
		}
	}

	public void Update_RefreshAngles()
	{
		if (conductor.crotchetAtStart == 0.0 || !isChosen)
		{
			return;
		}
		if (!GCS.d_stationary)
		{
			angle = snappedLastAngle + (conductor.songposition_minusi - player.lastHit) / conductor.crotchetAtStart * 3.1415927410125732 * planetarySystem.speed * (double)(planetarySystem.isCW ? 1 : (-1));
			if (shouldPrint)
			{
				PrintImportantVariables();
				shouldPrint = false;
			}
		}
		else
		{
			if (Input.GetKey(KeyCode.DownArrow))
			{
				angle += 0.10000000149011612;
			}
			if (Input.GetKey(KeyCode.UpArrow))
			{
				angle -= 0.10000000149011612;
			}
		}
		float num = (float)(angle + (double)cosmeticAngle);
		float num2 = 2f;
		float num3 = 2f;
		float num4 = num;
		float num5 = 0f;
		float num6 = 0f;
		double num7 = 0.0;
		float num8 = 0f;
		scrFloor scrFloor2 = null;
		movingToNext = next;
		bool flag = false;
		if (currfloor != null)
		{
			scrFloor2 = (controller.gameworld ? ((currfloor.seqID > 0) ? scrLevelMaker.instance.listFloors[currfloor.seqID - 1] : currfloor) : currfloor);
			if (currfloor.nextfloor != null && currfloor.nextfloor.entryTime - currfloor.entryTime > 0.0)
			{
				num7 = (conductor.songposition_minusi - currfloor.entryTime) / (currfloor.nextfloor.entryTime - currfloor.entryTime);
			}
			num2 = currfloor.numPlanets;
			if (currfloor.nextfloor != null && currfloor.numPlanets < currfloor.nextfloor.numPlanets && scrFloor2.numPlanets > currfloor.numPlanets)
			{
				num6 = Mathf.Lerp(scrFloor2.numPlanets - currfloor.numPlanets, 0f, (float)num7);
				num3 = scrFloor2.numPlanets;
				flag = true;
			}
			else if (currfloor.nextfloor != null && currfloor.numPlanets > currfloor.nextfloor.numPlanets && scrFloor2.numPlanets < currfloor.numPlanets)
			{
				num3 = currfloor.numPlanets;
			}
			else if (currfloor.nextfloor != null && currfloor.numPlanets < currfloor.nextfloor.numPlanets)
			{
				num3 = Mathf.Lerp(currfloor.numPlanets, currfloor.nextfloor.numPlanets, (float)num7);
			}
			else if (scrFloor2.numPlanets > currfloor.numPlanets)
			{
				num3 = Mathf.Lerp(scrFloor2.numPlanets, currfloor.numPlanets, (float)num7);
				num6 = Mathf.Lerp(scrFloor2.numPlanets - currfloor.numPlanets, 0f, (float)num7);
			}
			else
			{
				num3 = currfloor.numPlanets;
			}
			if (num6 > 0f && num2 > 2f)
			{
				num8 = ((!flag) ? ((float)((!currfloor.isCCW) ? 1 : (-1)) * ((float)scrMisc.GetInverseAnglePerBeatMultiplanet(num3) - (float)scrMisc.GetInverseAnglePerBeatMultiplanet(num2))) : ((float)((!currfloor.isCCW) ? 1 : (-1)) * ((float)scrMisc.GetInverseAnglePerBeatMultiplanet(num3) - (float)scrMisc.GetInverseAnglePerBeatMultiplanet(num2 + ((float)(scrFloor2.numPlanets - currfloor.numPlanets) - num6)))));
			}
			num += num8;
			if (currfloor.holdLength > -1)
			{
				num -= (1f - (float)num7) * mpHoldTween * (float)scrMisc.GetInverseAnglePerBeatMultiplanet(currfloor.numPlanets);
			}
			num4 = num - (float)((!currfloor.isCCW) ? 1 : (-1)) * (float)scrMisc.GetInverseAnglePerBeatMultiplanet(num3) / 2f;
			if (num2 > 2f)
			{
				num5 = (0f - (float)scrMisc.GetInverseAnglePerBeatMultiplanet(num3 - 1f)) * endingTween * (float)((!currfloor.isCCW) ? 1 : (-1));
			}
			if (controller.rotationEase != Ease.Linear)
			{
				float num9 = (float)targetExitAngle - (float)currfloor.angleLength;
				float num10 = scrMisc.EasedAngle(num9 + num8, (float)targetExitAngle, num, controller.rotationEase, currfloor.planetEaseParts, currfloor.planetEasePartBehavior);
				if (!float.IsNaN(num10) && !float.IsInfinity(num10) && num >= num9)
				{
					num4 += num10 - num;
					num = num10;
				}
			}
			if (currfloor.stickToFloor)
			{
				num -= (currfloor.transform.rotation.eulerAngles - currfloor.startRot).z * (MathF.PI / 180f);
				num4 -= (currfloor.transform.rotation.eulerAngles - currfloor.startRot).z * (MathF.PI / 180f);
			}
		}
		tempTransPos = base.transform.position;
		if (currfloor != null && currfloor.nextfloor != null && controller.state == States.PlayerControl && currfloor != null && currfloor.nextfloor.radiusScale != currfloor.radiusScale)
		{
			foreach (scrPlanet planet in planetarySystem.planetList)
			{
				easedPerc = num7;
				endpoint = 1.0;
				easeParts = currfloor.planetEaseParts;
				easePartBehavior = currfloor.planetEasePartBehavior;
				easedPerc *= easeParts;
				endpoint /= easeParts;
				loops = Mathf.FloorToInt(Mathf.Abs((float)easedPerc));
				newStart = (float)loops / (float)easeParts;
				if (loops % 2 == 0 || easePartBehavior == EasePartBehavior.Repeat)
				{
					easedPerc = DOVirtual.EasedValue(newStart, newStart + (float)endpoint, (float)easedPerc - (float)loops, controller.rotationEase);
				}
				else
				{
					easedPerc = newStart + (float)endpoint - DOVirtual.EasedValue(0f, (float)endpoint, 1f - ((float)easedPerc - (float)loops), controller.rotationEase);
				}
				planet.cosmeticRadius = scrController.instance.tileSize * Mathf.Lerp(currfloor.radiusScale, currfloor.nextfloor.radiusScale, (float)easedPerc);
			}
		}
		double normal = 0.0;
		if (currfloor != null)
		{
			double entryangle = currfloor.entryangle;
			double num11 = currfloor.exitangle;
			if (Math.Abs(entryangle - num11) > 6.2831854820251465)
			{
				num11 += (double)((float)((num11 < entryangle) ? 2 : (-2)) * MathF.PI);
			}
			normal = ((Modulo(Math.Abs(entryangle - num11), 6.2831854820251465) < 0.0001 || Modulo(Math.Abs(entryangle - num11), 6.2831854820251465) > 6.283085482025147) ? entryangle : ((!(entryangle > num11)) ? (entryangle + (num11 - Math.PI - entryangle) * num7) : (entryangle + (num11 + Math.PI - entryangle) * num7)));
		}
		actualPos = tempTransPos + ClockwiseAngleToVector(num - num5) * cosmeticRadius;
		oppositePos = tempTransPos + ClockwiseAngleToVector(scrMisc.ReflectAngle(num - num5, normal)) * cosmeticRadius;
		if (!movingToNext.dead || currfloor == null || !currfloor.freeroam)
		{
			if (num2 <= 2f)
			{
				movingToNext.transform.position = ((foolSwirl && controller.gameworld) ? oppositePos : actualPos);
			}
			else
			{
				movingToNext.transform.position = Vector3.Lerp(actualPos, oppositePos, swirlTween);
			}
		}
		if (!movingToNext.dead)
		{
			movingToNext.transform.position -= addBob;
		}
		if (!(num2 > 2f) || !(currfloor != null))
		{
			return;
		}
		double num12 = (double)cosmeticRadius / (2.0 * (double)Mathf.Sin(MathF.PI / num3));
		double num13 = 0.0;
		double num14 = (double)(endingTween * cosmeticRadius) + num12 * (double)(1f - endingTween);
		actualPivotPos = tempTransPos + ClockwiseAngleToVector(num4) * (float)num12 * (1f - endingTween);
		oppositePivotPos = tempTransPos + ClockwiseAngleToVector(scrMisc.ReflectAngle(num4, normal)) * (float)num12 * (1f - endingTween);
		tempMPPivotPos = Vector3.Lerp(actualPivotPos, oppositePivotPos, swirlTween);
		for (int i = 2; (float)i < num2; i++)
		{
			p = planetarySystem.GetMultiPlanet(planetIndex, i);
			if (!p.dead)
			{
				num13 = (double)((!currfloor.isCCW) ? 1 : (-1)) * (Math.PI * 2.0 * (((double)(-i) - ((double)num3 - (double)endingTween) / 2.0) / ((double)num3 - (double)endingTween))) + (double)(num4 * (1f - endingTween)) + (double)(num * endingTween);
				if (num6 > 0f && (float)i == num2 - 1f)
				{
					num13 -= (double)(num6 / num3 * 2f) * Math.PI;
				}
				actualMPPos = tempMPPivotPos + ClockwiseAngleToVector(num13) * (float)num14;
				oppositeMPPos = tempMPPivotPos + ClockwiseAngleToVector(scrMisc.ReflectAngle(num13, normal)) * (float)num14;
				p.transform.position = Vector3.Lerp(actualMPPos, oppositeMPPos, swirlTween);
				p.transform.position -= addBob;
			}
		}
	}

	public void AsyncRefreshAngles()
	{
		angle = AsyncInputUtils.GetAngle(this, snappedLastAngle, AsyncInputManager.targetSongTick);
	}

	public void FirstFloorAngleSetup()
	{
		snappedLastAngle = MathF.PI * (0.5f - conductor.adjustedCountdownTicks);
		scrFloor scrFloor2 = currfloor;
		if (controller.firstFloor != null)
		{
			scrFloor2 = controller.firstFloor;
		}
		else if ((bool)ADOBase.customLevel)
		{
			scrFloor2 = ADOBase.lm.listFloors[0];
		}
		else if (currfloor == null)
		{
			Collider2D[] array = Physics2D.OverlapPointAll(Vector3.zero, 1 << LayerMask.NameToLayer("Floor"));
			if (array != null)
			{
				for (int i = 0; i < array.Length; i++)
				{
					scrFloor component = array[i].GetComponent<scrFloor>();
					if (component == null)
					{
						component = array[i].transform.parent.GetComponent<scrFloor>();
					}
					if (component != null && component.seqID == 0)
					{
						scrFloor2 = component;
					}
				}
			}
		}
		currfloor = scrFloor2;
		if (currfloor != null && !dummyPlanets)
		{
			planetRenderer.ringComp.Switch(chosen: true);
		}
		if (currfloor != null)
		{
			targetExitAngle = snappedLastAngle + currfloor.angleLength;
		}
		else if (ADOBase.lm != null && ADOBase.lm.listFloors != null && ADOBase.lm.listFloors.Count > 0)
		{
			currfloor = ADOBase.lm.listFloors[0];
			if (currfloor != null)
			{
				targetExitAngle = snappedLastAngle + currfloor.angleLength;
			}
		}
	}

	public bool AutoShouldHitNow()
	{
		int seqID = currfloor.seqID;
		if (!controller.gameworld || seqID >= ADOBase.lm.listFloors.Count - 1)
		{
			return false;
		}
		float num = 0.5f;
		if (RDC.useOldAuto)
		{
			num = 10f;
		}
		float num2 = num * (MathF.PI / 180f);
		bool isCW = planetarySystem.isCW;
		if (isCW)
		{
			num2 = 0f - num2;
		}
		double num3 = targetExitAngle + (double)num2;
		if ((isCW && angle > num3) || (!isCW && angle < num3))
		{
			return true;
		}
		return false;
	}

	public scrPlanet SwitchChosen()
	{
		scrFloor scrFloor2 = null;
		double marginScale = ((currfloor.nextfloor == null) ? 1.0 : currfloor.nextfloor.marginScale);
		bool flag = currfloor.nextfloor != null && currfloor.nextfloor.auto;
		HitMargin hitMargin = scrMisc.GetHitMargin((float)cachedAngle, (float)targetExitAngle, planetarySystem.isCW, (float)((double)conductor.bpm * planetarySystem.speed), conductor.song.pitch, marginScale);
		double num = 0.0;
		if (controller.gameworld)
		{
			if (scrMisc.IsValidHit(hitMargin) || RDC.debug || ((player.auto || flag) && !RDC.useOldAuto) || player.midspinInfiniteMargin || controller.noFailInfiniteMargin)
			{
				scrBackgroundBars instance = scrBackgroundBars.instance;
				if (instance != null)
				{
					instance.Flash(scrBackgroundBars.BarStatus.hit);
				}
				scrFloor2 = currfloor.nextfloor;
				num = targetExitAngle;
				controller.forceOK = false;
				if (controller.noFailInfiniteMargin)
				{
					hitMargin = HitMargin.FailMiss;
				}
			}
		}
		else
		{
			float interval = controller.freeroamAngleInterval * (MathF.PI / 180f);
			float offset = controller.freeroamAngleOffset * (MathF.PI / 180f);
			Vector3 vector = SnappedCardinalDirection(SnapAngleCardinal(angle, interval, offset), interval, offset);
			Collider2D[] array = Physics2D.OverlapPointAll(new Vector2(vector.x, vector.y), 1 << LayerMask.NameToLayer("Floor"));
			Collider2D collider2D = null;
			if (array.Length != 0 && aliveTime > controller.popuptime)
			{
				for (int i = 0; i < array.Length; i++)
				{
					_ = array[i];
					collider2D = array[i];
					scrFloor component = collider2D.GetComponent<scrFloor>();
					if (component == null)
					{
						component = collider2D.transform.parent.GetComponent<scrFloor>();
					}
					if (component == null)
					{
						continue;
					}
					if (controller.independentPlayers)
					{
						bool flag2 = false;
						foreach (scrPlayer item in ADOBase.playerManager)
						{
							if (item.currFloor == component)
							{
								flag2 = true;
								break;
							}
						}
						if (flag2)
						{
							continue;
						}
					}
					if (!component.isLandable || ((!component.freeroamGenerated || !component.freeroam) && (component.freeroam || component.freeroamGenerated)))
					{
						continue;
					}
					if (currfloor.unstable && currfloor.isLandable)
					{
						currfloor.isLandable = false;
						currfloor.ToggleCollider(collEn: false);
						currfloor.MoveToBack();
						currfloor.hideIcon = true;
						scrFloor f = currfloor;
						f.TweenOpacity(0f, 3f, Ease.InCubic);
						if ((bool)f.legacyFloorSpriteRenderer)
						{
							f.legacyFloorSpriteRenderer.DOColor(new Color(1f, 1f, 1f, 0f), 3f).SetEase(Ease.InCubic);
						}
						currfloor.transform.DOScale(0.5f, 3f).SetEase(Ease.InCubic).OnComplete(delegate
						{
							f.enabled = false;
							f.transform.position += Vector3.up * 9999f;
						});
						currfloor.transform.DOMoveY(-2f, 3f).SetRelative(isRelative: true).SetEase(Ease.InCubic);
						currfloor.transform.DORotate(Vector3.forward * 45f, 3f).SetRelative(isRelative: true).SetEase(Ease.InCubic);
						currfloor.topGlow.gameObject.SetActive(value: false);
						currfloor.bottomGlow.gameObject.SetActive(value: false);
					}
					scrFloor2 = component;
					num = SnapAngleCardinal(angle, interval, offset);
					storedPosition = component.transform.position;
					component.entryTime = GetNearestIntervalToTime(conductor.songposition_minusi, controller.freeroamAngleInterval / 180f);
					holdOffsetPos = Vector3.zero;
				}
			}
		}
		bool flag3 = false;
		if (scrFloor2 == null || (hitMargin != HitMargin.Perfect && hitMargin != HitMargin.LatePerfect && hitMargin != HitMargin.EarlyPerfect))
		{
			Collider2D[] array2 = Physics2D.OverlapCircleAll(next.transform.position, 0.3f);
			foreach (Collider2D collider2D2 in array2)
			{
				if (collider2D2.gameObject.CompareTag("ReviveBubble") && collider2D2.TryGetComponent<PlayerBubble>(out var component2) && !(component2.player == null))
				{
					flag3 = true;
					component2.Pop(player);
				}
			}
		}
		if (flag3)
		{
			return this;
		}
		HitMargin hitMargin2 = HitMargin.Perfect;
		if (scrFloor2 == null || (player.failBar.DidFail(checkForDuplicateDeath: false) && !controller.noFail))
		{
			if (GCS.d_drumcontroller && controller.boothModeDebounceCounter > 0f)
			{
				return this;
			}
			if (GCS.d_drumcontroller && controller.gameworld)
			{
				controller.boothModeDebounceCounter = 0.1f;
			}
			if (currfloor.seqID > 0 && controller.gameworld && scrLevelMaker.instance.listFloors[currfloor.seqID - 1].holdLength > -1 && Mathf.Abs((float)snappedLastAngle - (float)angle) < MathF.PI / 2f && !currfloor.freeroam)
			{
				if (controller.strictHolds && currfloor.nextfloor.holdLength < 0)
				{
					player.OnDamage();
					player.marginTracker.AddHit(HitMargin.TooEarly);
				}
				return this;
			}
			if ((!currfloor.freeroam && currfloor.holdLength > -1 && GCS.checkpointNum != currfloor.seqID) || currfloor.holdLength == -1)
			{
				bool flag4 = player.OnDamage(multipress: false, applyMultipressDamage: false, hitMargin == HitMargin.TooLate || currfloor.freeroam, hitMargin);
				if (controller.gameworld)
				{
					if (player.midspinInfiniteMargin)
					{
						hitMargin = HitMargin.Perfect;
					}
					else
					{
						hitMargin2 = hitMargin;
					}
					if (flag4 && (GCS.hitMarginLimit != HitMarginLimit.PurePerfectOnly || controller.noFail))
					{
						hitMargin2 = HitMargin.FailOverload;
					}
					if (player.midspinInfiniteMargin)
					{
						hitMargin2 = HitMargin.Perfect;
					}
					player.marginTracker.AddHit(hitMargin2);
					if (!currfloor.hideJudgment)
					{
						float missAngle = (float)(targetExitAngle - angle);
						ADOBase.playerManager.hitTextManager.ShowHitText(hitMargin2, next, missAngle);
					}
					if (conditionalFloor != null)
					{
						if (hitMargin == HitMargin.TooEarly)
						{
							foreach (ffxPlusBase tooEarlyEffect in conditionalFloor.tooEarlyEffects)
							{
								tooEarlyEffect.StartEffect(movingToNext);
							}
						}
						if (hitMargin == HitMargin.TooLate)
						{
							foreach (ffxPlusBase tooLateEffect in conditionalFloor.tooLateEffects)
							{
								tooLateEffect.StartEffect(movingToNext);
							}
						}
					}
				}
				controller.multipressPenalty = false;
				controller.multipressAndHasPressedFirstPress = false;
			}
			if (GCS.checkpointNum == currfloor.seqID)
			{
				controller.lastCamPulseFloor = currfloor.seqID;
			}
			return this;
		}
		bool flag5 = false;
		if (controller.multipressAndHasPressedFirstPress && controller.multipressPenalty && !player.midspinInfiniteMargin && !currfloor.freeroam)
		{
			bool flag6 = false;
			if ((bool)currfloor)
			{
				flag6 = true;
				double bpm = (double)conductor.bpm * planetarySystem.speed * (double)conductor.song.pitch;
				double angleMoved = scrMisc.GetAngleMoved(currfloor.entryangle, currfloor.exitangle, !currfloor.isCCW);
				double num3 = scrMisc.AngleToTime(angleMoved, bpm);
				double num4 = 1.5690509853884578;
				flag6 = flag6 && angleMoved > num4;
				double num5 = controller.averageFrameTime * 3.5f;
				flag6 = flag6 && num3 > num5;
				double num6 = 0.03999999910593033;
				flag6 = flag6 && num3 > num6;
			}
			player.OnDamage(multipress: true, flag6, skipDamage: false, hitMargin);
			if (player.consecMultipressCounter > 5 || flag6)
			{
				flag5 = true;
			}
			if (controller.gameworld)
			{
				controller.multipressPenalty = false;
				controller.multipressAndHasPressedFirstPress = false;
			}
		}
		if (player.keyLimiterOverCounter > 0)
		{
			player.keyLimiterOverCounter--;
			player.missesOnCurrFloor.Add(MarkMiss());
			ADOBase.playerManager.hitTextManager.ShowHitText(HitMargin.OverPress, movingToNext);
			return this;
		}
		if (GCS.d_drumcontroller && controller.gameworld)
		{
			controller.boothModeDebounceCounter = 0.1f;
		}
		bool flag7 = Persistence.multiTapTileBehavior == MultitapTileBehavior.HitOnce;
		player.tapsOnThisFloor++;
		if ((bool)scrFloor2 && scrFloor2.tapsSoFar < player.tapsOnThisFloor)
		{
			scrFloor2.tapsSoFar = player.tapsOnThisFloor;
		}
		if ((bool)scrFloor2 && player.tapsOnThisFloor < scrFloor2.tapsNeeded)
		{
			Material material = scrFloor2.floorRenderer.material;
			scrFloor2.UpdateMultitapTexture();
			material.DOFloat(0f, "_FlashColor", (float)conductor.crotchetAtStart * 0.5f).From(1.4f);
			if (!flag7)
			{
				return this;
			}
		}
		movingToNext = next;
		if (controller.gameworld)
		{
			hitMargin2 = (scrFloor2.grade = hitMargin);
			if (player.midspinInfiniteMargin || ((player.auto || flag) && !RDC.useOldAuto))
			{
				hitMargin2 = HitMargin.Perfect;
			}
			if (flag)
			{
				hitMargin2 = HitMargin.Auto;
			}
			player.marginTracker.AddHit(hitMargin2);
			if (flag)
			{
				hitMargin2 = HitMargin.Perfect;
			}
			player.ClearMisses();
			player.tapsOnThisFloor = 0;
			if (scrFloor2.hasConditionalChange)
			{
				controller.conditionalFloor = scrFloor2;
			}
			if (conditionalFloor != null)
			{
				HashSet<ffxPlusBase> hashSet = hitMargin2 switch
				{
					HitMargin.Perfect => conditionalFloor.perfectEffects, 
					HitMargin.EarlyPerfect => conditionalFloor.earlyPerfectEffects, 
					HitMargin.LatePerfect => conditionalFloor.latePerfectEffects, 
					HitMargin.VeryLate => conditionalFloor.veryLateEffects, 
					HitMargin.VeryEarly => conditionalFloor.veryEarlyEffects, 
					_ => null, 
				};
				if (hashSet != null)
				{
					foreach (ffxPlusBase item2 in hashSet)
					{
						item2.StartEffectWithOffset(movingToNext);
					}
				}
			}
		}
		if (controller.isPuzzleRoom)
		{
			foreach (scrPlanet planet in planetarySystem.planetList)
			{
				if (!(planet == movingToNext))
				{
					planet.iFrames = 15f / (conductor.bpm * (float)planetarySystem.speed) / conductor.song.pitch;
				}
			}
		}
		if ((bool)scrFloor2 && scrFloor2.midSpin && scrFloor2.numPlanets > 2)
		{
			movingToNext = prev;
		}
		MoveToNextFloor(scrFloor2, (float)num, hitMargin2);
		if (ADOBase.levelSelect != null)
		{
			ADOBase.levelSelect.PlanetLandedOnFloor(movingToNext, scrFloor2);
		}
		if (controller.gameworld && !scrFloor2.hideJudgment)
		{
			HitMargin hitMargin3 = hitMargin2;
			if (flag5)
			{
				hitMargin3 = HitMargin.Multipress;
			}
			if (!player.midspinInfiniteMargin)
			{
				ADOBase.playerManager.hitTextManager.ShowHitText(hitMargin3, movingToNext, flag5 ? 0f : ((float)(targetExitAngle - angle)));
			}
		}
		return movingToNext;
	}

	private void MoveToNextFloor(scrFloor floor, float exitAngle, HitMargin hitMargin)
	{
		_cosmeticAngleTween?.Kill();
		movingToNext = next;
		if (floor.midSpin && floor.numPlanets > 2)
		{
			movingToNext = prev;
		}
		bool flag = currfloor.holdLength > -1;
		if (!currfloor.stickToFloor && currfloor.holdLength > -1 && controller.state == States.PlayerControl)
		{
			targetPosition = currfloor.nextfloor.startPos - scrController.instance.tileSize * currfloor.nextfloor.radiusScale * ClockwiseAngleToVector(currfloor.exitangle);
			holdOffsetPos = targetPosition - currfloor.startPos;
			if (controller.gameworld || (!controller.gameworld && currfloor.nextfloor.freeroam))
			{
				base.transform.position = storedPosition + holdOffsetPos;
			}
			else
			{
				base.transform.position = storedPosition;
			}
			handledPositionAlready = true;
		}
		if (currfloor.holdLength > -1 && controller.state == States.PlayerControl)
		{
			addBob = Vector3.zero;
			camy.SetHoldOffset(Vector3.zero);
			currfloor.holdCompletion = 1f;
			currfloor.holdCompletionEased = 1f;
			currfloor.holdRenderer.UpdateColor(1f);
		}
		if (floor != null && floor.freeroam)
		{
			controller.gameworld = false;
			if (!currfloor.freeroam)
			{
				if (player.lockInput < 0.05f)
				{
					player.LockInput(0.05f);
				}
				foreach (scrFloor listFloor in ADOBase.lm.listFloors)
				{
					if (listFloor.seqID < floor.seqID)
					{
						listFloor.ToggleCollider(collEn: false);
					}
				}
				floor.ToggleCollider(collEn: false);
				if ((bool)controller.errorMeter)
				{
					controller.errorMeter.wrapperRectTransform.gameObject.SetActive(value: false);
				}
			}
			else if (floor.isSwirl)
			{
				if (!controller.isPuzzleRoom)
				{
					foreach (scrFloor item2 in ADOBase.lm.listFreeroam[floor.freeroamRegion])
					{
						item2.isCCW = !item2.isCCW;
						if (item2.isSwirl)
						{
							item2.UpdateIconSprite();
						}
					}
				}
				else
				{
					foreach (scrFloor listFloor2 in PuzzleRoom.instance.listFloors)
					{
						listFloor2.isCCW = !listFloor2.isCCW;
						if (listFloor2.isSwirl)
						{
							listFloor2.UpdateIconSprite();
						}
					}
				}
			}
		}
		if (controller.gameworld && currfloor.freeroamGenerated && !floor.freeroamGenerated && (bool)controller.errorMeter)
		{
			controller.errorMeter.wrapperRectTransform.gameObject.SetActive(value: true);
		}
		if (floor != null && floor.nextfloor != null && floor.isSwirl && floor.numPlanets > 2 && floor.prevfloor.numPlanets > 2)
		{
			movingToNext.swirlTween = ((!foolSwirl) ? 1 : 0);
			float num = 1f;
			if (floor.nextfloor != null)
			{
				num = (float)floor.nextfloor.entryTimePitchAdj - (float)floor.entryTimePitchAdj;
			}
			else if (floor.prevfloor != null)
			{
				num = (float)floor.entryTimePitchAdj - (float)floor.prevfloor.entryTimePitchAdj;
			}
			if (floor.freeroam)
			{
				movingToNext.swirlTween = 0.5f;
				num = (float)(conductor.crotchetAtStart * (double)floor.speed) * 0.5f * ((float)floor.numPlanets * 0.5f);
			}
			DOTween.To(() => movingToNext.swirlTween, delegate(float x)
			{
				movingToNext.swirlTween = x;
			}, foolSwirl ? 1 : 0, 0.5f * num).SetEase(Ease.OutSine);
		}
		if (floor != null && floor.nextfloor != null && floor.holdLength > -1 && floor.numPlanets > 2)
		{
			movingToNext.mpHoldTween = 0f;
			float num2 = 1f;
			if (floor.nextfloor != null)
			{
				num2 = (float)floor.nextfloor.entryTimePitchAdj - (float)floor.entryTimePitchAdj;
			}
			else if (floor.prevfloor != null)
			{
				num2 = (float)floor.entryTimePitchAdj - (float)floor.prevfloor.entryTimePitchAdj;
			}
			float duration = Mathf.Min(num2 * 0.5f, (float)(conductor.crotchetAtStart * (double)floor.speed) / conductor.song.pitch);
			DOTween.To(() => movingToNext.mpHoldTween, delegate(float x)
			{
				movingToNext.mpHoldTween = x;
			}, 1f, duration).SetEase(Ease.OutExpo);
		}
		if (planetarySystem.planetList != null && floor.numPlanets != currfloor.numPlanets)
		{
			planetarySystem.SetNumPlanets(floor.numPlanets);
			foreach (PlanetRenderer item3 in scrController.instance.dummyPlanets.FindAll((PlanetRenderer x) => x.transform.parent == floor.transform))
			{
				if (item3 != null)
				{
					item3.Destroy();
				}
			}
			if (floor.multiplanetLine != null)
			{
				LineRenderer l = floor.multiplanetLine;
				DOTween.To(() => l.startColor.a, delegate(float x)
				{
					l.startColor = l.startColor.WithAlpha(x);
				}, 0f, 0.5f).SetUpdate(isIndependentUpdate: true);
				DOTween.To(() => l.endColor.a, delegate(float x)
				{
					l.endColor = l.endColor.WithAlpha(x);
				}, 0f, 0.5f).SetUpdate(isIndependentUpdate: true);
			}
		}
		movingToNext.currfloor = floor;
		player.onHit(floor);
		if (GCS.bb)
		{
			BBManager.instance.LightUpFloor(floor.seqID);
		}
		controller.currentSeqID = floor.seqID;
		double num3 = scrMisc.GetInverseAnglePerBeatMultiplanet(floor.numPlanets) * (double)((!floor.isCCW) ? 1 : (-1));
		if ((bool)floor.prevfloor && floor.prevfloor.midSpin && floor.numPlanets > 2)
		{
			num3 -= 2.0 * scrMisc.GetInverseAnglePerBeatMultiplanet(floor.prevfloor.numPlanets) * (double)((!floor.prevfloor.isCCW) ? 1 : (-1));
		}
		movingToNext.snappedLastAngle = scrMisc.mod(exitAngle + MathF.PI, 6.2831854820251465) + num3;
		movingToNext.targetExitAngle = movingToNext.snappedLastAngle + floor.angleLength * (double)((!floor.isCCW) ? 1 : (-1));
		if (floor.radiusScale != currfloor.radiusScale && planetarySystem.planetList != null)
		{
			foreach (scrPlanet planet in planetarySystem.planetList)
			{
				planet.cosmeticRadius = scrController.instance.tileSize * floor.radiusScale;
			}
		}
		if (GCS.d_stationary)
		{
			movingToNext.angle = movingToNext.snappedLastAngle;
		}
		player.actualLastHit = conductor.songposition_minusi;
		player.lastHit += ((double)exitAngle - snappedLastAngle) * (double)(planetarySystem.isCW ? 1 : (-1)) / 3.1415927410125732 * conductor.crotchetAtStart / planetarySystem.speed;
		if (floor.isportal)
		{
			controller.OnLandOnPortal(movingToNext, floor.levelnumber, floor.arguments);
		}
		if (controller.gameworld || floor.freeroam || floor.freeroamGenerated)
		{
			planetarySystem.speed = floor.speed;
		}
		if (!floor.hasLit)
		{
			camy.RotateSmooth(floor.rotatecamera);
		}
		planetarySystem.isCW = !floor.isCCW;
		controller.rotationEase = floor.planetEase;
		controller.rotationEaseParts = floor.planetEaseParts;
		controller.rotationEasePartBehavior = floor.planetEasePartBehavior;
		if (movingToNext.currfloor.freeroam && flag)
		{
			storedPosition = movingToNext.currfloor.transform.position;
			movingToNext.transform.position = storedPosition;
		}
		else if ((!flag && !controller.gameworld) || controller.gameworld)
		{
			movingToNext.MoveOneUnitInDirection(exitAngle);
		}
		else
		{
			movingToNext.transform.position = storedPosition;
		}
		if (!floor.freeroam || (floor.freeroam && floor.freeroamGenerated && !floor.unstable))
		{
			floor.LightUp(hitMargin);
			floor.SetToRandomColor();
		}
		ffxPlusBase[] components = floor.GetComponents<ffxPlusBase>();
		if (ADOBase.isFreeroamScene || ADOBase.isCLS)
		{
			ffxPlusBase[] array = components;
			foreach (ffxPlusBase item in array)
			{
				movingToNext.DoFFX(item);
			}
		}
		else
		{
			ffxPlusBase[] array = components;
			foreach (ffxPlusBase ffxPlusBase2 in array)
			{
				if (ffxPlusBase2.runOnHit && !ffxPlusBase2.triggered)
				{
					movingToNext.DoFFX(ffxPlusBase2);
				}
			}
		}
		HandlePause(floor, movingToNext);
		if (floor.freeroam && !floor.freeroamGenerated)
		{
			Vector3 position = movingToNext.transform.position;
			Collider2D[] array2 = Physics2D.OverlapPointAll(new Vector2(position.x, position.y), 1 << LayerMask.NameToLayer("Floor"));
			bool flag2 = false;
			if (array2.Length != 0)
			{
				foreach (Collider2D collider2D in array2)
				{
					scrFloor component = collider2D.GetComponent<scrFloor>();
					if (component == null)
					{
						component = collider2D.transform.parent.GetComponent<scrFloor>();
					}
					if (component == null)
					{
						continue;
					}
					flag2 = true;
					if (!component.isLandable)
					{
						continue;
					}
					if (component.isSwirl)
					{
						foreach (scrFloor item4 in ADOBase.lm.listFreeroam[component.freeroamRegion])
						{
							item4.isCCW = !item4.isCCW;
							if (item4.isSwirl)
							{
								item4.UpdateIconSprite();
							}
						}
						planetarySystem.isCW = !component.isCCW;
					}
					movingToNext.currfloor = component;
					component.LightUp(hitMargin);
					component.SetToRandomColor();
				}
			}
			if (!flag2)
			{
				controller.gameworld = true;
				p.player.DieByHitbox(RDString.Get("neoCosmos.invalidFreeroam"));
			}
		}
		if (floor.nextfloor == null)
		{
			DOTween.To(() => movingToNext.endingTween, delegate(float x)
			{
				movingToNext.endingTween = x;
			}, 1f, 0.3f).SetEase(Ease.OutSine);
		}
		controller.multipressPenalty = GetMultipressPenalty(floor);
		bool multipressAndHasPressedFirstPress = player.keyTimes.Count != 0;
		controller.multipressAndHasPressedFirstPress = multipressAndHasPressedFirstPress;
	}

	private float GetSnappedNextAngle()
	{
		float interval = controller.freeroamAngleInterval * (MathF.PI / 180f);
		float offset = controller.freeroamAngleOffset * (MathF.PI / 180f);
		return SnapAngleCardinal(angle, interval, offset);
	}

	public void MovePlanet(Vector2 position, bool instant = false, scrFloor floor = null)
	{
		if (instant)
		{
			base.transform.MoveXY(position);
		}
		else
		{
			base.transform.DOMove(position, 0.3f).SetEase(Ease.OutCubic);
		}
		if (instant)
		{
			camy.ViewObjectInstant(base.transform);
		}
		else
		{
			camy.Refocus(base.transform);
		}
		currfloor = ((floor == null) ? RDUtils.GetFloorAtPosition(position) : floor);
		if ((bool)movingToNext)
		{
			movingToNext = next;
		}
		MoveToNextFloor(currfloor, GetSnappedNextAngle(), HitMargin.Perfect);
	}

	public void MovePlanetReunify(scrPlanet leadingPlanet, Vector2 position)
	{
		base.transform.DOMove(position, 0.3f).SetEase(Ease.OutCubic);
		MoveToNextFloor(leadingPlanet.currfloor, leadingPlanet.GetSnappedNextAngle(), HitMargin.Perfect);
	}

	private bool GetMultipressPenalty(scrFloor floor)
	{
		if (!controller.gameworld)
		{
			return false;
		}
		if ((bool)floor && floor.midSpin)
		{
			floor = floor.nextfloor;
		}
		if (!floor)
		{
			return false;
		}
		scrFloor nextfloor = floor.nextfloor;
		if ((bool)nextfloor && nextfloor.midSpin)
		{
			nextfloor = nextfloor.nextfloor;
		}
		if (!nextfloor)
		{
			return false;
		}
		scrFloor nextfloor2 = nextfloor.nextfloor;
		if ((bool)nextfloor2 && nextfloor2.midSpin)
		{
			nextfloor2 = nextfloor2.nextfloor;
		}
		if (!nextfloor2)
		{
			return false;
		}
		scrFloor nextfloor3 = nextfloor2.nextfloor;
		if ((bool)nextfloor3 && nextfloor3.midSpin)
		{
			nextfloor3 = nextfloor3.nextfloor;
		}
		if (!nextfloor3)
		{
			return false;
		}
		double num = 0.5253441007807851;
		double num2 = 0.2635447150096297;
		double angleMoved = scrMisc.GetAngleMoved(floor.entryangle, floor.exitangle, !floor.isCCW);
		double angleMoved2 = scrMisc.GetAngleMoved(nextfloor.entryangle, nextfloor.exitangle, !nextfloor.isCCW);
		double angleMoved3 = scrMisc.GetAngleMoved(nextfloor2.entryangle, nextfloor2.exitangle, !nextfloor2.isCCW);
		double angleMoved4 = scrMisc.GetAngleMoved(nextfloor3.entryangle, nextfloor3.exitangle, !nextfloor3.isCCW);
		if (angleMoved < num && angleMoved2 > num)
		{
			return false;
		}
		if (angleMoved < num && angleMoved2 < num && angleMoved3 > num)
		{
			return false;
		}
		if (angleMoved < num2 && angleMoved2 < num2 && angleMoved3 < num2 && angleMoved4 > num2)
		{
			return false;
		}
		return true;
	}

	public void DoFFX(ffxPlusBase item)
	{
		if (item.enabled && (!GCS.lofiVersion || !controller.gameworld) && item.IsAllowedByVisualSettings(visualQuality, controller.visualEffects))
		{
			item.StartEffect(this);
		}
	}

	public void ScrubToFloorNumber(int floorNum, float? windbackTime = null, bool movePos = true, bool doingRevive = false)
	{
		if (floorNum > scrLevelMaker.instance.listFloors.Count)
		{
			Debug.LogError("Trying to scrub past last tile. Is CustomCheckpoint ticked by accident in LevelSwitcher?");
		}
		if (scrController.instance != null)
		{
			int i;
			for (i = 1; i <= floorNum; i++)
			{
				foreach (PlanetRenderer item in scrController.instance.dummyPlanets.FindAll((PlanetRenderer x) => x.attachedDummyFloor == i))
				{
					if (item != null && item.ring.enabled)
					{
						item.Destroy();
					}
				}
				foreach (LineRenderer item2 in scrController.instance.multiPlanetLines.FindAll((LineRenderer x) => x.transform.parent == scrLevelMaker.instance.listFloors[i].transform))
				{
					if (item2 != null)
					{
						item2.enabled = false;
					}
				}
			}
		}
		scrFloor scrFloor2 = (currfloor = scrLevelMaker.instance.listFloors[floorNum]);
		if (!doingRevive)
		{
			controller.currentSeqID = floorNum;
			scrFloor scrFloor3 = null;
			for (int num = floorNum; num > 0; num--)
			{
				scrFloor scrFloor4 = ADOBase.lm.listFloors[num];
				if (scrFloor4.hasConditionalChange)
				{
					scrFloor3 = scrFloor4;
					break;
				}
			}
			if (scrFloor3 != null)
			{
				controller.conditionalFloor = scrFloor3;
			}
		}
		if (!dummyPlanets)
		{
			planetRenderer.ringComp.Switch(chosen: true);
		}
		if (movePos)
		{
			if (scrFloor2.stickToFloor)
			{
				base.transform.position = scrFloor2.transform.position;
			}
			else
			{
				base.transform.position = scrFloor2.startPos;
			}
		}
		double num2 = 0.0;
		num2 = scrMisc.GetInverseAnglePerBeatMultiplanet(scrFloor2.numPlanets) * (double)((!scrFloor2.isCCW) ? 1 : (-1));
		if ((bool)scrFloor2.prevfloor && scrFloor2.prevfloor.midSpin && scrFloor2.numPlanets > 2)
		{
			num2 -= 1.0 * scrMisc.GetInverseAnglePerBeatMultiplanet(scrFloor2.prevfloor.numPlanets) * (double)((!scrFloor2.prevfloor.isCCW) ? 1 : (-1));
		}
		snappedLastAngle = scrFloor2.entryangle + num2;
		planetarySystem.isCW = !scrFloor2.isCCW;
		planetarySystem.speed = scrFloor2.speed;
		controller.rotationEase = scrFloor2.planetEase;
		targetExitAngle = snappedLastAngle + scrFloor2.angleLength * (double)((!scrFloor2.isCCW) ? 1 : (-1));
		storedPosition = scrFloor2.transform.position;
		if (scrFloor2.nextfloor == null)
		{
			DOTween.To(() => endingTween, delegate(float x)
			{
				endingTween = x;
			}, 1f, 0.3f).SetEase(Ease.OutSine);
		}
		double num3 = 0.0;
		double timeDiff = 0.0;
		if (windbackTime.HasValue)
		{
			double num4 = 0.0;
			num4 = scrFloor2.entryTimePitchAdj - (double)windbackTime.Value;
			timeDiff = scrFloor2.entryTime - scrFloor2.entryTime;
			num3 = scrMisc.TimeToAngleInRad(scrFloor2.entryTimePitchAdj - num4, conductor.bpm * scrFloor2.speed, conductor.song.pitch);
			snappedLastAngle -= num3 * (double)(planetarySystem.isCW ? 1 : (-1));
		}
		Update_RefreshAngles();
		if (!ADOBase.isScnGame)
		{
			timeDiff = 0.0;
		}
		HandlePause(currfloor, this, num3 * (double)(planetarySystem.isCW ? 1 : (-1)), timeDiff);
		if (currfloor.radiusScale == 1f || planetarySystem.planetList == null)
		{
			return;
		}
		foreach (scrPlanet planet in planetarySystem.planetList)
		{
			planet.cosmeticRadius = scrController.instance.tileSize * currfloor.radiusScale;
		}
	}

	private Vector3 SnappedCardinalDirection(float snappedAngle, float interval = MathF.PI / 2f, float offset = 0f)
	{
		Vector3 zero = Vector3.zero;
		int x = Mathf.RoundToInt((snappedAngle + offset) / interval);
		int m = Mathf.RoundToInt(MathF.PI * 2f / interval);
		float angleRadians = 0f - ((float)scrMisc.ModInt(x, m) - offset) * interval + MathF.PI / 2f;
		zero = AngleToVector(angleRadians);
		return base.transform.position + zero * scrController.instance.tileSize * currfloor.radiusScale;
	}

	[Obsolete("This method isn't used!!")]
	private void SnapToCardinalDirection(float snappedAngle)
	{
		int num = Mathf.RoundToInt(snappedAngle / (MathF.PI / 2f));
		Vector3 vector = (num % 4) switch
		{
			0 => Vector3.up, 
			1 => Vector3.right, 
			2 => Vector2.down, 
			3 => Vector2.left, 
			_ => Vector3.zero, 
		};
		base.transform.position = prev.transform.position + vector;
	}

	private void MoveOneUnitInDirection(float snappedAngle)
	{
		float num = snappedAngle;
		if (currfloor.stickToFloor && currfloor != null)
		{
			num -= (currfloor.transform.rotation.eulerAngles - currfloor.startRot).z * (MathF.PI / 180f);
		}
		if (currfloor != null && currfloor.midSpin && currfloor.numPlanets > 2)
		{
			num += (float)scrMisc.GetInverseAnglePerBeatMultiplanet(currfloor.numPlanets) * (currfloor.isCCW ? (-1f) : 1f);
		}
		Vector3 vector = ClockwiseAngleToVector(num) * radius * currfloor.radiusScale;
		if (currfloor.midSpin && currfloor.numPlanets > 2)
		{
			lastMovedPlanet = next;
		}
		else
		{
			lastMovedPlanet = prev;
		}
		base.transform.position = lastMovedPlanet.transform.position + vector;
		storedPosition = base.transform.position;
	}

	private float SnapAngleCardinal(double angle, float interval = MathF.PI / 2f, float offset = 0f)
	{
		return (float)Mathf.RoundToInt((float)((angle + (double)offset) / (double)interval)) * interval - offset;
	}

	public void Die(float explosionSize = 1f)
	{
		dead = true;
		hittable = false;
		if (!(planetarySystem.chosenPlanet.currfloor?.GetComponent<ffxTractorbeamFloors>() != null) || controller.visualQuality != VisualQuality.High)
		{
			planetRenderer.Explode(explosionSize);
		}
	}

	public void Destroy(bool keepRing = false)
	{
		hittable = false;
		onlyRing = keepRing;
		planetRenderer.Destroy(keepRing);
	}

	public void TweenCosmeticAngle(float cosmeticAngleOffset)
	{
		cosmeticAngle = cosmeticAngleOffset;
		_cosmeticAngleTween = DOTween.To(() => cosmeticAngle, delegate(float x)
		{
			cosmeticAngle = x;
		}, 0f, (float)(conductor.crotchetAtStart / (double)conductor.song.pitch / planetarySystem.speed)).OnKill(delegate
		{
			cosmeticAngle = 0f;
		}).SetEase(Ease.OutSine);
	}

	public void SetSnappedLastAngle(double newVal)
	{
		snappedLastAngle = newVal;
	}

	public void SetTargetExitAngle(double newVal)
	{
		targetExitAngle = newVal;
	}

	public void HandlePause(scrFloor floor, scrPlanet p, double angleDiff = 0.0, double timeDiff = 0.0)
	{
		if (!floor.freeroam && !(floor.extraBeats <= 0f))
		{
			lockTime = (float)(conductor.crotchetAtStart / (double)floor.speed) * (floor.extraBeats + 0.2f) + (float)timeDiff;
			if (floor.nextfloor.entryTime - floor.entryTime - (double)lockTime < 0.05000000074505806)
			{
				lockTime = 0f;
			}
			States states = (States)(object)controller.stateMachine.GetState();
			if (lockTime > 0f && states == States.PlayerControl)
			{
				player.LockInput(lockTime);
			}
			p.angle = p.snappedLastAngle - angleDiff + (conductor.songposition_minusi - player.lastHit) / conductor.crotchetAtStart * Math.PI * planetarySystem.speed * (double)(planetarySystem.isCW ? 1 : (-1));
			int num = ((!floor.isCCW) ? 1 : (-1));
			double num2 = scrMisc.mod(p.angle, Math.PI * 2.0);
			player.lastHit = floor.entryTime;
			double num3 = (Math.PI - floor.angleLength) * (double)num;
			double num4 = floor.nextfloor.entryangle - Math.PI * (double)num;
			double num5 = num4 - Math.PI * (double)(floor.extraBeats + 1f) * (double)num - angleDiff + num3;
			p.SetTargetExitAngle(num4);
			p.SetSnappedLastAngle(num5);
			p.angle = p.snappedLastAngle + (conductor.songposition_minusi - player.lastHit) / conductor.crotchetAtStart * Math.PI * 0.5 * planetarySystem.speed * (double)(planetarySystem.isCW ? 1 : (-1));
			double num6 = (p.angle = scrMisc.mod(p.angle, Math.PI * 2.0));
			double num7 = (double)floor.angleCorrectionType * (num2 - num6) * (double)((!floor.isCCW) ? 1 : (-1)) * (double)((num6 < num2) ? 1 : (-1));
			p.TweenCosmeticAngle(0f - (float)num7);
		}
	}

	public double GetNearestIntervalToTime(double time, float beatInterval = 0.5f)
	{
		return (double)Mathf.Round((float)((time - conductor.crotchetAtStart) * (double)(conductor.bpm / 60f)) * (1f / beatInterval * (float)planetarySystem.speed)) / (double)(1f / beatInterval * (float)planetarySystem.speed) * (double)(60f / conductor.bpm) + conductor.crotchetAtStart;
	}

	public scrMissIndicator MarkMiss()
	{
		scrMissIndicator component = UnityEngine.Object.Instantiate(ADOBase.gc.missIndicator, next.transform.position, Quaternion.identity).GetComponent<scrMissIndicator>();
		if (scrController.coopMode)
		{
			component.sprite.color = planetRenderer.planetColor.ToRealColor();
		}
		return component;
	}

	public scrMissIndicator MarkFail()
	{
		scrMissIndicator component = UnityEngine.Object.Instantiate(ADOBase.gc.failIndicator, next.transform.position, Quaternion.identity).GetComponent<scrMissIndicator>();
		if (scrController.coopMode)
		{
			component.sprite.color = planetRenderer.planetColor.ToRealColor();
		}
		return component;
	}

	public void SyncPlanetWithAnother(scrPlanet planet)
	{
		angle = planet.angle;
		cachedAngle = planet.cachedAngle;
		player.lastHit = planet.player.lastHit;
		player.actualLastHit = planet.player.actualLastHit;
		SetSnappedLastAngle(planet.snappedLastAngle);
		SetTargetExitAngle(planet.targetExitAngle);
	}

	private float Modulo(float a, float b)
	{
		return a - Mathf.Floor(a / b) * b;
	}

	private double Modulo(double a, double b)
	{
		return a - Math.Floor(a / b) * b;
	}

	private Vector3 AngleToVector(float angleRadians)
	{
		return new Vector3(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians), 0f);
	}

	private Vector3 AngleToVector(double angleRadians)
	{
		return new Vector3((float)Math.Cos(angleRadians), (float)Math.Sin(angleRadians), 0f);
	}

	private Vector3 ClockwiseAngleToVector(float angleRadians)
	{
		return new Vector3(Mathf.Sin(angleRadians), Mathf.Cos(angleRadians), 0f);
	}

	private Vector3 ClockwiseAngleToVector(double angleRadians)
	{
		return new Vector3((float)Math.Sin(angleRadians), (float)Math.Cos(angleRadians), 0f);
	}
}
