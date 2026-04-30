using Game.Session.Sim.Modules;

namespace Game.Session.Entities;

public struct AddModuleDef
{
	public IModuleConfig config;

	public bool passesReqs;

	public bool passesVisreqs;

	public bool IsSet => config != null;

	public bool IsNotSet => config == null;
}
