using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CapacityOverrunBooleanMod : BaseDeltaMultiplierModifier
{
	public PlayerCrew.CapacityType type;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override bool DoesPass(ModQuery query)
	{
		return query.FindPlayer().crew.GetCapacityOverrun(type, overrun: true) > 0;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get(CapacityOverrunMod.GetLocKey(type), "delta", AbstractModifier.FormatDelta(delta));
	}
}
