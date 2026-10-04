using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class scrPlayer : ADOBase
{
	public PlanetarySystem planetarySystem;

	public scrFailBar failBar;

	[NonSerialized]
	public bool alive;

	[NonSerialized]
	public bool responsive = true;

	[NonSerialized]
	public float lockInput;

	[NonSerialized]
	public int playerID;

	[NonSerialized]
	public List<scrMissIndicator> missesOnCurrFloor = new List<scrMissIndicator>();

	[NonSerialized]
	public double lastHit;

	[NonSerialized]
	public double actualLastHit;

	private int[] lastHitFrame = new int[Enum.GetValues(typeof(InputEventState)).Length];

	private int ignoreInputCount;

	[NonSerialized]
	public bool midspinInfiniteMargin;

	[NonSerialized]
	public Dictionary<object, int> keyFrequency = new Dictionary<object, int>();

	[NonSerialized]
	public List<double> keyTimes = new List<double>();

	[NonSerialized]
	public List<AnyKeyCode> holdKeys = new List<AnyKeyCode>();

	private bool validInputWasReleasedThisFrame;

	[NonSerialized]
	public int keyTotal;

	[NonSerialized]
	public int consecMultipressCounter;

	[NonSerialized]
	public int keyLimiterOverCounter;

	[NonSerialized]
	public int tapsOnThisFloor;

	public static bool shouldReplaceCamyToPos;

	public static Vector3 overrideCamyToPos;

	private Vector2 cachedCamyToPos;

	[NonSerialized]
	public float invincibilityTimer;

	[NonSerialized]
	public float revivalGracePeriod;

	[NonSerialized]
	public bool missCooldownActive;

	[NonSerialized]
	public float missCooldownTime = 2f;

	public Action<scrFloor> onHit = delegate
	{
	};

	[NonSerialized]
	public bool isReunifying;

	private Dictionary<AnyKeyCode, float> downKeysDuration = new Dictionary<AnyKeyCode, float>();

	private bool __nextTileIsHoldCached;

	public scrMarginTracker marginTracker => scrMistakesManager.marginTrackers[playerID];

	private scrPlanet chosenPlanet => planetarySystem.chosenPlanet;

	public scrFloor currFloor => chosenPlanet.currfloor;

	public bool holding => holdKeys.Count != 0;

	public bool invincible => invincibilityTimer > 0f;

	public bool doingRevivalCountdown => invincibilityTimer > revivalGracePeriod;

	public bool auto
	{
		get
		{
			if (!RDC.auto)
			{
				return invincibilityTimer > 0f;
			}
			return true;
		}
	}

	private bool touchEnabled
	{
		get
		{
			if (!ADOBase.isMobile)
			{
				if (ADOBase.isSwitch)
				{
					return !scrController.coopMode;
				}
				return false;
			}
			return true;
		}
	}

	private double _minAngleMargin => scrMisc.GetAdjustedAngleBoundaryInDeg(HitMarginGeneral.Counted, (float)((double)ADOBase.conductor.bpm * ADOBase.controller.playerOne.planetarySystem.speed), ADOBase.conductor.song.pitch) * 0.01745329238474369;

	private float _marginScale
	{
		get
		{
			if (!(currFloor.nextfloor != null))
			{
				return 1f;
			}
			return (float)currFloor.nextfloor.marginScale;
		}
	}

	private double _holdMargin => 1.0 - _minAngleMargin * (double)_marginScale / currFloor.angleLength;

	private bool _nextTileIsAuto
	{
		get
		{
			if (currFloor.nextfloor != null)
			{
				return currFloor.nextfloor.auto;
			}
			return false;
		}
	}

	private bool _nextTileIsHold
	{
		get
		{
			if (!__nextTileIsHoldCached)
			{
				if ((bool)currFloor.nextfloor)
				{
					return currFloor.nextfloor.holdLength > -1;
				}
				return false;
			}
			return true;
		}
	}

	public void Init(int id)
	{
		base.gameObject.SetActive(value: true);
		playerID = id;
		failBar = new scrFailBar(this);
		planetarySystem.Init();
	}

	public void UnlockInput()
	{
		lockInput = 0f;
		responsive = true;
	}

	public void LockInput(float fSecs)
	{
		if (!(fSecs <= 0f))
		{
			lockInput = fSecs;
			responsive = false;
		}
	}

	public void UpdateLockInput()
	{
		if (!(lockInput <= 0f) && !responsive)
		{
			lockInput -= Time.deltaTime * ADOBase.conductor.song.pitch;
			if (!(lockInput > 0f))
			{
				responsive = true;
				lockInput = 0f;
			}
		}
	}

	public void Rewind()
	{
		planetarySystem.chosenPlanet = planetarySystem.planetRed;
		planetarySystem.isCW = true;
		foreach (scrPlanet planet in planetarySystem.planetList)
		{
			planet.Rewind();
		}
		failBar.Rewind();
		keyTimes.Clear();
		keyFrequency.Clear();
		keyTotal = 0;
		consecMultipressCounter = 0;
		alive = true;
	}

	public void Revive(int floorID, scrPlayer helperPlayer = null)
	{
		Rewind();
		scrFloor scrFloor2 = ADOBase.lm.listFloors[floorID];
		foreach (scrPlanet planet in planetarySystem.planetList)
		{
			planet.cosmeticRadius = scrController.instance.tileSize * scrFloor2.radiusScale;
		}
		planetarySystem.ScrubToFloorNumber(floorID, null, movePos: true, doingRevive: true);
		scrPlanet obj = planetarySystem.chosenPlanet;
		obj.planetRenderer.ringComp.enabled = false;
		obj.planetRenderer.ringComp.Switch(chosen: false, instant: true);
		lastHit = scrFloor2.entryTime;
		revivalGracePeriod = 2f;
		invincibilityTimer = revivalGracePeriod;
		ADOBase.playerManager.deadPlayersQueue.Remove(this);
		marginTracker.RegisterDeadTiles(floorID);
		VirtualAvatarCanvas.instance?.Revive(playerID);
	}

	private void Update()
	{
		failBar.Update();
		InvincibilityUpdate();
	}

	private void Start()
	{
		lockInput = 0f;
		if (!ADOBase.isLevelEditor)
		{
			planetarySystem.LoadPlanetColors(playerID);
		}
	}

	private void InvincibilityUpdate()
	{
		if (invincibilityTimer <= 0f)
		{
			return;
		}
		invincibilityTimer -= Time.deltaTime;
		bool flag = invincibilityTimer % 0.25f < 0.125f;
		foreach (scrPlanet allPlanet in planetarySystem.allPlanets)
		{
			allPlanet.planetRenderer.transform.localScale = ((doingRevivalCountdown && flag) ? Vector3.zero : Vector3.one);
			allPlanet.planetRenderer.ringComp.enabled = !doingRevivalCountdown;
		}
	}

	public bool OnDamage(bool multipress = false, bool applyMultipressDamage = false, bool skipDamage = false, HitMargin hitMargin = HitMargin.TooEarly)
	{
		bool flag = false;
		double bpm = (double)ADOBase.conductor.bpm * ADOBase.controller.playerOne.planetarySystem.speed * (double)ADOBase.conductor.song.pitch;
		double num = scrMisc.AngleToTime(scrMisc.GetAngleMoved(currFloor.entryangle, currFloor.exitangle, !currFloor.isCCW), bpm);
		if (multipress && !applyMultipressDamage && num > 0.019999999552965164)
		{
			consecMultipressCounter++;
		}
		if (consecMultipressCounter > 8)
		{
			consecMultipressCounter = 4;
			Die(overload: true, multipress: true);
			flag = true;
		}
		if (!multipress || (multipress && applyMultipressDamage))
		{
			if (!skipDamage)
			{
				flag |= failBar.Damage(multipress);
			}
			if (ADOBase.controller.gameworld && (!ADOBase.controller.noFail || !flag) && !currFloor.hideJudgment)
			{
				missesOnCurrFloor.Add(chosenPlanet.MarkMiss());
			}
		}
		if (GCS.hitMarginLimit == HitMarginLimit.PurePerfectOnly && hitMargin != HitMargin.Perfect && !ADOBase.controller.noFail && !ADOBase.controller.currFloor.freeroam)
		{
			string failMessage = RDString.Get("status.purePerfectOnlyExplainer", new Dictionary<string, object> { 
			{
				"judgement",
				RDString.Get($"HitMargin.{hitMargin}")
			} });
			Die(overload: false, multipress: false, failMessage);
			flag = true;
		}
		return flag;
	}

	public void DieByHitbox(string failMessage = "")
	{
		Die(overload: false, multipress: false, failMessage, hitbox: true);
	}

	public void Die(bool overload = false, bool multipress = false, string failMessage = "", bool hitbox = false)
	{
		bool flag = currFloor.nextfloor != null && currFloor.nextfloor.auto;
		if (!hitbox)
		{
			if (((auto || flag) && !RDC.useOldAuto) || (RDC.debug && !overload) || (!ADOBase.controller.gameworld && !currFloor.freeroam))
			{
				return;
			}
			bool flag2 = ADOBase.controller.noFail || (currFloor.isSafe && GCS.hitMarginLimit == HitMarginLimit.None) || missCooldownActive;
			bool flag3 = !missCooldownActive;
			int num = scrLivesCounter.instance?.lives ?? 0;
			if (ADOBase.isExpo && !flag2)
			{
				_ = scrLivesCounter.instance;
				num--;
				if (num > 0)
				{
					missCooldownActive = true;
					DOTween.Sequence().AppendInterval(missCooldownTime).AppendCallback(delegate
					{
						missCooldownActive = false;
					});
				}
				scrLivesCounter.instance.SetLives(num);
			}
			if (flag2 || num > 0)
			{
				if (overload)
				{
					if (!currFloor.hideJudgment && flag3)
					{
						chosenPlanet.MarkFail()?.BlinkForSeconds(3f);
					}
					return;
				}
				scrFloor nextfloor = currFloor.nextfloor;
				if ((!nextfloor || tapsOnThisFloor + 1 == nextfloor.tapsNeeded) && !currFloor.hideJudgment && flag3)
				{
					chosenPlanet.MarkFail()?.BlinkForSeconds(3f);
				}
				if (!midspinInfiniteMargin)
				{
					ADOBase.controller.noFailInfiniteMargin = true;
					Hit();
					ADOBase.controller.noFailInfiniteMargin = false;
				}
				return;
			}
		}
		foreach (scrMissIndicator item in missesOnCurrFloor)
		{
			item.StartBlinking();
		}
		if (!GCS.lofiVersion && ADOBase.playerManager.GetActivePlayers().Count == 1)
		{
			scrFlash.OnDamage();
		}
		PlanetarySystem.DeathAnimation deathAnimation = ((!ADOBase.controller.instantExplode) ? PlanetarySystem.DeathAnimation.CrumbleAndExplode : PlanetarySystem.DeathAnimation.Explode);
		planetarySystem.Die(deathAnimation, ADOBase.controller.Fail2Action);
		alive = false;
		ADOBase.controller.OnPlayerDied(this, overload, multipress, failMessage, hitbox);
		ADOBase.playerManager.deadPlayersQueue.Add(this);
		marginTracker.MarkDeath(currFloor.seqID);
		ADOBase.controller.camy.UpdateFollowCam();
		VirtualAvatarCanvas.instance?.Lose(playerID);
	}

	public void ClearMisses()
	{
		foreach (scrMissIndicator item in missesOnCurrFloor)
		{
			item.FadeOut();
		}
		missesOnCurrFloor.Clear();
	}

	public void Simulated_PlayerControl_Update(ulong? targetTick = null)
	{
		if (!alive || ADOBase.controller.paused || currFloor == null || ADOBase.controller.isCutscene)
		{
			return;
		}
		__nextTileIsHoldCached = false;
		validInputWasReleasedThisFrame = ValidInputWasReleased();
		cachedCamyToPos = ADOBase.controller.camy.topos;
		if ((bool)currFloor.nextfloor)
		{
			scrFloor nextfloor = currFloor.nextfloor;
			while (nextfloor.midSpin && (bool)nextfloor.nextfloor)
			{
				nextfloor = nextfloor.nextfloor;
			}
			__nextTileIsHoldCached = nextfloor.holdLength > -1;
		}
		AsyncInputUtils.WhileFloorNotChange(this, delegate
		{
			CheckPostHoldFail(targetTick);
		});
		AsyncInputUtils.WhileFloorNotChange(this, delegate
		{
			OttoHoldHit(targetTick);
		});
		HitAutoFloors(targetTick);
		UpdateHoldBehavior(targetTick);
		AsyncInputUtils.WhileFloorNotChange(this, delegate
		{
			HitHoldFloorsIfStartedAtHold(targetTick);
		});
		AsyncInputUtils.WhileFloorNotChange(this, delegate
		{
			CheckPreHoldFail(targetTick);
		});
		AsyncInputUtils.WhileFloorNotChange(this, delegate
		{
			UpdateHoldKeys(targetTick);
		});
		if (RDInput.GetMain(ButtonState.WentUp) > 0)
		{
			HitInputEvent(isAuto: false, InputEventState.Up);
		}
		Vector2 topos = ADOBase.controller.camy.topos;
		if (cachedCamyToPos != topos)
		{
			shouldReplaceCamyToPos = true;
			overrideCamyToPos = topos;
		}
	}

	private void CheckPostHoldFail(ulong? targetTick = null)
	{
		if (targetTick.HasValue)
		{
			ulong valueOrDefault = targetTick.GetValueOrDefault();
			AsyncInputUtils.AdjustAngle(this, valueOrDefault);
		}
		double minAngleMargin = _minAngleMargin;
		double num = Math.Max(3.1415927410125732, minAngleMargin * 2.0);
		if (ADOBase.controller.noFail || currFloor.isSafe)
		{
			num = minAngleMargin * 1.01;
		}
		double num2 = chosenPlanet.angle - chosenPlanet.targetExitAngle;
		if (!planetarySystem.isCW)
		{
			num2 *= -1.0;
		}
		if (ADOBase.controller.gameworld && ADOBase.lm.listFloors.Count > currFloor.seqID + 1 && num2 > num)
		{
			Die();
		}
	}

	private void OttoHoldHit(ulong? targetTick = null)
	{
		if (targetTick.HasValue)
		{
			ulong valueOrDefault = targetTick.GetValueOrDefault();
			AsyncInputUtils.AdjustAngle(this, valueOrDefault);
		}
		bool nextTileIsAuto = _nextTileIsAuto;
		if ((!(auto || ADOBase.controller.benchmarkMode || nextTileIsAuto) && (currFloor.holdLength <= -1 || !currFloor.auto)) || !ADOBase.controller)
		{
			return;
		}
		int num = (RDC.useOldAuto ? 1 : 5);
		while (num > 0 && chosenPlanet.AutoShouldHitNow())
		{
			bool num2 = RDC.auto;
			RDC.auto = true;
			if (currFloor.holdLength > -1)
			{
				currFloor.holdRenderer.Hit();
			}
			keyTimes.Clear();
			Hit(isAuto: true);
			RDC.auto = num2;
			num--;
		}
	}

	private void HitAutoFloors(ulong? targetTick = null)
	{
		if (targetTick.HasValue)
		{
			ulong valueOrDefault = targetTick.GetValueOrDefault();
			AsyncInputUtils.AdjustAngle(this, valueOrDefault);
		}
		if (ValidInputWasTriggered())
		{
			int num = CountValidKeysPressed();
			for (int i = 0; i < num; i++)
			{
				keyTimes.Add(Time.unscaledTimeAsDouble);
			}
			if (num == 1)
			{
				consecMultipressCounter = 0;
			}
		}
	}

	private void CancelHold(bool unfill = true)
	{
		currFloor.holdRenderer.Unfill(unfill);
		currFloor.holdCompletion = 0f;
		scrCamera camy = ADOBase.controller.camy;
		camy.Refocus(currFloor.prevfloor.transform);
		DOTween.To(() => camy.holdOffset, delegate(Vector2 x)
		{
			camy.holdOffset = x;
		}, Vector2.zero, 0.3f).SetEase(Ease.OutCubic);
		chosenPlanet.transform.DOMove(currFloor.prevfloor.transform.position, 0.4f).SetEase(Ease.OutCubic);
		chosenPlanet.currfloor = currFloor.prevfloor;
		if (ADOBase.playerManager.GetActivePlayers().Count == 1)
		{
			scrFlash.OnDamage();
		}
		LockInput(0.4f);
	}

	private void UpdateHoldBehavior(ulong? targetTick = null)
	{
		if (targetTick.HasValue)
		{
			ulong valueOrDefault = targetTick.GetValueOrDefault();
			AsyncInputUtils.AdjustAngle(this, valueOrDefault);
		}
		double minAngleMargin = _minAngleMargin;
		double num = _holdMargin;
		if (!ADOBase.controller.gameworld && currFloor.holdLength > -1)
		{
			float num2 = MathF.PI * (float)(currFloor.holdLength * 2 + 1);
			num = 1.0 - minAngleMargin * 1.0 / (double)num2;
			if (validInputWasReleasedThisFrame || (ValidInputWasTriggered() && !ADOBase.controller.strictHolds))
			{
				if ((double)currFloor.holdCompletion > num)
				{
					currFloor.holdRenderer.Unfill();
					currFloor.holdCompletion = 0f;
					Hit();
				}
				else
				{
					CancelHold();
				}
			}
			if ((double)currFloor.holdCompletion > 2.0 - num || currFloor.holdCompletion < -0.3f)
			{
				CancelHold(unfill: false);
			}
		}
		bool nextTileIsAuto = _nextTileIsAuto;
		bool nextTileIsHold = _nextTileIsHold;
		if (!ADOBase.controller.gameworld || !validInputWasReleasedThisFrame || nextTileIsAuto || currFloor.auto || auto || ADOBase.controller.benchmarkMode || currFloor.holdLength <= -1)
		{
			return;
		}
		if ((double)currFloor.holdCompletion < num)
		{
			if (GCS.checkpointNum != currFloor.seqID && ADOBase.controller.requireHolding)
			{
				Die();
			}
		}
		else if (!nextTileIsHold)
		{
			currFloor.holdRenderer.Hit();
			Hit();
		}
	}

	private void HitHoldFloorsIfStartedAtHold(ulong? targetTick = null)
	{
		if (targetTick.HasValue)
		{
			ulong valueOrDefault = targetTick.GetValueOrDefault();
			AsyncInputUtils.AdjustAngle(this, valueOrDefault);
		}
		if (!auto && !ADOBase.controller.benchmarkMode && chosenPlanet.AutoShouldHitNow() && currFloor.holdLength > -1 && GCS.checkpointNum == currFloor.seqID && ADOBase.controller.state != States.Fail && ADOBase.controller.state != States.Fail2)
		{
			Hit();
		}
	}

	private void CheckPreHoldFail(ulong? targetTick = null)
	{
		if (targetTick.HasValue)
		{
			ulong valueOrDefault = targetTick.GetValueOrDefault();
			AsyncInputUtils.AdjustAngle(this, valueOrDefault);
		}
		bool nextTileIsAuto = _nextTileIsAuto;
		double holdMargin = _holdMargin;
		if (ADOBase.controller.gameworld && !nextTileIsAuto && !currFloor.auto && (double)currFloor.holdCompletion > holdMargin && (double)currFloor.holdCompletion < 1.0 - holdMargin && !auto && !holding && ADOBase.controller.requireHolding && GCS.checkpointNum != currFloor.seqID && !ADOBase.controller.benchmarkMode && currFloor.holdLength > -1)
		{
			Die();
		}
	}

	public bool HitInputEvent(bool isAuto = false, InputEventState state = InputEventState.Down)
	{
		if (isAuto)
		{
			return true;
		}
		bool ignoreInput;
		bool flag = (ignoreInput = state == InputEventState.Down);
		bool flag2 = lastHitFrame[(int)state] != Time.frameCount;
		lastHitFrame[(int)state] = Time.frameCount;
		if (ignoreInput && flag2)
		{
			ignoreInputCount = 0;
		}
		ffxSetInputEventPlus[] array = ADOBase.controller.inputEventFfx[(int)state];
		if (flag2)
		{
			Handle(array[0]);
			if (flag ? RDInput.action1Press : RDInput.action1WentUp)
			{
				Handle(array[1]);
			}
			if (flag ? RDInput.action2Press : RDInput.action2WentUp)
			{
				Handle(array[2]);
			}
			if (flag ? RDInput.confirmPress : RDInput.confirmWentUp)
			{
				Handle(array[3]);
			}
			if (flag ? RDInput.upPress : RDInput.upWentUp)
			{
				Handle(array[4]);
			}
			if (flag ? RDInput.downPress : RDInput.downWentUp)
			{
				Handle(array[5]);
			}
			if (flag ? RDInput.leftPress : RDInput.leftWentUp)
			{
				Handle(array[6]);
			}
			if (flag ? RDInput.rightPress : RDInput.rightWentUp)
			{
				Handle(array[7]);
			}
		}
		if (ignoreInput && ignoreInputCount > 0)
		{
			ignoreInputCount--;
			return false;
		}
		return true;
		void Handle(ffxSetInputEventPlus ffx)
		{
			if (!(ffx == null))
			{
				ffx.inputEvents.ForEach(delegate(ffxPlusBase x)
				{
					x.StartEffectWithOffset(chosenPlanet);
				});
				if (ignoreInput && ffx.ignoreInput)
				{
					ignoreInputCount++;
				}
			}
		}
	}

	private void UpdateHoldKeys(ulong? targetTick = null)
	{
		if (targetTick.HasValue)
		{
			ulong valueOrDefault = targetTick.GetValueOrDefault();
			AsyncInputUtils.AdjustAngle(this, valueOrDefault);
		}
		bool nextTileIsHold = _nextTileIsHold;
		double holdMargin = _holdMargin;
		if (keyTimes.Count <= 0 || GCS.d_stationary || (!((currFloor.holdLength > -1 && !ADOBase.controller.strictHolds) || nextTileIsHold) && currFloor.holdLength != -1 && !((double)currFloor.holdCompletion < holdMargin)) || (ADOBase.controller.gameworld && currFloor.seqID >= ADOBase.lm.listFloors.Count - 1) || ADOBase.controller.benchmarkMode)
		{
			return;
		}
		keyTimes.RemoveAt(0);
		if ((auto && ADOBase.controller.gameworld) || (_nextTileIsAuto && !currFloor.freeroam))
		{
			HitInputEvent();
			return;
		}
		if (Hit() && currFloor.holdLength > -1)
		{
			holdKeys.Clear();
			IterateValidKeysHeld(delegate(AnyKeyCode k)
			{
				holdKeys.Add(k);
			}, delegate(AnyKeyCode k)
			{
				holdKeys.Remove(k);
			}, onlyPressedKeys: true);
		}
		if (!midspinInfiniteMargin)
		{
			return;
		}
		keyTimes.RemoveAt(0);
		if (Hit() && currFloor.holdLength > -1)
		{
			holdKeys.Clear();
			IterateValidKeysHeld(delegate(AnyKeyCode k)
			{
				holdKeys.Add(k);
			}, delegate(AnyKeyCode k)
			{
				holdKeys.Remove(k);
			}, onlyPressedKeys: true);
		}
	}

	public bool Hit(bool isAuto = false)
	{
		scrMisc.Vibrate(50L);
		if (!responsive || isReunifying)
		{
			return false;
		}
		if (ADOBase.isLevelEditor && ADOBase.controller.paused)
		{
			return false;
		}
		bool flag = chosenPlanet.currfloor.nextfloor != null && chosenPlanet.currfloor.nextfloor.auto;
		chosenPlanet.cachedAngle = chosenPlanet.angle;
		if (!HitInputEvent(isAuto))
		{
			return false;
		}
		scrFloor scrFloor2 = planetarySystem.chosenPlanet.player.currFloor;
		chosenPlanet.next.planetRenderer.ChangeFace(pulse: true);
		scrPlanet scrPlanet2 = chosenPlanet;
		planetarySystem.chosenPlanet = chosenPlanet.SwitchChosen();
		bool result = scrPlanet2 != chosenPlanet;
		if ((bool)ADOBase.controller.errorMeter && ADOBase.controller.gameworld && Persistence.hitErrorMeterSize != ErrorMeterSize.Off)
		{
			float num = (float)(scrPlanet2.cachedAngle - scrPlanet2.targetExitAngle);
			if (scrFloor2.isCCW)
			{
				num *= -1f;
			}
			if (!midspinInfiniteMargin)
			{
				if ((auto || flag) && !RDC.useOldAuto)
				{
					ADOBase.controller.errorMeter.AddHit(0f, 1f, planetarySystem.chosenPlanet, scrFloor2);
				}
				else
				{
					ADOBase.controller.errorMeter.AddHit(num, (float)currFloor.marginScale, planetarySystem.chosenPlanet, scrFloor2);
				}
			}
		}
		if (ADOBase.playerIsOnIntroScene)
		{
			return result;
		}
		bool flag2 = chosenPlanet.currfloor.holdLength == -1 || (chosenPlanet.currfloor.holdLength > -1 && ADOBase.controller.lastCamPulseFloor < chosenPlanet.currfloor.seqID);
		ADOBase.controller.lastCamPulseFloor = chosenPlanet.currfloor.seqID;
		scrCamera camy = ADOBase.controller.camy;
		if (flag2)
		{
			camy.UpdateFollowCam();
		}
		if (camy.isPulsingOnHit && flag2)
		{
			camy.Pulse();
		}
		bool flag3 = true;
		if (ADOBase.lm != null && ADOBase.controller.gameworld)
		{
			if (currFloor.midSpin || (currFloor.seqID > 0 && ADOBase.lm.listFloors[currFloor.seqID - 1].holdLength > -1))
			{
				flag3 = false;
			}
			if (currFloor.seqID > 1 && ADOBase.lm.listFloors[currFloor.seqID - 1].midSpin && ADOBase.lm.listFloors[currFloor.seqID - 2].holdLength > -1)
			{
				flag3 = false;
			}
		}
		if (flag3)
		{
			if (scnEditor.instance != null)
			{
				scnEditor.instance.OttoBlink();
			}
			if (VirtualAvatarCanvas.instance != null)
			{
				VirtualAvatarCanvas.instance.Hit(playerID);
			}
		}
		if (currFloor.midSpin)
		{
			midspinInfiniteMargin = true;
			keyTimes.Add(Time.unscaledTimeAsDouble);
		}
		else
		{
			midspinInfiniteMargin = false;
		}
		chosenPlanet.Update_RefreshAngles();
		return result;
	}

	public bool ValidInputWasTriggered()
	{
		if (ADOBase.controller.exitingToMainMenu)
		{
			return false;
		}
		if (ADOBase.controller.paused)
		{
			return false;
		}
		bool flag = false;
		if (touchEnabled)
		{
			Touch[] touches = Input.touches;
			for (int i = 0; i < touches.Length; i++)
			{
				Touch touch = touches[i];
				if (touch.phase == TouchPhase.Began && !ADOBase.controller.IsScreenPointInsideUIElements(touch.position))
				{
					flag = true;
					break;
				}
			}
		}
		bool flag2 = Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyDown(KeyCode.Mouse1);
		bool flag3;
		if ((ADOBase.isSwitch && !Application.isEditor) || scrController.coopMode)
		{
			flag3 = ((!RDC.force4PlayerCoop) ? RDInput.playerInputs[playerID].Any((RDInputType input) => input.mainPress) : KeyWasPressedForDebugCoop());
		}
		else if (ADOBase.isMobile)
		{
			flag3 = (Input.anyKeyDown && !flag2) || flag;
		}
		else
		{
			bool num = Input.anyKeyDown || RDInput.GetMain(ButtonState.IsDown) > 0;
			bool flag4 = flag2 && EventSystem.current.IsPointerOverGameObject();
			if (ADOBase.controller.isCutscene)
			{
				flag4 = false;
			}
			flag3 = num && !flag4;
		}
		if (!flag3)
		{
			return false;
		}
		return CountValidKeysPressed() > 0;
	}

	public bool ValidInputWasReleased()
	{
		bool result = false;
		if (touchEnabled)
		{
			if (holdKeys.Count != 0 && Input.touchCount == 0 && !Input.anyKey)
			{
				holdKeys.Clear();
				return true;
			}
		}
		else
		{
			bool num = holding;
			int count = holdKeys.Count;
			if (num && count == 0)
			{
				return true;
			}
			List<AnyKeyCode> mainHeldKeys = RDInput.GetMainHeldKeys();
			for (int num2 = holdKeys.Count - 1; num2 >= 0; num2--)
			{
				AnyKeyCode item = holdKeys[num2];
				if (item.value == null)
				{
					holdKeys.RemoveAt(num2);
				}
				else if (!(item.value is AsyncKeyCode) && mainHeldKeys.IndexOf(item) == -1)
				{
					holdKeys.RemoveAt(num2);
					result = true;
				}
				else
				{
					object value = item.value;
					if (value is AsyncKeyCode)
					{
						AsyncKeyCode k = (AsyncKeyCode)value;
						if (!mainHeldKeys.Any((AnyKeyCode _k) => _k.value is AsyncKeyCode asyncKeyCode && asyncKeyCode == k))
						{
							holdKeys.RemoveAt(num2);
							result = true;
						}
					}
				}
			}
		}
		return result;
	}

	private bool KeyWasPressedForDebugCoop()
	{
		if (playerID != 1)
		{
			if (playerID != 2)
			{
				if (playerID != 3)
				{
					return RDInput.leftPress;
				}
				return RDInput.upPress;
			}
			return RDInput.rightPress;
		}
		return RDInput.downPress;
	}

	public int CountValidKeysPressed()
	{
		int num = 0;
		keyLimiterOverCounter = 0;
		if (touchEnabled)
		{
			Touch[] touches = Input.touches;
			for (int i = 0; i < touches.Length; i++)
			{
				Touch touch = touches[i];
				if (touch.phase == TouchPhase.Began && !ADOBase.controller.IsScreenPointInsideUIElements(touch.position))
				{
					num++;
				}
			}
		}
		if ((ADOBase.isSwitch && !Application.isEditor) || scrController.coopMode)
		{
			if (RDC.force4PlayerCoop)
			{
				num += (KeyWasPressedForDebugCoop() ? 1 : 0);
			}
			else
			{
				HashSet<RDInputType> source = RDInput.playerInputs[playerID];
				num += source.Count((RDInputType input) => input.mainPress);
			}
		}
		else
		{
			num += RDInput.mainPressCount;
		}
		if ((States)(object)ADOBase.controller.stateMachine.GetState() == States.PlayerControl)
		{
			int num2 = (ADOBase.controller.unlockKeyLimiter ? 16 : 1000);
			for (int num3 = downKeysDuration.Count - 1; num3 >= 0; num3--)
			{
				KeyValuePair<AnyKeyCode, float> keyValuePair = downKeysDuration.ElementAt(num3);
				if (Time.time - keyValuePair.Value >= 0.5f)
				{
					downKeysDuration.Remove(keyValuePair.Key);
				}
			}
			foreach (AnyKeyCode mainPressKey in RDInput.GetMainPressKeys())
			{
				bool flag = false;
				if (!downKeysDuration.ContainsKey(mainPressKey) && downKeysDuration.Count >= num2)
				{
					keyLimiterOverCounter++;
					flag = true;
				}
				if (!flag)
				{
					downKeysDuration[mainPressKey] = Time.time;
				}
				if (mainPressKey.value is KeyCode keyCode)
				{
					keyFrequency[keyCode] = (keyFrequency.ContainsKey(keyCode) ? (keyFrequency[keyCode] + 1) : 0);
					keyTotal++;
				}
				if (mainPressKey.value is AsyncKeyCode asyncKeyCode)
				{
					keyFrequency[asyncKeyCode] = (keyFrequency.ContainsKey(asyncKeyCode) ? (keyFrequency[asyncKeyCode] + 1) : 0);
					keyTotal++;
				}
			}
			ADOBase.controller.maximumUsedKeys = Math.Max(ADOBase.controller.maximumUsedKeys, downKeysDuration.Count);
		}
		else
		{
			downKeysDuration.Clear();
			ADOBase.controller.maximumUsedKeys = 0;
		}
		return Math.Max(0, num);
	}

	private void IterateValidKeysHeld(Action<AnyKeyCode> foundHeld, Action<AnyKeyCode> foundSpecial, bool onlyPressedKeys = false)
	{
		if (touchEnabled)
		{
			Touch[] touches = Input.touches;
			foreach (Touch touch in touches)
			{
				if (!ADOBase.controller.IsScreenPointInsideUIElements(touch.position))
				{
					foundHeld(new AnyKeyCode(null));
				}
			}
		}
		foreach (AnyKeyCode item in onlyPressedKeys ? RDInput.GetMainPressKeys() : RDInput.GetMainHeldKeys())
		{
			foundHeld(item);
		}
	}
}
