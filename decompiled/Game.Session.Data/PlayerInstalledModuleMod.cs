using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class PlayerInstalledModuleMod : BaseModifier
{
	public Label id;

	public int permodule;

	public string lockey;

	public override string Lockey => lockey;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		PlayerInfo playerInfo = query.FindPlayer();
		int num = 0;
		foreach (EntityID item in playerInfo.territory.GetAllControlledBuildingsUnsafe())
		{
			num += CountModulesIn(item.FindEntity());
		}
		return source + permodule * num;
	}

	private int CountModulesIn(Entity building)
	{
		if (!ModulesUtil.HasModule(building, id))
		{
			return 0;
		}
		return 1;
	}
}
