using Game.Core;

namespace Game.Session.Data;

public class CheckCrewTraits : CheckSomeonesTraits
{
	protected override TagList GetTraitsOrNull(VisitState visit)
	{
		return visit.peep.data.person.traitIds;
	}
}
