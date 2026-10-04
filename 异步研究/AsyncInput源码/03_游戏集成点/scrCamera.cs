using System;
using ADOFAI;
using DG.Tweening;
using UnityEngine;

public class scrCamera : ADOBase
{
	public const float DefaultCameraOrthoSize = 5f;

	private const float DefaultRotationDuration = 5f;

	private const float CameraZ = -10f;

	private static scrCamera _instance;

	[Header("Cameras")]
	public Camera camobj;

	public Camera BGcam;

	public Camera Bgcamstatic;

	public Camera Overlaycam;

	public Camera PausePlanetsCam;

	[NonSerialized]
	public bool forceRTCam;

	private bool useRTCam;

	private RenderTexture camRT;

	[NonSerialized]
	public bool isPulsingOnHit = true;

	[NonSerialized]
	public bool isSizeTweening = true;

	[NonSerialized]
	public bool isZoomingOut;

	[NonSerialized]
	public bool isMoveTweening = true;

	[NonSerialized]
	public float timer;

	[NonSerialized]
	public bool followMode = true;

	[NonSerialized]
	public bool followMovingPlatforms;

	[NonSerialized]
	public Vector2 topos;

	[NonSerialized]
	public Vector2 pos;

	[NonSerialized]
	public Vector2 frompos;

	public int positionStateInt;

	[Header("Properties")]
	public float camspeed;

	public float rotdur = 5f;

	public float camsizenormal = 5f;

	public float pulsemagnitude = 0.2f;

	public bool speedAffectedByBPMChanges;

	[NonSerialized]
	public bool speedAffectedBySpeedTrial = true;

	public bool lockedSpeed;

	public bool editorRotation;

	[NonSerialized]
	public Color fromcol;

	[NonSerialized]
	public Color tocol;

	[NonSerialized]
	public float coltimer;

	[NonSerialized]
	public float coldur;

	[NonSerialized]
	public Color col;

	private float rottimer;

	[NonSerialized]
	public float tosize = 5f;

	[NonSerialized]
	public float fromsize = 5f;

	private float sizeTweenTime = 1f;

	[NonSerialized]
	public float userSizeMultiplier = 1f;

	[NonSerialized]
	public float zoomSize = 1f;

	private float pulsetimer;

	[NonSerialized]
	public bool enableCustomFPS;

	[NonSerialized]
	public float frameRate = 60f;

	[NonSerialized]
	public bool lockCustomFrameUpdate;

	private double lastCustomFPSUpdate;

	private MeshRenderer camQuadMesh;

	[Header("Other")]
	public Renderer flashPlusRendererBg;

	public Renderer flashPlusRendererFg;

	public Renderer flashEndscreen;

	public GameObject quad;

	[NonSerialized]
	public float torot;

	[NonSerialized]
	public float fromrot;

	[NonSerialized]
	public float rot;

	[NonSerialized]
	public Vector2 offset;

	[NonSerialized]
	public Vector2 holdOffset;

	[NonSerialized]
	public Vector2 lastEventRelativePosition = Vector2.zero;

	[NonSerialized]
	public CamMovementType lastUsedMovementType;

	[NonSerialized]
	public int lastTileCamFloor;

	[NonSerialized]
	public Vector2 shake;

	[NonSerialized]
	public bool skipUpdate;

	private int furthestFloorID = -1;

	[NonSerialized]
	public scrPlanet furthestPlanet;

