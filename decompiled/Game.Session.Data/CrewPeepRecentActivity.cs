using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CrewPeepRecentActivity : BaseDeltaMultiplierModifier
{
	public enum Type
	{
		None,
		Arrest,
		Injury
	}

	public Type type;

	public int dayz;

	public override ModQueryElement QueryMustProvide => ModQueryElement.CrewPeep;

	public override bool DoesPass(ModQuery query)
	{
		AgentComponent agentComponent = query.FindCrewPeep()?.components.agent;
		if (agentComponent == null)
		{
			return false;
		}
		int? num = null;
		switch (type)
		{
		case Type.Arrest:
			num = agentComponent.DaysSinceLastArrest();
			break;
		case Type.Injury:
			num = agentComponent.DaysSinceLastInjury();
			break;
		}
		if (num.HasValue)
		{
			return num.Value <= dayz;
		}
		return false;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get((type == Type.Arrest) ? "mod.crew-recent-arrest" : ((type == Type.Injury) ? "mod.crew-recent-injury" : null), "delta", AbstractModifier.FormatDelta(delta));
	}
}
