using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CrewPeepUnpaid : BaseDeltaMultiplierModifier
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.CrewPeep;

	public override bool DoesPass(ModQuery query)
	{
		Entity entity = query.FindCrewPeep();
		if (entity == null)
		{
			return false;
		}
		if (entity.data.person.IsAlive)
		{
			return entity.components.agent?.WasCrewUnpaidLastTurn() ?? false;
		}
		return false;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.crew-unpaid", "delta", AbstractModifier.FormatDelta(delta));
	}
}
