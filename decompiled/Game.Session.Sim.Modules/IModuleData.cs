using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Sim.Modules;

public interface IModuleData
{
	Label Id { get; }

	SimTime EnableTime { get; set; }

	List<Label> Expansions { get; set; }

	void InitializeOnCreate(ModuleInitData init);
}
