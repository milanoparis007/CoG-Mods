using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Entities;

[DebuggerDisplay("{DebugString}")]
public sealed class Discounts
{
	[DebuggerDisplay("{DebugString}")]
	public sealed class Entry
	{
		public static readonly Fixnum DEFAULT_DISCOUNT = new Fixnum(1);

		public PlayerID pid = PlayerID.INVALID;

		public Label resid = Label.NULL;

		public Fixnum multiplier = DEFAULT_DISCOUNT;

		private string DebugString => $"[{pid}/{resid}:{multiplier}]";

		public override string ToString()
		{
			return DebugString;
		}
	}

	public List<Entry> data = new List<Entry>();

	private string DebugString => "[Discounts " + string.Join(",", data) + "]";

	public int GetIndex(PlayerID pid, Resource res)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].pid == pid && data[i].resid == res.resid)
			{
				return i;
			}
		}
		return -1;
	}

	public Entry GetOrNull(PlayerID pid, Resource res)
	{
		int index = GetIndex(pid, res);
		if (index < 0)
		{
			return null;
		}
		return data[index];
	}

	public Entry GetOrAdd(PlayerID pid, Resource res)
	{
		int num = GetIndex(pid, res);
		if (num < 0)
		{
			num = data.Count;
			data.Add(new Entry
			{
				pid = pid,
				resid = res.resid,
				multiplier = Entry.DEFAULT_DISCOUNT
			});
		}
		return data[num];
	}

	public void Remove(PlayerID pid, Resource res)
	{
		int index = GetIndex(pid, res);
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
