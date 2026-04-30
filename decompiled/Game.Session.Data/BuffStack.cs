using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class BuffStack
{
	public List<BuffState> states = new List<BuffState>();

	public SimTime lastUpdate = SimTime.MIN_DATE;

	private static readonly Comparison<BuffState> RelBuffSorter = (BuffState x, BuffState y) => x.priority - y.priority;

	public bool IsEmpty => states.Count == 0;

	public static BuffSettings GetSettings()
	{
		return Game.serv.globals.settings.people.allBuffs;
	}

	public bool Remove(Label relId)
	{
		bool flag = false;
		for (int num = states.Count - 1; num >= 0; num--)
		{
			if (states[num].id == relId)
			{
				states.RemoveAt(num);
				flag = true;
			}
		}
		if (flag)
		{
			SortByPriority();
		}
		return flag;
	}

	public bool RemoveAll()
	{
		if (IsEmpty)
		{
			return false;
		}
		states.Clear();
		return true;
	}

	public bool Add(Label buffId, ModQuery query, EntityID crewpeep)
	{
		BuffConfig config = GetSettings().GetConfig(buffId);
		if (config == null)
		{
			Logger.Error($"Unknown buff id {buffId}");
			return false;
		}
		TrimExpiredBuffs(query);
		TrimCanceled(config);
		bool result = Remove(config.id);
		states.Add(BuffState.Make(config, query, crewpeep));
		SortByPriority();
		return result;
	}

	private void TrimExpiredBuffs(ModQuery query)
	{
		if (lastUpdate.days >= query.time.days)
		{
			return;
		}
		for (int num = states.Count - 1; num >= 0; num--)
		{
			if (states[num].IsExpired(query))
			{
				states.RemoveAt(num);
			}
		}
		lastUpdate = query.time;
	}

	private void TrimCanceled(BuffConfig def)
	{
		if (def.cancels == null)
		{
			return;
		}
		for (int num = states.Count - 1; num >= 0; num--)
		{
			if (def.cancels.Contains(states[num].id))
			{
				states.RemoveAt(num);
			}
		}
	}

	public Fixnum EvaluateDelta(ModQuery query)
	{
		TrimExpiredBuffs(query);
		Fixnum result = 0;
		for (int i = 0; i < states.Count; i++)
		{
			result += states[i].EvaluateDelta(query);
		}
		return result;
	}

	public void Explain(StringBuilder sb, ModQuery query)
	{
		foreach (BuffState item in states.OrderByDescending((BuffState x) => x.EvaluateDelta(query)))
		{
			string text = item.Explain(query);
			if (text != null)
			{
				sb.AppendLine(text);
			}
		}
	}

	public bool Contains(Label id)
	{
		return CountBuffsByID(id) > 0;
	}

	private void SortByPriority()
	{
		states.StableSort(RelBuffSorter);
	}

	private int CountBuffsByID(Label id)
	{
		int num = 0;
		for (int i = 0; i < states.Count; i++)
		{
			if (states[i].id == id)
			{
				num++;
			}
		}
		return num;
	}
}
