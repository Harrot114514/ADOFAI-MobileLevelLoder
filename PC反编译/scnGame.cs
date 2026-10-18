using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ADOFAI;
using ADOFAI.FloorFX;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class scnGame : ADOBase
{
	private const float SpeedMargin = 0.05f;

	private const string InternalLevelsSharedPath = "InternalLevels/Shared";

	private const string PlanetsWarning = "Planets more than 3 works but is an unreleased feature right now. If you're reading this, please do not release a mod to disable it or share footage, so we can keep the spoiler";

	public const string NoTag = "NO TAG";

	private static readonly string[] ConditionalEventTags = new string[9] { "perfectTag", "earlyPerfectTag", "latePerfectTag", "veryEarlyTag", "veryLateTag", "tooEarlyTag", "tooLateTag", "lossTag", "onCheckpointTag" };

	public static scnGame instance;

	[Header("Components")]
	public scrDecorationManager decManager;

	public TextureManager imgHolder;

	public VideoPlayer videoBG;

	public GameObject editorBG;

	[NonSerialized]
	public GameObject customEditorBG;

	public Transform camParent;

	public scrCustomBackgroundSprite custBG;

	public GameObject levelEditor;

	[NonSerialized]
	public TutorialBackground customTutorialBackground;

	[NonSerialized]
	public float highestBPM;

	[NonSerialized]
	public LevelData levelData;

	[NonSerialized]
	public scrLevelMaker levelMaker;

	[NonSerialized]
	public new string levelPath;

	[NonSerialized]
	public int checkpointsUsed;

	[NonSerialized]
	public bool isLoading;

	[NonSerialized]
	public TutorialBackground tutorialBackground;

	[NonSerialized]
	public Material customWorldMaterial;

	public bool forceOldLevelStyle;

	private bool usingCustomTutorialBackground;

	private Camera camera;

	private GameObject flash;

	private int startFrame;

	private bool backgroundsLoaded;

	private bool floorSpritesLoaded;

	private string currentSongKey;

	private static FieldInfo _isIndependentUpdateField = typeof(Tween).GetField("isIndependentUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

	private List<scrFloor> floors => scrLevelMaker.instance.listFloors;

	public List<LevelEvent> events => levelData.levelEvents;

	public List<LevelEvent> decorations => levelData.decorations;

	private bool paused => scrController.instance.paused;

	private Dictionary<Filter, MonoBehaviour> filterToComp => scrVfxPlus.instance.filterToComp;

	public static scrDecorationManager suitableDecManager
	{
		get
		{
			if (!(instance == null))
			{
				return instance.decManager;
			}
			return GameObject.Find("Decoration Container").GetComponent<scrDecorationManager>();
		}
	}

	private static IReadOnlyList<HashSet<ffxPlusBase>> GetFloorConditionalEvents(scrFloor floor)
	{
		return new List<HashSet<ffxPlusBase>> { floor.perfectEffects, floor.earlyPerfectEffects, floor.latePerfectEffects, floor.veryEarlyEffects, floor.veryLateEffects, floor.tooEarlyEffects, floor.tooLateEffects, floor.lossEffects, floor.onCheckpointEffects };
	}

	public void PrepVfx(int seqID, bool remakeFloors = true)
	{
		PrepVfx(floors, seqID, events, remakeFloors);
	}

	private void Awake()
	{
		instance = this;
		if (ADOBase.isScnGame && GCS.internalLevelName.IsNullOrEmpty() && (GCS.customLevelPaths == null || GCS.customLevelPaths.Length == 0))
		{
			if (scrController.IsWorldAndLevelInternalLevel(Persistence.savedCurrentLevel))
			{
				if (GCS.internalLevelName == null)
				{
					GCS.internalLevelName = Persistence.savedCurrentLevel;
				}
			}
			else if (GCS.internalLevelName == null)
			{
				GCS.internalLevelName = "AR-X";
			}
		}
		isLoading = true;
		printesp("Awake");
		FlushUnusedMemory();
		Resources.UnloadUnusedAssets();
		startFrame = Time.frameCount;
		levelData = new LevelData();
		levelMaker = scrLevelMaker.instance;
		camera = ADOBase.controller.camy.GetComponent<Camera>();
		flash = GameObject.Find("Flash");
		imgHolder = new TextureManager();
		GameObject.Find("Camera").GetComponent<scrCamera>();
		ffxSetFilterAdvancedPlus.ResetVariables();
		videoBG.prepareCompleted += PrepareCompleted;
		videoBG.errorReceived += VideoErrorReceived;
	}

	private void Update()
	{
		if ((GCS.customLevelPaths != null || ADOBase.isInternalLevel) && !ADOBase.isLevelEditor && Time.frameCount - startFrame == 3)
		{
			string text = GCS.internalLevelName ?? GCS.customLevelPaths[GCS.customLevelIndex];
			LoadAndPlayLevel(text);
		}
		float num = 2f * camera.orthographicSize;
		float x = num * camera.aspect;
		Vector2 vector = new Vector2(x, num);
		scrCamera.instance.flashPlusRendererBg.transform.ScaleXY(vector.x, vector.y);
		scrCamera.instance.flashPlusRendererFg.transform.ScaleXY(vector.x, vector.y);
		scrCamera.instance.flashEndscreen.transform.ScaleXY(vector.x, vector.y);
		flash.transform.ScaleXY(vector.x, vector.y);
	}

	public void DisableFilters()
	{
		foreach (MonoBehaviour value in filterToComp.Values)
		{
			value.enabled = false;
		}
		ffxSetFilterAdvancedPlus.ResetAllFilters();
	}

	public bool LoadLevel(string levelPath, out LoadResult status)
	{
		FlushUnusedMemory();
		Resources.UnloadUnusedAssets();
		printesp("");
		isLoading = true;
		bool num = levelData.LoadLevel(levelPath, out status);
		if (num)
		{
			this.levelPath = levelPath;
			scrUIController.instance.LevelFinishedLoading();
		}
		return num;
	}

	public bool LoadAndPlayLevel(string levelPath)
	{
		LoadResult status;
		bool num = LoadLevel(levelPath, out status);
		if (num)
		{
			if (ADOBase.isLevelEditor)
			{
				ADOBase.editor.filenameText.text = Path.GetFileName(levelPath);
				ADOBase.editor.filenameText.fontStyle = FontStyles.Normal;
			}
			if (ADOBase.isInternalLevel)
			{
				string[] array = levelPath.Split('-');
				string text = array[0];
				if (array[1] != "X")
				{
					string path = "InternalLevels/Shared/Materials/FloorMeshWorld" + text;
					customWorldMaterial = Resources.Load<Material>(path);
				}
			}
			RemakePath(applyEventsToFloors: false);
			ReloadAssets(force: true, reloadDecorations: false);
			UpdateDecorationObjects();
			ApplyEventsToFloors(floors);
			decManager.FinishUpdateDecorations();
			levelMaker.DrawMultiPlanet();
			levelMaker.DrawHolds();
			DiscordController.instance?.UpdatePresence();
			Play();
			return num;
		}
		SceneManager.LoadScene("scnCLS");
		UnityEngine.Object.Instantiate(RDConstants.data.prefab_errorCanvas).GetComponent<ErrorCanvas>().ShowError(RDString.Get($"editor.notification.loadingFailed.{status}"));
		return num;
	}

	public void ReloadAssets(bool force = false, bool reloadDecorations = true)
	{
		if ((bool)levelEditor)
		{
			imgHolder.MarkAllUnused();
		}
		printesp("ReloadAssets");
		ReloadSong(force);
		UpdateBackgroundSprites();
		if (reloadDecorations)
		{
			if (force)
			{
				UpdateDecorationObjects(reloadDecorations: false);
			}
			else
			{
				decManager.ResetDecorations();
			}
		}
		UpdateFloorSprites();
		SetBackground();
		if (force)
		{
			UpdateVideo();
		}
		ReloadCustomSounds(force);
		imgHolder.Unload(onlyIfUnused: true);
	}

	public void ApplyEventsToFloors(List<scrFloor> floors)
	{
		ApplyEventsToFloors(floors, levelData, ADOBase.lm, events);
	}

	public static void ApplyEventsToFloors(List<scrFloor> floors, LevelData levelData, scrLevelMaker lm, List<LevelEvent> events)
	{
		List<LevelEvent>[] array = new List<LevelEvent>[floors.Count];
		int num = 0;
		for (num = 0; num < array.Length; num++)
		{
			array[num] = new List<LevelEvent>();
		}
		for (num = 0; num < events.Count; num++)
		{
			LevelEvent levelEvent = events[num];
			if (levelEvent.active)
			{
				array[levelEvent.floor].Add(levelEvent);
			}
		}
		float bpm = levelData.bpm;
		float num2 = scrConductor.instance.song.pitch;
		if (GCS.speedTrialMode && GCS.editorQuickPitchedPlaying)
		{
			num2 *= GCS.currentSpeedTrial;
		}
		foreach (scrFloor floor3 in floors)
		{
			ffxPlusBase[] components = floor3.GetComponents<ffxPlusBase>();
			for (int i = 0; i < components.Length; i++)
			{
				UnityEngine.Object.DestroyImmediate(components[i]);
			}
			floor3.plusEffects.Clear();
			lm.CalculateSingleFloorAngleLength(floor3);
		}
		ApplyCoreEventsToFloors(array);
		lm.CalculateFloorEntryTimes();
		Ease planetEase = levelData.planetEase;
		int planetEaseParts = levelData.planetEaseParts;
		EasePartBehavior planetEasePartBehavior = levelData.planetEasePartBehavior;
		bool flag = levelData.stickToFloors;
		float bpm2 = levelData.bpm;
		Color color1 = levelData.trackColor;
		Color color2 = levelData.secondaryTrackColor;
		Color? trackShadowColor = levelData.trackShadowColor;
		TrackColorType colorType = levelData.trackColorType;
		float colorAnimDur = levelData.trackColorAnimDuration;
		TrackColorPulse pulseType = levelData.trackColorPulse;
		Texture2D texture = levelData.trackTexture;
		float textureScale = levelData.trackTextureScale;
		int pulseLength = levelData.trackPulseLength;
		int startOfColorChange = floors[0].seqID;
		bool outline = levelData.floorIconOutlines;
		_ = levelData.tileShape;
		TrackAnimationType animationType = levelData.trackAnimation;
		TrackAnimationType2 animationType2 = levelData.trackDisappearAnimation;
		bool flag2 = true;
		TrackStyle style = levelData.trackStyle;
		float num3 = levelData.trackBeatsAhead;
		float num4 = levelData.trackBeatsBehind;
		float glowMult = levelData.trackGlowIntensity / 100f;
		float num5 = 1f;
		float num6 = 1f;
		int num7 = 2;
		bool auto = false;
		bool showStatusText = false;
		bool output = false;
		float num8 = 1f;
		Vector2 vector = Vector2.zero;
		Vector2 vector2 = Vector2.zero;
		bool flag3 = false;
		Vector2 vector3 = Vector2.zero;
		Vector2 zero = Vector2.zero;
		float num9 = 1f;
		float num10 = 1f;
		float num11 = 0f;
		float output2 = 1f;
		float output3 = 1f;
		float output4 = 0f;
		bool output5 = flag;
		Color tempColor1 = color1;
		Color tempColor2 = color2;
		TrackColorType tempColorType = colorType;
		float tempColorAnimDur = colorAnimDur;
		TrackColorPulse tempPulseType = pulseType;
		Texture2D tempTexture = texture;
		float tempTextureScale = textureScale;
		int tempPulseLength = pulseLength;
		int tempStartOfColorChange = startOfColorChange;
		bool tempOutline = outline;
		TrackStyle tempStyle = style;
		float tempGlowMult = glowMult;
		Color? customShadowColor = trackShadowColor;
		bool hideJudgment = false;
		bool hideIcon = false;
		int num12 = 0;
		float a = 0f;
		int num13 = 0;
		float lengthMult = 1f;
		float widthMult = 1f;
		foreach (scrFloor floor4 in floors)
		{
			if (flag3)
			{
				vector += vector3;
				vector2 += vector3;
				flag3 = false;
			}
			List<LevelEvent> obj = array[floor4.seqID];
			float speed = floor4.speed;
			bool isCCW = floor4.isCCW;
			foreach (LevelEvent item5 in obj)
			{
				item5.TryGet<bool>("justThisTile", out var output6);
				switch (item5.eventType)
				{
				case LevelEventType.SetPlanetRotation:
					planetEase = (Ease)item5["ease"];
					planetEaseParts = (int)item5["easeParts"];
					planetEasePartBehavior = (EasePartBehavior)item5["easePartBehavior"];
					break;
				case LevelEventType.ColorTrack:
				{
					if (!item5.disabled["trackColor"])
					{
						item5.TryGet<Color>("trackColor", out tempColor1);
					}
					if (!item5.disabled["secondaryTrackColor"])
					{
						item5.TryGet<Color>("secondaryTrackColor", out tempColor2);
					}
					if (!item5.disabled["trackColorAnimDuration"])
					{
						item5.TryGet<float>("trackColorAnimDuration", out tempColorAnimDur);
					}
					bool num17 = !item5.disabled["trackColorType"] && item5.TryGet<TrackColorType>("trackColorType", out tempColorType);
					if (!item5.disabled["trackColorPulse"])
					{
						item5.TryGet<TrackColorPulse>("trackColorPulse", out tempPulseType);
					}
					if (!item5.disabled["trackPulseLength"])
					{
						item5.TryGet<int>("trackPulseLength", out tempPulseLength);
					}
					if (num17)
					{
						tempStartOfColorChange = floor4.seqID;
					}
					if (!item5.disabled["floorIconOutlines"])
					{
						tempOutline = item5.GetBool("floorIconOutlines");
					}
					if (!item5.disabled["trackTexture"])
					{
						string text = (string)item5["trackTexture"];
						if (!string.IsNullOrEmpty(text))
						{
							string filePath = Path.Combine(Path.GetDirectoryName(ADOBase.customLevel.levelPath), text);
							tempTexture = instance.imgHolder.AddTexture(text, out var _, filePath)?.texture;
							if ((bool)tempTexture)
							{
								tempTexture.wrapMode = TextureWrapMode.Repeat;
							}
						}
						else
						{
							tempTexture = null;
						}
					}
					if (!item5.disabled["trackTextureScale"])
					{
						tempTextureScale = item5.GetFloat("trackTextureScale");
					}
					if (!item5.disabled["trackStyle"])
					{
						tempStyle = (TrackStyle)item5["trackStyle"];
					}
					if (!item5.disabled["trackGlowIntensity"] && item5.TryGet<float>("trackGlowIntensity", out var output10))
					{
						tempGlowMult = output10 / 100f;
					}
					if (!output6)
					{
						color1 = tempColor1;
						color2 = tempColor2;
						colorAnimDur = tempColorAnimDur;
						colorType = tempColorType;
						pulseType = tempPulseType;
						pulseLength = tempPulseLength;
						startOfColorChange = tempStartOfColorChange;
						outline = tempOutline;
						texture = tempTexture;
						textureScale = tempTextureScale;
						style = tempStyle;
						glowMult = tempGlowMult;
					}
					break;
				}
				case LevelEventType.AnimateTrack:
				{
					flag2 = !item5.disabled["trackAnimation"];
					bool num15 = !item5.disabled["trackDisappearAnimation"];
					if (flag2)
					{
						animationType = (TrackAnimationType)item5["trackAnimation"];
						num3 = item5.GetFloat("beatsAhead");
					}
					if (num15)
					{
						animationType2 = (TrackAnimationType2)item5["trackDisappearAnimation"];
						num4 = item5.GetFloat("beatsBehind");
					}
					num5 = num6;
					num6 = speed;
					break;
				}
				case LevelEventType.TileDimensions:
					lengthMult = item5.GetFloat("length") / 100f;
					widthMult = item5.GetFloat("width") / 100f;
					break;
				case LevelEventType.Hold:
					if ((int)item5["duration"] >= 0)
					{
						double angleMoved = scrMisc.GetAngleMoved(floor4.entryangle, floor4.exitangle, !isCCW);
						bool flag4 = Math.Abs(angleMoved) < 1E-05 || Math.Abs(angleMoved - Math.PI * 2.0) < 1E-05;
						float num16 = 1f;
						if (item5.TryGet<int>("distanceMultiplier", out var output9))
						{
							num16 = (float)output9 / 100f;
						}
						floor4.holdDistance = (flag4 ? 0f : ((float)((int)item5["duration"] * 2 + 1) * num16));
						floor4.holdLength = (int)item5["duration"];
						vector3 = (flag4 ? Vector2.zero : (new Vector2(Mathf.Cos(Convert.ToSingle(floor4.exitangle) - MathF.PI / 2f), Mathf.Sin(Convert.ToSingle(floor4.exitangle) + MathF.PI / 2f)) * (floor4.holdDistance * ADOBase.controller.tileSize)));
						flag3 = true;
						item5.TryGet<bool>("landingAnimation", out floor4.showHoldTiming);
					}
					else
					{
						floor4.holdLength = -1;
					}
					break;
				case LevelEventType.PositionTrack:
				{
					item5.TryGet<bool>("editorOnly", out var output7);
					if ((!output7 || !scrController.instance.paused) && output7)
					{
						break;
					}
					if (!item5.disabled["positionOffset"])
					{
						int num14 = floor4.seqID;
						if (item5.TryGet<Tuple<int, TileRelativeTo>>("relativeTo", out var output8))
						{
							num14 = IDFromTile(output8, floor4.seqID, floors);
						}
						vector2 += item5.Get<Vector2>("positionOffset") * ADOBase.controller.tileSize;
						if (num14 != floor4.seqID)
						{
							scrFloor scrFloor2 = floors[num14];
							Vector2 vector4 = (Vector2)(scrFloor2.startPos + scrFloor2.offsetPos) - ((Vector2)floor4.startPos + vector);
							vector2 += vector4;
						}
						if (!output6)
						{
							vector = vector2;
						}
					}
					if (item5.TryGetAndSet("scale", ref output3, onlyIfEnabled: true))
					{
						output3 /= 100f;
						if (!output6)
						{
							num10 = output3;
						}
					}
					if (item5.TryGetAndSet("opacity", ref output2, onlyIfEnabled: true))
					{
						output2 /= 100f;
						if (!output6)
						{
							num9 = output2;
						}
					}
					if (item5.TryGetAndSet("rotation", ref output4, onlyIfEnabled: true) && !output6)
					{
						num11 = output4;
					}
					if (item5.TryGetAndSet("stickToFloors", ref output5, onlyIfEnabled: true) && !output6)
					{
						flag = output5;
					}
					break;
				}
				case LevelEventType.MultiPlanet:
					if (!floor4.prevfloor || ((bool)floor4.prevfloor && !floor4.prevfloor.midSpin))
					{
						num7 = (int)item5["planets"];
						if (num7 < 2)
						{
							num7 = 2;
						}
						num7 = Math.Min(num7, 3);
						if (num7 > 3)
						{
							Debug.Log("Planets more than 3 works but is an unreleased feature right now. If you're reading this, please do not release a mod to disable it or share footage, so we can keep the spoiler");
						}
					}
					else if ((bool)floor4.prevfloor && floor4.prevfloor.midSpin)
					{
						num7 = (int)item5["planets"];
						if (num7 < 2)
						{
							num7 = 2;
						}
						num7 = Math.Min(num7, 3);
						if (num7 > 3)
						{
							Debug.Log("Planets more than 3 works but is an unreleased feature right now. If you're reading this, please do not release a mod to disable it or share footage, so we can keep the spoiler");
						}
						floor4.prevfloor.numPlanets = num7;
					}
					break;
				case LevelEventType.AutoPlayTiles:
					auto = item5.GetBool("enabled");
					showStatusText = item5.GetBool("showStatusText");
					item5.TryGetAndSet("safetyTiles", ref output);
					break;
				case LevelEventType.ScaleMargin:
					num8 = item5.GetFloat("scale") / 100f;
					break;
				case LevelEventType.Hide:
					hideJudgment = item5.GetBool("hideJudgment");
					hideIcon = item5.GetBool("hideTileIcon");
					break;
				case LevelEventType.FreeRoam:
					if (item5.GetInt("duration") >= 2)
					{
						num9 = 0f;
					}
					break;
				case LevelEventType.ChangeTrack:
					color1 = item5.GetColor("trackColor");
					color2 = item5.GetColor("secondaryTrackColor");
					colorAnimDur = item5.GetFloat("trackColorAnimDuration");
					colorType = (TrackColorType)item5["trackColorType"];
					pulseType = (TrackColorPulse)item5["trackColorPulse"];
					pulseLength = (int)item5["trackPulseLength"];
					startOfColorChange = floor4.seqID;
					animationType = (TrackAnimationType)item5["trackAnimation"];
					animationType2 = (TrackAnimationType2)item5["trackDisappearAnimation"];
					style = (TrackStyle)item5["trackStyle"];
					num3 = item5.GetFloat("beatsAhead");
					num4 = item5.GetFloat("beatsBehind");
					MoveColorTrackTempValues();
					break;
				}
			}
			floor4.numPlanets = num7;
			floor4.isSafe = output;
			floor4.auto = auto;
			floor4.showStatusText = showStatusText;
			floor4.hideJudgment = hideJudgment;
			floor4.hideIcon = hideIcon;
			floor4.marginScale = num8;
			floor4.lengthMult = lengthMult;
			floor4.widthMult = widthMult;
			floor4.planetEase = planetEase;
			floor4.planetEaseParts = planetEaseParts;
			floor4.planetEasePartBehavior = planetEasePartBehavior;
			floor4.stickToFloor = flag;
			zero += new Vector2(Mathf.Cos(Convert.ToSingle(floor4.entryangle) - MathF.PI / 2f), Mathf.Sin(Convert.ToSingle(floor4.entryangle) + MathF.PI / 2f)) * ((0f - (floor4.radiusScale - 1f)) * ADOBase.controller.tileSize);
			floor4.transform.position = floor4.startPos + new Vector3(vector2.x, vector2.y, 0f) + new Vector3(zero.x, zero.y, 0f);
			floor4.offsetPos = new Vector3(vector2.x, vector2.y, 0f) + new Vector3(zero.x, zero.y, 0f);
			vector2 = vector;
			floor4.customTexture = tempTexture;
			floor4.customTextureScale = tempTextureScale;
			floor4.outline = tempOutline;
			floor4.SetColor(tempColorType switch
			{
				TrackColorType.Stripes => ((floor4.seqID - startOfColorChange) % 2 == 0) ? tempColor1 : tempColor2, 
				TrackColorType.Rainbow => Color.white, 
				_ => tempColor1, 
			});
			floor4.styleNum = (int)tempStyle;
			floor4.UpdateAngle();
			floor4.SetTrackStyle(tempStyle, initial: true, customShadowColor);
			ffxChangeTrack orAddComponent = floor4.GetOrAddComponent<ffxChangeTrack>();
			orAddComponent.color1 = tempColor1;
			orAddComponent.color2 = tempColor2;
			orAddComponent.colorType = tempColorType;
			orAddComponent.colorAnimDuration = tempColorAnimDur;
			orAddComponent.pulseType = tempPulseType;
			orAddComponent.pulseLength = tempPulseLength;
			orAddComponent.startOfColorChange = tempStartOfColorChange;
			orAddComponent.texture = tempTexture;
			float num18 = speed / num6;
			float num19 = speed / num5;
			orAddComponent.animationType = animationType;
			orAddComponent.animationType2 = animationType2;
			orAddComponent.tilesAhead = num3 * (flag2 ? num18 : num19);
			orAddComponent.tilesBehind = num4 * (flag2 ? num18 : num19);
			floor4.glowMultiplier = tempGlowMult;
			Vector3 startScale = (floor4.transform.localScale = new Vector3(output3, output3, 0f));
			floor4.startScale = startScale;
			floor4.SetOpacity(scrController.instance.paused ? Mathf.Max(output2, 0.1f) : output2);
			floor4.opacityVal = output2;
			floor4.rotationOffset = output4;
			floor4.SetRotation(output4);
			floor4.stickToFloor = output5;
			output3 = num10;
			output2 = num9;
			output4 = num11;
			output5 = flag;
			a = Mathf.Max(a, speed * bpm2 * num2);
			num12++;
			MoveColorTrackTempValues();
		}
		if (ADOBase.customLevel != null)
		{
			ADOBase.customLevel.highestBPM = a;
		}
		num7 = 2;
		lm.ClearFreeroam();
		List<LevelEvent>[] array2 = new List<LevelEvent>[floors.Count];
		for (int j = 0; j < events.Count; j++)
		{
			int floor = events[j].floor;
			if (array2[floor] == null)
			{
				array2[floor] = new List<LevelEvent>();
			}
			array2[floor].Add(events[j]);
		}
		foreach (scrFloor floor5 in floors)
		{
			List<LevelEvent> list = array2[floor5.seqID];
			if (list == null)
			{
				continue;
			}
			foreach (LevelEvent item6 in list)
			{
				switch (item6.eventType)
				{
				case LevelEventType.FreeRoam:
					if (!(floor5.nextfloor == null) && item6.GetInt("duration") >= 2)
					{
						floor5.freeroamRegion = num13;
						num13++;
						floor5.freeroam = true;
						floor5.freeroamDimensions = (Vector2)item6["size"];
						floor5.freeroamOffset = (Vector2)item6["positionOffset"];
						int num22 = item6.GetInt("duration");
						int val = item6.GetInt("outTime");
						val = Math.Min(val, num22 - 1);
						floor5.freeroamEndEarlyBeats = val;
						floor5.freeroamEndEase = (Ease)item6["outEase"];
						item6.TryGetAndSet("outCam", ref floor5.moveCamAtFreeroamEnd);
						item6.TryGetAndSet("hitsoundOnBeats", ref floor5.freeroamSoundOnBeat);
						item6.TryGetAndSet("hitsoundOffBeats", ref floor5.freeroamSoundOffBeat);
						floor5.SetOpacity(scrController.instance.paused ? 0.1f : 0f);
						floor5.opacityVal = 0f;
						lm.MakeFreeroamGrid(floor5);
					}
					break;
				case LevelEventType.FreeRoamTwirl:
					if (!(floor5.nextfloor == null))
					{
						Vector2 vector7 = (Vector2)item6["position"];
						int num21 = (int)floor5.freeroamDimensions.x * (int)vector7.y + (int)vector7.x;
						if (!((float)num21 >= floor5.freeroamDimensions.x * floor5.freeroamDimensions.y))
						{
							scrFloor obj2 = lm.listFreeroam[floor5.freeroamRegion][num21];
							obj2.floorIcon = FloorIcon.Swirl;
							obj2.UpdateIconSprite();
							obj2.isSwirl = true;
						}
					}
					break;
				case LevelEventType.FreeRoamRemove:
				{
					if (floor5.nextfloor == null)
					{
						break;
					}
					Vector2 vector8 = (Vector2)item6["position"];
					Vector2 vector9 = (Vector2)item6["size"];
					for (int k = (int)vector8.y; k < (int)vector8.y + (int)vector9.y; k++)
					{
						for (int l = (int)vector8.x; l < (int)vector8.x + (int)vector9.x; l++)
						{
							int num23 = (int)floor5.freeroamDimensions.x * k + l;
							if (!((float)num23 >= floor5.freeroamDimensions.x * floor5.freeroamDimensions.y))
							{
								scrFloor obj3 = lm.listFreeroam[floor5.freeroamRegion][num23];
								obj3.isLandable = false;
								obj3.transform.position = Vector3.one * 99999f;
								obj3.startPos = obj3.transform.position;
								obj3.freeroamRemoved = true;
							}
						}
					}
					break;
				}
				case LevelEventType.FreeRoamWarning:
					if (!(floor5.nextfloor == null))
					{
						Vector2 vector6 = (Vector2)item6["position"];
						int num20 = (int)floor5.freeroamDimensions.x * (int)vector6.y + (int)vector6.x;
						if (!((float)num20 >= floor5.freeroamDimensions.x * floor5.freeroamDimensions.y))
						{
							lm.listFreeroam[floor5.freeroamRegion][num20].isWarning = true;
						}
					}
					break;
				}
			}
		}
		ffxFlashPlus.legacyFlash = levelData.legacyFlash;
		ffxCameraPlus.legacyRelativeTo = levelData.legacyCamRelativeTo;
		ColourScheme currentColourScheme = scrVfx.instance.currentColourScheme;
		currentColourScheme.colourText = levelData.defaultTextColor;
		currentColourScheme.colourTextShadow = levelData.defaultTextShadowColor;
		int num24 = 0;
		foreach (scrFloor floor6 in floors)
		{
			FloorIcon floorIcon = FloorIcon.None;
			List<LevelEvent> list2 = array[floor6.seqID];
			List<LevelEvent> list3 = new List<LevelEvent>(list2);
			if (floor6.seqID > 0)
			{
				list3.AddRange(array[floor6.seqID - 1].Where((LevelEvent e) => e.eventType == LevelEventType.SetSpeed));
			}
			bool flag5 = false;
			floor6.usedCustomFloorIcon = false;
			if (list3.Count > 0)
			{
				flag5 = list3.Any((LevelEvent e) => e.eventType == LevelEventType.EditorComment);
				floorIcon = FloorIcon.Vfx;
				floor6.eventIcon = LevelEventType.None;
				bool flag6 = false;
				int num25 = 0;
				int num26 = 2;
				LevelEventType levelEventType = ((list2.Count > 0) ? list2[0].eventType : LevelEventType.AddDecoration);
				LevelEventType filteredEvent = GCS.filteredEvent;
				bool flag7 = filteredEvent != LevelEventType.None && scrController.instance.paused;
				bool flag8 = false;
				LevelEvent levelEvent2 = list2.Find((LevelEvent e) => e.eventType == LevelEventType.SetFloorIcon);
				if (levelEvent2 != null)
				{
					floor6.usedCustomFloorIcon = true;
					floor6.floorIcon = RDUtils.ParseEnum(((CustomFloorIcon)levelEvent2["icon"]/*cast due to .constrained prefix*/).ToString(), FloorIcon.None);
				}
				else
				{
					foreach (LevelEvent item7 in list3)
					{
						if (!item7.active)
						{
							continue;
						}
						LevelEventType eventType = item7.eventType;
						if (eventType == filteredEvent && flag7)
						{
							flag8 = true;
						}
						if (eventType == LevelEventType.Checkpoint)
						{
							if (num25 >= 1)
							{
								continue;
							}
							num25 = 1;
							floorIcon = FloorIcon.Checkpoint;
							flag6 = true;
						}
						switch (eventType)
						{
						case LevelEventType.SetSpeed:
						{
							if (num25 >= 2)
							{
								continue;
							}
							num25 = 2;
							float num29 = ((floor6.seqID <= 0) ? 1f : floors[floor6.seqID - 1].speed);
							float num30 = (floor6.speed - num29) / num29;
							float num31 = Mathf.Abs(num30);
							if (num31 > 0.05f)
							{
								floorIcon = ((!(num30 > 0f)) ? ((1f - num31 > 0.45f) ? FloorIcon.Snail : FloorIcon.DoubleSnail) : ((num31 < 1.05f) ? FloorIcon.Rabbit : FloorIcon.DoubleRabbit));
							}
							else if (item7.floor == floor6.seqID)
							{
								floorIcon = FloorIcon.SameSpeed;
								num25 = 0;
							}
							if (item7.floor == floor6.seqID)
							{
								flag6 = true;
							}
							break;
						}
						case LevelEventType.Twirl:
							if (num25 >= 2)
							{
								continue;
							}
							num25 = 2;
							floorIcon = FloorIcon.Swirl;
							flag6 = true;
							break;
						default:
							if (item7.eventType == LevelEventType.Hold)
							{
								if (num25 >= 2)
								{
									continue;
								}
								floorIcon = ((floor6.holdLength != 0) ? FloorIcon.HoldArrowLong : FloorIcon.HoldArrowShort);
								flag6 = true;
							}
							else if (item7.eventType == LevelEventType.MultiPlanet)
							{
								if (num25 >= 2)
								{
									continue;
								}
								int num27 = ((floor6.seqID <= 0) ? 1 : floors[floor6.seqID - 1].numPlanets);
								float num28 = floor6.numPlanets;
								if (num28 == 2f)
								{
									floorIcon = FloorIcon.MultiPlanetTwo;
								}
								else if (num28 > (float)num27)
								{
									floorIcon = FloorIcon.MultiPlanetThreeMore;
								}
								else if (num28 <= (float)num27)
								{
									floorIcon = FloorIcon.MultiPlanetThreeLess;
								}
								flag6 = true;
							}
							else if (!flag6 && eventType != levelEventType && num25 == 0 && item7.floor == floor6.seqID)
							{
								flag6 = true;
							}
							break;
						}
						if (num25 == num26)
						{
							break;
						}
					}
				}
				if (!flag6)
				{
					if (list2.Count > 0)
					{
						floorIcon = FloorIcon.Vfx;
						floor6.eventIcon = levelEventType;
					}
					else if (floorIcon == FloorIcon.Vfx)
					{
						floorIcon = FloorIcon.None;
					}
				}
				if (flag7)
				{
					if (flag8)
					{
						floorIcon = FloorIcon.Vfx;
						floor6.eventIcon = filteredEvent;
					}
					else
					{
						floorIcon = FloorIcon.None;
					}
				}
			}
			if (num24 > 0 && (floorIcon == FloorIcon.Vfx || floorIcon == FloorIcon.None))
			{
				floorIcon = ((num24 == 1) ? FloorIcon.HoldReleaseShort : FloorIcon.HoldReleaseLong);
			}
			num24 = 0;
			if (list3.Exists((LevelEvent x) => x.eventType == LevelEventType.Hold))
			{
				num24 = ((floor6.holdLength == 0) ? 1 : 2);
			}
			if (!floor6.usedCustomFloorIcon)
			{
				floor6.floorIcon = floorIcon;
			}
			floor6.UpdateIconSprite();
			floor6.UpdateCommentGlow(scrController.instance.paused && flag5);
		}
		if (scrController.instance.paused)
		{
			return;
		}
		Dictionary<int, Dictionary<string, Tuple<int, float, bool, int>>> dictionary = new Dictionary<int, Dictionary<string, Tuple<int, float, bool, int>>>();
		foreach (LevelEvent item8 in events.FindAll((LevelEvent x) => x.info.type == LevelEventType.RepeatEvents))
		{
			if (item8.active)
			{
				bool flag9 = (RepeatType)item8["repeatType"] == RepeatType.Beat;
				int item = (int)item8[flag9 ? "repetitions" : "floorCount"];
				float item2 = (flag9 ? item8.GetFloat("interval") : (-1f));
				string[] array3 = (item8.GetString("tag") ?? "").Split(" ");
				bool item3 = item8.GetBool("executeOnCurrentFloor");
				int item4 = item8.GetInt("gapLength");
				if (!dictionary.ContainsKey(item8.floor))
				{
					dictionary[item8.floor] = new Dictionary<string, Tuple<int, float, bool, int>>();
				}
				string[] array4 = array3;
				foreach (string key in array4)
				{
					dictionary[item8.floor][key] = new Tuple<int, float, bool, int>(item, item2, item3, item4);
				}
			}
		}
		Dictionary<int, string[]> dictionary2 = new Dictionary<int, string[]>();
		foreach (LevelEvent item9 in events.FindAll((LevelEvent x) => x.info.type == LevelEventType.SetConditionalEvents))
		{
			if (item9.active)
			{
				string[] value = new string[9]
				{
					item9.GetString("perfectTag"),
					item9.GetString("earlyPerfectTag").NullIfEmptyConditionalTag() ?? item9.GetString("hitTag"),
					item9.GetString("latePerfectTag").NullIfEmptyConditionalTag() ?? item9.GetString("hitTag"),
					item9.GetString("veryEarlyTag").NullIfEmptyConditionalTag() ?? item9.GetString("barelyTag"),
					item9.GetString("veryLateTag").NullIfEmptyConditionalTag() ?? item9.GetString("barelyTag"),
					item9.GetString("tooEarlyTag").NullIfEmptyConditionalTag() ?? item9.GetString("missTag"),
					item9.GetString("tooLateTag").NullIfEmptyConditionalTag() ?? item9.GetString("missTag"),
					item9.GetString("lossTag"),
					item9.GetString("onCheckpointTag")
				};
				dictionary2.Add(item9.floor, value);
				floors[item9.floor].hasConditionalChange = true;
			}
		}
		foreach (LevelEvent @event in events)
		{
			if (!@event.active)
			{
				continue;
			}
			int num32 = 0;
			float num33 = 0f;
			bool flag10 = false;
			int floor2 = @event.floor;
			int num34 = 1;
			if (@event.TryGet<string>("eventTag", out var output11))
			{
				string[] array5 = output11.Split(" ");
				if (dictionary.Keys.Contains(floor2))
				{
					Dictionary<string, Tuple<int, float, bool, int>> dictionary3 = dictionary[floor2];
					string[] array4 = array5;
					foreach (string key2 in array4)
					{
						if (dictionary3.ContainsKey(key2))
						{
							num32 = dictionary3[key2].Item1;
							num33 = dictionary3[key2].Item2;
							flag10 = dictionary3[key2].Item3;
							num34 = dictionary3[key2].Item4;
							break;
						}
					}
				}
			}
			scrFloor scrFloor3 = floors[@event.floor];
			for (num = 0; num <= num32; num++)
			{
				bool flag11 = num33 > 0f;
				int num35 = scrFloor3.seqID + ((!flag11) ? (num * num34) : 0);
				if (num35 >= floors.Count)
				{
					break;
				}
				scrFloor scrFloor4 = floors[num35];
				float offset = (float)(flag11 ? ((double)(num33 * (float)num * 180f)) : ((flag10 ? 0.0 : (scrFloor4.entryBeat - scrFloor3.entryBeat)) * 180.0));
				ffxPlusBase ffxPlusBase2 = ApplyEvent(@event, bpm, num2, floors, offset, flag10 ? new int?(scrFloor4.seqID) : ((int?)null));
				if (EditorConstants.soloTypes.Contains(@event.eventType) || @event.eventType == LevelEventType.RepeatEvents)
				{
					break;
				}
				if (!dictionary2.Keys.Contains(floor2))
				{
					continue;
				}
				if (ffxPlusBase2 == null)
				{
					break;
				}
				bool[] array6 = new bool[9];
				bool flag12 = false;
				for (int num36 = 0; num36 < dictionary2[floor2].Length; num36++)
				{
					string text2 = dictionary2[floor2][num36];
					array6[num36] = !text2.IsNoneConditionalTag() && @event.GetString("eventTag") == text2;
					if (array6[num36])
					{
						flag12 = true;
					}
				}
				if (flag12)
				{
					ffxPlusBase2.conditionalInfo = array6;
					break;
				}
			}
		}
		ffxCameraPlus ffxCameraPlus2 = floors[0].gameObject.AddComponent<ffxCameraPlus>();
		floors[0].plusEffects.Insert(0, ffxCameraPlus2);
		ffxCameraPlus2.startTime = 0.0;
		ffxCameraPlus2.duration = 0f;
		ffxCameraPlus2.targetPos = levelData.camPosition * ADOBase.controller.tileSize;
		ffxCameraPlus2.targetRot = levelData.camRotation;
		ffxCameraPlus2.targetZoom = levelData.camZoom / 100f;
		ffxCameraPlus2.ease = Ease.Linear;
		ffxCameraPlus2.movementType = levelData.camRelativeTo;
		ffxCameraPlus2.dontDisable = levelData.camEnabledOnLowVFX;
		void ApplyCoreEventsToFloors(List<LevelEvent>[] floorEvents)
		{
			if (floorEvents == null)
			{
				floorEvents = new List<LevelEvent>[floors.Count];
				int num37 = 0;
				for (num37 = 0; num37 < floorEvents.Length; num37++)
				{
					floorEvents[num37] = new List<LevelEvent>();
				}
				for (num37 = 0; num37 < events.Count; num37++)
				{
					LevelEvent levelEvent3 = events[num37];
					floorEvents[levelEvent3.floor].Add(levelEvent3);
				}
			}
			float num38 = 1f;
			float speed2 = 1f;
			bool flag13 = false;
			bool flag14 = false;
			float bpm3 = levelData.bpm;
			float radiusScale = 1f;
			int num39 = 2;
			int num40 = 2;
			float widthMult2 = 1f;
			float lengthMult2 = 1f;
			foreach (scrFloor floor7 in floors)
			{
				floor7.extraBeats = 0f;
				List<LevelEvent> obj4 = floorEvents[floor7.seqID];
				List<LevelEvent> list4 = obj4.FindAll((LevelEvent e) => e.eventType == LevelEventType.SetSpeed);
				List<float> list5 = list4.ConvertAll((LevelEvent e) => e.GetFloat("angleOffset")).Distinct().ToList();
				list5.Sort();
				foreach (LevelEvent item10 in obj4)
				{
					switch (item10.eventType)
					{
					case LevelEventType.Twirl:
						flag14 = !flag14;
						floor7.isSwirl = true;
						break;
					case LevelEventType.Checkpoint:
						floor7.gameObject.AddComponent<ffxCheckpoint>();
						break;
					case LevelEventType.Pause:
						if (floor7.nextfloor != null && !floor7.midSpin)
						{
							floor7.extraBeats += item10.GetFloat("duration");
							floor7.countdownTicks = item10.GetInt("countdownTicks");
							AngleCorrectionDirection output13 = AngleCorrectionDirection.None;
							if (item10.TryGetAndSet("angleCorrectionDir", ref output13))
							{
								floor7.angleCorrectionType = (float)output13;
							}
						}
						break;
					case LevelEventType.FreeRoam:
						if (floor7.nextfloor != null && item10.GetInt("duration") >= 2)
						{
							double num41 = floor7.angleLength / 3.1415927410125732;
							floor7.extraBeats += (float)Math.Max(num41, (double)item10.GetInt("duration") - num41);
							floor7.countdownTicks = item10.GetInt("countdownTicks");
							AngleCorrectionDirection output12 = AngleCorrectionDirection.None;
							if (item10.TryGetAndSet("angleCorrectionDir", ref output12))
							{
								floor7.angleCorrectionType = (float)output12;
							}
							item10.TryGetAndSet("hitsoundOnBeats", ref floor7.freeroamSoundOnBeat);
							item10.TryGetAndSet("hitsoundOffBeats", ref floor7.freeroamSoundOffBeat);
						}
						break;
					case LevelEventType.ScaleRadius:
						radiusScale = item10.GetFloat("scale") / 100f;
						break;
					case LevelEventType.Hold:
					{
						int num42 = (int)item10["duration"];
						floor7.holdLength = (((bool)floor7.nextfloor && num42 >= 0) ? num42 : (-1));
						break;
					}
					case LevelEventType.TileDimensions:
						lengthMult2 = item10.GetFloat("length") / 100f;
						widthMult2 = item10.GetFloat("width") / 100f;
						break;
					case LevelEventType.MultiPlanet:
					{
						num39 = (int)item10["planets"];
						num39 = Math.Max(num39, 2);
						num39 = Math.Min(num39, 3);
						if (num39 > 3)
						{
							Debug.Log("Planets more than 3 works but is an unreleased feature right now. If you're reading this, please do not release a mod to disable it or share footage, so we can keep the spoiler");
						}
						scrFloor prevfloor = floor7.prevfloor;
						if ((object)prevfloor != null && prevfloor.midSpin)
						{
							floor7.prevfloor.numPlanets = num39;
						}
						break;
					}
					case LevelEventType.Multitap:
						floor7.tapsNeeded = (int)item10["taps"];
						floor7.tapsSoFar = 0;
						break;
					}
				}
				if (list5.Count > 0)
				{
					float num43 = (scrMisc.ApproximatelyFloor(floor7.entryangle, floor7.exitangle) ? 360f : ((float)scrMisc.GetAngleMoved(floor7.entryangle, floor7.exitangle, !flag14) * 57.29578f));
					float num44 = bpm3 * num38;
					float num45 = 60f / num44;
					float num46 = list5[0] / 180f * num45;
					foreach (float angleOffset in list5)
					{
						LevelEvent levelEvent4 = list4.FindLast((LevelEvent e) => e.GetFloat("angleOffset") == angleOffset);
						float num47 = list5.Find((float e) => e > angleOffset);
						if (num47 == 0f)
						{
							num47 = num43;
						}
						float num48 = 0f;
						if ((SpeedType)levelEvent4["speedType"] == SpeedType.Bpm)
						{
							num48 = levelEvent4.GetFloat("beatsPerMinute");
						}
						else
						{
							List<LevelEvent> list6 = list4.FindAll((LevelEvent e) => e.GetFloat("angleOffset") == angleOffset);
							for (int num49 = 0; num49 < list6.Count; num49++)
							{
								LevelEvent levelEvent5 = list6[num49];
								if ((SpeedType)levelEvent5["speedType"] == SpeedType.Bpm)
								{
									num48 = levelEvent5.GetFloat("beatsPerMinute");
								}
								else
								{
									float num50 = levelEvent5.GetFloat("bpmMultiplier");
									num48 = ((num49 <= 0) ? (bpm3 * num38 * num50) : (num48 * num50));
								}
							}
						}
						float num51 = 60f / num48;
						bool num52 = angleOffset > 0f && angleOffset <= num43;
						float num53 = (num47 - angleOffset) / 180f * num51;
						num46 += num53;
						if (num52)
						{
							flag13 = true;
						}
						num38 = num48 / bpm3;
					}
					if (flag13)
					{
						speed2 = 60f / num46 * (num43 / 180f) / bpm3;
					}
				}
				floor7.radiusScale = radiusScale;
				if (flag13)
				{
					floor7.speed = speed2;
					flag13 = false;
				}
				else
				{
					floor7.speed = num38;
				}
				floor7.isCCW = flag14;
				floor7.numPlanets = num39;
				floor7.lengthMult = lengthMult2;
				floor7.widthMult = widthMult2;
				if (num39 > num40)
				{
					num40 = num39;
				}
			}
			foreach (scrPlayer item11 in ADOBase.playerManager)
			{
				item11.planetarySystem.maxPlanetColors = num40;
			}
		}
		void MoveColorTrackTempValues()
		{
			tempColor1 = color1;
			tempColor2 = color2;
			tempColorType = colorType;
			tempColorAnimDur = colorAnimDur;
			tempPulseType = pulseType;
			tempTexture = texture;
			tempTextureScale = textureScale;
			tempPulseLength = pulseLength;
			tempStartOfColorChange = startOfColorChange;
			tempOutline = outline;
			tempStyle = style;
			tempGlowMult = glowMult;
		}
	}

	public static ffxPlusBase ApplyEvent(LevelEvent evnt, float bpm, float pitch, List<scrFloor> floors, float offset = 0f, int? customFloorID = null)
	{
		int num = customFloorID ?? evnt.floor;
		GameObject gameObject = floors[num].gameObject;
		float speed = floors[num].speed;
		float crotchet = 60f / (bpm * pitch * speed);
		if (instance == null && evnt.eventType == LevelEventType.CustomBackground)
		{
			return null;
		}
		ffxPlusBase ffxPlusBase2 = evnt.eventType switch
		{
			LevelEventType.Checkpoint => gameObject.GetComponent<ffxCheckpoint>(), 
			LevelEventType.SetHitsound => gameObject.AddComponent<ffxSetHitsound>(), 
			LevelEventType.SetHoldSound => gameObject.AddComponent<ffxSetHoldsound>(), 
			LevelEventType.CustomBackground => gameObject.AddComponent<ffxCustomBackgroundPlus>(), 
			LevelEventType.MoveCamera => gameObject.AddComponent<ffxCameraPlus>(), 
			LevelEventType.Flash => gameObject.AddComponent<ffxFlashPlus>(), 
			LevelEventType.RecolorTrack => gameObject.AddComponent<ffxRecolorFloorPlus>(), 
			LevelEventType.MoveTrack => gameObject.AddComponent<ffxMoveFloorPlus>(), 
			LevelEventType.MoveDecorations => gameObject.AddComponent<ffxMoveDecorationsPlus>(), 
			LevelEventType.SetParticle => gameObject.AddComponent<ffxSetParticlePlus>(), 
			LevelEventType.EmitParticle => gameObject.AddComponent<ffxEmitParticlePlus>(), 
			LevelEventType.SetText => gameObject.AddComponent<ffxSetTextPlus>(), 
			LevelEventType.SetObject => gameObject.AddComponent<ffxSetObjectPlus>(), 
			LevelEventType.SetDefaultText => gameObject.AddComponent<ffxSetDefaultText>(), 
			LevelEventType.SetFilter => gameObject.AddComponent<ffxSetFilterPlus>(), 
			LevelEventType.SetFilterAdvanced => gameObject.AddComponent<ffxSetFilterAdvancedPlus>(), 
			LevelEventType.HallOfMirrors => gameObject.AddComponent<ffxHallOfMirrorsPlus>(), 
			LevelEventType.ShakeScreen => gameObject.AddComponent<ffxShakeScreenPlus>(), 
			LevelEventType.Bloom => gameObject.AddComponent<ffxBloomPlus>(), 
			LevelEventType.ScreenTile => gameObject.AddComponent<ffxScreenTilePlus>(), 
			LevelEventType.ScreenScroll => gameObject.AddComponent<ffxScreenScrollPlus>(), 
			LevelEventType.CallMethod => gameObject.AddComponent<ffxCallMethod>(), 
			LevelEventType.AddComponent => gameObject.AddComponent<ffxAddComponent>(), 
			LevelEventType.KillPlayer => gameObject.AddComponent<ffxKillPlayer>(), 
			LevelEventType.PlaySound => gameObject.AddComponent<ffxPlaySound>(), 
			LevelEventType.ScalePlanets => gameObject.AddComponent<ffxScalePlanetsPlus>(), 
			LevelEventType.SetFrameRate => gameObject.AddComponent<ffxSetFrameRatePlus>(), 
			LevelEventType.SetInputEvent => gameObject.AddComponent<ffxSetInputEventPlus>(), 
			_ => null, 
		};
		if (ffxPlusBase2 == null)
		{
			return null;
		}
		ffxPlusBase2.floorID = num;
		ffxPlusBase2.floors = floors;
		ffxPlusBase2.crotchet = crotchet;
		ffxPlusBase2.Decode(evnt);
		floors[num].plusEffects.Add(ffxPlusBase2);
		evnt.TryGet<float>("angleOffset", out var output);
		ffxPlusBase2.SetStartTime(bpm, output + offset);
		evnt.TryGet<string>("eventTag", out var output2);
		if (!string.IsNullOrEmpty(output2))
		{
			string[] array = output2.Split(" ");
			foreach (string key in array)
			{
				scrDecorationManager scrDecorationManager2 = scrDecorationManager.instance;
				if (scrDecorationManager2 != null)
				{
					if (!scrDecorationManager2.hitboxEventTagDecorations.TryGetValue(key, out var value))
					{
						continue;
					}
					foreach (scrDecoration item in value)
					{
						item.hitboxEvents.Add(ffxPlusBase2);
					}
				}
				ffxPlusBase2.runManually = true;
			}
		}
		ffxPlusBase2.sourceLevelEvent = evnt;
		return ffxPlusBase2;
	}

	public static int IDFromTile(Tuple<int, TileRelativeTo> tile, int tileID, List<scrFloor> floors)
	{
		return Mathf.Clamp(tile.Item2 switch
		{
			TileRelativeTo.ThisTile => tileID + tile.Item1, 
			TileRelativeTo.Start => tile.Item1, 
			TileRelativeTo.End => floors.Count - 1 + tile.Item1, 
			_ => 0, 
		}, 0, floors.Count - 1);
	}

	public static Tuple<int, TileRelativeTo> StringToTile(string sTile)
	{
		if (sTile.StartsWith("(") && sTile.EndsWith(")"))
		{
			sTile = sTile.Substring(1, sTile.Length - 2);
		}
		string[] array = sTile.Split(',');
		return new Tuple<int, TileRelativeTo>(int.Parse(array[0]), (TileRelativeTo)Enum.Parse(typeof(TileRelativeTo), array[1]));
	}

	public void UpdateDecorationObjects(bool reloadDecorations = true)
	{
		if (ADOBase.controller.visualQuality == VisualQuality.Low && (ADOBase.isOfficialLevel || Persistence.forceVisualSettings) && !ADOBase.levelIsMikoSkip)
		{
			return;
		}
		decManager.ClearDecorations();
		foreach (LevelEvent decoration in decorations)
		{
			if (!decoration.active)
			{
				continue;
			}
			bool spritesLoaded = false;
			string output;
			if (reloadDecorations)
			{
				decManager.CreateDecoration(decoration, out spritesLoaded);
			}
			else if (decoration.TryGet<string>("decorationImage", out output))
			{
				bool flag = output.StartsWith("prefab:", StringComparison.CurrentCultureIgnoreCase);
				if (!string.IsNullOrEmpty(output) && !flag && !ADOBase.IsNotAMikoSkipMandatorySprite(output))
				{
					string filePath = Path.Combine(Path.GetDirectoryName(levelPath), output);
					imgHolder.GetOrAddSprite(output, filePath, out var status);
					ADOBase.editor?.UpdateImageLoadResult(output, status);
				}
			}
		}
		foreach (LevelEvent @event in events)
		{
			if (@event.eventType == LevelEventType.MoveDecorations)
			{
				string output2 = null;
				if (@event.TryGetAndSet("decorationImage", ref output2, onlyIfEnabled: true) && !output2.IsNullOrEmpty())
				{
					string filePath2 = Path.Combine(Path.GetDirectoryName(levelPath), output2);
					imgHolder.GetOrAddSprite(output2, filePath2, out var status2);
					ADOBase.editor?.UpdateImageLoadResult(output2, status2);
				}
			}
		}
	}

	public void RemakePath(bool applyEventsToFloors = true, bool remakeLevel = true)
	{
		ADOBase.conductor.SetupConductorWithLevelData(levelData);
		if (levelMaker == null)
		{
			levelMaker = scrLevelMaker.instance;
		}
		if (remakeLevel)
		{
			levelMaker.leveldata = levelData.pathData;
			levelMaker.floorAngles = levelData.angleData.ToArray();
			levelMaker.isOldLevel = levelData.isOldLevel;
			bool bigTiles = levelData.tileShape == TileShape.Long;
			levelMaker.GetComponent<scrLevelMaker2>().BigTiles = bigTiles;
			ADOBase.controller.tileShape = levelData.tileShape;
			levelMaker.MakeLevel();
		}
		scrConductor.instance.countdownTicks = levelData.countdownTicks;
		if (applyEventsToFloors && remakeLevel)
		{
			scrDecorationManager.instance.ResetDecorationHitboxEvents();
			ApplyEventsToFloors(floors);
		}
		levelMaker.DrawHolds(!remakeLevel);
		levelMaker.DrawMultiPlanet(!remakeLevel);
	}

	public void ReloadCustomSounds(bool force = false)
	{
		StartCoroutine(ReloadCustomSoundsCo(force));
	}

	private IEnumerator ReloadCustomSoundsCo(bool force = false)
	{
		List<LevelEvent> list = events.FindAll((LevelEvent x) => x.eventType == LevelEventType.PlaySound && !Enum.IsDefined(typeof(HitSound), x.GetString("hitsound")));
		HashSet<string> addedKeys = new HashSet<string>();
		foreach (LevelEvent item in list)
		{
			string text = item.GetString("hitsound");
			string text2 = text + "*external";
			if (!addedKeys.Contains(text2))
			{
				addedKeys.Add(text2);
				if (!ADOBase.audioManager.audioLib.ContainsKey(text2) || force)
				{
					ADOBase.audioManager.audioLib.Remove(text2);
					yield return LoadSoundCo(text);
				}
			}
		}
	}

	private IEnumerator LoadSoundCo(string filename)
	{
		string path = Path.Combine(Path.GetDirectoryName(levelPath), filename);
		yield return ADOBase.audioManager.FindOrLoadAudioClipExternal(path, mp3Streaming: false, 0f, stream: false);
	}

	public void UpdateBackgroundSprites()
	{
		if (!ADOBase.customLevel && backgroundsLoaded)
		{
			return;
		}
		foreach (LevelEvent item in events.FindAll((LevelEvent x) => x.eventType == LevelEventType.CustomBackground))
		{
			string text = item["bgImage"].ToString();
			if (!string.IsNullOrEmpty(text))
			{
				string filePath = Path.Combine(Path.GetDirectoryName(levelPath), text);
				imgHolder.GetOrAddSprite(text, filePath, out var _);
			}
		}
		backgroundsLoaded = true;
	}

	public void UpdateFloorSprites()
	{
		printesp("");
		if (!ADOBase.customLevel && floorSpritesLoaded)
		{
			return;
		}
		foreach (LevelEvent item in events.FindAll(delegate(LevelEvent x)
		{
			LevelEventType eventType = x.eventType;
			return eventType == LevelEventType.ColorTrack || eventType == LevelEventType.RecolorTrack;
		}))
		{
			string text = item["trackTexture"] as string;
			if (!string.IsNullOrEmpty(text))
			{
				string filePath = Path.Combine(Path.GetDirectoryName(levelPath), text);
				imgHolder.AddTexture(text, out var _, filePath);
			}
		}
		floorSpritesLoaded = true;
	}

	public bool Play(int seqID = 0, bool remakeFloors = true)
	{
		if (levelData.miscSettings.TryGet<string>("customClass", out var output))
		{
			Type type = Type.GetType(output);
			if (type != null)
			{
				Level level = Activator.CreateInstance(type) as Level;
				scrController.instance.level = level;
			}
		}
		printesp("Play");
		if (floors.Count == 1)
		{
			return false;
		}
		scrController scrController2 = scrController.instance;
		scrConductor scrConductor2 = scrConductor.instance;
		scrController2.customTxtCongrats = ADOBase.customLevel.levelData.miscSettings["congratsText"] as string;
		scrController2.customTxtPurePerfect = ADOBase.customLevel.levelData.miscSettings["perfectText"] as string;
		scrController2.originalLevelName = scrController2.txtLevelName.text;
		ffxSetDefaultText.UpdateHudTexts();
		checkpointsUsed = scrController.checkpointsUsed;
		scrController2.gameworld = true;
		string fullCaptionTagged = levelData.fullCaptionTagged;
		if (!ADOBase.isInternalLevel)
		{
			scrController2.caption = fullCaptionTagged;
		}
		scrConductor2.onBeats.Clear();
		AudioManager.Instance.StopAllSounds();
		scrCamera.instance.Rewind();
		camParent.transform.position = Vector3.zero;
		scrConductor2.Rewind();
		scrController2.paused = false;
		scrController2.Awake_Rewind();
		scrUIController.instance.canvas.enabled = true;
		scrCamera scrCamera2 = scrCamera.instance;
		scrCamera2.zoomSize = 1f;
		if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
		{
			scrCamera2.userSizeMultiplier = 1f;
			if (ADOBase.editor != null)
			{
				ADOBase.editor.camUserSizeMultiplier = 1f;
			}
		}
		scrCamera2.SetHoldOffset(Vector3.zero);
		scrCamera2.lastEventRelativePosition = Vector2.zero;
		scrCamera2.lastUsedMovementType = CamMovementType.Player;
		scrVfxPlus.instance.vidOffset = (float)(int)levelData.miscSettings["vidOffset"] * 0.001f;
		scrCamera2.SetupRTCam(enable: true);
		scrFloor scrFloor2 = floors[seqID];
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			PlanetarySystem planetarySystem = item.planetarySystem;
			scrPlanet planetRed = planetarySystem.planetRed;
			scrPlanet planetBlue = planetarySystem.planetBlue;
			item.Rewind();
			foreach (scrPlanet planet in planetarySystem.planetList)
			{
				planet.transform.localPosition = Vector3.zero;
			}
			planetBlue.transform.localPosition = Vector3.right;
			planetarySystem.isCW = !scrFloor2.isCCW;
			planetarySystem.speed = scrFloor2.speed;
			planetarySystem.chosenPlanet = ((scrFloor2.seqID % 2 == 0) ? planetRed : planetBlue);
		}
		if (ADOBase.isLevelEditor)
		{
			scrConductor2.Start();
			FinishCustomLevelLoading(seqID, remakeFloors);
			scrController2.Start_Rewind(seqID);
			ADOBase.editor.inStrictlyEditingMode = false;
		}
		scrController2.rotationEase = scrFloor2.planetEase;
		scrController2.startedFromCheckpoint = seqID > 0;
		scrController2.forceNoCountdown = (scrConductor2.fastTakeoff = RDC.auto && seqID == 0);
		scrController2.txtAllStrictClear.gameObject.SetActive(value: false);
		scrController2.txtCongrats.gameObject.SetActive(value: false);
		scrController2.detailedResults.gameObject.SetActive(value: false);
		scrController2.txtPercent.gameObject.SetActive(value: false);
		if (!ADOBase.isLevelEditor)
		{
			StartCoroutine(scrController2.WaitForStartCo(seqID, remakeFloors));
		}
		return true;
	}

	public void FinishCustomLevelLoading(int seqID, bool remakeFloors = true)
	{
		if (ADOBase.isLevelEditor)
		{
			if (remakeFloors)
			{
				scrDecorationManager.instance.ResetDecorationHitboxEvents();
				ApplyEventsToFloors(floors);
			}
			else
			{
				foreach (scrFloor floor in floors)
				{
					ffxPlusBase[] components = floor.GetComponents<ffxPlusBase>();
					for (int i = 0; i < components.Length; i++)
					{
						components[i].triggered = false;
					}
				}
				ADOBase.controller.conditionalFloor = null;
				ADOBase.controller.ResetInputEventFfx();
			}
			foreach (scrPlayer item in ADOBase.playerManager)
			{
				item.planetarySystem.chosenPlanet.FirstFloorAngleSetup();
			}
			scrDecorationManager.instance.ResetDecorations();
		}
		foreach (scrFloor floor2 in floors)
		{
			if (floor2.seqID > seqID)
			{
				break;
			}
			floor2.topGlow.gameObject.SetActive(value: true);
		}
		UpdateVideo();
		if (ADOBase.isLevelEditor)
		{
			PrepVfx(seqID, remakeFloors);
		}
		if (ADOBase.isCLSLevel && !GCS.useNoFail && !GCS.useUnlockKeyLimiter)
		{
			Persistence.IncrementCustomWorldAttempts(levelData.Hash);
		}
		ADOBase.controller.currentSeqID = GCS.checkpointNum;
	}

	public static void PrepVfx(List<scrFloor> floors, int seqID, List<LevelEvent> events = null, bool remakeFloors = true)
	{
		List<LevelEvent>[] array = new List<LevelEvent>[floors.Count];
		if (events != null)
		{
			int num = 0;
			for (num = 0; num < array.Length; num++)
			{
				array[num] = new List<LevelEvent>();
			}
			for (num = 0; num < events.Count; num++)
			{
				LevelEvent levelEvent = events[num];
				array[levelEvent.floor].Add(levelEvent);
			}
		}
		scrVfxPlus scrVfxPlus2 = scrVfxPlus.instance;
		scrVfxPlus2.Reset();
		scrConductor scrConductor2 = scrConductor.instance;
		foreach (scrFloor floor in floors)
		{
			ffxChangeTrack component = floor.GetComponent<ffxChangeTrack>();
			if (component != null)
			{
				component.PrepFloor(remakeFloors);
			}
			floor.startPos = floor.transform.position;
			if (floor.opacityVal != 1f)
			{
				floor.SetOpacity(floor.opacityVal);
			}
			if (remakeFloors && events != null)
			{
				List<LevelEvent> list = array[floor.seqID];
				Dictionary<string, Tuple<int, float>> dictionary = new Dictionary<string, Tuple<int, float>>();
				string[] array2 = new string[9];
				bool flag = false;
				foreach (LevelEvent item3 in list.FindAll((LevelEvent x) => x.eventType == LevelEventType.RepeatEvents))
				{
					int item = item3.GetInt("repetitions");
					float item2 = item3.GetFloat("interval");
					string[] array3 = (item3.GetString("tag") ?? "").Split(" ");
					foreach (string key in array3)
					{
						dictionary[key] = new Tuple<int, float>(item, item2);
					}
				}
				foreach (LevelEvent item4 in list.FindAll((LevelEvent x) => x.eventType == LevelEventType.SetConditionalEvents))
				{
					for (int num3 = 0; num3 < array2.Length; num3++)
					{
						array2[num3] = item4.GetString(ConditionalEventTags[num3]);
					}
					flag = true;
				}
				foreach (LevelEvent item5 in list)
				{
					if (!item5.TryGet<string>("bgImage", out var output) || string.IsNullOrEmpty(output))
					{
						continue;
					}
					int num4 = 0;
					float num5 = 0f;
					string[] array3 = (item5.GetString("eventTag") ?? "").Split(" ");
					foreach (string key2 in array3)
					{
						if (dictionary.ContainsKey(key2))
						{
							num4 = dictionary[key2].Item1;
							num5 = dictionary[key2].Item2;
							break;
						}
					}
					for (int num6 = 0; num6 <= num4; num6++)
					{
						ffxCustomBackgroundPlus ffxCustomBackgroundPlus2 = floor.gameObject.AddComponent<ffxCustomBackgroundPlus>();
						ffxCustomBackgroundPlus2.SetStartTime(scrConductor2.bpm, item5.GetFloat("angleOffset") + num5 * (float)num6 * 180f);
						ffxCustomBackgroundPlus2.color = item5.GetColor("color");
						ffxCustomBackgroundPlus2.filePath = output;
						ffxCustomBackgroundPlus2.imageColor = item5.GetColor("imageColor");
						Vector2 vector = item5.Get<Vector2>("parallax");
						ffxCustomBackgroundPlus2.parallax = (item5.info.propertiesInfo["parallax"].CheckIfEnabled(item5, null) ? (vector / 100f) : Vector2.one);
						BgDisplayMode bgDisplayMode = (BgDisplayMode)item5["bgDisplayMode"];
						ffxCustomBackgroundPlus2.tiled = bgDisplayMode == BgDisplayMode.Tiled;
						ffxCustomBackgroundPlus2.fitScreen = bgDisplayMode != BgDisplayMode.Unscaled;
						ffxCustomBackgroundPlus2.scalingRatio = (float)item5.GetInt("scalingRatio") / 100f;
						ffxCustomBackgroundPlus2.looping = item5.GetBool("loopBG");
						ffxCustomBackgroundPlus2.lockRot = item5.GetBool("lockRot");
						ffxCustomBackgroundPlus2.imageSmoothing = item5.GetBool("imageSmoothing");
						floor.plusEffects.Add(ffxCustomBackgroundPlus2);
						if (flag)
						{
							bool[] array4 = new bool[9];
							for (int num7 = 0; num7 < array2.Length; num7++)
							{
								string text = array2[num7];
								array4[num7] = !text.IsNoneConditionalTag() && item5.GetString("eventTag") == text;
							}
							ffxCustomBackgroundPlus2.conditionalInfo = array4;
							break;
						}
					}
				}
			}
			IReadOnlyList<HashSet<ffxPlusBase>> floorConditionalEvents = GetFloorConditionalEvents(floor);
			foreach (HashSet<ffxPlusBase> item6 in floorConditionalEvents)
			{
				item6.Clear();
			}
			foreach (ffxPlusBase plusEffect in floor.plusEffects)
			{
				_ = plusEffect == null;
				bool flag2 = false;
				for (int num8 = 0; num8 < plusEffect.conditionalInfo.Length; num8++)
				{
					if (plusEffect.conditionalInfo[num8])
					{
						floorConditionalEvents[num8].Add(plusEffect);
						flag2 = true;
					}
				}
				if (flag2)
				{
					plusEffect.triggered = true;
					floor.hasConditionalChange = true;
				}
				else if (plusEffect.runManually)
				{
					plusEffect.triggered = true;
				}
				else if (!plusEffect.runOnHit)
				{
					scrVfxPlus2.effects.Add(plusEffect);
				}
				plusEffect.PrepVfx();
			}
		}
		if (seqID == 0)
		{
			scrVfxPlus2.ScrubToTime(0f);
		}
		else
		{
			scrFloor scrFloor2 = floors.GetRange(0, seqID + 1).LastOrDefault((scrFloor f) => f.onCheckpointEffects.Count > 0);
			if ((bool)scrFloor2)
			{
				foreach (ffxPlusBase onCheckpointEffect in scrFloor2.onCheckpointEffects)
				{
					onCheckpointEffect.triggered = false;
					scrVfxPlus2.effects.Add(onCheckpointEffect);
				}
			}
		}
		scrVfxPlus2.effects = (from fx in scrVfxPlus2.effects
			orderby fx.startTime - fx.startEffectOffset, fx.floor.seqID
			select fx).ToList();
	}

	public void UpdateVideo()
	{
		string text = levelData.miscSettings.GetString("bgVideo");
		if (text.IsNullOrEmpty())
		{
			videoBG.gameObject.SetActive(value: false);
			return;
		}
		videoBG.gameObject.SetActive(value: true);
		if (ADOBase.isInternalLevel)
		{
			string text2 = GCS.internalLevelName.Split('-')[0];
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
			string text3 = "InternalLevels/" + text2 + "/" + fileNameWithoutExtension;
			if (ADOBase.isDLCLevel)
			{
				VideoClip clip = Addressables.LoadAssetAsync<VideoClip>(text3).WaitForCompletion();
				videoBG.clip = clip;
			}
			else
			{
				VideoClip clip2 = Resources.Load<VideoClip>(text3);
				videoBG.clip = clip2;
			}
		}
		else if (ADOBase.isBundleLevel)
		{
			VideoClip clip3 = Addressables.LoadAssetAsync<VideoClip>(levelPath + "/" + Path.GetFileNameWithoutExtension(text)).WaitForCompletion();
			videoBG.clip = clip3;
		}
		else
		{
			string text4 = Path.Combine(Path.GetDirectoryName(levelPath), text);
			if (!File.Exists(text4))
			{
				printesp("Video does not exist in path " + text4);
				return;
			}
			videoBG.url = text4;
		}
		videoBG.Stop();
		videoBG.Prepare();
		videoBG.isLooping = levelData.miscSettings.GetBool("loopVideo");
	}

	public void PrepareCompleted(VideoPlayer source)
	{
	}

	public void VideoErrorReceived(VideoPlayer source, string message)
	{
	}

	public void UpdateTrackColors()
	{
	}

	public void SetBackground()
	{
		printesp("");
		scrCamera.instance.Bgcamstatic.backgroundColor = levelData.backgroundColor;
		if (!string.IsNullOrEmpty(levelData.bgImage))
		{
			ShowTutorialBackground(visible: false);
			if (Persistence.visualQuality != VisualQuality.Low)
			{
				string filePath = Path.Combine(Path.GetDirectoryName(levelPath), levelData.bgImage);
				custBG.SetBaseSprite(filePath, levelData.bgImage);
				custBG.SetCustomBG(custBG.baseSprite, levelData.bgImageColor, levelData.bgTiling, levelData.bgLooping, levelData.bgFitScreen, levelData.scalingRatio, levelData.bgLockRot, levelData.bgSmoothing);
				scrParallax parallax = custBG.parallax;
				if (levelData.backgroundSettings.info.propertiesInfo["parallax"].CheckIfEnabled(levelData.backgroundSettings, null))
				{
					parallax.multiplier_x = levelData.bgParallax.x;
					parallax.multiplier_y = levelData.bgParallax.y;
				}
				else
				{
					parallax.multiplier_x = (parallax.multiplier_y = 1f);
				}
			}
			return;
		}
		string ifEnabled = levelData.backgroundSettings.GetIfEnabled<string>("customBGPrefab");
		if (ifEnabled != null)
		{
			GameObject gameObject = Resources.Load<GameObject>("InternalLevels/Shared/Prefabs/" + ifEnabled);
			if (gameObject != null)
			{
				editorBG.SetActive(value: false);
				if ((bool)customEditorBG)
				{
					UnityEngine.Object.Destroy(customEditorBG);
				}
				customEditorBG = UnityEngine.Object.Instantiate(gameObject);
				customEditorBG.name = "BG";
				tutorialBackground = customEditorBG.GetComponent<TutorialBackground>();
				usingCustomTutorialBackground = true;
			}
		}
		if (customEditorBG == null)
		{
			tutorialBackground = editorBG.GetComponent<TutorialBackground>();
		}
		custBG.baseSprite = null;
		custBG.SetCustomBG(null, Color.white);
		bool visible = levelData.bgShowDefaultBGIfNoImage && (levelData.bgShowDefaultBGTile || levelData.showBGShape);
		ShowTutorialBackground(visible);
		if (!usingCustomTutorialBackground)
		{
			tutorialBackground.tile.gameObject.SetActive(levelData.bgShowDefaultBGTile);
			tutorialBackground.SetTileColor(levelData.bgDefaultBGTileColor);
			tutorialBackground.SetShapeType(levelData.bgShapeType);
			if (levelData.bgShapeType == BGShapeType.SingleColor)
			{
				tutorialBackground.SetShapeColor(levelData.bgDefaultBGShapeColor);
			}
		}
	}

	public void SetStartingBG()
	{
		printesp("SetStartingBG");
		if (custBG.baseSprite != null)
		{
			ShowTutorialBackground(visible: false);
			custBG.SetCustomBG(custBG.baseSprite, levelData.bgImageColor, levelData.bgTiling, levelData.bgLooping, levelData.bgFitScreen, levelData.scalingRatio, levelData.bgLockRot);
			scrParallax parallax = custBG.parallax;
			if (levelData.backgroundSettings.info.propertiesInfo["parallax"].CheckIfEnabled(levelData.backgroundSettings, null))
			{
				parallax.multiplier_x = levelData.bgParallax.x;
				parallax.multiplier_y = levelData.bgParallax.y;
			}
			else
			{
				parallax.multiplier_x = (parallax.multiplier_y = 1f);
			}
		}
		else
		{
			custBG.SetCustomBG(null, Color.white);
			ShowTutorialBackground(levelData.bgShowDefaultBGIfNoImage);
		}
	}

	public void ShowTutorialBackground(bool visible)
	{
		if ((bool)customEditorBG)
		{
			customEditorBG.SetActive(visible);
			editorBG.SetActive(value: false);
		}
		else
		{
			editorBG.SetActive(visible);
		}
	}

	public static void SetFxPlusFromComponents(List<scrFloor> listFloors, bool useComponentNotation)
	{
		scrConductor scrConductor2 = scrConductor.instance;
		TrackColorType trackColorType = TrackColorType.Single;
		TrackAnimationType animationType = TrackAnimationType.None;
		TrackAnimationType2 animationType2 = TrackAnimationType2.None;
		Color color2;
		Color color = (color2 = listFloors[0].floorRenderer.color);
		int num = 0;
		foreach (scrFloor listFloor in listFloors)
		{
			float num2 = 60f / (scrConductor2.bpm * scrConductor2.song.pitch * listFloor.speed);
			ffxPlusBase[] components = listFloor.GetComponents<ffxPlusBase>();
			foreach (ffxPlusBase ffxPlusBase2 in components)
			{
				ffxPlusBase2.SetStartTime(scrConductor2.bpm, ffxPlusBase2.degreeOffset);
				listFloor.plusEffects.Add(ffxPlusBase2);
				if (useComponentNotation)
				{
					ffxPlusBase2.duration *= num2;
				}
			}
			ffxChangeTrack component = listFloor.GetComponent<ffxChangeTrack>();
			if (component != null)
			{
				color2 = ((component.color1 == Color.clear) ? listFloor.floorRenderer.color : component.color1);
				color = ((component.color2 == Color.clear) ? listFloor.floorRenderer.color : component.color2);
				trackColorType = component.colorType;
				animationType = component.animationType;
				animationType2 = component.animationType2;
			}
			else
			{
				ffxChangeTrack obj = listFloor.gameObject.AddComponent<ffxChangeTrack>();
				obj.color1 = color2;
				obj.color2 = color;
				obj.colorType = trackColorType;
				obj.animationType = animationType;
				obj.animationType2 = animationType2;
			}
			switch (trackColorType)
			{
			case TrackColorType.Single:
				listFloor.SetColor(color2);
				break;
			case TrackColorType.Stripes:
				listFloor.SetColor(((listFloor.seqID - num) % 2 == 0) ? color2 : color);
				break;
			}
		}
	}

	public void ReloadSong(bool force = false)
	{
		if (string.IsNullOrEmpty(levelData.songFilename))
		{
			AudioManager audioManager = AudioManager.Instance;
			if (!string.IsNullOrEmpty(currentSongKey))
			{
				audioManager.audioLib.Remove(currentSongKey);
				if (audioManager.audioLibHandles.TryGetValue(currentSongKey, out var value))
				{
					Addressables.Release(value);
					audioManager.audioLibHandles.Remove(currentSongKey);
				}
			}
			scrConductor.instance.song.clip = null;
		}
		else
		{
			StartCoroutine(ReloadSongCo(force));
		}
	}

	private IEnumerator ReloadSongCo(bool force = false)
	{
		printesp("ReloadSongCo");
		string newSongKey = levelData.songFilename;
		if (!ADOBase.isInternalLevel && !ADOBase.isBundleLevel)
		{
			newSongKey += "*external";
		}
		if (currentSongKey == newSongKey && !force)
		{
			yield break;
		}
		string text = Path.Combine(Path.GetDirectoryName(levelPath), levelData.songFilename);
		AudioManager audioManager = AudioManager.Instance;
		if (!string.IsNullOrEmpty(currentSongKey))
		{
			audioManager.audioLib.Remove(currentSongKey);
			if (audioManager.audioLibHandles.TryGetValue(currentSongKey, out var value))
			{
				Addressables.Release(value);
				audioManager.audioLibHandles.Remove(currentSongKey);
			}
		}
		bool mp3Streaming = ADOBase.isScnGame;
		float length = 0f;
		if (ADOBase.isScnGame)
		{
			length = (float)floors[floors.Count - 1].entryTime;
		}
		if (ADOBase.isInternalLevel)
		{
			audioManager.FindOrLoadAudioClip(text, ADOBase.controller.levelName, ADOBase.isBundleLevel);
		}
		else if (ADOBase.isBundleLevel)
		{
			yield return audioManager.LoadAddressableAudio(text);
		}
		else
		{
			yield return audioManager.FindOrLoadAudioClipExternal(text, mp3Streaming, length);
		}
		Dictionary<string, AudioClip> audioLib = audioManager.audioLib;
		if (audioLib.ContainsKey(newSongKey))
		{
			AudioClip clip = audioLib[newSongKey];
			scrConductor.instance.song.clip = clip;
			currentSongKey = newSongKey;
		}
		else if (ADOBase.editor != null)
		{
			ADOBase.editor.ShowNotification(RDString.Get("editor.notification.songNotFound", new Dictionary<string, object> { { "file", levelData.songFilename } }));
		}
	}

	public string[] GetWorldPaths()
	{
		return GetWorldPaths(levelPath);
	}

	public static string[] GetWorldPaths(string levelPath, bool excludeMain = false, bool renamed = false)
	{
		List<string> list = new List<string>();
		string directoryName = Path.GetDirectoryName(levelPath);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(levelPath);
		int num = 1;
		string item;
		while (RDFile.Exists(item = Path.Combine(directoryName, ((renamed || fileNameWithoutExtension == "main") ? "sub" : fileNameWithoutExtension) + num + ".adofai")))
		{
			list.Add(item);
			num++;
		}
		if (!excludeMain)
		{
			list.Add(levelPath);
		}
		return list.ToArray();
	}

	public void ResetScene(bool isResetCustomLevel = false)
	{
		isLoading = true;
		printesp("ResetScene");
		scrUIController.instance.txtCountdown.GetComponent<scrCountdown>().CancelGo();
		ADOBase.controller.conditionalFloor = null;
		ADOBase.controller.ResetInputEventFfx();
		ReloadAssets(force: false, reloadDecorations: false);
		scrCamera scrCamera2 = scrCamera.instance;
		scrCamera2.transform.localPosition = scrCamera2.transform.position;
		scrCamera2.transform.rotation = Quaternion.identity;
		camParent.transform.position = Vector3.zero;
		scrCamera2.followMode = true;
		scrCamera2.zoomSize = 1f;
		scrCamera2.shake = Vector3.zero;
		scrCamera2.SetCustomFrameRate(enable: false);
		ADOBase.controller.EnableHallOfMirrors(homEnabled: false);
		scrCamera2.Bgcamstatic.backgroundColor = levelData.backgroundColor;
		scrCamera2.GetComponent<VideoBloom>().enabled = false;
		scrCamera2.GetComponent<ScreenScroll>().enabled = false;
		ScreenTile component = scrCamera2.GetComponent<ScreenTile>();
		component.enabled = false;
		component.tileX = 1f;
		component.tileY = 1f;
		DisableFilters();
		SetStartingBG();
		if (videoBG.isPlaying)
		{
			videoBG.Stop();
		}
		ResetPlanetsPosition();
		scrLivesCounter.instance?.Reset();
		if (ADOBase.isLevelEditor && !paused)
		{
			ADOBase.controller.TogglePauseGame();
		}
		if (ADOBase.isScnGame && paused)
		{
			ADOBase.controller.TogglePauseGame();
		}
		GameObject[] array = GameObject.FindGameObjectsWithTag("MissIndicator");
		for (int i = 0; i < array.Length; i++)
		{
			UnityEngine.Object.Destroy(array[i]);
		}
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			item.missesOnCurrFloor.Clear();
		}
		foreach (scrLetterPress typingLetter in ADOBase.controller.typingLetters)
		{
			UnityEngine.Object.Destroy(typingLetter.gameObject);
		}
		ADOBase.controller.typingLetters.Clear();
		List<Tween> list = new List<Tween>();
		DOTween.PlayingTweens(list);
		foreach (Tween item2 in list)
		{
			if (!(bool)_isIndependentUpdateField.GetValue(item2))
			{
				item2.Kill();
			}
		}
		scrCamera.instance.flashPlusRendererBg.material.color = Color.clear;
		scrCamera.instance.flashPlusRendererFg.material.color = Color.clear;
		scrCamera.instance.flashEndscreen.material.color = Color.clear;
		scrVfxPlus.instance.Reset();
		RemakePath(applyEventsToFloors: true, isResetCustomLevel);
		if (!isResetCustomLevel)
		{
			foreach (scrFloor floor in floors)
			{
				if (ADOBase.isScnGame)
				{
					ffxPlusBase[] components = floor.GetComponents<ffxPlusBase>();
					for (int i = 0; i < components.Length; i++)
					{
						components[i].triggered = false;
					}
				}
				floor.ResetToLevelStart();
			}
		}
		decManager.ResetDecorations();
		if (ADOBase.controller.txtLevelNameOriginalPosition.HasValue)
		{
			ADOBase.controller.txtLevelName.rectTransform.anchoredPosition = ADOBase.controller.txtLevelNameOriginalPosition.Value;
			ADOBase.controller.txtLevelName.text = ADOBase.controller.originalLevelName;
		}
		ColourScheme currentColourScheme = scrVfx.instance.currentColourScheme;
		currentColourScheme.colourText = levelData.miscSettings.GetColor("defaultTextColor");
		currentColourScheme.colourTextShadow = levelData.miscSettings.GetColor("defaultTextShadowColor");
		ffxSetDefaultText.UpdateHudTexts();
		ADOBase.conductor.song.Stop();
	}

	public void ResetPlanetsPosition()
	{
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			PlanetarySystem planetarySystem = item.planetarySystem;
			if (floors.Count > 0)
			{
				planetarySystem.planetRed.transform.position = floors[0].transform.position;
			}
			if (floors.Count > 1)
			{
				planetarySystem.planetBlue.transform.position = floors[1].transform.position;
			}
			planetarySystem.ResetPlanets();
		}
		ADOBase.playerManager.hitTextManager.hitTextContainer?.SetActive(value: false);
	}

	private static void printesp(string s)
	{
		string.IsNullOrEmpty(s);
	}

	private void OnDestroy()
	{
		imgHolder.Unload(onlyIfUnused: false);
	}
}
