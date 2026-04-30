using Game.Core;
using Game.Session.Data;

namespace Game.Session.Sim.Modules;

public sealed class ManufactureModuleData : ModuleData<ManufactureModule, ManufactureModuleConfig, ManufactureModuleData>
{
	public int recipeIndex;

	public SimTime lastUpdate;

	public ResourceAndQtyList lastProduced = new ResourceAndQtyList();

	public ModulesMonthlyStats monthlyStats = new ModulesMonthlyStats();

	public SimTime lastStall;
}
