using System.Text;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Core;

public sealed class Respect : ProportionalValue
{
	public Fixnum staticFromCheats;

	public Fixnum fromAOE;

	public Fixnum fromRelationships;

	public Fixnum fromNeighbors;

	public Fixnum fromEthnicity;

	public Fixnum fromSafehouse;

	public override Fixnum GetPerTurnValue()
	{
		return fromAOE + fromRelationships + fromNeighbors + fromEthnicity + fromSafehouse;
	}

	public override void ResetPerTurnValue()
	{
		fromAOE = (fromRelationships = (fromNeighbors = (fromEthnicity = (fromSafehouse = 0))));
	}

	public override Fixnum GetStaticValue()
	{
		return staticFromCheats;
	}

	public override Fixnum CalculateBaseValue(ModQuery query)
	{
		return GetBuffsDelta(query) + GetStaticValue() + GetPerTurnValue();
	}

	protected override void Explain(StringBuilder sb, ModQuery query, string name)
	{
		string text = ProportionalValue.ExplainCurrentAndGoal(current, goal);
		sb.AppendLine(Loc.Get("ui.corner.respect.explain.header", "name", name, "exp", text));
		sb.AppendLine(Loc.Get("ui.corner.respect.explain.sources"));
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.respect.explain.source.safehouse"), fromSafehouse);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.respect.explain.source.territory"), fromNeighbors);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.respect.explain.source.relation"), fromRelationships);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.respect.explain.source.ethnic"), fromEthnicity);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.respect.explain.source.near-biz"), fromAOE);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.respect.explain.source.other"), staticFromCheats);
		buffs?.Explain(sb, query);
	}

	public override string ToString()
	{
		return $"{pid}:{current}->{goal}";
	}
}
