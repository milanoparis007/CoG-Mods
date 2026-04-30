using System.Collections.Generic;
using Game.Session.Sim.Modules;

namespace Game.Session.Entities;

public sealed class ModulesData : BaseData
{
	public List<IModuleData> slotData;
}
