using SkyHook;

public struct AsyncKeyCode
{
	public readonly ushort key;

	public readonly KeyLabel label;

	public AsyncKeyCode(ushort key, KeyLabel label)
	{
		this.key = key;
		this.label = label;
	}

	public AsyncKeyCode(ushort key)
	{
		this.key = key;
		label = KeyLabel.Unknown;
	}

	public AsyncKeyCode(KeyLabel label)
	{
		key = ushort.MaxValue;
		this.label = label;
	}

	public AsyncKeyCode((ushort, KeyLabel) eventKey)
	{
		(key, label) = eventKey;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (!(obj is AsyncKeyCode asyncKeyCode) || !(asyncKeyCode == this))
		{
			return base.Equals(obj);
		}
		return true;
	}

	public static bool operator ==(AsyncKeyCode a, AsyncKeyCode b)
	{
		if (a.key != b.key)
		{
			return a.label == b.label;
		}
		return true;
	}

	public static bool operator !=(AsyncKeyCode a, AsyncKeyCode b)
	{
		return !(a == b);
	}

	public override string ToString()
	{
		return $"{label}({key})";
	}
}