	public static scrCamera instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = UnityEngine.Object.FindAnyObjectByType<scrCamera>();
			}
			return _instance;
		}
		set
		{
			if (instance == null)
			{
				_instance = value;
			}
		}
	}

	private bool camRTNeedsRecreation
	{
		get
		{
			if (!(camRT == null) && camRT.width == Screen.width)
			{
				return camRT.height != Screen.height;
			}
			return true;
		}
	}

	public PositionState positionState
	{
		get
		{
			return (PositionState)positionStateInt;
		}
		set
		{
			positionStateInt = (int)value;
		}
	}

	private void Awake()
	{
		instance = this;
		camobj = GetComponent<Camera>();
		if (quad != null && (object)camQuadMesh == null)
		{
			camQuadMesh = quad.GetComponent<MeshRenderer>();
		}
		if (forceRTCam)
		{
			SetupRTCam(enable: true);
		}
	}

	private void Start()
	{
		if (ADOBase.controller != null)
		{
			furthestPlanet = ADOBase.controller.chosenPlanet;
		}
		Vector3 localPosition = camobj.transform.localPosition;
		topos = (frompos = localPosition);
		if (!GCS.lofiVersion)
		{
			tocol = camobj.backgroundColor;
		}
		tosize = camsizenormal;
		if (GCS.lofiVersion)
		{
			Transform transform = base.transform.Find("BgController");
			if (transform != null)
			{
				transform.gameObject.SetActive(value: true);
			}
		}
		if (ADOBase.customLevel != null)
		{
			editorRotation = true;
		}
		camobj.cullingMask |= 128;
	}

	public void Rewind()
	{
		isPulsingOnHit = true;
		isSizeTweening = true;
		isZoomingOut = false;
		isMoveTweening = true;
		topos = (pos = (frompos = Vector2.zero));
		fromcol = Color.clear;
		tocol = Color.clear;
		coltimer = 0f;
		coldur = 1f;
		col = Color.clear;
		rotdur = 5f;
		timer = 0f;
		pulsemagnitude = 0.1f;
		camsizenormal = 5f;
		camspeed = 0f;
		tosize = 0f;
		fromsize = 5f;
		base.transform.localPosition = Vector3.zero;
		furthestFloorID = -1;
		Start();
	}

	public void SetupRTCam(bool enable)
	{
		useRTCam = enable;
		RenderTexture targetTexture = (enable ? camRT : null);
		Bgcamstatic.targetTexture = targetTexture;
		BGcam.targetTexture = targetTexture;
		camobj.targetTexture = targetTexture;
		Overlaycam.gameObject.SetActive(enable);
		quad.SetActive(enable);
	}

	private void Update()
	{
		if (skipUpdate)
		{
			return;
		}
		if (useRTCam && camRTNeedsRecreation)
		{
			if (camRT != null)
			{
				camRT.Release();
			}
			camRT = new RenderTexture(Screen.width, Screen.height, 24);
			if (useRTCam)
			{
				SetupRTCam(enable: true);
			}
			float num = Overlaycam.orthographicSize * 2f;
			float x = num * (float)Screen.width / (float)Screen.height;
			quad.transform.localScale = new Vector3(x, num, 1f);
			if (!enableCustomFPS)
			{
				camQuadMesh.material.mainTexture = camRT;
			}
		}
		if (!ADOBase.customLevel || !ADOBase.controller.paused || !followMode)
		{
			if ((bool)ADOBase.customLevel)
			{
				float num2 = ADOBase.conductor.bpm * (float)furthestPlanet.planetarySystem.speed * ADOBase.conductor.song.pitch;
				float num3 = 60f / num2;
				camspeed = num3 * 2f;
			}
			else if (!lockedSpeed)
			{
				camspeed = (float)(scrConductor.instance.crotchetAtStart * 2.0);
				if (speedAffectedByBPMChanges)
				{
					camspeed /= (float)furthestPlanet.planetarySystem.speed;
				}
				if (speedAffectedBySpeedTrial)
				{
					camspeed /= GCS.currentSpeedTrial;
				}
			}
			timer += Time.deltaTime;
			topos = positionState switch
			{
				PositionState.Origin => new Vector2(0f, 0f), 
				PositionState.Levels => new Vector2(topos.x, -2f), 
				PositionState.DLC => new Vector2(topos.x, -12f), 
				PositionState.Xtra => new Vector2(-9f, 13f), 
				PositionState.HopGem => new Vector2(0f, 3f), 
				PositionState.GemToXtra => topos, 
				PositionState.ChangingRoom => new Vector2(0f, scrController.coopMode ? (-8f) : topos.y), 
				PositionState.XtraIsland => new Vector2(0f, 11f), 
				PositionState.CLS => new Vector2(-5.5f, topos.y), 
				PositionState.CLSIntro => new Vector2(0f, 3.5f), 
				PositionState.CrownIsland => new Vector2(0f, 24.5f), 
				PositionState.MuseDashIsland => new Vector2(-25f, 24f), 
				PositionState.CR2024Island => new Vector2(topos.x, 28f), 
				PositionState.ToTaroWorld => new Vector2(8f, topos.y), 
				PositionState.ExitTaroWorld => new Vector2(8f, -4f), 
				PositionState.TaroMenu0TopLane => new Vector2(topos.x, 3f), 
				PositionState.TaroMenu0BottomLane => new Vector2(topos.x, -10f), 
				PositionState.TaroMenu1TopLane => new Vector2(topos.x, 15f), 
				PositionState.TaroMenu2BottomLane => new Vector2(topos.x, -11f), 
				PositionState.TaroMenu3BottomLane => new Vector2(topos.x, -5f), 
				PositionState.TaroMenu2TopLane => new Vector2(topos.x, 5f), 
				PositionState.TaroMenu3TopLane => new Vector2(topos.x, 5f), 
				PositionState.NeoCosmosCredits => new Vector2(42f, 1.5f), 
				_ => topos, 
			};
			if (isMoveTweening || ADOBase.controller.gameworld)
			{
				float num4 = Vector2.Distance(frompos, topos);
				float num5 = ((num4 > 5f) ? Mathf.Min(1f, Mathf.InverseLerp(5f, 10f, num4)) : 0f) * 0.5f + 1f;
				if (!followMovingPlatforms)
				{
					num5 = 1f;
				}
				pos = Vector2.Lerp(frompos, topos + offset + holdOffset, timer / (camspeed / num5));
				base.transform.localPosition = pos + shake;
			}
			if (!editorRotation)
			{
				rottimer += Time.deltaTime;
				rot = Mathf.Lerp(fromrot, torot, rottimer / rotdur);
				base.transform.localEulerAngles = new Vector3(base.transform.localEulerAngles.x, base.transform.localEulerAngles.y, rot);
			}
			coltimer += Time.deltaTime;
			col = Color.Lerp(fromcol, tocol, coltimer / coldur);
			camobj.backgroundColor = col;
		}
		if (isSizeTweening && ADOBase.controller != null)
		{
			UpdateSize();
		}
		if (AsyncInputManager.isActive && scrPlayer.shouldReplaceCamyToPos)
		{
			scrPlayer.shouldReplaceCamyToPos = false;
			topos = scrPlayer.overrideCamyToPos;
		}
	}

	private void LateUpdate()
	{
		CheckUpdateFrameRateScreen();
		SetPosition(base.transform.position);
	}

	public void UpdateSize()
	{
		if (!ADOBase.controller.paused)
		{
			pulsetimer += Time.deltaTime;
		}
		float num = Mathf.Lerp(fromsize, tosize, pulsetimer / sizeTweenTime);
		if (zoomSize == 0f)
		{
			camobj.enabled = false;
			return;
		}
		if (!camobj.enabled)
		{
			camobj.enabled = true;
		}
		camobj.orthographicSize = num * userSizeMultiplier * zoomSize;
	}

	public void ViewObjectInstant(Transform _trans, bool includeOffset = false)
	{
		Vector2 position = (topos = new Vector2(_trans.position.x, _trans.position.y));
		if (includeOffset)
		{
			position += offset + holdOffset;
		}
		frompos = position;
		SetPosition(position);
	}

	public void ViewVectorInstant(Vector2 dpos, bool includeOffset = false)
	{
		topos = dpos;
		if (includeOffset)
		{
			dpos += offset;
		}
		frompos = dpos;
		SetPosition(dpos);
	}

	public void Pulse()
	{
		if (ADOBase.controller.visualEffects == VisualEffects.Minimum)
		{
			return;
		}
		if ((bool)ADOBase.customLevel)
		{
			LevelData levelData = ADOBase.customLevel.levelData;
			if (levelData != null && !levelData.pulseCamOnLandingFloor)
			{
				return;
			}
		}
		fromsize = camsizenormal - pulsemagnitude;
		tosize = camsizenormal;
		sizeTweenTime = 1f;
		pulsetimer = 0f;
	}

	public void ZoomOut()
	{
		if (!GCS.DisableAllZooming)
		{
			isZoomingOut = true;
			pulsetimer = 0f;
			tosize = 100f;
			sizeTweenTime = 10f;
		}
	}

	public void RotateSmooth(float angle)
	{
		torot += angle;
		fromrot = rot;
		rottimer = 0f;
	}

	public void FlashBg(Color? _fromcol = null)
	{
		fromcol = _fromcol ?? Color.white;
		tocol = scrVfx.instance.currentColourScheme.colourBg;
		coltimer = 0f;
	}

	public void SetBgColour()
	{
		tocol = scrVfx.instance.currentColourScheme.colourBg;
		coltimer = coldur;
	}

	public void setCamSizeInstant(float size)
	{
		fromsize = size;
		tosize = size;
	}

	public void setCamSizeSmooth(float size, float duration = -999f)
	{
		fromsize = camobj.orthographicSize;
		tosize = size;
		camsizenormal = size;
		if (duration != -999f)
		{
			sizeTweenTime = duration;
		}
		pulsetimer = 0f;
	}

	public void setCamSizeLerp(float from, float to, float dur)
	{
		fromsize = from;
		tosize = to;
		sizeTweenTime = dur;
		pulsetimer = 0f;
	}

	public void setNewCamSizeNormal(float size, bool smooth)
	{
		if (smooth)
		{
			setCamSizeSmooth(size);
		}
		else
		{
			setCamSizeInstant(size);
		}
		camsizenormal = size;
	}

	public void SetRotAngleLerp(float from, float to, float duration)
	{
		fromrot = from;
		torot = to;
		rottimer = 0f;
		rotdur = duration;
	}

	public void SetXOffset(float offsetX)
	{
		offset.x = offsetX;
	}

	public void SetYOffset(float offsetY)
	{
		offset.y = offsetY;
	}

	public void SetHoldOffset(Vector2 offset)
	{
		holdOffset = offset;
	}

	public void Refocus(Transform thing)
	{
		frompos = pos;
		topos = SetPosition(thing.position);
		timer = 0f;
	}

	public void Refocus(Vector2 destination)
	{
		frompos = pos;
		topos = destination;
		timer = 0f;
	}

	public void SetToFreeMode()
	{
		topos = (pos = (frompos = Vector2.zero));
		base.transform.localPosition = Vector3.zero;
		followMode = false;
	}

	public Vector3 SetPosition(Vector2 position)
	{
		Vector3 vector = new Vector3(position.x, position.y, -10f);
		base.transform.position = vector;
		return vector;
	}

	public void SetCustomFrameRate(bool enable, float frame = 0f)
	{
		enableCustomFPS = enable;
		frameRate = frame;
		if (enable)
		{
			RenderTexture mainTexture = new RenderTexture(camRT.width, camRT.height, 24);
			camQuadMesh.material.mainTexture = mainTexture;
			UpdateCustomFrameRateScreen();
			return;
		}
		RenderTexture renderTexture = camQuadMesh.material.mainTexture as RenderTexture;
		if (renderTexture != null)
		{
			renderTexture.Release();
		}
		camQuadMesh.material.mainTexture = camRT;
	}

	public void CheckUpdateFrameRateScreen()
	{
		if (lockCustomFrameUpdate)
		{
			lockCustomFrameUpdate = false;
		}
		else if (enableCustomFPS && (double)Time.time - lastCustomFPSUpdate >= (double)(1f / frameRate))
		{
			UpdateCustomFrameRateScreen();
		}
	}

	private void UpdateCustomFrameRateScreen()
	{
		lastCustomFPSUpdate = Time.time;
		Bgcamstatic.Render();
		BGcam.Render();
		camobj.Render();
		RenderTexture dest = camQuadMesh.material.mainTexture as RenderTexture;
		Graphics.Blit(camRT, dest);
	}

	public void UpdateFollowCam(bool force = false)
	{
		if (scrController.coopMode && ADOBase.controller.gameworld)
		{
			int num = furthestFloorID;
			scrPlayer[] players = ADOBase.playerManager.players;
			foreach (scrPlayer scrPlayer2 in players)
			{
				if (scrPlayer2.alive && !scrPlayer2.doingRevivalCountdown)
				{
					scrPlanet chosenPlanet = scrPlayer2.planetarySystem.chosenPlanet;
					int seqID = chosenPlanet.currfloor.seqID;
					if (seqID > furthestFloorID)
					{
						furthestFloorID = seqID;
						furthestPlanet = chosenPlanet;
					}
				}
			}
			if (furthestFloorID == num && !force)
			{
				return;
			}
		}
		else if (LevelSelectBase.instance != null && ADOBase.levelSelect != null && ADOBase.levelSelect.lastPlanetLanded != null)
		{
			furthestPlanet = ADOBase.levelSelect.lastPlanetLanded;
		}
		else
		{
			furthestPlanet = ADOBase.controller.chosenPlanet;
		}
		if (followMode)
		{
			frompos = base.transform.localPosition - shake.WithZ(0f);
			topos = furthestPlanet.transform.position;
			timer = 0f;
		}
	}

	public void FlashPlus(float duration = 0.4f, Color? startColor = null, Color? endColor = null, bool foreground = true, Ease ease = Ease.Linear)
	{
		Renderer obj = (foreground ? flashPlusRendererFg : flashPlusRendererBg);
		float num = 2f * camobj.orthographicSize;
		float x = num * camobj.aspect;
		Vector2 vector = new Vector2(x, num);
		obj.transform.ScaleXY(vector.x, vector.y);
		Material material = obj.material;
		DOTween.Kill(material, complete: true);
		material.color = startColor.GetValueOrDefault(Color.white.WithAlpha(0.5f));
		material.DOColor(endColor.GetValueOrDefault(Color.white.WithAlpha(0f)), duration).SetEase(ease);
	}
}
