using System;
using UnityEngine;

namespace Game.Services.Input;

public struct KeyMappingTuple : IEquatable<KeyMappingTuple>
{
	public KeyAction action;

	public KeyCode mainkey;

	public KeyCode altkey;

	public bool isValid => action != KeyAction.None;

	public KeyMappingTuple(KeyAction action, KeyCode mainkey, KeyCode altkey = KeyCode.None)
	{
		this.action = action;
		this.mainkey = mainkey;
		this.altkey = altkey;
	}

	public bool Equals(KeyMappingTuple other)
	{
		if (action == other.action && mainkey == other.mainkey)
		{
			return altkey == other.altkey;
		}
		return false;
	}
}
