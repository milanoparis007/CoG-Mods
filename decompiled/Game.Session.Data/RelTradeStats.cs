using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class RelTradeStats
{
	[DebuggerDisplay("{DebugString}")]
	public struct Stat : IEquatable<Stat>
	{
		public Fixnum value;

		public SimTime expires;

		public bool rec;

		public bool illegal;

		private string DebugString => ToString();

		public Stat(Fixnum value, SimTime expires, bool rec, bool illegal)
		{
			this = default(Stat);
			this.value = value;
			this.expires = expires;
			this.rec = rec;
			this.illegal = illegal;
		}

		public static bool Equals(Stat a, Stat b)
		{
			if (a.value == b.value && a.expires.days == b.expires.days && a.rec == b.rec)
			{
				return a.illegal == b.illegal;
			}
			return false;
		}

		public bool Equals(Stat other)
		{
			return Equals(this, other);
		}

		public override string ToString()
		{
			return $"{value}:{expires}";
		}
	}

	public List<Stat> onetime = new List<Stat>();

	public List<Stat> recur = new List<Stat>();

	public bool IsEmpty
	{
		get
		{
			if (onetime.Count == 0)
			{
				return recur.Count == 0;
			}
			return false;
		}
	}

	private string DebugString => ToString();

	public static int StatComparerDescending(Stat a, Stat b)
	{
		return b.expires.days - a.expires.days;
	}

	private List<Stat> GetStats(bool recurring)
	{
		if (!recurring)
		{
			return onetime;
		}
		return recur;
	}

	public Fixnum Evaluate(SimTime now, bool recurring, bool illegal)
	{
		List<Stat> stats = GetStats(recurring);
		TrimExpired(stats, now);
		Fixnum result = 0;
		int i = 0;
		for (int count = stats.Count; i < count; i++)
		{
			Stat stat = stats[i];
			if (stat.rec == recurring && (!illegal || stat.illegal))
			{
				result += stats[i].value;
			}
		}
		return result;
	}

	public int Count(SimTime now, bool recurring, bool illegal)
	{
		List<Stat> stats = GetStats(recurring);
		TrimExpired(stats, now);
		int num = 0;
		int i = 0;
		for (int count = stats.Count; i < count; i++)
		{
			Stat stat = stats[i];
			if (stat.rec == recurring && (!illegal || stat.illegal))
			{
				num++;
			}
		}
		return num;
	}

	public bool AddIfNew(SimTime now, Fixnum value, SimTime expires, bool recurring, bool illegal, int maxCount)
	{
		List<Stat> stats = GetStats(recurring);
		Stat item = new Stat(value, expires, recurring, illegal);
		if (stats.Count > 0 && stats.Contains(item))
		{
			return false;
		}
		TrimExpired(stats, now);
		TrimToMaxCount(stats, maxCount - 1);
		stats.Insert(0, item);
		stats.StableSort(StatComparerDescending);
		return true;
	}

	public SimTime? GetMostRecentTradeExpiration(bool recurring)
	{
		List<Stat> stats = GetStats(recurring);
		if (stats.Count <= 0)
		{
			return null;
		}
		return stats[0].expires;
	}

	public SimTime? GetMostRecentTradeExpirationIllegal(bool recurring)
	{
		foreach (Stat stat in GetStats(recurring))
		{
			if (stat.illegal)
			{
				return stat.expires;
			}
		}
		return null;
	}

	private static void TrimExpired(List<Stat> stats, SimTime now)
	{
		while (stats.Count > 0 && stats.LastOrDefaultFast().expires.days < now.days)
		{
			stats.RemoveLast();
		}
	}

	private static void TrimToMaxCount(List<Stat> stats, int count)
	{
		while (stats.Count > 0 && stats.Count > count)
		{
			stats.RemoveLast();
		}
	}

	public override string ToString()
	{
		return "Stats: rec " + ToString(recurring: true) + " - onetime " + ToString(recurring: false);
	}

	private string ToString(bool recurring)
	{
		List<Stat> stats = GetStats(recurring);
		return $"{stats.Count}: " + string.Join(",", stats);
	}
}
