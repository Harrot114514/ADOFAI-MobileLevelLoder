using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SkyHook;
using UnityEngine;

public class AsyncInputManager : MonoBehaviour
{
	public static ConcurrentQueue<SkyHookEvent> keyQueue = new ConcurrentQueue<SkyHookEvent>();

	public static ulong currFrameTick;

	public static ulong targetSongTick;

	public static ulong prevFrameTick;

	public static ulong offsetTick;

	public static bool offsetTickUpdated = false;

	public static double previousFrameTime;

	public static readonly HashSet<AsyncKeyCode> keyMask = new HashSet<AsyncKeyCode>();

	public static readonly HashSet<AsyncKeyCode> keyDownMask = new HashSet<AsyncKeyCode>();

	public static readonly HashSet<AsyncKeyCode> keyUpMask = new HashSet<AsyncKeyCode>();

	public static readonly HashSet<AsyncKeyCode> frameDependentKeyMask = new HashSet<AsyncKeyCode>();

	public static readonly HashSet<AsyncKeyCode> frameDependentKeyDownMask = new HashSet<AsyncKeyCode>();

	public static readonly HashSet<AsyncKeyCode> frameDependentKeyUpMask = new HashSet<AsyncKeyCode>();

	public static ulong lastReportedTargetTick;

	private static readonly RDCheatCode AsyncInputTestCheatCode = new RDCheatCode("asyncinputdebug");

	public static bool DebugMode;

	private static bool _dllWarnShown;

	private static readonly List<KeyLabel> MouseKeys = new List<KeyLabel>
	{
		KeyLabel.MouseLeft,
		KeyLabel.MouseMiddle,
		KeyLabel.MouseRight,
		KeyLabel.MouseX1,
		KeyLabel.MouseX2
	};

	private static AsyncInputManager _instance;

	private static GUIStyle _style;

	public static bool isActive
	{
		get
		{
			try
			{
				return SkyHookManager.Instance.isHookActive;
			}
			catch (DllNotFoundException arg)
			{
				if (!_dllWarnShown)
				{
					_dllWarnShown = true;
					Debug.LogWarning($"SkyHook Native DLL not found: {arg}");
				}
				return false;
			}
		}
	}

	public static void ToggleHook(bool active)
	{
		if (isActive != active)
		{
			if (active)
			{
				StartHook();
			}
			else
			{
				StopHook();
			}
			_instance.enabled = active;
		}
	}

	private static void StartHook()
	{
		SkyHookManager.StartHook();
	}

	private static void StopHook()
	{
		SkyHookManager.StopHook();
	}

	private static void ResetHookMetadata()
	{
		ClearKeys();
		currFrameTick = (targetSongTick = (prevFrameTick = (offsetTick = 0uL)));
		offsetTickUpdated = false;
		previousFrameTime = 0.0;
	}

	public static void ClearKeys()
	{
		keyMask.Clear();
		keyQueue.Clear();
		keyDownMask.Clear();
		keyUpMask.Clear();
	}

	[RuntimeInitializeOnLoadMethod]
	private static void Setup()
	{
		SkyHookManager.KeyUpdated.AddListener(delegate(SkyHookEvent keyEvent)
		{
			if (!MouseKeys.Contains(keyEvent.Label))
			{
				keyQueue.Enqueue(keyEvent);
			}
		});
		SkyHookManager.Instance.requireFocus = false;
		GameObject obj = new GameObject("Async Input Manager");
		UnityEngine.Object.DontDestroyOnLoad(obj);
		_instance = obj.AddComponent<AsyncInputManager>();
		_instance.enabled = false;
	}

	private void Update()
	{
		if (AsyncInputTestCheatCode.CheckCheatCode())
		{
			DebugMode = !DebugMode;
		}
		RDInputType_AsyncKeyboard asyncKeyboard = RDInput.asyncKeyboard;
		RDInputType_AsyncKeyboard asyncKeyboardLeft = RDInput.asyncKeyboardLeft;
		RDInputType_AsyncKeyboard asyncKeyboardRight = RDInput.asyncKeyboardRight;
		RDInputType_Keyboard keyboardInput = RDInput.keyboardInput;
		RDInputType_Keyboard keyboardLeft = RDInput.keyboardLeft;
		RDInputType_Keyboard keyboardRight = RDInput.keyboardRight;
		if (asyncKeyboard != null && Persistence.GetChosenAsynchronousInput() && isActive && (bool)scrController.instance)
		{
			bool flag = scrController.instance.state == States.PlayerControl;
			if (keyboardInput != null)
			{
				keyboardInput.isActive = !flag;
			}
			keyboardLeft.isActive = !flag;
			keyboardRight.isActive = !flag;
			asyncKeyboard.isActive = flag;
			asyncKeyboardLeft.isActive = flag;
			asyncKeyboardRight.isActive = flag;
			if (!flag)
			{
				ClearKeys();
			}
		}
		else
		{
			if (keyboardInput != null)
			{
				keyboardInput.isActive = true;
			}
			if (asyncKeyboard != null)
			{
				asyncKeyboard.isActive = false;
			}
		}
	}

	private void OnGUI()
	{
		if (DebugMode)
		{
			Rect position = new Rect(Screen.width - 200, 0f, 200f, 100f);
			string text = string.Format("async input {0}\n<color=#c4fdff>{1}: key queue count</color>\n{2}: keys (total: {3})\n<color=#c4fdff>{4}: keyDown (total: {5})</color>\n{6}: keyUp (total: {7})", isActive ? "ON" : "OFF", keyQueue.Count, string.Join(", ", keyMask), keyMask.Count, string.Join(", ", keyDownMask), keyDownMask.Count, string.Join(", ", keyUpMask), keyUpMask.Count);
			object obj = _style;
			if (obj == null)
			{
				obj = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.UpperRight,
					richText = true
				};
				_style = (GUIStyle)obj;
			}
			GUI.Label(position, text, (GUIStyle)obj);
		}
	}

	private void OnDisable()
	{
		if (RDInput.keyboardInput != null)
		{
			RDInput.keyboardInput.isActive = true;
		}
		if (RDInput.asyncKeyboard != null)
		{
			RDInput.asyncKeyboard.isActive = false;
		}
	}
}
