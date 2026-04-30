using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckRelationshipMilestones : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		if (visit?.npc == null)
		{
			return 0;
		}
		return ((Fixnum?)GetPlayer(visit).social.GetRelationshipFromSourceToPlayer(visit.npc.Id)?.GetTicketsGrantedTotal()) ?? Fixnum.ZERO;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		Fixnum fixnum = GetPlayer(visit).social.GetRelationshipFromSourceToPlayer(visit.npc.Id).CalculateRelPointsForMilestone(value);
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.relationship.milestones", "relNeeded", fixnum));
	}
}
