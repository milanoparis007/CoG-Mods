using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public class TraitsModOnManager : TraitsMod
{
	protected override Label FindMatchingTrait(ModQuery query)
	{
		if (@if == null || @if.Count == 0)
		{
			return Label.NULL;
		}
		Entity entity = query.FindBuildingForTarget();
		Entity entity2 = ((entity != null) ? ModulesUtil.GetManagerOrNull(entity).manager : null);
		if (entity2 == null)
		{
			return Label.NULL;
		}
		return FindTraitOn(entity2.Id);
	}
}
