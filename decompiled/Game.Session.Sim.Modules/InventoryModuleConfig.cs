using Game.Core;

namespace Game.Session.Sim.Modules;

public class InventoryModuleConfig : ModuleConfig<InventoryModule, InventoryModuleConfig, InventoryModuleData>
{
	public Volume capacity;

	public bool isManualExpansion;
}
