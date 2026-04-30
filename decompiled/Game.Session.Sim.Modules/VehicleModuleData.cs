using Game.Core;

namespace Game.Session.Sim.Modules;

public sealed class VehicleModuleData : ModuleData<VehicleModule, VehicleModuleConfig, VehicleModuleData>
{
	public SimTime lastUpdate;

	public int numRepairs;
}
