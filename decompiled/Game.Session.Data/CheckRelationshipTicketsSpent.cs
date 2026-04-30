using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckRelationshipTicketsSpent : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		if (visit?.npc == null)
		{
			return 0;
		}
		return GetPlayer(visit).social.GetSocialTicketsSpent(visit.npc.Id);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.relationship.tickets.spent"));
	}
}
