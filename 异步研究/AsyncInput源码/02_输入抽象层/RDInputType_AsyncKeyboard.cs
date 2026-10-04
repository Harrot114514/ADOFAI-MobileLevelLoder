using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SkyHook;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class RDInputType_AsyncKeyboard : RDInputType
{
	private static readonly KeyLabel[] pausedKeys = new KeyLabel[10]
	{
		KeyLabel.Q,
		KeyLabel.ArrowLeft,
		KeyLabel.ArrowRight,
		KeyLabel.ArrowUp,
		KeyLabel.ArrowDown,
		KeyLabel.MouseLeft,
		KeyLabel.MouseMiddle,
		KeyLabel.MouseRight,
		KeyLabel.MouseX1,
		KeyLabel.MouseX2
	};

	private static readonly KeyLabel[] SpecialKeys = new KeyLabel[4]
	{
		KeyLabel.PrintScreen,
		KeyLabel.F12,
		KeyLabel.LAlt,
		KeyLabel.Super
	};

	public static readonly KeyLabel[] LevelSelectKeys = new KeyLabel[15]
	{
		KeyLabel.Grave,
		KeyLabel.Alpha0,
		KeyLabel.Alpha1,
		KeyLabel.Alpha2,
		KeyLabel.Alpha3,
		KeyLabel.Alpha4,
		KeyLabel.Alpha5,
		KeyLabel.Alpha6,
		KeyLabel.Alpha7,
		KeyLabel.Alpha8,
		KeyLabel.Q,
		KeyLabel.ArrowLeft,
		KeyLabel.ArrowRight,
		KeyLabel.ArrowUp,
		KeyLabel.ArrowDown
	};

	public static readonly KeyLabel[] LevelSelectArrowKeys = new KeyLabel[2]
	{
		KeyLabel.ArrowUp,
		KeyLabel.ArrowDown
	};

	public static readonly KeyLabel[] CLSKeys = new KeyLabel[12]
	{
		KeyLabel.Q,
		KeyLabel.ArrowLeft,
		KeyLabel.ArrowRight,
		KeyLabel.ArrowUp,
		KeyLabel.ArrowDown,
		KeyLabel.R,
		KeyLabel.S,
		KeyLabel.Delete,
		KeyLabel.I,
		KeyLabel.F,
		KeyLabel.O,
		KeyLabel.N
	};

	public static readonly KeyLabel[] MouseKeys = new KeyLabel[5]
	{
		KeyLabel.MouseLeft,
		KeyLabel.MouseMiddle,
		KeyLabel.MouseRight,
		KeyLabel.MouseX1,
		KeyLabel.MouseX2
	};

	public static readonly KeyLabel[] LeftKeys = new KeyLabel[17]
	{
		KeyLabel.Q,
		KeyLabel.W,
		KeyLabel.E,
		KeyLabel.R,
		KeyLabel.T,
		KeyLabel.CapsLock,
		KeyLabel.A,
		KeyLabel.S,
		KeyLabel.D,
		KeyLabel.F,
		KeyLabel.G,
		KeyLabel.LShift,
		KeyLabel.Z,
		KeyLabel.X,
		KeyLabel.C,
		KeyLabel.V,
		KeyLabel.B
	};

	public static readonly KeyLabel[] RightKeys = new KeyLabel[20]
	{
		KeyLabel.Y,
		KeyLabel.U,
		KeyLabel.I,
		KeyLabel.O,
		KeyLabel.P,
		KeyLabel.LeftBrace,
		KeyLabel.RightBrace,
		KeyLabel.H,
		KeyLabel.J,
		KeyLabel.K,
		KeyLabel.L,
		KeyLabel.Semicolon,
		KeyLabel.Apostrophe,
		KeyLabel.N,
		KeyLabel.M,
		KeyLabel.Comma,
		KeyLabel.Dot,
		KeyLabel.Slash,
		KeyLabel.RShift,
		KeyLabel.Enter
	};

	public static readonly KeyLabel[] ConfirmKeys = new KeyLabel[3]
	{
		KeyLabel.Space,
		KeyLabel.Enter,
		KeyLabel.KeypadEnter
	};

	private readonly KeyLabel[] mainKeys;

	public static readonly IEnumerable<KeyLabel> AllAsyncKeys = Enum.GetValues(typeof(KeyLabel)).Cast<KeyLabel>();

	private ControllerType controllerType;

	private static int _defunctWarns;

	private static readonly HashSet<string> DefunctWarnScenes = new HashSet<string>
	{
		GCNS.sceneLevelSelect,
		"scnTaroMenu0",
		"scnTaroMenu1",
		"scnTaroMenu2",
		"scnTaroMenu3"
	};

	private static bool _coroutineWorking;

	private static bool _shouldWarnInThisScene;

	public RDInputType_AsyncKeyboard(ControllerType type)
	{
		_isActive = false;
		controllerType = type;
		switch (type)
		{
		case ControllerType.KeyboardFull:
			mainKeys = AllAsyncKeys.ToArray();
			break;
		case ControllerType.KeyboardLeft:
			mainKeys = LeftKeys;
			break;
		case ControllerType.KeyboardRight:
			mainKeys = RightKeys;
			break;
		default:
			Debug.LogError("Wrong selected ControllerType for RDInputType_Keyboard!");
			break;
		}
		SceneManager.sceneLoaded += OnSceneChanged;
		StartDecrementWarnCoroutine();
	}

	private static bool CheckKeyState(KeyLabel key, ButtonState state = ButtonState.WentDown)
	{
		return state switch
		{
			ButtonState.WentDown => AsyncInput.GetKeyDown(key), 
			ButtonState.WentUp => AsyncInput.GetKeyUp(key), 
			ButtonState.IsDown => AsyncInput.GetKey(key), 
			ButtonState.IsUp => !AsyncInput.GetKey(key), 
			_ => false, 
		};
	}

	private static bool CheckAnyKeyState(KeyLabel[] keys, ButtonState state = ButtonState.WentDown)
	{
		return keys.Any((KeyLabel key) => CheckKeyState(key, state));
	}

	public override int Main(ButtonState state)
	{
		if (!isActive)
		{
			return 0;
		}
		MainStateCount stateCount = GetStateCount(state);
		stateCount.lastFrameUpdated = Time.frameCount;
		stateCount.keys = new List<AnyKeyCode>();
		HashSet<AsyncKeyCode> hashSet = new HashSet<AsyncKeyCode>((state switch
		{
			ButtonState.IsDown => AsyncInputManager.keyMask, 
			ButtonState.IsUp => new HashSet<AsyncKeyCode>(from k in AllAsyncKeys
				where !AsyncInput.GetKey(k)
				select new AsyncKeyCode(k)), 
			ButtonState.WentDown => AsyncInputManager.keyDownMask, 
			ButtonState.WentUp => AsyncInputManager.keyUpMask, 
			_ => new HashSet<AsyncKeyCode>(), 
		}).Where((AsyncKeyCode k) => Array.IndexOf(mainKeys, k.label) != -1));
		if (state == ButtonState.WentDown)
		{
			foreach (AsyncKeyCode key in GetSpecialInput())
			{
				hashSet.RemoveWhere((AsyncKeyCode k) => k == key);
			}
		}
		HashSet<ushort> asyncKeysCache = Persistence.keyLimiterKeys.asyncKeysCache;
		foreach (AsyncKeyCode item in hashSet)
		{
			if (!RDInput.useKeyLimiter || asyncKeysCache.Count <= 0 || asyncKeysCache.Contains(item.key))
			{
				stateCount.keys.Add(new AnyKeyCode(item));
			}
		}
		return stateCount.keys.Count;
	}

	private List<AsyncKeyCode> GetSpecialInput()
	{
		List<AsyncKeyCode> keys = new List<AsyncKeyCode>();
		if (Cancel(ButtonState.WentDown))
		{
			keys.Add(new AsyncKeyCode(KeyLabel.Escape));
		}
		Array.ForEach(SpecialKeys, delegate(KeyLabel key)
		{
			if (CheckKeyState(key))
			{
				keys.Add(new AsyncKeyCode(key));
			}
		});
		if (!base.controller.pauseMenu.settingsMenu.editingKeys)
		{
			if (!base.isPlaying)
			{
				Array.ForEach(pausedKeys, delegate(KeyLabel key)
				{
					if (CheckKeyState(key))
					{
						keys.Add(new AsyncKeyCode(key));
					}
				});
			}
			if (base.controller.currentState != States.PlayerControl && base.controller.currentState != States.Fail2 && !ADOBase.isEditingLevel && ADOBase.uiController.difficultyUIMode != DifficultyUIMode.DontShow)
			{
				if (Left(ButtonState.WentDown))
				{
					keys.Add(new AsyncKeyCode(KeyLabel.ArrowLeft));
				}
				else if (Right(ButtonState.WentDown))
				{
					keys.Add(new AsyncKeyCode(KeyLabel.ArrowRight));
				}
			}
			bool flag = ADOBase.sceneName.StartsWith("scnTaroMenu");
			if ((bool)scnLevelSelect.instance || flag)
			{
				Array.ForEach(LevelSelectKeys, delegate(KeyLabel key)
				{
					if (CheckKeyState(key))
					{
						keys.Add(new AsyncKeyCode(key));
					}
				});
			}
			if ((bool)base.controller?.creditsText && base.controller.creditsText.planetOnPosition)
			{
				Array.ForEach(LevelSelectArrowKeys, delegate(KeyLabel key)
				{
					if (CheckKeyState(key))
					{
						keys.Add(new AsyncKeyCode(key));
					}
				});
			}
			if (ADOBase.isCLS)
			{
				scnCLS instance = scnCLS.instance;
				if (!instance.showingInitialMenu)
				{
					Array.ForEach(CLSKeys, delegate(KeyLabel key)
					{
						if (CheckKeyState(key))
						{
							keys.Add(new AsyncKeyCode(key));
						}
					});
				}
				if (instance.optionsPanels.showingAnyPanel)
				{
					Array.ForEach(MouseKeys, delegate(KeyLabel key)
					{
						if (CheckKeyState(key))
						{
							keys.Add(new AsyncKeyCode(key));
						}
					});
				}
			}
		}
		return keys;
	}

	private static void OnSceneChanged(Scene scene, LoadSceneMode _)
	{
		_defunctWarns = 0;
		_shouldWarnInThisScene = DefunctWarnScenes.Contains(scene.name);
	}

	private static void StartDecrementWarnCoroutine()
	{
		if (!_coroutineWorking)
		{
			_coroutineWorking = true;
			SkyHookManager.Instance.StartCoroutine(DecrementDefuncWarn());
		}
	}

	private static IEnumerator DecrementDefuncWarn()
	{
		while (_coroutineWorking)
		{
			yield return new WaitForSeconds(5f);
			if (_defunctWarns > 0)
			{
				_defunctWarns--;
			}
		}
	}

	public static void IncrementDefuncWarn()
	{
		if (_coroutineWorking)
		{
			_defunctWarns++;
			if (_defunctWarns >= 5 && _shouldWarnInThisScene)
			{
				DisableAsyncInput();
			}
		}
	}

	private static void DisableAsyncInput()
	{
		_defunctWarns = 0;
		GameObject gameObject = UnityEngine.Object.Instantiate(RDConstants.data.prefab_errorCanvas);
		UnityEngine.Object.Destroy(gameObject.GetComponent<StandaloneInputModule>());
		UnityEngine.Object.Destroy(gameObject.GetComponent<EventSystem>());
		gameObject.GetComponent<ErrorCanvas>().ShowError(RDString.Get("error.asyncInputDisabled"));
		AsyncInputManager.ToggleHook(active: false);
		Persistence.SetChosenAsynchronousInput(enabled: false);
		Persistence.generalPrefs.Save();
		_coroutineWorking = false;
	}

	public override bool Restart(ButtonState state)
	{
		return CheckKeyState(KeyLabel.R, state);
	}

	public override bool Cancel(ButtonState state)
	{
		return CheckKeyState(KeyLabel.Escape, state);
	}

	public override bool Back(ButtonState state)
	{
		return CheckKeyState(KeyLabel.Escape, state);
	}

	public override bool Quit(ButtonState state)
	{
		return CheckKeyState(KeyLabel.Q, state);
	}

	public override bool Left(ButtonState state)
	{
		return CheckKeyState(KeyLabel.ArrowLeft, state);
	}

	public override bool Right(ButtonState state)
	{
		return CheckKeyState(KeyLabel.ArrowRight, state);
	}

	public override bool Up(ButtonState state)
	{
		return CheckKeyState(KeyLabel.ArrowUp, state);
	}

	public override bool Down(ButtonState state)
	{
		return CheckKeyState(KeyLabel.ArrowDown, state);
	}

	public override bool LeftAlt(ButtonState state)
	{
		return CheckKeyState(KeyLabel.LShift, state);
	}

	public override bool RightAlt(ButtonState state)
	{
		return CheckKeyState(KeyLabel.RShift, state);
	}

	public override bool UpAlt(ButtonState state)
	{
		return false;
	}

	public override bool DownAlt(ButtonState state)
	{
		return false;
	}

	public override bool Action1(ButtonState state)
	{
		return CheckAnyKeyState(LeftKeys, state);
	}

	public override bool Action2(ButtonState state)
	{
		return CheckAnyKeyState(RightKeys, state);
	}

	public override bool Confirm(ButtonState state)
	{
		return CheckAnyKeyState(ConfirmKeys, state);
	}

	public override bool FaceUp(ButtonState state)
	{
		return CheckKeyState(KeyLabel.S, state);
	}

	public override bool FaceLeft(ButtonState state)
	{
		return CheckKeyState(KeyLabel.A, state);
	}
}
