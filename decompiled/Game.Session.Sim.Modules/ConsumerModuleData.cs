using Game.Core;
using Game.Session.Data;

namespace Game.Session.Sim.Modules;

public sealed class ConsumerModuleData : ModuleData<ConsumerModule, ConsumerModuleConfig, ConsumerModuleData>
{
	public SimTime lastUpdate;

	public ResourceAndQtyList lastConsumed = new ResourceAndQtyList();

	public ResourceAndQtyList lifetimeConsumed = new ResourceAndQtyList();

	public bool isWorking;

	public ModulesMonthlyStats monthlyStats = new ModulesMonthlyStats();
}
