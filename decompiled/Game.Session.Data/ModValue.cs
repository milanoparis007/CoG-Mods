using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class ModValue
{
	public Fixnum value = 0;

	public ModifierList mods;

	public bool HasMods
	{
		get
		{
			if (mods != null)
			{
				return mods.Count > 0;
			}
			return false;
		}
	}

	private string DebugString => $"[ModValue value = {value}, {mods?.Count ?? 0} mods]";

	public Fixnum Evaluate(ModQuery query)
	{
		if (mods != null && mods.Count != 0)
		{
			return mods.Evaluate(query, value);
		}
		return value;
	}

	public Fixnum Evaluate(PlayerID pid, Node node = null)
	{
		return Evaluate(pid, node, null, null);
	}

	public Fixnum Evaluate(PlayerID pid, Entity target, Entity crewpeep)
	{
		return Evaluate(pid, null, target, crewpeep);
	}

	public Fixnum Evaluate(PlayerID pid, Node node, Entity target, Entity crewpeep)
	{
		NodeID nodeId = node?.id ?? NodeID.INVALID;
		EntityID targetId = target?.Id ?? EntityID.INVALID;
		EntityID crewPeepId = crewpeep?.Id ?? EntityID.INVALID;
		ModQuery query = new ModQuery(pid, targetId, crewPeepId, nodeId);
		return Evaluate(query);
	}

	public string Explain(ModQuery query, bool addHeader, bool includeZeros = false)
	{
		return Explain(value, mods, query, addHeader, includeZeros);
	}

	private string Explain(Fixnum value, ModifierList mods, ModQuery query, bool addHeader, bool includeZeros)
	{
		string text = "";
		if (addHeader)
		{
			text += Loc.Get("mod.base-value", "base", value);
		}
		if (mods != null)
		{
			text += mods.Explain(query, value, includeZeros);
		}
		return text;
	}
}
