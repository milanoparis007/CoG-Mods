using Game.Core;

namespace Game.Session.Data;

public class CheckOwnerTraits : CheckSomeonesTraits
{
	protected override TagList GetTraitsOrNull(VisitState visit)
	{
		return visit.npc.data.person.traitIds;
	}
}
