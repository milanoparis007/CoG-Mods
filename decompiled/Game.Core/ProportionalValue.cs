using System.Text;
using Game.Services;
using Game.Session.Data;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.Core;

public abstract class ProportionalValue : IPlayerKeyedListEntry
{
	public PlayerID pid;

	public Fixnum current;

	public Fixnum goal;

	public BuffStack buffs;

	public bool AnyValue => current > 0;

	public PlayerID PID
	{
		get
		{
			return pid;
		}
		set
		{
			pid = value;
		}
	}

	public abstract Fixnum GetPerTurnValue();

	public abstract void ResetPerTurnValue();

	public abstract Fixnum GetStaticValue();

	public abstract Fixnum CalculateBaseValue(ModQuery query);

	public bool AddBuff(Label id, ModQuery query, EntityID crewpeep)
	{
		if (buffs == null)
		{
			buffs = new BuffStack();
		}
		return buffs.Add(id, query, crewpeep);
	}

	public bool RemoveBuff(Label id)
	{
		return buffs?.Remove(id) ?? false;
	}

	public bool ContainsBuff(Label id)
	{
		return buffs?.Contains(id) ?? false;
	}

	protected Fixnum GetBuffsDelta(ModQuery query)
	{
		if (buffs == null)
		{
			return 0;
		}
		Fixnum result = buffs.EvaluateDelta(query);
		if (buffs.IsEmpty)
		{
			buffs = null;
		}
		return result;
	}

	public static string Explain(ProportionalValue proportionalValue, ModQuery query, string name)
	{
		if (proportionalValue == null || proportionalValue.current == 0)
		{
			return Loc.Get("ui.value.prop-none", "value", name);
		}
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		proportionalValue.Explain(sb, query, name);
		return sb.ToStringAndReturnToPool();
	}

	protected abstract void Explain(StringBuilder sb, ModQuery query, string name);

	protected static string ExplainCurrentAndGoal(Fixnum from, Fixnum to)
	{
		if (from == to)
		{
			return Loc.FormatNumber(to);
		}
		Fixnum fixnum = to - from;
		string text = Loc.FormatNumber(from);
		string text2 = Loc.FormatNumber(to);
		string text3 = TextUtil.ColorGreenRed(fixnum, (fixnum > 0) ? "↑" : "↓");
		string text4 = ((fixnum > 0) ? Loc.Get("ui.value.rising", "toval", text2) : Loc.Get("ui.value.falling", "toval", text2));
		return Loc.Get("ui.value.from", "fromval", text, "arrow", text3, "difference", text4);
	}

	protected static void MaybeAppend(StringBuilder sb, string msg, Fixnum value)
	{
		if (value != 0)
		{
			sb.AppendLine(Loc.Get("ui.value.indented", "value", value, "msg", msg));
		}
	}

	public override string ToString()
	{
		return $"{current}->{goal}";
	}
}
