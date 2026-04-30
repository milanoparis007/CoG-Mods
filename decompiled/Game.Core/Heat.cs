using System.Text;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Core;

public class Heat : ProportionalValue
{
	public Fixnum staticFromCheats;

	public Fixnum fromNeighbors;

	public Fixnum fromUnpopularity;

	public Fixnum fromBusinesses;

	public Fixnum fromGambling;

	public override Fixnum GetPerTurnValue()
	{
		return fromNeighbors + fromUnpopularity + fromBusinesses + fromGambling;
	}

	public override void ResetPerTurnValue()
	{
		fromNeighbors = (fromUnpopularity = (fromBusinesses = (fromGambling = 0)));
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
		sb.AppendLine(Loc.Get("ui.corner.heat.explain.header", "name", name, "exp", text));
		sb.AppendLine(Loc.Get("ui.corner.heat.explain.sources"));
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.heat.explain.source.neighbors"), fromNeighbors);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.heat.explain.source.shadybiz"), fromBusinesses);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.heat.explain.source.disliked"), fromUnpopularity);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.heat.explain.source.gambling"), fromGambling);
		ProportionalValue.MaybeAppend(sb, Loc.Get("ui.corner.heat.explain.source.other"), staticFromCheats);
		if (goal.IsZero)
		{
			sb.AppendLine(Loc.Get("ui.corner.heat.explain.none"));
		}
		buffs?.Explain(sb, query);
	}

	public Fixnum CalculatePropagationValue(ModQuery query)
	{
		return GetBuffsDelta(query);
	}

	public override string ToString()
	{
		return $"{pid}:{current}->{goal}";
	}
}
