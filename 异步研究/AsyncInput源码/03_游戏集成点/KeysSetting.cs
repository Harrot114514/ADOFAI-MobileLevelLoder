using System;
using System.Collections.Generic;
using System.Linq;
using SkyHook;
using UnityEngine;

public class KeysSetting
{
	private string settingName;

	private HashSet<KeyCode> _unityKeysCache;

	private HashSet<ushort> _asyncKeysCache;

	public string unityKeysName => settingName + "_unity";

	public string asyncKeysName => settingName + "_async";

	public HashSet<KeyCode> unityKeysCache => _unityKeysCache ?? (_unityKeysCache = unityKeys);

	private HashSet<KeyCode> unityKeys
	{
		get
		{
			return (from x in Persistence.generalPrefs.GetList(unityKeysName)
				select Enum.Parse<KeyCode>(x.ToString())).ToHashSet();
		}
		set
		{
			Persistence.generalPrefs.SetList(unityKeysName, value.Cast<object>().ToList());
			_unityKeysCache = value;
		}
	}

	public HashSet<ushort> asyncKeysCache => _asyncKeysCache ?? (_asyncKeysCache = asyncKeys);

	private HashSet<ushort> asyncKeys
	{
		get
		{
			return (from x in Persistence.generalPrefs.GetList(asyncKeysName)
				select ushort.Parse(x.ToString())).ToHashSet();
		}
		set
		{
			Persistence.generalPrefs.SetList(asyncKeysName, value.Cast<object>().ToList());
			_asyncKeysCache = value;
		}
	}

	public List<string> KeyLabels => (RDC.useAsyncInput ? asyncKeys.Select(delegate(ushort x)
	{
		KeyLabel keyLabel = SkyHookKeyMapper.NativeKeyCodeToKeyLabel(x);
		return (keyLabel == KeyLabel.Unknown) ? $"Unknown ({x})" : keyLabel.ToString();
	}) : unityKeys.Select((KeyCode x) => x.ToString())).ToList();

	public int Count
	{
		get
		{
			if (!RDC.useAsyncInput)
			{
				return unityKeys.Count;
			}
			return asyncKeys.Count;
		}
	}

	public KeysSetting(string name)
	{
		settingName = name;
	}

	private void Add(KeyCode unityKey, ushort? asyncKey)
	{
		HashSet<KeyCode> hashSet = unityKeys;
		HashSet<ushort> hashSet2 = asyncKeys;
		if (unityKey != KeyCode.None)
		{
			hashSet.Add(unityKey);
		}
		if (asyncKey.HasValue && asyncKey.Value != ushort.MaxValue)
		{
			hashSet2.Add(asyncKey.Value);
		}
		unityKeys = hashSet;
		asyncKeys = hashSet2;
	}

	private void Remove(KeyCode unityKey, ushort? asyncKey)
	{
		HashSet<KeyCode> hashSet = unityKeys;
		HashSet<ushort> hashSet2 = asyncKeys;
		hashSet.Remove(unityKey);
		if (asyncKey.HasValue && asyncKey.Value != ushort.MaxValue)
		{
			hashSet2.Remove(asyncKey.Value);
		}
		unityKeys = hashSet;
		asyncKeys = hashSet2;
	}

	public void Clear()
	{
		HashSet<KeyCode> hashSet = unityKeys;
		HashSet<ushort> hashSet2 = asyncKeys;
		hashSet.Clear();
		hashSet2.Clear();
		unityKeys = hashSet;
		asyncKeys = hashSet2;
	}

	public void Add(KeyCode unityKey)
	{
		ushort value = SkyHookKeyMapper.KeyLabelToNativeKeyCode(SkyHookKeyMapper.UnityKeyToSkyHookKey(unityKey));
		Add(unityKey, value);
	}

	public void Add(ushort asyncKey)
	{
		KeyCode unityKey = SkyHookKeyMapper.SkyHookKeyToUnityKey(SkyHookKeyMapper.NativeKeyCodeToKeyLabel(asyncKey));
		Add(unityKey, asyncKey);
	}

	public void Remove(KeyCode unityKey)
	{
		ushort value = SkyHookKeyMapper.KeyLabelToNativeKeyCode(SkyHookKeyMapper.UnityKeyToSkyHookKey(unityKey));
		Remove(unityKey, value);
	}

	public void Remove(ushort asyncKey)
	{
		KeyCode unityKey = SkyHookKeyMapper.SkyHookKeyToUnityKey(SkyHookKeyMapper.NativeKeyCodeToKeyLabel(asyncKey));
		Remove(unityKey, asyncKey);
	}

	public void Toggle(KeyCode unityKey)
	{
		if (unityKeys.Contains(unityKey))
		{
			Remove(unityKey);
		}
		else
		{
			Add(unityKey);
		}
	}

	public void Toggle(ushort asyncKey)
	{
		if (asyncKeys.Contains(asyncKey))
		{
			Remove(asyncKey);
		}
		else
		{
			Add(asyncKey);
		}
	}
}
