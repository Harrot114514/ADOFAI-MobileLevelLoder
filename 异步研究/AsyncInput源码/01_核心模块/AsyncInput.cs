using System.Collections.Generic;
using SkyHook;

public static class AsyncInput
{
	private enum KeyState
	{
		Held = 0,
		Down = 1,
		Up = 2
	}

	private static HashSet<AsyncKeyCode> GetKeyMask(KeyState state, bool frameDependent)
	{
		return state switch
		{
			KeyState.Held => frameDependent ? AsyncInputManager.frameDependentKeyMask : AsyncInputManager.keyMask, 
			KeyState.Down => frameDependent ? AsyncInputManager.frameDependentKeyDownMask : AsyncInputManager.keyDownMask, 
			KeyState.Up => frameDependent ? AsyncInputManager.frameDependentKeyUpMask : AsyncInputManager.keyUpMask, 
			_ => new HashSet<AsyncKeyCode>(), 
		};
	}

	private static bool Contains(HashSet<AsyncKeyCode> mask, AsyncKeyCode keyCode)
	{
		foreach (AsyncKeyCode item in mask)
		{
			if (item == keyCode)
			{
				return true;
			}
		}
		return false;
	}

	private static bool ContainsRawCode(HashSet<AsyncKeyCode> mask, ushort keyCode)
	{
		foreach (AsyncKeyCode item in mask)
		{
			if (item.key == keyCode)
			{
				return true;
			}
		}
		return false;
	}

	private static bool ContainsKeyWithLabel(HashSet<AsyncKeyCode> mask, KeyLabel label)
	{
		foreach (AsyncKeyCode item in mask)
		{
			if (item.label == label)
			{
				return true;
			}
		}
		return false;
	}

	public static bool GetKey(AsyncKeyCode keyCode, bool frameDependent = true)
	{
		return Contains(GetKeyMask(KeyState.Held, frameDependent), keyCode);
	}

	public static bool GetKey(ushort keyCode, bool frameDependent = true)
	{
		return ContainsRawCode(GetKeyMask(KeyState.Held, frameDependent), keyCode);
	}

	public static bool GetKey(KeyLabel label, bool frameDependent = true)
	{
		return ContainsKeyWithLabel(GetKeyMask(KeyState.Held, frameDependent), label);
	}

	public static bool GetKeyDown(AsyncKeyCode keyCode, bool frameDependent = true)
	{
		return Contains(GetKeyMask(KeyState.Down, frameDependent), keyCode);
	}

	public static bool GetKeyDown(ushort keyCode, bool frameDependent = true)
	{
		return ContainsRawCode(GetKeyMask(KeyState.Down, frameDependent), keyCode);
	}

	public static bool GetKeyDown(KeyLabel label, bool frameDependent = true)
	{
		return ContainsKeyWithLabel(GetKeyMask(KeyState.Down, frameDependent), label);
	}

	public static bool GetKeyUp(AsyncKeyCode keyCode, bool frameDependent = true)
	{
		return Contains(GetKeyMask(KeyState.Up, frameDependent), keyCode);
	}

	public static bool GetKeyUp(ushort keyCode, bool frameDependent = true)
	{
		return ContainsRawCode(GetKeyMask(KeyState.Up, frameDependent), keyCode);
	}

	public static bool GetKeyUp(KeyLabel label, bool frameDependent = true)
	{
		return ContainsKeyWithLabel(GetKeyMask(KeyState.Up, frameDependent), label);
	}
}
