using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class ManagerExistsMod : BaseDeltaMultiplierModifier
{
	public bool expected = true;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	public override bool DoesPass(ModQuery query)
	{
		Entity entity = null;
		if (query.FindTarget() != null)
		{
			Entity entity2 = query.FindBuildingForTarget();
			entity = ((entity2 != null) ? ModulesUtil.GetManagerOrNull(entity2).manager : null);
		}
		return entity != null == expected;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.manager-exists", "delta", AbstractModifier.FormatDelta(delta));
	}
}
