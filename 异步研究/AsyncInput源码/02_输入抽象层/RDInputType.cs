using System.Collections.Generic;
using UnityEngine;

public abstract class RDInputType
{
	public class MainStateCount
	{
		public int lastFrameUpdated;

		public List<AnyKeyCode> keys;

		public MainStateCount()
		{
			lastFrameUpdated = -1;
			keys = new List<AnyKeyCode>();
		}
	}

	protected int schemeIndex;

	protected bool _isActive = true;

	public MainStateCount pressCount = new MainStateCount();

	public MainStateCount heldCount = new MainStateCount();

	public MainStateCount releaseCount = new MainStateCount();

	public MainStateCount isReleaseCount = new MainStateCount();

	public MainStateCount dummyCount = new MainStateCount();

	protected scrController controller => scrController.instance;

	protected bool isPlaying
	{
		get
		{
			if (controller != null)
			{
				return !controller.paused;
			}
			return false;
		}
	}

	protected bool isCLS
	{
		get
		{
			if (ADOBase.isCLS)
			{
				return isPlaying;
			}
			return false;
		}
	}

	public virtual bool isActive
	{
		get
		{
			return _isActive;
		}
		set
		{
			_isActive = value;
		}
	}

	public int mainPressCount => Main(ButtonState.WentDown);

	public bool mainPress => mainPressCount > 0;

	public abstract int Main(ButtonState state);

	public abstract bool Restart(ButtonState state);

	public abstract bool Cancel(ButtonState state);

	public abstract bool Back(ButtonState state);

	public abstract bool Quit(ButtonState state);

	public abstract bool Left(ButtonState state);

	public abstract bool Right(ButtonState state);

	public abstract bool Up(ButtonState state);

	public abstract bool Down(ButtonState state);

	public abstract bool LeftAlt(ButtonState state);

	public abstract bool RightAlt(ButtonState state);

	public abstract bool UpAlt(ButtonState state);

	public abstract bool DownAlt(ButtonState state);

	public abstract bool Action1(ButtonState state);

	public abstract bool Action2(ButtonState state);

	public abstract bool Confirm(ButtonState state);

	public abstract bool FaceUp(ButtonState state);

	public abstract bool FaceLeft(ButtonState state);

	public virtual Vector2 Position()
	{
		Vector2 zero = Vector2.zero;
		if (Left(ButtonState.IsDown))
		{
			zero += Vector2.left;
		}
		if (Right(ButtonState.IsDown))
		{
			zero += Vector2.right;
		}
		if (Up(ButtonState.IsDown))
		{
			zero += Vector2.up;
		}
		if (Down(ButtonState.IsDown))
		{
			zero += Vector2.down;
		}
		return Vector2.ClampMagnitude(zero, 1f);
	}

	public virtual void Update()
	{
	}

	protected MainStateCount GetStateCount(ButtonState state)
	{
		if (!isActive)
		{
			return dummyCount;
		}
		return state switch
		{
			ButtonState.WentDown => pressCount, 
			ButtonState.IsDown => heldCount, 
			ButtonState.WentUp => releaseCount, 
			_ => isReleaseCount, 
		};
	}

	public bool Get(InputAction action, ButtonState state = ButtonState.WentDown)
	{
		if (!isActive)
		{
			return false;
		}
		return action switch
		{
			InputAction.Cancel => Cancel(state), 
			InputAction.Back => Back(state), 
			InputAction.Quit => Quit(state), 
			InputAction.Left => Left(state), 
			InputAction.Right => Right(state), 
			InputAction.Up => Up(state), 
			InputAction.Down => Down(state), 
			InputAction.LeftAlt => LeftAlt(state), 
			InputAction.RightAlt => RightAlt(state), 
			InputAction.UpAlt => UpAlt(state), 
			InputAction.DownAlt => DownAlt(state), 
			InputAction.Action1 => Action1(state), 
			InputAction.Action2 => Action2(state), 
			InputAction.Confirm => Confirm(state), 
			InputAction.FaceUp => FaceUp(state), 
			InputAction.FaceLeft => FaceLeft(state), 
			_ => false, 
		};
	}
}
