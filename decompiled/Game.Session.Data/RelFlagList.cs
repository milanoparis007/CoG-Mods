using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class RelFlagList
{
	public SimTime lastUpdate;

	public List<RelFlag> data = new List<RelFlag>();

	private string DebugString => "FLAGS: [" + data.JoinToString(", ") + "]";

	public void TrimExpired(SimTime now)
	{
		if (lastUpdate.days >= now.days)
		{
			return;
		}
		for (int num = data.Count - 1; num >= 0; num--)
		{
			if (data[num].IsExpired(now))
			{
				data.RemoveAt(num);
			}
		}
		lastUpdate = now;
	}

	public int IndexOf(Label id)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].id == id)
			{
				return i;
			}
		}
		return -1;
	}

	public bool Contains(Label id, SimTime now)
	{
		TrimExpired(now);
		return IndexOf(id) >= 0;
	}

	public bool Remove(Label id, SimTime now)
	{
		TrimExpired(now);
		int num = IndexOf(id);
		if (num >= 0)
		{
			data.RemoveAt(num);
			return true;
		}
		return false;
	}

	public void Add(RelFlag flag, SimTime now)
	{
		Remove(flag.id, now);
		data.Add(flag);
	}

	public bool IsEmpty(SimTime now)
	{
		TrimExpired(now);
		return data.Count == 0;
	}

	public override string ToString()
	{
		return DebugString;
	}
}
