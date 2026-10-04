using System;
using System.Collections.Generic;
using System.Linq;
using Rewired;
using UnityEngine;

public static class RDInput
{
	public static List<RDInputType> inputs;

	public static RDInputType_Joystick[] joystickInputs;

	public static RDInputType_Keyboard keyboardInput;

	public static RDInputType_Keyboard keyboardLeft;

	public static RDInputType_Keyboard keyboardRight;

	public static RDInputType_Mouse mouseInput;

	public static RDInputType_AsyncKeyboard asyncKeyboard;

	public static RDInputType_AsyncKeyboard asyncKeyboardLeft;

	public static RDInputType_AsyncKeyboard asyncKeyboardRight;

	public static InputManager rewiredManager;

	public static List<HashSet<RDInputType>> playerInputs = new List<HashSet<RDInputType>>();

	public static readonly ControllerType[] desktopControllerTypes = new ControllerType[5]
	{
		ControllerType.Gamepad,
		ControllerType.KeyboardLeft,
		ControllerType.KeyboardRight,
		ControllerType.KeyboardFull,
		ControllerType.Mouse
	};

	private static bool didSetup;

	public static bool holdingControl
	{
		get
		{
			if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) && !Input.GetKey(KeyCode.LeftMeta))
			{
				return Input.GetKey(KeyCode.RightMeta);
			}
			return true;
		}
	}

	public static bool holdingShift
	{
		get
		{
			if (!Input.GetKey(KeyCode.LeftShift))
			{
				return Input.GetKey(KeyCode.RightShift);
			}
			return true;
		}
	}

	public static bool holdingAlt
	{
		get
		{
			if (!Input.GetKey(KeyCode.LeftAlt))
			{
				return Input.GetKey(KeyCode.RightAlt);
			}
			return true;
		}
	}

	public static int mainPressCount => GetMain();

	public static bool mainPress => mainPressCount > 0;

	public static int mainHeldCount => GetMain(ButtonState.IsDown);

	public static bool mainHeld => mainHeldCount > 0;

	public static bool restartPress => GetState(InputAction.Restart);

	public static bool cancelPress => GetState(InputAction.Cancel);

	public static bool backPress => GetState(InputAction.Back);

	public static bool backIsPressed => GetState(InputAction.Back, ButtonState.IsDown);

	public static bool quitPress => GetState(InputAction.Quit);

	public static bool faceLeft => GetState(InputAction.FaceLeft);

	public static bool faceUp => GetState(InputAction.FaceUp);

	public static bool leftPress => GetState(InputAction.Left);

	public static bool leftWentUp => GetState(InputAction.Left, ButtonState.WentUp);

	public static bool leftIsPressed => GetState(InputAction.Left, ButtonState.IsDown);

	public static bool rightPress => GetState(InputAction.Right);

	public static bool rightWentUp => GetState(InputAction.Right, ButtonState.WentUp);

	public static bool rightIsPressed => GetState(InputAction.Right, ButtonState.IsDown);

	public static bool upPress => GetState(InputAction.Up);

	public static bool upWentUp => GetState(InputAction.Up, ButtonState.WentUp);

	public static bool upIsPressed => GetState(InputAction.Up, ButtonState.IsDown);

	public static bool downPress => GetState(InputAction.Down);

	public static bool downWentUp => GetState(InputAction.Down, ButtonState.WentUp);

	public static bool downIsPressed => GetState(InputAction.Down, ButtonState.IsDown);

	public static bool leftAltPress => GetState(InputAction.LeftAlt);

	public static bool leftAltIsPressed => GetState(InputAction.LeftAlt, ButtonState.IsDown);

	public static bool rightAltPress => GetState(InputAction.RightAlt);

	public static bool rightAltIsPressed => GetState(InputAction.RightAlt, ButtonState.IsDown);

	public static bool confirmPress => GetState(InputAction.Confirm);

	public static bool confirmWentUp => GetState(InputAction.Confirm, ButtonState.WentUp);

	public static bool confirmIsPressed => GetState(InputAction.Confirm, ButtonState.IsDown);

	public static bool action1Press => GetState(InputAction.Action1);

	public static bool action1WentUp => GetState(InputAction.Action1, ButtonState.WentUp);

	public static bool action1IsPressed => GetState(InputAction.Action1, ButtonState.IsDown);

	public static bool action2Press => GetState(InputAction.Action2);

	public static bool action2WentUp => GetState(InputAction.Action2, ButtonState.WentUp);

	public static bool action2IsPressed => GetState(InputAction.Action2, ButtonState.IsDown);

	public static Vector2 position => GetPosition();

	private static bool mouseInWindow
	{
		get
		{
			Vector3 mousePosition = Input.mousePosition;
			if (mousePosition.x >= 0f && mousePosition.y >= 0f && mousePosition.x <= (float)Screen.width)
			{
				return mousePosition.y <= (float)Screen.height;
			}
			return false;
		}
	}

	public static Vector2 mouseScrollDelta
	{
		get
		{
			if (!mouseInWindow)
			{
				return Vector2.zero;
			}
			return Input.mouseScrollDelta;
		}
	}

	public static bool useKeyLimiter
	{
		get
		{
			if (scrController.instance.gameworld)
			{
				return !scrController.instance.pauseMenu.isActiveAndEnabled;
			}
			return false;
		}
	}

	public static void Setup(InputManager rewiredManager = null)
	{
		if (!didSetup)
		{
			didSetup = true;
			if (rewiredManager != null)
			{
				RDInput.rewiredManager = rewiredManager;
			}
			keyboardInput = new RDInputType_Keyboard(ControllerType.KeyboardFull);
			mouseInput = new RDInputType_Mouse();
			keyboardLeft = new RDInputType_Keyboard(ControllerType.KeyboardLeft);
			keyboardRight = new RDInputType_Keyboard(ControllerType.KeyboardRight);
			asyncKeyboard = new RDInputType_AsyncKeyboard(ControllerType.KeyboardFull);
			asyncKeyboardLeft = new RDInputType_AsyncKeyboard(ControllerType.KeyboardLeft);
			asyncKeyboardRight = new RDInputType_AsyncKeyboard(ControllerType.KeyboardRight);
			joystickInputs = new RDInputType_Joystick[4];
			inputs = new List<RDInputType> { keyboardInput, mouseInput, asyncKeyboard };
			for (int i = 0; i < 4; i++)
			{
				RDInputType_Joystick item = (joystickInputs[i] = new RDInputType_Joystick(i));
				inputs.Add(item);
			}
			ReassignControllers(1);
		}
	}

	public static void ReassignControllers(int playerCount, ControllerType[] types = null, Joystick[] joysticks = null)
	{
		playerInputs.Clear();
		int num = 4;
		IList<Joystick> joysticks2 = ReInput.controllers.Joysticks;
		for (int i = 0; i < num; i++)
		{
			ReInput.players.GetPlayer(i).controllers.ClearAllControllers();
		}
		if (playerCount == 1)
		{
			Player player = ReInput.players.GetPlayer(0);
			foreach (Joystick item2 in joysticks2)
			{
				player.controllers.AddController(item2, removeFromOtherPlayers: true);
			}
			playerInputs.Add(new HashSet<RDInputType> { joystickInputs[0] });
			return;
		}
		for (int j = 0; j < playerCount; j++)
		{
			ControllerType controllerType = types[j];
			HashSet<RDInputType> item = controllerType switch
			{
				ControllerType.Gamepad => new HashSet<RDInputType> { joystickInputs[j] }, 
				ControllerType.KeyboardFull => new HashSet<RDInputType> { keyboardInput, asyncKeyboard }, 
				ControllerType.KeyboardLeft => new HashSet<RDInputType> { keyboardLeft, asyncKeyboardLeft }, 
				ControllerType.KeyboardRight => new HashSet<RDInputType> { keyboardRight, asyncKeyboardRight }, 
				ControllerType.Mouse => new HashSet<RDInputType> { mouseInput }, 
				_ => throw new NotImplementedException(), 
			};
			playerInputs.Add(item);
			if (controllerType == ControllerType.Gamepad)
			{
				ReInput.players.GetPlayer(j).controllers.AddController(joysticks[j], removeFromOtherPlayers: true);
			}
		}
		Player player2 = ReInput.players.GetPlayer(0);
		foreach (Joystick item3 in joysticks2)
		{
			if (!joysticks.Contains(item3))
			{
				player2.controllers.AddController(item3, removeFromOtherPlayers: true);
			}
		}
	}

	public static void SetMapping(string mapName)
	{
		RDInputType_Joystick[] array = joystickInputs;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetMapping(mapName);
		}
	}

	public static void Reinitialize()
	{
		didSetup = false;
		Setup();
	}

	public static bool GetState(InputAction inputAction, ButtonState state = ButtonState.WentDown)
	{
		foreach (RDInputType input in inputs)
		{
			if (input != null && input.isActive && input.Get(inputAction, state))
			{
				return true;
			}
		}
		return false;
	}

	public static int GetMain(ButtonState state = ButtonState.WentDown)
	{
		int num = 0;
		foreach (RDInputType input in inputs)
		{
			if (input.isActive)
			{
				int num2 = input.Main(state);
				num += num2;
			}
		}
		return num;
	}

	public static List<AnyKeyCode> GetStateKeys(ButtonState state = ButtonState.WentDown)
	{
		GetMain(state);
		List<AnyKeyCode> list = new List<AnyKeyCode>();
		foreach (RDInputType input in inputs)
		{
			if (input.isActive)
			{
				switch (state)
				{
				case ButtonState.WentDown:
					list.AddRange(input.pressCount.keys);
					break;
				case ButtonState.IsDown:
					list.AddRange(input.heldCount.keys);
					break;
				case ButtonState.WentUp:
					list.AddRange(input.releaseCount.keys);
					break;
				case ButtonState.IsUp:
					list.AddRange(input.isReleaseCount.keys);
					break;
				}
			}
		}
		return list;
	}

	private static Vector2 GetPosition()
	{
		Vector2 zero = Vector2.zero;
		foreach (RDInputType input in inputs)
		{
			if (input.isActive)
			{
				zero += input.Position();
			}
		}
		return Vector2.ClampMagnitude(zero, 1f);
	}

	public static bool WentDown(this KeyCode key)
	{
		return Input.GetKeyDown(key);
	}

	public static bool WentUp(this KeyCode key)
	{
		return Input.GetKeyUp(key);
	}

	public static bool IsDown(this KeyCode key)
	{
		return Input.GetKey(key);
	}

	public static List<AnyKeyCode> GetMainPressKeys()
	{
		return GetStateKeys();
	}

	public static List<AnyKeyCode> GetMainHeldKeys()
	{
		return GetStateKeys(ButtonState.IsDown);
	}
}
