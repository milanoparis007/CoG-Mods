using Game.Core;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public class CheckManagerTraits : CheckSomeonesTraits
{
	protected override TagList GetTraitsOrNull(VisitState visit)
	{
		return ModulesUtil.GetManagerOrNull(visit.building).manager?.data.person.traitIds;
	}
}
