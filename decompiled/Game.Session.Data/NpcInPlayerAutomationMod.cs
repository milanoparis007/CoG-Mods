using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class NpcInPlayerAutomationMod : BaseDeltaMultiplierModifier
{
	public bool expected;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Target;

	public override bool DoesPass(ModQuery query)
	{
		Entity entity = BuildingUtil.FindBuildingForBizOwner(query.FindTarget());
		return query.FindPlayer().automation.CountForTarget(entity.Id, includeActive: true, includePaused: false) > 0 == expected;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get(expected ? "mod.player-deliveries.true" : "mod.player-deliveries.false", "delta", delta);
	}
}
