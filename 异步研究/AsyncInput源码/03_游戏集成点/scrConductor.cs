using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ADOFAI;
using ADOFAI.Common.Platform;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class scrConductor : ADOBase
{
	private struct HitSoundsData
	{
		public HitSound hitSound;

		public double time;

		public float volume;

		public bool played;

		public HitSoundsData(HitSound hitSound, double time, float volume)
		{
			this.hitSound = hitSound;
			this.time = time;
			this.volume = volume;
			played = false;
		}

		public override string ToString()
		{
			return $"hitSound {hitSound}, time {time}, volume {volume}, played {played}";
		}
	}

	private struct HoldSoundsData
	{
		public string name;

		public double time;

		public double endTime;

		public float volume;

		public bool played;

		public HoldSoundsData(string name, double time, double endTime, float volume)
		{
			this.name = name;
			this.time = time;
			this.endTime = endTime;
			this.volume = volume;
			played = false;
		}
	}

	public struct ExtraTickData
	{
		public double time;

		public int count;

		public float speed;

		public ExtraTickData(double time, int count, float speed)
		{
			this.time = time;
			this.count = count;
			this.speed = speed;
		}
	}

	public enum DuckState
	{
		None = 0,
		Starting = 1,
		Ducked = 2,
		Stopping = 3
	}

	public static bool isAudioOutputDeviceChanged = false;

	public static List<CalibrationPreset> defaultPresets = new List<CalibrationPreset>();

	public static List<CalibrationPreset> userPresets = new List<CalibrationPreset>();

	public static CalibrationPreset currentPreset;

	public static int visualOffset;

	[Header("Components")]
	public AudioSource song;

	public AudioSource song2;

	public AudioSource song3;

	public scnEditor editorComponent;

	public scnCLS CLSComponent;

	public scnGame customLevelComponent;

	public Text txtOffset;

	[Header("Song Parameters")]
	public double addoffset;

	public float bpm;

	public bool isGameWorld = true;

	[Tooltip("Set to true if the song file does NOT have a countdown baked into it. The game will add its own countdown and cymbal crash")]
	public bool separateCountdownTime;

	public bool playCountdownHihats = true;

	public bool playEndingCymbal = true;

	[Header("Hitsounds")]
	[Tooltip("In non-editor levels, i.e. game scene, needs this to be on to have separate hitsounds.")]
	public bool forceHitSounds;

	public HitSound hitSound = HitSound.Kick;

	public AudioMixerGroup hitSoundGroup;

	[Range(0f, 2f)]
	public float hitSoundVolume = 1f;

	public HoldStartSound holdStartSound;

	public HoldEndSound holdEndSound;

	public HoldLoopSound holdLoopSound;

	public HoldMidSound holdMidSound = HoldMidSound.None;

	public HoldMidSoundType holdMidSoundType;

	public float holdMidSoundDelay = 0.5f;

	public HoldMidSoundTimingRelativeTo holdMidSoundTiming = HoldMidSoundTimingRelativeTo.End;

	[Range(0f, 2f)]
	public float holdSoundVolume = 1f;

	private List<HoldSoundsData> holdSoundsData = new List<HoldSoundsData>();

	private int nextHoldSoundToSchedule;

	public List<ExtraTickData> extraTicksCountdown = new List<ExtraTickData>();

	private int nextExtraTickToSchedule;

	public bool useMidspinHitSound;

	private const HitSound DefaultMidspinHitSound = HitSound.ReverbClack;

	public HitSound midspinHitSound = HitSound.ReverbClack;

	[Header("Mixer")]
	[Range(0f, 2f)]
	public float worldBalanceVolume = 1f;

	[NonSerialized]
	public double crotchetAtStart;

	[NonSerialized]
	public double dspTime;

	[NonSerialized]
	public double deltaSongPos;

	[NonSerialized]
	public int beatNumber;

	[NonSerialized]
	public int barNumber;

	public int countdownTicks = 4;

	public float countdownSpeedMultiplier = 1f;

	[NonSerialized]
	public bool hasSongStarted;

	[NonSerialized]
	public bool skipOffset;

	[NonSerialized]
	public bool fastTakeoff;

	[NonSerialized]
	public bool getSpectrum;

	private bool playedHitSounds;

	private List<HitSoundsData> hitSoundsData = new List<HitSoundsData>();

	private int nextHitSoundToSchedule;

	private int crotchetsPerBar = 8;

	private double _songposition_minusi;

	private double previousFrameTime;

	private double lastReportedPlayheadPosition;

	private double nextBeatTime;

	private double nextBarTime;

	[NonSerialized]
	public int onBeatFrame = -1;

	private float buffer = 1f;

	[NonSerialized]
	public double dspTimeSong;

	[NonSerialized]
	public double prev_dspTime;

	[NonSerialized]
	public double prev_unityDspTime;

	[NonSerialized]
	public double prev_deltaTime;

	private DuckState duckState;

	private float songPreduckVolume;

	private float song2PreduckVolume;

	private float song3PreduckVolume;

	private double[] countdownTimes = new double[4];

	private float[] _spectrum = new float[1024];

	public List<ADOBase> onBeats = new List<ADOBase>();

	private static scrConductor _instance;

	private Coroutine startMusicCoroutine;

	private Coroutine toggleHasSongStartedCourotine;

	private bool hasSetMidspinHitsound;

	public static float calibration_i => (float)currentPreset.inputOffset / 1000f;

	public static float calibration_v => (float)visualOffset / 1000f;

	public float adjustedCountdownTicks => (float)countdownTicks / countdownSpeedMultiplier;

	public double dspTimeSongPosZero => dspTimeSong + addoffset / (double)song.pitch;

	public double songposition_minusi
	{
		get
		{
			if (!GCS.d_webglConductor)
			{
				return _songposition_minusi;
			}
			if (song != null)
			{
				return _songposition_minusi - (double)scrMisc.Remap(song.pitch, 1f, 1.8f, 0f, 0.074f);
			}
			return _songposition_minusi;
		}
		set
		{
			_songposition_minusi = value;
		}
	}

	public float[] spectrum
	{
		get
		{
			getSpectrum = true;
			return _spectrum;
		}
	}

	public static scrConductor instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = UnityEngine.Object.FindAnyObjectByType<scrConductor>();
			}
			return _instance;
		}
	}

	public double songposition_minusv => songposition_minusi + (double)calibration_i - (double)calibration_v;

	public bool onBeatHappened => Time.frameCount == onBeatFrame;

	private void Awake()
	{
		float num;
		switch (Application.platform)
		{
		case RuntimePlatform.OSXEditor:
		case RuntimePlatform.OSXPlayer:
		case RuntimePlatform.WindowsPlayer:
		case RuntimePlatform.WindowsEditor:
		case RuntimePlatform.LinuxPlayer:
		case RuntimePlatform.LinuxEditor:
			num = 0.5f;
			break;
		default:
			num = buffer;
			break;
		}
		buffer = num;
		if (GCS.d_webglConductor)
		{
			buffer = 0.5f;
		}
		if (ADOBase.uiController != null)
		{
			txtOffset = ADOBase.uiController.txtOffset;
		}
		if (scnEditor.instance == null)
		{
			scnCLS.instance = CLSComponent;
		}
		if (ADOBase.controller != null)
		{
			ADOBase.controller.SetupImportantVariables();
		}
	}

	public void Start()
	{
		crotchetAtStart = 60f / bpm;
		AudioSource[] components = GetComponents<AudioSource>();
		song = components[0];
		if (components.Length > 1)
		{
			song2 = components[1];
		}
		nextBeatTime = 0.0;
		nextBarTime = 0.0;
		if (txtOffset != null)
		{
			txtOffset.text = "";
		}
		lastReportedPlayheadPosition = AudioSettings.dspTime;
		dspTime = AudioSettings.dspTime;
		previousFrameTime = Time.unscaledTimeAsDouble;
		if (song.pitch == 0f && !ADOBase.customLevel)
		{
			Debug.LogError("Song pitch is zero set to zero?!");
		}
		if (ADOBase.controller != null)
		{
			ADOBase.controller.startVolume = song.volume;
		}
		RDUtils.SetMixerVolume("WorldVolume", worldBalanceVolume);
		ADOBase.conductor.onBeats.Add(this);
	}

	public void Rewind()
	{
		isGameWorld = true;
		crotchetAtStart = 0.0;
		nextBeatTime = 0.0;
		nextBarTime = 0.0;
		beatNumber = 0;
		barNumber = 0;
		hasSongStarted = false;
		getSpectrum = false;
		txtOffset = null;
		dspTimeSong = 0.0;
		if (toggleHasSongStartedCourotine != null)
		{
			StopCoroutine(toggleHasSongStartedCourotine);
			toggleHasSongStartedCourotine = null;
		}
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			item.lastHit = 0.0;
		}
		lastReportedPlayheadPosition = AudioSettings.dspTime;
		dspTime = AudioSettings.dspTime;
		previousFrameTime = Time.unscaledTimeAsDouble;
	}

	public void SetupConductorWithLevelData(LevelData levelData)
	{
		bpm = levelData.bpm;
		crotchetAtStart = 60f / bpm;
		addoffset = (float)levelData.offset * 0.001f;
		song.volume = (float)levelData.volume * 0.01f;
		hitSoundVolume = (float)levelData.hitsoundVolume * 0.01f;
		hitSound = levelData.hitsound;
		midspinHitSound = levelData.hitsound;
		separateCountdownTime = levelData.separateCountdownTime;
		float num = (float)levelData.pitch * 0.01f;
		if (ADOBase.isScnGame)
		{
			num *= GCS.currentSpeedTrial;
		}
		else if (ADOBase.isLevelEditor)
		{
			num *= ADOBase.editor.playbackSpeed;
		}
		song.pitch = num;
	}

	public void PlayHitTimes()
	{
		if (playedHitSounds)
		{
			AudioManager.Instance.StopAllSounds();
		}
		playedHitSounds = true;
		if (ADOBase.sceneName.Contains("scnCalibration") || ADOBase.lm == null || !GCS.d_hitsounds || (ADOBase.controller != null && !ADOBase.customLevel && !forceHitSounds))
		{
			return;
		}
		int num = ((GCS.checkpointNum >= ADOBase.lm.listFloors.Count) ? 1 : (GCS.checkpointNum + 1));
		int num2 = (GCS.practiceMode ? (GCS.checkpointNum + GCS.practiceLength) : (ADOBase.lm.listFloors.Count - 1));
		extraTicksCountdown.Clear();
		extraTicksCountdown = new List<ExtraTickData>();
		nextExtraTickToSchedule = 0;
		for (int i = 1; i < ADOBase.lm.listFloors.Count - 1; i++)
		{
			if (ADOBase.lm.listFloors[i].countdownTicks <= 0)
			{
				continue;
			}
			new List<double>();
			for (int j = 0; j < ADOBase.lm.listFloors[i].countdownTicks; j++)
			{
				double num3 = dspTimeSong + ADOBase.lm.listFloors[i + 1].entryTimePitchAdj - (double)(ADOBase.lm.listFloors[i].countdownTicks - j) * (crotchetAtStart / (double)ADOBase.lm.listFloors[i].speed) / (double)song.pitch / (double)countdownSpeedMultiplier + addoffset / (double)song.pitch;
				if (i >= num && i < num2 && num3 > dspTime)
				{
					extraTicksCountdown.Add(new ExtraTickData(num3, ADOBase.lm.listFloors[i].countdownTicks - (j + 1), ADOBase.lm.listFloors[i].speed));
				}
			}
		}
		HitSound hitSound = this.hitSound;
		float volume = hitSoundVolume;
		List<scrFloor> listFloors = ADOBase.lm.listFloors;
		int num4 = ((GCS.checkpointNum >= listFloors.Count) ? 1 : (GCS.checkpointNum + 1));
		int num5 = (GCS.practiceMode ? (GCS.checkpointNum + GCS.practiceLength) : (listFloors.Count - 1));
		double num6 = dspTimeSong + addoffset / (double)song.pitch;
		hitSoundsData = new List<HitSoundsData>();
		nextHitSoundToSchedule = 0;
		midspinHitSound = hitSound;
		for (int k = 1; k < listFloors.Count; k++)
		{
			scrFloor scrFloor2 = listFloors[k];
			ffxSetHitsound setHitsound = scrFloor2.setHitsound;
			if (setHitsound != null)
			{
				if (setHitsound.gameSound == GameSound.Midspin)
				{
					useMidspinHitSound = true;
					midspinHitSound = setHitsound.hitSound;
				}
				else
				{
					hitSound = setHitsound.hitSound;
					if (!hasSetMidspinHitsound)
					{
						hasSetMidspinHitsound = true;
						midspinHitSound = hitSound;
					}
				}
				volume = setHitsound.volume;
			}
			if (ADOBase.lm.listFloors[k].holdLength > -1 || ADOBase.lm.listFloors[k - 1].holdLength > -1 || (ADOBase.lm.listFloors[k - 1].midSpin && k >= 2 && ADOBase.lm.listFloors[k - 2].holdLength > -1))
			{
				continue;
			}
			scrFloor scrFloor3 = listFloors[k - 1];
			HitSound key = ((scrFloor3 != null && scrFloor3.midSpin && useMidspinHitSound) ? midspinHitSound : hitSound);
			ADOBase.gc.hitSoundOffsets.TryGetValue(key, out var value);
			double num7 = dspTimeSongPosZero + scrFloor2.entryTimePitchAdj - value;
			if (k >= num4 && k <= num5 && num7 > dspTime && !scrFloor2.midSpin && hitSound != HitSound.None)
			{
				HitSound hitSound2 = ((scrFloor3 != null && scrFloor3.midSpin && useMidspinHitSound) ? midspinHitSound : hitSound);
				if (scrFloor2.tapsNeeded > 1)
				{
					hitSound2 = midspinHitSound;
				}
				HitSoundsData item = new HitSoundsData(hitSound2, num7, volume);
				hitSoundsData.Add(item);
			}
			if (scrFloor2.freeroam && (scrFloor2.freeroamSoundOnBeat != HitSound.None || scrFloor2.freeroamSoundOffBeat != HitSound.None))
			{
				float extraBeats = scrFloor2.extraBeats;
				HitSound freeroamSoundOnBeat = scrFloor2.freeroamSoundOnBeat;
				HitSound freeroamSoundOffBeat = scrFloor2.freeroamSoundOffBeat;
				float[] array = new float[4] { 0.8f, 0.7f, 0.8f, 0.7f };
				int num8 = -1;
				for (float num9 = 0f; num9 < extraBeats - (float)scrFloor2.countdownTicks / countdownSpeedMultiplier + 1f; num9 += 0.5f)
				{
					num8 = (num8 + 1) % 4;
					double num10 = (double)num9 * (scrMisc.bpm2crotchet(bpm) / (double)scrFloor2.speed) / (double)song.pitch;
					if (((num8 % 2 == 0 && scrFloor2.freeroamSoundOnBeat != HitSound.None) || (num8 % 2 == 1 && scrFloor2.freeroamSoundOffBeat != HitSound.None)) && k >= num4 && k <= num5 && num7 + num10 > dspTime && !scrFloor2.midSpin)
					{
						hitSoundsData.Add(new HitSoundsData((num8 % 2 == 0) ? freeroamSoundOnBeat : freeroamSoundOffBeat, num7 + num10, volume * array[num8]));
					}
				}
			}
			double num11 = num6 + scrFloor2.entryTimePitchAdj;
			if (k >= num4 && num11 > dspTime && (bool)scrFloor2.prevfloor && scrFloor2.numPlanets != scrFloor2.prevfloor.numPlanets)
			{
				HitSound hitSound3 = ((scrFloor2.numPlanets > scrFloor2.prevfloor.numPlanets) ? HitSound.VehiclePositive : HitSound.VehicleNegative);
				HitSoundsData item2 = new HitSoundsData(hitSound3, num11, volume * 0.6f);
				hitSoundsData.Add(item2);
			}
		}
		HoldStartSound holdStartSound = this.holdStartSound;
		HoldEndSound holdEndSound = this.holdEndSound;
		HoldLoopSound holdLoopSound = this.holdLoopSound;
		HoldMidSound holdMidSound = this.holdMidSound;
		HoldMidSoundType holdMidSoundType = this.holdMidSoundType;
		float num12 = holdMidSoundDelay;
		HoldMidSoundTimingRelativeTo holdMidSoundTimingRelativeTo = holdMidSoundTiming;
		float volume2 = holdSoundVolume;
		int num13 = ((GCS.checkpointNum >= ADOBase.lm.listFloors.Count) ? 1 : GCS.checkpointNum);
		int num14 = (GCS.practiceMode ? (GCS.checkpointNum + GCS.practiceLength) : (listFloors.Count - 1));
		holdSoundsData = new List<HoldSoundsData>();
		nextHoldSoundToSchedule = 0;
		for (int l = 1; l < ADOBase.lm.listFloors.Count - 1; l++)
		{
			ffxSetHoldsound component = ADOBase.lm.listFloors[l].GetComponent<ffxSetHoldsound>();
			if (component != null)
			{
				holdStartSound = component.holdStartSound;
				holdEndSound = component.holdEndSound;
				holdLoopSound = component.holdLoopSound;
				holdMidSound = component.holdMidSound;
				holdMidSoundType = component.holdMidSoundType;
				num12 = component.holdMidSoundDelay;
				holdMidSoundTimingRelativeTo = component.holdMidSoundTiming;
				volume2 = component.volume;
			}
			if (ADOBase.lm.listFloors[l].holdLength == -1)
			{
				continue;
			}
			int holdLength = ADOBase.lm.listFloors[l].holdLength;
			double num15 = ADOBase.lm.listFloors[l].exitangle + (double)((float)holdLength * MathF.PI * 2f) - ADOBase.lm.listFloors[l].entryangle;
			if (ADOBase.lm.listFloors[l].entryangle > ADOBase.lm.listFloors[l].exitangle - 0.05000000074505806)
			{
				num15 += 6.2831854820251465;
			}
			double num16 = 0.0;
			num16 = ((holdStartSound != HoldStartSound.Fuse) ? 0.0 : 0.0);
			double num17 = dspTimeSong + ADOBase.lm.listFloors[l].entryTimePitchAdj - num16 + addoffset / (double)song.pitch;
			double endTime = -1.0;
			if (l >= num13 && num17 > dspTime && !ADOBase.lm.listFloors[l].midSpin && l < num14 && holdStartSound.ToString() != "None")
			{
				HoldSoundsData item3 = new HoldSoundsData("sndHeldbeatStart" + holdStartSound, num17, endTime, volume2);
				holdSoundsData.Add(item3);
			}
			int num18 = ((holdLoopSound != HoldLoopSound.Fuse) ? 0 : 0);
			num16 = num18;
			num17 = dspTimeSong + ADOBase.lm.listFloors[l].entryTimePitchAdj - num16 + addoffset / (double)song.pitch;
			endTime = dspTimeSong + ADOBase.lm.listFloors[l + 1].entryTimePitchAdj + addoffset / (double)song.pitch;
			if (l >= num13 && num17 > dspTime && !ADOBase.lm.listFloors[l].midSpin && l < num14 && holdLoopSound.ToString() != "None")
			{
				HoldSoundsData item4 = new HoldSoundsData("sndHeldbeatLoop" + holdLoopSound, num17, endTime, volume2);
				holdSoundsData.Add(item4);
			}
			num16 = holdMidSound switch
			{
				HoldMidSound.Fuse => 0.107, 
				HoldMidSound.SingSing => 0.212, 
				_ => 0.0, 
			};
			if (holdMidSound.ToString() != "None")
			{
				float num19 = num12 / song.pitch;
				if (holdMidSoundType == HoldMidSoundType.Once)
				{
					num17 = dspTimeSong + ADOBase.lm.listFloors[l].entryTimePitchAdj - num16 + addoffset / (double)song.pitch;
					endTime = dspTimeSong + ADOBase.lm.listFloors[l + 1].entryTimePitchAdj - num16 + addoffset / (double)song.pitch;
					double num20 = ((holdMidSoundTimingRelativeTo == HoldMidSoundTimingRelativeTo.Start) ? (num17 + (double)num19) : (endTime - (double)num19));
					if (l >= num13 && num20 > num17 && num20 < endTime && num20 > dspTime && !ADOBase.lm.listFloors[l].midSpin && l < num14)
					{
						HoldSoundsData item5 = new HoldSoundsData("sndHeldbeatMid" + holdMidSound, num20, -1.0, volume2);
						holdSoundsData.Add(item5);
					}
				}
				else
				{
					num17 = dspTimeSong + ADOBase.lm.listFloors[l].entryTimePitchAdj - num16 + addoffset / (double)song.pitch;
					endTime = dspTimeSong + ADOBase.lm.listFloors[l + 1].entryTimePitchAdj - num16 + addoffset / (double)song.pitch;
					if (num19 >= 0f && holdMidSoundTimingRelativeTo == HoldMidSoundTimingRelativeTo.Start)
					{
						for (double num21 = num17 + (double)num19; num21 < endTime; num21 += (double)num19)
						{
							if (l >= num13 && num21 > num17 && num21 < endTime && num21 > dspTime && l < num14)
							{
								HoldSoundsData item6 = new HoldSoundsData("sndHeldbeatMid" + holdMidSound, num21, -1.0, volume2);
								holdSoundsData.Add(item6);
							}
						}
					}
					else if (num19 >= 0f && holdMidSoundTimingRelativeTo == HoldMidSoundTimingRelativeTo.End)
					{
						int count = holdSoundsData.Count;
						for (double num22 = endTime - (double)num19; num22 > num17; num22 -= (double)num19)
						{
							if (l >= num13 && num22 > num17 && num22 < endTime && num22 > dspTime && l < num14)
							{
								HoldSoundsData item7 = new HoldSoundsData("sndHeldbeatMid" + holdMidSound, num22, -1.0, volume2);
								holdSoundsData.Insert(count, item7);
							}
						}
					}
				}
			}
			double num23 = ((holdEndSound != HoldEndSound.Fuse) ? 0.0 : 0.099);
			num16 = num23;
			num17 = dspTimeSong + ADOBase.lm.listFloors[l + 1].entryTimePitchAdj - num16 + addoffset / (double)song.pitch;
			endTime = -1.0;
			if (l >= num13 && num17 > dspTime && !ADOBase.lm.listFloors[l].midSpin && l < num14 && holdEndSound.ToString() != "None")
			{
				HoldSoundsData item8 = new HoldSoundsData("sndHeldbeatEnd" + holdEndSound, num17, endTime, volume2);
				holdSoundsData.Add(item8);
			}
		}
		hasSetMidspinHitsound = false;
		if (!playCountdownHihats)
		{
			return;
		}
		if (!fastTakeoff)
		{
			countdownTimes = new double[countdownTicks];
			for (int m = 0; m < countdownTicks; m++)
			{
				double countdownTime = GetCountdownTime(m);
				if (!(countdownTime <= dspTime))
				{
					countdownTimes[m] = countdownTime;
					AudioManager.Play("sndHat", countdownTime, hitSoundGroup, hitSoundVolume, 10);
				}
			}
		}
		if (playEndingCymbal)
		{
			AudioManager.Play("sndCymbalCrash", dspTimeSong + ADOBase.lm.listFloors[num5].entryTimePitchAdj + addoffset / (double)song.pitch, hitSoundGroup, hitSoundVolume, 10);
		}
	}

	public double GetCountdownTime(int i)
	{
		if (GCS.checkpointNum != 0)
		{
			int index = ((GCS.checkpointNum != ADOBase.lm.listFloors.Count - 1) ? (GCS.checkpointNum + 1) : GCS.checkpointNum);
			return dspTimeSong + ADOBase.lm.listFloors[index].entryTimePitchAdj - (double)(countdownTicks - i) * (crotchetAtStart / ((double)(ADOBase.lm.listFloors[GCS.checkpointNum].speed * (float)ADOBase.lm.listFloors[GCS.checkpointNum].numPlanets) * 0.5)) / (double)song.pitch / (double)countdownSpeedMultiplier + addoffset / (double)song.pitch;
		}
		return dspTimeSong + (double)i * crotchetAtStart / (double)song.pitch / (double)countdownSpeedMultiplier + addoffset / (double)song.pitch;
	}

	public void StartMusic(Action onComplete = null, Action onSongScheduled = null)
	{
		if (startMusicCoroutine != null)
		{
			StopCoroutine(startMusicCoroutine);
		}
		startMusicCoroutine = StartCoroutine(StartMusicCo(onComplete, onSongScheduled));
	}

	private IEnumerator StartMusicCo(Action onComplete, Action onSongScheduled = null)
	{
		if (!isGameWorld)
		{
			nextBeatTime = 0.0;
		}
		dspTimeSong = dspTime + (double)buffer + 0.10000000149011612;
		if (!GCS.d_oldConductor)
		{
			for (float timer = 0.1f; timer >= 0f; timer -= Time.deltaTime)
			{
				yield return 0;
			}
			dspTimeSong = dspTime + (double)buffer;
			if (fastTakeoff)
			{
				dspTimeSong -= (double)adjustedCountdownTicks * crotchetAtStart / (double)song.pitch;
			}
			song.UnPause();
			SkipIntroBehavior skipIntroBehavior = Persistence.skipIntroBehavior;
			skipOffset = (skipIntroBehavior == SkipIntroBehavior.AfterFirstTry && scrController.deaths > 0) || (skipIntroBehavior == SkipIntroBehavior.Always && addoffset > GCS.longIntroThresholdSec && GCS.checkpointNum == 0);
			if (dspTimeSong - addoffset / (double)song.pitch + (separateCountdownTime ? (crotchetAtStart / (double)song.pitch * (double)adjustedCountdownTicks) : 0.0) > dspTime + (double)buffer)
			{
				skipOffset = false;
			}
			if (skipOffset)
			{
				dspTimeSong -= addoffset / (double)song.pitch;
			}
			double num = dspTimeSong + (separateCountdownTime ? (crotchetAtStart / (double)song.pitch * (double)adjustedCountdownTicks) : 0.0);
			double num2 = dspTime + (double)buffer;
			if (skipOffset)
			{
				song.PlayScheduled(num2);
				if (song.clip != null)
				{
					song.time = ((float)num2 - (float)num) * song.pitch;
				}
			}
			else
			{
				song.PlayScheduled(num);
				if (song.clip != null)
				{
					song.time = 0f;
				}
			}
			song2?.PlayScheduled(num);
			song3?.PlayScheduled(num);
			if (toggleHasSongStartedCourotine != null)
			{
				StopCoroutine(toggleHasSongStartedCourotine);
			}
			toggleHasSongStartedCourotine = StartCoroutine(ToggleHasSongStarted(dspTimeSong));
		}
		else
		{
			song.volume = 0f;
			song.Play();
			while (!song.isPlaying)
			{
				Debug.Log("song is not playing right after play called");
				yield return null;
			}
			yield return new WaitForSeconds(0.2f);
			song.Pause();
			yield return new WaitForSeconds(0.1f);
			song.UnPause();
			song.volume = 1f;
		}
		if (GCS.checkpointNum == 0)
		{
			PlayHitTimes();
		}
		yield return 0;
		onSongScheduled?.Invoke();
		yield return new WaitForSeconds(4f);
		while (song.isPlaying)
		{
			yield return null;
		}
		onComplete?.Invoke();
	}

	private IEnumerator ToggleHasSongStarted(double songstarttime)
	{
		if (GCS.d_webglConductor)
		{
			song.volume = 0f;
		}
		while (ADOBase.conductor.dspTime < songstarttime)
		{
			yield return null;
		}
		hasSongStarted = true;
		scrDebugHUDMessage.Log("Song started forreal!");
		if (GCS.d_webglConductor)
		{
			yield return new WaitForSeconds(0.2f);
			song.Pause();
			yield return new WaitForSeconds(0.1f);
			song.UnPause();
			song.volume = 1f;
		}
	}

	private void Update()
	{
		if (isAudioOutputDeviceChanged)
		{
			scrController.CheckForAudioOutputChange();
			isAudioOutputDeviceChanged = false;
		}
		PlatformHelper.instance.Update();
		bool pause = AudioListener.pause;
		if (AudioSettings.dspTime != lastReportedPlayheadPosition)
		{
			dspTime = AudioSettings.dspTime;
			lastReportedPlayheadPosition = AudioSettings.dspTime;
		}
		else if (!pause && Application.isFocused)
		{
			double num = Time.unscaledTimeAsDouble - previousFrameTime;
			dspTime += num;
		}
		previousFrameTime = Time.unscaledTimeAsDouble;
		if (AsyncInputManager.isActive)
		{
			AsyncInputManager.prevFrameTick = AsyncInputManager.currFrameTick;
			AsyncInputManager.currFrameTick = (ulong)DateTime.Now.Ticks;
			AsyncInputManager.previousFrameTime = Time.unscaledTimeAsDouble;
			if (!pause)
			{
				AsyncInputUtils.UpdateOffsetTime(100L);
			}
			if (ADOBase.controller != null && !ADOBase.controller.paused)
			{
				ADOBase.controller.UpdateInput();
			}
		}
		prev_dspTime = dspTime;
		prev_unityDspTime = AudioSettings.dspTime;
		if (hasSongStarted && isGameWorld)
		{
			States state = ADOBase.controller.state;
			if (state != States.Fail && state != States.Fail2)
			{
				while (nextExtraTickToSchedule < extraTicksCountdown.Count)
				{
					double time = extraTicksCountdown[nextExtraTickToSchedule].time;
					if (!(dspTime + 5.0 > time))
					{
						break;
					}
					AudioManager.Play("sndHat", time, hitSoundGroup, hitSoundVolume, 10);
					nextExtraTickToSchedule++;
				}
				while (nextHoldSoundToSchedule < this.holdSoundsData.Count)
				{
					HoldSoundsData holdSoundsData = this.holdSoundsData[nextHoldSoundToSchedule];
					if (!(dspTime + 5.0 > holdSoundsData.time))
					{
						break;
					}
					if (holdSoundsData.endTime > 0.0)
					{
						PlayWithEndTime(holdSoundsData.name, holdSoundsData.time, holdSoundsData.endTime, holdSoundsData.volume);
					}
					else
					{
						AudioManager.Play(holdSoundsData.name, holdSoundsData.time, hitSoundGroup, holdSoundsData.volume);
					}
					nextHoldSoundToSchedule++;
				}
				while (nextHitSoundToSchedule < this.hitSoundsData.Count)
				{
					HitSoundsData hitSoundsData = this.hitSoundsData[nextHitSoundToSchedule];
					if (hitSoundsData.time >= dspTime + 5.0)
					{
						break;
					}
					AudioManager.Play("snd" + hitSoundsData.hitSound, hitSoundsData.time, hitSoundGroup, hitSoundsData.volume);
					nextHitSoundToSchedule++;
				}
			}
		}
		crotchetAtStart = 60f / bpm;
		double num2 = songposition_minusi;
		if (!GCS.d_oldConductor && !GCS.d_webglConductor)
		{
			songposition_minusi = (double)((float)(dspTime - dspTimeSong - (double)calibration_i) * song.pitch) - addoffset;
		}
		else
		{
			songposition_minusi = (double)(song.time - calibration_i) - addoffset / (double)song.pitch;
		}
		deltaSongPos = songposition_minusi - num2;
		deltaSongPos = Math.Max(deltaSongPos, 0.0);
		if (songposition_minusi > nextBeatTime)
		{
			PropagateOnBeat();
			nextBeatTime += crotchetAtStart;
			beatNumber++;
		}
		if (songposition_minusi > nextBarTime)
		{
			nextBarTime += crotchetAtStart * (double)crotchetsPerBar;
			barNumber++;
		}
		if (Input.GetKeyDown(KeyCode.G) && Application.isEditor)
		{
			_ = song.time;
			if (separateCountdownTime)
			{
				_ = crotchetAtStart;
				_ = adjustedCountdownTicks;
			}
			_ = songposition_minusi;
			_ = calibration_i;
			_ = song.pitch;
			_ = addoffset;
		}
		if (!getSpectrum || GCS.lofiVersion)
		{
			return;
		}
		AudioSource audioSource = song;
		if (CLSComponent != null)
		{
			PreviewSongPlayer previewSongPlayer = CLSComponent.previewSongPlayer;
			if (previewSongPlayer.playing)
			{
				audioSource = previewSongPlayer.audioSource;
			}
		}
		audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
	}

	public void PropagateOnBeat()
	{
		List<ADOBase> list = onBeats;
		int num = list.Count;
		int num2 = 0;
		while (num2 < num)
		{
			if (list[num2] == null)
			{
				list.RemoveAt(num2);
				num--;
			}
			else
			{
				list[num2].OnBeat();
				num2++;
			}
		}
		if (ADOBase.controller != null && ADOBase.controller.gameworld)
		{
			List<scrFloor> listFloors = ADOBase.lm.listFloors;
			int count = listFloors.Count;
			for (int i = 0; i < count; i++)
			{
				listFloors[i].OnBeat();
			}
		}
		onBeatFrame = Time.frameCount;
	}

	[Obsolete("This method isn't used!!")]
	public void ScrubMusicToTile(int tileID)
	{
		AudioListener.pause = true;
		AudioManager.Instance.StopAllSounds();
		song.SetScheduledStartTime(dspTime);
		double num = (separateCountdownTime ? (crotchetAtStart * (double)adjustedCountdownTicks) : 0.0);
		song.time = (float)(ADOBase.lm.listFloors[tileID].entryTime + addoffset - num);
		dspTimeSong = dspTime - ADOBase.lm.listFloors[tileID].entryTimePitchAdj - addoffset / (double)song.pitch;
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			item.lastHit = ADOBase.lm.listFloors[tileID].entryTime;
		}
		StartCoroutine(DesyncFix());
	}

	public void ScrubMusicToTime(double newTime)
	{
		double num = newTime / (double)song.pitch;
		AudioManager.Instance.StopAllSounds();
		if (song.clip != null)
		{
			song.SetScheduledStartTime(dspTime + 0.10000000149011612);
			double num2 = (separateCountdownTime ? (crotchetAtStart * (double)countdownTicks) : 0.0);
			song.time = (float)(newTime + addoffset - num2);
		}
		dspTimeSong = dspTime - num - addoffset / (double)song.pitch + 0.10000000149011612;
		foreach (scrPlayer item in ADOBase.playerManager)
		{
			item.lastHit = newTime;
		}
	}

	private IEnumerator DesyncFix()
	{
		for (int framecounty = 2; framecounty > 0; framecounty--)
		{
			yield return 0;
		}
		AudioListener.pause = false;
		PlayHitTimes();
		int numberOfAttempts = 0;
		int framesToWait = 10;
		double maxDifference = 0.005;
		for (int i = 0; i < numberOfAttempts; i++)
		{
			for (int framecount = framesToWait; framecount > 0; framecount--)
			{
				yield return 0;
			}
			if (song.isPlaying || song.clip == null)
			{
				break;
			}
			double num = (double)song.time + (separateCountdownTime ? (crotchetAtStart * (double)adjustedCountdownTicks) : 0.0);
			double num2 = songposition_minusi + (double)(calibration_i * song.pitch) + addoffset;
			if (Math.Abs(num2 - num) > maxDifference)
			{
				double num3 = num - num2;
				Debug.Log("Desync Fix Attempt: found difference " + num3);
				Debug.Log("Attempt " + i);
				Debug.Log("song time " + num);
				Debug.Log("dsptime " + num2);
				dspTimeSong -= num3;
				PlayHitTimes();
			}
		}
	}

	public static AudioOutputType GetCurrentAudioOutputType()
	{
		return PlatformHelper.instance.GetActiveAudioDeviceType();
	}

	public static string GetCurrentAudioOutputName()
	{
		return PlatformHelper.instance.GetActiveAudioDeviceName();
	}

	public static void UpdateCurrentAudioOutput()
	{
		currentPreset = GetSuitablePresetForCurrentAudioOutput();
		if ((bool)scrController.instance)
		{
			PauseMenu pauseMenu = ADOBase.controller.pauseMenu;
			if ((bool)pauseMenu)
			{
				pauseMenu.settingsMenu.AudioOutputWasUpdated();
			}
		}
	}

	public static CalibrationPreset GetSuitablePresetForCurrentAudioOutput(bool searchOnlyForDefaultPresets = false)
	{
		AudioOutputType currentAudioOutputType = GetCurrentAudioOutputType();
		string currentAudioOutputName = GetCurrentAudioOutputName();
		if (!searchOnlyForDefaultPresets)
		{
			CalibrationPreset? calibrationPreset = null;
			foreach (CalibrationPreset userPreset in userPresets)
			{
				if (userPreset.outputType == currentAudioOutputType)
				{
					bool flag = userPreset.outputType == AudioOutputType.Bluetooth || userPreset.outputType == AudioOutputType.Other || userPreset.outputType == AudioOutputType.Speaker;
					if ((flag && userPreset.outputName == currentAudioOutputName) || !flag)
					{
						CalibrationPreset result = userPreset;
						result.confident = true;
						return result;
					}
				}
			}
			if (calibrationPreset.HasValue)
			{
				CalibrationPreset valueOrDefault = calibrationPreset.GetValueOrDefault();
				valueOrDefault.outputName = currentAudioOutputName;
				valueOrDefault.confident = true;
				return valueOrDefault;
			}
		}
		List<CalibrationPreset> list = new List<CalibrationPreset>();
		foreach (CalibrationPreset defaultPreset in defaultPresets)
		{
			if (defaultPreset.outputType == currentAudioOutputType || defaultPreset.outputType == AudioOutputType.Any)
			{
				list.Add(defaultPreset);
			}
		}
		foreach (CalibrationPreset item in list.OrderByDescending((CalibrationPreset preset) => preset.priority))
		{
			if (CalibrationPreset.StringMatchesFilter(currentAudioOutputName, item.outputName))
			{
				CalibrationPreset result2 = item;
				result2.outputType = currentAudioOutputType;
				result2.outputName = currentAudioOutputName;
				return result2;
			}
		}
		Debug.LogError("We didn't find a suitable preset, this should never happen because all fallbacks should be in the default presets file.");
		return new CalibrationPreset
		{
			outputType = currentAudioOutputType,
			outputName = currentAudioOutputName,
			inputOffset = 100,
			confident = false,
			priority = -1
		};
	}

	public static void UsePreset(CalibrationPreset preset)
	{
		currentPreset = preset;
	}

	public static bool HasAudioOutputChanged()
	{
		AudioOutputType currentAudioOutputType = GetCurrentAudioOutputType();
		if (currentAudioOutputType != currentPreset.outputType)
		{
			return true;
		}
		if ((currentAudioOutputType == AudioOutputType.Bluetooth || currentAudioOutputType == AudioOutputType.Other || currentAudioOutputType == AudioOutputType.Speaker) && GetCurrentAudioOutputName() != currentPreset.outputName)
		{
			return true;
		}
		return false;
	}

	private int GetOffsetChange(bool fine)
	{
		if (!fine)
		{
			return 10;
		}
		return 1;
	}

	public void ReduceInputOffset(bool fine = false)
	{
		currentPreset.inputOffset -= GetOffsetChange(fine);
		SaveCurrentPreset();
	}

	public void IncreaseInputOffset(bool fine = false)
	{
		currentPreset.inputOffset += GetOffsetChange(fine);
		SaveCurrentPreset();
	}

	public void ReduceVisualOffset(bool fine = false)
	{
		visualOffset -= GetOffsetChange(fine);
		SaveVisualOffset(calibration_v);
	}

	public void IncreaseVisualOffset(bool fine = false)
	{
		visualOffset += GetOffsetChange(fine);
		SaveVisualOffset(calibration_v);
	}

	public static void SaveCurrentPreset()
	{
		for (int i = 0; i < userPresets.Count; i++)
		{
			CalibrationPreset value = userPresets[i];
			if (currentPreset.outputType == value.outputType && currentPreset.outputName == value.outputName)
			{
				value.inputOffset = currentPreset.inputOffset;
				userPresets[i] = value;
				return;
			}
		}
		userPresets.Add(currentPreset);
	}

	public void DuckSongStart(float duckFactor = 0.25f, float fadeLength = 0.1f)
	{
		if (duckState != DuckState.Starting && duckState != DuckState.Ducked)
		{
			if (duckState == DuckState.None)
			{
				songPreduckVolume = song.volume;
				song2PreduckVolume = song2.volume;
			}
			duckState = DuckState.Starting;
			float volume = song.volume;
			float volume2 = song2.volume;
			float num = (songPreduckVolume - volume) / (songPreduckVolume * (1f - duckFactor));
			float num2 = (song2PreduckVolume - volume2) / (song2PreduckVolume * (1f - duckFactor));
			float num3 = (song2PreduckVolume - volume2) / (song2PreduckVolume * (1f - duckFactor));
			song.DOKill();
			song2.DOKill();
			song.DOFade(songPreduckVolume * duckFactor, fadeLength * num).SetEase(Ease.OutExpo).OnComplete(delegate
			{
				DuckSongDucked();
			});
			song2.DOFade(song2PreduckVolume * duckFactor, fadeLength * num2).SetEase(Ease.OutExpo);
			song3.DOFade(song3PreduckVolume * duckFactor, fadeLength * num3).SetEase(Ease.OutExpo);
		}
	}

	public void DuckSongDucked()
	{
		if (duckState == DuckState.Starting)
		{
			duckState = DuckState.Ducked;
		}
	}

	public void DuckSongStop(float fadeLength = 1f)
	{
		if (duckState != DuckState.None && duckState != DuckState.Stopping)
		{
			duckState = DuckState.Stopping;
			song.DOFade(songPreduckVolume, fadeLength).SetEase(Ease.OutExpo).OnComplete(delegate
			{
				DuckSongFinish();
			});
			song2.DOFade(song2PreduckVolume, fadeLength).SetEase(Ease.OutExpo);
			song3.DOFade(song3PreduckVolume, fadeLength).SetEase(Ease.OutExpo);
		}
	}

	public void DuckSongFinish()
	{
		if (duckState == DuckState.Stopping)
		{
			duckState = DuckState.None;
		}
	}

	private void SaveVisualOffset(double offset)
	{
		Persistence.visualOffset = (float)offset;
		PlayerPrefs.SetFloat("offset_v", (float)offset);
		PlayerPrefs.Save();
	}

	public static string GetDeviceInfo()
	{
		return "Device Info:\n" + $"Platform: {ADOBase.platform}\n" + "OS: " + SystemInfo.operatingSystem + "\nDevice Model: " + SystemInfo.deviceModel + "\n" + $"Output Type: {currentPreset.outputType}\n" + "Output Name: " + currentPreset.outputName + "\n" + $"Input Offset: {currentPreset.inputOffset}\n" + $"One-line:\n{ADOBase.platform},{SystemInfo.operatingSystem},{SystemInfo.deviceModel},{currentPreset.outputType},{currentPreset.outputName},{currentPreset.inputOffset}";
	}

	public void PlayWithEndTime(string snd, double time, double endTime, float volume = 1f, int priority = 128)
	{
		AudioSource audioSource = AudioManager.Instance.MakeSource(snd);
		audioSource.loop = true;
		audioSource.pitch = 1f;
		audioSource.volume = volume;
		audioSource.priority = priority;
		audioSource.PlayScheduled(time);
		audioSource.SetScheduledEndTime(endTime);
		float num = (audioSource.clip ? audioSource.clip.length : float.PositiveInfinity);
		AudioManager.Instance.liveSources.Enqueue(audioSource, time + (double)num);
	}

	public void KillAllSounds()
	{
		StartCoroutine(KillAllSoundsCoroutine());
	}

	private IEnumerator KillAllSoundsCoroutine()
	{
		AudioManager.Instance.StopAllSounds();
		yield return null;
		yield return null;
		yield return null;
		AudioManager.Instance.StopAllSounds();
	}
}
