using System.Collections.Generic;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public class PlayerKeyedList<T> where T : class, IPlayerKeyedListEntry, new()
{
	public List<T> data = new List<T>();

	private string DebugString => "[PFLAGS " + string.Join("/", data) + "]";

	public int GetIndex(PlayerID pid)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].PID == pid)
			{
				return i;
			}
		}
		return -1;
	}

	public T GetOrNull(PlayerID pid)
	{
		int index = GetIndex(pid);
		if (index < 0)
		{
			return null;
		}
		return data[index];
	}

	public T GetOrAdd(PlayerID pid)
	{
		int num = GetIndex(pid);
		if (num < 0)
		{
			num = data.Count;
			data.Add(new T
			{
				PID = pid
			});
		}
		return data[num];
	}

	public void Remove(PlayerID pid)
	{
		int index = GetIndex(pid);
		if (index >= 0)
		{
			data.RemoveAt(index);
		}
	}

	public override string ToString()
	{
		return DebugString;
	}
}
