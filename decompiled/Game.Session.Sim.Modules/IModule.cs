using Game.Core;

namespace Game.Session.Sim.Modules;

public interface IModule
{
	IModuleData ModuleData { get; }

	IModuleConfig ModuleConfig { get; }

	void Initialize(ModuleInitData init);

	void Release(bool shutdown);

	ModuleResult DoUpdate(ModuleQuery q, SimTime time, bool initial, bool enabled);

	bool IsEnabled(SimTime time);
}
