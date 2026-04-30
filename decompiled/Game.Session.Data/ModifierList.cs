using System.Collections.Generic;
using System.Diagnostics;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class ModifierList : List<IModifier>
{
	public Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		Fixnum fixnum = source;
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			fixnum = enumerator.Current.Evaluate(query, fixnum);
		}
		return fixnum;
	}

	[Conditional("UNITY_EDITOR")]
	private void CheckMod(IModifier mod, ModQuery query)
	{
		if (mod.DoesNeed(ModQueryElement.Player))
		{
			_ = query.pid.IsNotValid;
		}
		if (mod.DoesNeed(ModQueryElement.Node))
		{
			_ = query.nodeId.IsNotValid;
		}
		if (mod.DoesNeed(ModQueryElement.Target))
		{
			_ = query.targetId.IsNotValid;
		}
		if (mod.DoesNeed(ModQueryElement.CrewPeep))
		{
			_ = query.crewPeepId.IsNotValid;
		}
	}

	public string Explain(ModQuery query, Fixnum source, bool includeZeros = false)
	{
		ListPool<string>.PooledBlockList pooledBlockList = ListPool<string>.Allocate();
		Explain(query, source, pooledBlockList, includeZeros);
		string result = ((pooledBlockList.Count > 0) ? string.Join("\n", pooledBlockList) : "");
		ListPool<string>.Free(pooledBlockList);
		return result;
	}

	private void Explain(ModQuery query, Fixnum source, List<string> results, bool includeZeros)
	{
		Fixnum fixnum = source;
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			IModifier current = enumerator.Current;
			Fixnum fixnum2 = current.Evaluate(query, fixnum);
			Fixnum fixnum3 = fixnum2 - fixnum;
			if (!(fixnum3 == 0) || includeZeros)
			{
				string item = IndentExplanation(current.Explain(query, fixnum3));
				results.Add(item);
				fixnum = fixnum2;
			}
		}
	}

	public static string IndentExplanation(string exp)
	{
		return "  " + exp;
	}
}
