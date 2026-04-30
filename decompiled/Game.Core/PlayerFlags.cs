using System.Collections.Generic;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public sealed class PlayerFlags
{
	public List<PlayerID> data;

	public bool IsAnySet => data.Count > 0;

	private string DebugString => "[PFLAGS " + string.Join("/", data) + "]";

	public PlayerFlags(int capacity)
	{
		data = new List<PlayerID>(capacity);
	}

	public PlayerFlags()
		: this(8)
	{
	}

	public int GetIndex(PlayerID pid)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].id == pid.id)
			{
				return i;
			}
		}
		return -1;
	}

	public bool Get(PlayerID pid)
	{
		return GetIndex(pid) >= 0;
	}

	public void Set(PlayerID pid, bool value)
	{
		int index = GetIndex(pid);
		bool num = index >= 0;
		if (num && !value)
		{
			data.RemoveAt(index);
		}
		if (!num && value)
		{
			data.Add(pid);
		}
	}

	public bool Contains(PlayerID pid)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].id == pid.id)
			{
				return true;
			}
		}
		return false;
	}

	public PlayerID GetFirst()
	{
		if (data.Count <= 0)
		{
			return PlayerID.INVALID;
		}
		return data[0];
	}

	public override string ToString()
	{
		return DebugString;
	}
}
