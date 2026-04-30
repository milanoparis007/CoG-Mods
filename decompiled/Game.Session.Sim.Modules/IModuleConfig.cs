using Game.Core;

namespace Game.Session.Sim.Modules;

public interface IModuleConfig
{
	Label Id { get; }

	ModuleCommon Common { get; }

	IModule MakeModule();
}
