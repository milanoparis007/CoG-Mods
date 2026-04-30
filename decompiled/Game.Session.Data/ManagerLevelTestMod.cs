using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public sealed class ManagerLevelTestMod : LevelTestMod
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	protected override string LocKey => "mod.manager-experience";

	protected override Entity FindAgentToTest(ModQuery query)
	{
		if (query.FindTarget() == null)
		{
			return null;
		}
		Entity entity = query.FindBuildingForTarget();
		if (entity == null)
		{
			return null;
		}
		return ModulesUtil.GetManagerOrNull(entity).manager;
	}
}
